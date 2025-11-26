# Project

Two-assembly architecture consisting of:
* `Werewolves.StateModels` (.NET Class Library) - State representation and data models
* `Werewolves.GameLogic` (.NET Class Library) - Game logic, rules engine, and flow management

# Goal

To provide the core game logic and state management for a Werewolves of Miller's Hollow moderator helper application, handling rules and interactions based on the provided rulebook pages (excluding Buildings/Village expansion, but including specified New Moon events). The app **tracks the game state based on moderator input**. It assumes moderator input is accurate and provides deterministic state tracking and guidance based on that input. 

# String Management Principle

To ensure maintainability, localization capabilities, and type safety: 
*   All user-facing strings (e.g., moderator instructions, log entry descriptions, error messages displayed to the user) **must** be defined in the `Resources/GameStrings.resx` file and accessed via the generated `GameStrings` class. 
*   Internal identifiers or constants used purely for logic (e.g., specific action types for conditional checks) should strongly prefer the use of dedicated `enum` types over raw string literals to avoid weakly-typed comparisons and improve code clarity. 

# State Management Philosophy

This architecture employs a **Kernel-Facade Pattern** with **Event Sourcing** and a **Two-Speed State Model** to ensure strict separation between the mutable core, the read-only public API, and the transient execution logic.

## Two-Speed State Architecture

The system distinguishes between two types of state:

1.  **Persistent Domain State (Event Sourced):**
    *   **Scope:** `TurnNumber`, `Players` (Health/Roles), `WinningTeam`, `GameHistoryLog`.
    *   **Mechanism:** Changes occur **exclusively** via `GameLogEntryBase` (e.g., `AssignRoleLogEntry`, `VictoryConditionMetLogEntry`).
    *   **Purpose:** Represents the permanent historical record of the game. If the application restarts, the *consequences* of previous actions are preserved.
    *   **Replayability:** The `GameHistoryLog` is sufficient to reconstruct the **Game Status**, but not the **Execution Pointer**. Replaying the log restores the game to the start of the current Main Phase.
    *   **Initial State:** Games begin in the Night phase. There is no Setup phase; the `StartGameConfirmationInstruction` directly triggers Night phase execution when confirmed.

2.  **Transient Execution State (In-Memory):**
    *   **Scope:** `_phaseStateCache` (SubPhase, ActiveStage, ListenerState), `_pendingInstruction`.
    *   **Mechanism:** Direct mutations via `GameSession` methods, bypassing the event log.
    *   **Purpose:** Represents the fleeting "program counter" of the logic engine. Logging every state machine tick (e.g., transitioning between stages within `Night.Start`) would bloat the history log with technical noise.

## Key Pattern (Controlled Mutation Access)

Transient state updates don't go through GameLogEntryBase as it would pollute the log history with a lot of noise. Therefore, GameSession exposes methods that allow for direct mutation of different parts of the cached transient state values.
To enforce strict encapsulation and prevent arbitrary code from accessing those methods, the system uses a **Key Pattern**. Mutation methods require a specific interface implementation (a "Key") that is only implemented by authorized components.


*   **`IStateMutatorKey`:** Required to access the `SessionMutator` for persistent state changes. This is a nested interface inside `GameSession`, implemented by the private `SessionMutator` class.
*   **`IGameFlowManagerKey`:** Required to set the `PendingModeratorInstruction`. Implemented by `GameFlowManager`.
*   **`IPhaseManagerKey`:** Required to transition main phases and sub-phases in the cache. Implemented by `PhaseManager<T>`.
*   **`ISubPhaseManagerKey`:** Required to manage sub-phase stages. Implemented by `SubPhaseManager<T>`.
*   **`IHookSubPhaseKey`:** Required to update listener state. Implemented by `HookSubPhase`.

## Core Principles

*   **Canonical State Source:** The `GameSessionKernel.GameHistoryLog` is the **single, canonical source of truth** for all **state-altering** game events. This includes both **non-deterministic inputs** (moderator choices) and **deterministic consequences** (rule resolutions like infection). This append-only event store drives all persistent state mutations.
*   **The Kernel (Core):** The `GameSessionKernel` is the **sole owner of mutable memory**. It encapsulates the `GameHistoryLog`, `GamePhaseStateCache`, `Players`, `SeatingOrder` and `RolesInPlay`. It is an internal, hermetically sealed component.
*   **The Facade (Read-Only Projection & Mutation Gatekeeper):** The `GameSession` class acts as a **read-only projection** of the Kernel for the public API (via `IGameSession`). For the `Werewolves.GameLogic` assembly, it acts as the **Mutation Gatekeeper**, exposing methods to dispatch commands to the Kernel, protected by Keys.
*   **Transactional Mutation:** Persistent state mutation follows a strict transactional flow:
    1.  **Command Dispatch:** The Facade constructs a `GameLogEntryBase` (the command) and passes it to the Kernel.
    2.  **Proxy Mutator:** The Kernel creates a temporary `SessionMutator` (a private nested class implementing `ISessionMutator`) that has privileged access to the internal mutable state.
    3.  **Apply:** The `GameLogEntryBase.Apply(ISessionMutator)` method is called, allowing the entry to modify the state via the proxy.
    4.  **Commit:** If successful, the entry is appended to the `GameHistoryLog`. 

# Hook System Architecture

The architecture uses a declarative hook-based system where the `GameFlowManager` acts as a **Pure Dispatcher** rather than an orchestrator: 

*   **Game Hooks:** Declarative events fired at specific moments in the game flow (e.g., `NightMainActionLoop`, `OnVoteConcluded`).
*   **Hook Listeners:** Components (roles and events) that register to respond to specific hooks.
*   **Self-Contained State Machines:** Each listener manages its own state and logic, encapsulating all behavior.
*   **Capability-Based Logic:** The dispatcher does not check *who* a player is (e.g., "Is this the Village Idiot?"), but *what* they can do (e.g., "Is this player immune to lynching?"). Logic relies on `IPlayerState` computed properties.
*   **Unified State Cache:** Centralized state management for resuming paused operations and tracking execution progress. 

# Two-Assembly Architecture

The architecture is split into two separate library projects to achieve compiler-enforced encapsulation: 

*   **`Werewolves.StateModels`:** This library contains the complete state representation of the game. This includes `GameSession`, `Player`, `PlayerState`, all `GameLogEntryBase` derived classes, all `ModeratorInstruction` implementations (in `Models.Instructions`), and all shared `enums`. This project contains no game-specific rules logic (e.g., `GameFlowManager`, roles). Its purpose is to define the state, its mutation mechanisms, and the UI communication contract (instructions). 
*   **`Werewolves.GameLogic`:** This library contains the stateless "rules engine," including the `GameFlowManager`, `GameService`, and all `IGameHookListener` implementations (roles and events). This project has a one-way reference to `Werewolves.StateModels`. **Crucially, `Werewolves.StateModels` grants `[InternalsVisibleTo("Werewolves.GameLogic")]`.** This allows the Rules Engine to access the `internal` concrete `GameSession` and its mutation methods, while external consumers (UI) are restricted to the `public` read-only interfaces. 

# Core Components

## `GameSession` Class (Facade) & `GameSessionKernel` (Core)

The architecture separates the public API (`GameSession`) from the internal state container (`GameSessionKernel`) to enforce zero-leakage mutation and strict encapsulation.

### `GameSessionKernel` (Internal Core):
The hermetically sealed kernel that owns the game's mutable memory. It is not visible to the public API.

*   **Sole Owner of Mutable State:** The Kernel holds the master references to:
    *   `Id` (Guid): The unique game session identifier, injected at construction
    *   `GameHistoryLog` (The event source)
    *   `GamePhaseStateCache` (Transient execution state)
    *   `Players` (Dictionary of concrete `Player` objects)
    *   `SeatingOrder` (List of Guids)
    *   `RolesInPlay` (List of roles)
*   **Private Nested Classes:**
    *   **`Player` & `PlayerState`:** Concrete implementations of `IPlayer` and `IPlayerState` are defined as **private nested classes** within the Kernel. This ensures their setters are physically inaccessible to any code outside the Kernel file.
    *   **`SessionMutator`:** A **private nested class** implementing `ISessionMutator`. This is the "Proxy Mutator" that bridges the gap between the log entry and the private state.
*   **Transactional Apply Flow:** The Kernel exposes a single entry point for persistent mutation: `AddEntryAndUpdateState(GameLogEntryBase entry)`.

### `GameSession` (Facade):
A lightweight, stateless wrapper that implements `IGameSession` and delegates all operations to an internal `_gameSessionKernel` instance.

*   **Public API (IGameSession):** Read-only projection for UI consumers.
    *   `Id` (Guid): Unique identifier (pass-through to `GameSessionKernel.Id`).
    *   `TurnNumber` (int): The current turn number.
    *   `GetCurrentPhase()` (GamePhase): Returns the current main game phase.
    *   `GetPlayers()` (IEnumerable<IPlayer>): Returns all players.
    *   `GetPlayer(Guid id)` (IPlayer): Retrieves a player by ID.
    *   `GetPlayerState(Guid id)` (IPlayerState): Retrieves a player's state by ID.
    *   `GameHistoryLog` (IEnumerable<GameLogEntryBase>): The event log.
    *   `RoleInPlayCount(MainRoleType type)` (int): Returns count of a specific role in play.
*   **Internal API (GameLogic):** Mutation gatekeeper for the rules engine.
    *   **State Mutation Methods** (create and dispatch log entries):
        *   `EliminatePlayer(Guid playerId, EliminationReason reason)`: Eliminates a player by creating a `PlayerEliminatedLogEntry`.
        *   `AssignRole(Guid playerId, MainRoleType role)`: Assigns a role to a single player by creating an `AssignRoleLogEntry`.
        *   `AssignRole(List<Guid> playerIds, MainRoleType role)`: Assigns the same role to multiple players by creating an `AssignRoleLogEntry`.
        *   `ApplyStatusEffect(StatusEffectTypes effectType, Guid playerId)`: Applies a status effect by creating a `StatusEffectLogEntry`.
        *   `TransitionMainPhase(GamePhase newPhase)`: Transitions main phase by creating a `PhaseTransitionLogEntry`.
        *   `PerformNightActionNoTarget(NightActionType type)`: Records a night action with no target.
        *   `PerformNightAction(NightActionType type, Guid targetId)`: Records a night action targeting a single player.
        *   `PerformNightAction(NightActionType type, List<Guid> targetIds)`: Records a night action targeting multiple players.
        *   `PerformDayVote(Guid? reportedOutcomePlayerId)`: Records a vote outcome by creating a `VoteOutcomeReportedLogEntry`. Pass `null` for a tie.
        *   `VictoryConditionMet(Team winningTeam, string description)`: Records victory by creating a `VictoryConditionMetLogEntry`.
    *   **Cache Read Methods** (query transient execution state):
        *   `PendingModeratorInstruction` (ModeratorInstruction?): Returns the current pending instruction.
        *   `GetSubPhase<T>()` (T?): Returns the current sub-phase as a typed enum.
        *   `GetCurrentListener()` (ListenerIdentifier?): Returns the currently active/paused listener.
        *   `GetCurrentListenerState<T>(ListenerIdentifier listener)` (T?): Returns the listener's internal state machine value as a typed enum.
        *   `TryGetActiveGameHook(out GameHook hook)` (bool): Attempts to parse the active sub-phase stage as a `GameHook`.
    *   **Cache Write Methods** (require specific Keys for access):
        *   `SetPendingModeratorInstruction(IGameFlowManagerKey key, ModeratorInstruction instruction)`: Updates transient instruction state.
        *   `TransitionSubPhaseCache(IPhaseManagerKey key, Enum subPhase)`: Updates transient sub-phase state.
        *   `TryEnterSubPhaseStage(ISubPhaseManagerKey key, string subPhaseStageId)` (bool): Attempts to enter a sub-phase stage atomically. Returns `false` if already in a different stage or if the stage has already been completed.
        *   `CompleteSubPhaseStageCache(IPhaseManagerKey key)`: Marks the current sub-phase stage as completed.
        *   `TransitionListenerStateCache(IHookSubPhaseKey key, ListenerIdentifier listener, string state)`: Updates listener and its state.
    *   **Query Methods** (read derived state from log):
        *   `GetPlayersTargetedLastNight(NightActionType actionType, NumberRangeConstraint countConstraint, NumberRangeConstraint? turnsAgoConstraint)` (IEnumerable<IPlayer>): Returns players targeted by a specific night action type.
        *   `WasDayAbilityTriggeredThisTurn(DayPowerType powerType)` (bool): Checks if a specific day power was used this turn.
        *   `HasPlayerBeenVotedForPreviously(Guid playerId)` (bool): Checks if a player was the vote outcome target in any previous turn.
        *   `ShouldVoteRepeat()` (bool): Determines if the Stuttering Judge's extra vote should trigger a re-vote.
        *   `GetPlayersEliminatedThisDawn()` (IEnumerable<IPlayer>): Returns players eliminated during the current turn's Dawn phase.
        *   `GetPlayerEliminatedThisVote()` (IEnumerable<Guid>): Returns player IDs eliminated during the current turn's Day phase.
        *   `GetUnassignedRoles()` (List<MainRoleType>): Returns roles from `RolesInPlay` that have not yet been assigned to any player.

## `NightInteractionResolver` (Rule Engine)

A static helper class that serves as the "Rule Engine" for the Dawn phase, resolving complex interactions between conflicting night actions.

*   **Purpose:** Decouples the `GameFlowManager` from specific role logic (e.g., Witch vs. Defender vs. Infection).
*   **Process:**
    1.  **Input:** Accepts the `GameSession` state.
    2.  **Resolution:** Builds a map of all `NightActionType`s targeting players. Iterates through players to resolve conflicts based on the priority rules below.
    3.  **Output:** Directly calls `session.EliminatePlayer()` or `session.ApplyStatusEffect()` based on the resolved outcome.

*   **Resolution Priority & Special Rules:**
    1.  **Witch Save (Absolute Defense):** If the Witch saved a player, they are protected from wolf attacks.
    2.  **Defender Protection:** Blocks wolf faction actions (attacks and infection).
        *   **Exception - Little Girl:** Cannot be protected by the Defender. Protection fails silently.
    3.  **Elder Extra Life:** If an Elder with their extra life remaining is targeted by wolf actions (attacks or infection) and not otherwise protected, the extra life is consumed instead of applying the effect. The Elder survives but loses their extra life for future attacks.
    4.  **Infection:** If the Accursed Wolf-Father targets a player (and they are not protected), they become infected. Infection takes priority over physical wolf attacks (the player is infected, not killed).
    5.  **Wolf Attacks:** Physical attacks from Werewolves, Big Bad Wolf, or White Werewolf result in elimination if not blocked by the above.
    6.  **Unstoppable Actions:** The following actions **ignore all protection** (Defender, Witch Save) and always result in elimination:
        *   **Witch Kill (Death Potion):** Cannot be blocked or prevented.
        *   **Rusty Sword:** The Knight's posthumous revenge attack cannot be blocked.

The chosen architecture utilizes a dedicated `PlayerState` wrapper class. This class contains individual properties (e.g., `IsSheriff`, `IsImmuneToLynching`) for all dynamic boolean and data-carrying states, typically using `internal set` for controlled modification. The `Player` class then holds a single instance of `PlayerState`. This approach provides a balance of organization (grouping all volatile states together), strong typing, clear separation of concerns (keeping `Player` focused on identity/role), and strict encapsulation. 

## `IPlayer` Interface & `Player` Class

Represents a participant and their core identity information. 

*   **Interface-Based Architecture:** The system uses a `public IPlayer` interface (which extends `IEquatable<IPlayer>` for identity comparison) with a `private nested Player` implementation within `GameSessionKernel`. The `GameSession` exposes these instances as `IPlayer` to the UI (read-only). The `Werewolves.GameLogic` assembly cannot interact with `internal` members if necessary as it lacks access to the `private nested PlayerState` class.
*   **Enhanced Encapsulation through Nesting:** The `Player` class is implemented as a `private class` (not sealed) nested within `GameSessionKernel`, ensuring that only `GameSessionKernel` and its `SessionMutator` can directly access and modify player instances.
*   **`Player` Class Properties:
    *   `Id` (Guid): Unique identifier. 
    *   `Name` (string): Player's name. 
    *   `State` (IPlayerState): Encapsulates all dynamic, *persistent* states affecting the player. This approach keeps the core Player class focused on identity, while grouping volatile states for better organization. **This reflects the player's current, ongoing condition.** 
*   **Design Philosophy:** The `Player` class maintains only identity information, while all game-related dynamic state is managed through the `State` property, ensuring clear separation of concerns. State mutations are controlled exclusively through the `StateMutator` pattern to maintain architectural integrity.

## `IGameHookListener` Interface

Defines the contract for components that respond to game hooks (represents the *rules* of roles and events). 

*   **Interface Definition:** 
    ```csharp 
    internal interface IGameHookListener
    {
        ListenerIdentifier Id { get; }
        HookListenerActionResult Execute(GameSession session, ModeratorResponse input);
    }
    ``` 
*   **Accessibility:** The interface is marked as `internal` to hide implementation details from UI clients and ensure these components are only used within the game logic assembly.
*   **Interaction Contract:**  
    *   The `GameFlowManager` dispatches to all listeners registered for a fired hook by calling `Execute`.
    *   Each listener is responsible for determining if it should act based on game state and cached execution state 
    *   Listeners manage their own state machines and can pause/resume operations using the `GamePhaseStateCache`
    *   **Return Value Semantics:** The `HookListenerActionResult` communicates the outcome to the dispatcher: 
        *   `HookListenerActionResult.NeedInput(instruction, nextPhase)`: Listener requires input, processing pauses.
        *   `HookListenerActionResult.Complete(nextPhase)`: Listener has finished processing a given game hook, after performing some work.
        *   `HookListenerActionResult.Skip()`: Listener has not done any work, as it detected it has nothing to do for a given game.
*   **Advanced State Machine Features:** The implementation provides sophisticated state management capabilities including:
    *   Declarative state machine definition with runtime validation
    *   Comprehensive error checking and state transition validation
    *   Support for open-ended stages with unknown valid end states at runtime in state flows
    *   Support for end stages that prevent further state changes in state flows
    *   Generic `HookListenerActionResult<T>` for precise state tracking with `NextListenerPhase`
    *   Built-in protection against invalid state transitions and handler overwrites
*   **Polymorphic Listener Hierarchy:** The architecture provides a hierarchy of abstract base classes that implement `IGameHookListener`:
    *   **`RoleHookListener`**: Universal base for all role listeners, providing core logic and stateless implementation support
    *   **`RoleHookListener<TRoleStateEnum>`**: Base for stateful roles with a declarative state machine engine and runtime validation
    *   **`NightRoleHookListener<T>`**: Specialized base for night roles with wake/act/sleep lifecycle and Night 1 identification support
    *   **`StandardNightRoleHookListener<T>`**: Further specialization for standard "prompt target → process selection" workflow
    *   **`StandardNightRoleHookListener`**: Non-generic version using default state enum
    *   **`NightRoleIdOnlyHookListener`**: For roles that only require Night 1 identification without subsequent powers
    *   **`ImmediateFeedbackNightRoleHookListener`**: Specialized base for roles that require immediate moderator feedback during target selection processing
*   **Concrete Implementations:** All role classes inherit from appropriate base classes in the hierarchy, containing their complete state machine logic with built-in validation and state management. 
*   **TurnNumber Pattern for First-Night-Only Roles:** Roles with actions exclusive to the first night (e.g., Cupid, Thief, WolfHound, WildChild) are handled automatically by the `NightRoleHookListener` base class, which includes Night 1 identification in the wake-up flow.

## `IPlayerState` Interface & `PlayerState` Class

Wrapper class holding all dynamic state information for a `Player`. **Implemented with an `IPlayerState` interface and `private nested PlayerState` class within `GameSessionKernel` to provide clean abstraction, support testing, and strict encapsulation.** This improves organization and separation of concerns. Properties use `internal set` to ensure they are managed exclusively through the `StateMutator` pattern as part of the derived cached state pattern, maintaining state integrity. These properties represent the *persistent* or *longer-term* aspects of a player's current state (e.g., holding the Sheriff title, being in love, being infected, having used a specific potion). They reflect the player's ongoing status unless changed by a game event.

*   **Enhanced Encapsulation through Nesting:** The `PlayerState` class is implemented as a `private nested class` within `GameSessionKernel`, ensuring that only `SessionMutator` can directly access and modify player state instances.
*   **StateMutator Pattern Integration:** All state mutations are controlled exclusively through the `ISessionMutator` interface and its `private SessionMutator` implementation. This ensures that only log entries (through their `Apply` methods) can modify player state, maintaining architectural integrity.
*   **Restricted access to PlayerState**: The Player class exposes an IPlayerState property publicly but the PlayerState mutable property is only accessible through a `GetMutableState(IStateMutatorKey)`, ensuring that only `SessionMutator` can access it and its internal setters.
*   **Core Identity & Role Properties:**
    *   `MainRole` (MainRoleType?): The player's main character role type.
    *   `SecondaryRoles` (SecondaryRoleType): Flags enum indicating secondary roles (e.g., Sheriff, Lover).
    *   `Health` (PlayerHealth): Current health status (Alive, Dead, etc.).
    *   `IsInfected` (bool): True if the player is infected by the Father of Wolves.
    *   `IsSheriff` (bool): Indicates if the player currently holds the Sheriff title.
    *   `HasUsedElderExtraLife` (bool): True if the Elder has already used their extra life.
*   **Computed Capability Properties (Logic Decoupling):**
    *   `IsImmuneToLynching` (bool): Derived from role/events (e.g., Village Idiot).
    *   `LynchingImmunityAnnouncement` (string?): The text to announce if immunity triggers.
    *   `Team` (Team): The player's current allegiance, derived from MainRole and status effects. 

*Note on Devoted Servant:* When the Devoted Servant swaps roles, the responsible hook listener must explicitly reset any role-specific usage flags or counters (marked with *(Reset if...)* above) on the Servant's `PlayerState` to their default values.* 

## `EventCard` Abstract Class (NOT YET IMPLEMENTED)

Base for New Moon event cards (represents the *rules* of the event). Implements `IGameHookListener`. 

*   `Id` (string): Unique identifier (e.g., "FullMoonRising"). 
*   `Name` (string): Event card name. 
*   `Description` (string): Text description of the event. 
*   `Timing` (EventTiming Enum): Primary trigger time (e.g., `NextNight`, `Immediate`, `PermanentAssignment`). 
*   `Duration` (EventDuration Enum): How long the effect lasts (e.g., `OneNight`, `Permanent`, `UntilNextVote`). 
*   `Execute(GameSession session, ModeratorResponse input)` (HookListenerActionResult): Implements the hook listener interface.

## `ActiveEventState` Class (NOT YET IMPLEMENTED)

Stores runtime state for an active event in `GameSession.ActiveEvents`.

*   `EventId` (string): Matches the `EventCard.Id`. 
*   `CardReference` (EventCard): Reference to the static definition of the card. 
*   `TurnsRemaining` (int?): Countdown for temporary events. 
*   `StateData` (Dictionary<string, object>): Event-specific runtime data.

## `IGamePhaseStateCache` Interface & `GamePhaseStateCache` Struct

Unified state cache that serves as the single source of truth for the game's current execution point. Implemented as a `private record struct` within `GameSessionKernel`.
Its primary role is to track the "program counter" of the state machine and enforce atomic execution of sub-phase stages.

*   **Read-Only Interface (`IGamePhaseStateCache`):** Exposed to external consumers (UI) for reading the current state.
    *   `GetCurrentPhase()`: Returns current main phase.
    *   `GetSubPhase<T>()`: Returns current sub-phase.
    *   `GetActiveSubPhaseStage()`: Returns current stage.
    *   `GetCurrentListener()`: Returns currently paused listener.
    *   `CurrentListenerState` (int?): Returns the current listener's internal state machine value.
    *   `SubPhase` (object?): Returns the current sub-phase as an untyped object (for generic access).
*   **Mutation API (Internal):** Mutation methods are located on `GameSession` and require specific Keys to prevent unauthorized access.
    *   `TransitionMainPhase(...)`: Transitions main phase via `PhaseTransitionLogEntry` (event sourcing) - no key required.
    *   `TransitionSubPhase(IPhaseManagerKey, ...)`: Transitions sub-phase within current main phase.
    *   `TryEnterSubPhaseStage(ISubPhaseManagerKey, ...)`: Attempts to enter a sub-phase stage atomically.
    *   `TransitionListenerAndState(IHookSubPhaseKey, ...)`: Updates listener and its state.
*   **Automatic State Cleanup:**
    *   Transitioning to a new main phase clears all sub-phase and stage history, and listener data.
    *   Transitioning to a new sub-phase clears all stage history and listener data.
    *   Transitioning to a new sub-phase stage clears current listener data.

## `GameFlowManager` Class

Acts as a high-level phase controller and reactive hook dispatcher. It contains the complete, declarative definition of the game's state machine.

*   **Core Components:**
    *   `HookListeners` (static Dictionary<GameHook, List<ListenerIdentifier>>): Declarative mapping of hooks to the ordered list of listeners that respond to them.
    *   `ListenerImplementations` (static Dictionary<ListenerIdentifier, IGameHookListener>): Lookup for concrete listener implementations (i.e., the role classes).
    *   `PhaseDefinitions` (static Dictionary<GamePhase, IPhaseDefinition>): Declarative mapping of each main `GamePhase` to its corresponding `PhaseManager`.
*   **Primary Methods:**
    *   `GetInitialInstruction(List<MainRoleType> rolesInPlay, Guid gameId)` (StartGameConfirmationInstruction): **Static factory method for bootstrapping.** Returns the initial instruction required to construct a valid `GameSession`. This pure function performs input validation and generates the startup instruction without creating any game state.
    *   `HandleInput(GameSession session, ModeratorResponse input)` (ProcessResult): **The central state machine orchestrator.**
        *   Retrieves the current phase and delegates to the appropriate `IPhaseDefinition` (`PhaseManager`).
        *   The `PhaseManager` loops, executing the current `SubPhaseManager`'s sequence of atomic stages until an instruction is generated for the moderator.
        *   It validates all transitions (both sub-phase and main-phase) against the declarative rules.
        *   After a phase handler completes, it checks for victory conditions.
        *   Returns a `ProcessResult` with the next instruction.
    *   `CheckVictoryConditions(GameSession session)` (`private static`, returns `(Team WinningTeam, string Description)?`): Evaluates win conditions based on the current game state. Returns `null` if no victory condition is met, or a tuple containing the winning team and description.
*   **Declarative State Machine Architecture:** The game flow is defined by a hierarchy of declarative components:
    *   **`PhaseManager<TSubPhaseEnum>`**: Manages the flow between sub-phases for a single main `GamePhase`. It contains a dictionary of `SubPhaseManager`s.
    *   **`SubPhaseManager<TSubPhase>`**: Defines a single sub-phase. It contains a linear sequence of `SubPhaseStage`s that are executed in order. It also declares all valid transitions to other sub-phases or main phases.
    *   **`SubPhaseStage`**: An abstract class representing a single, **atomic, non-re-entrant** unit of work. The `GamePhaseStateCache` ensures each stage is executed at most once per sub-phase entry.
        *   `LogicSubPhaseStage`: Executes a custom logic handler.
        *   `HookSubPhaseStage`: Fires a `GameHook` and dispatches to all registered listeners.
        *   `NavigationSubPhaseStage`: A stage that results in a transition to a new sub-phase or main phase. Created via factory methods (`NavigationEndStage`, `NavigationEndStageSilent`) as the required final stage for any sub-phase.
*   **State Machine Validation:** The architecture provides strong runtime guarantees:
    *   **Transition Validation:** All transitions are validated against the `PossibleNextSubPhases` and `PossibleNextMainPhaseTransitions` sets defined in the `SubPhaseManager`. An illegal transition throws an `InvalidOperationException`.
    *   **Stage Atomicity:** The `Session.TryEnterSubPhaseStage` method prevents any stage from being executed more than once within a single sub-phase activation, eliminating the need for idempotent handlers.
*   **Key Pattern Usage:** Implements `IGameFlowManagerKey` to authorize updates to `GameSession.PendingModeratorInstruction`.

## `GameService` Class

Orchestrates the game flow based on moderator input and tracked state. **Delegates state machine management to `GameFlowManager` while handling high-level game logic and external interfaces.** 

*   **Public Methods:** 
    *   `StartNewGame(...)` (StartGameConfirmationInstruction): **Orchestrates atomic game initialization.** Generates a unique game ID, retrieves the initial instruction from `GameFlowManager.GetInitialInstruction`, constructs a `GameSession` with both the ID and instruction, stores the session, and returns the instruction.
    *   `ProcessInstruction(Guid gameId, ModeratorResponse input)` (ProcessResult): **The central entry point for processing moderator actions.** 
        *   Retrieves the current `GameSession` and delegates to `GameFlowManager.HandleInput`. 
        *   The `GameFlowManager` handles all state machine logic, validation, and transition management. 
        *   Returns the `ProcessResult` from the state machine, containing either the next instruction or a failure.
        *   **Session Cleanup:** If the result contains a `FinishedGameConfirmationInstruction`, the session is removed from the active sessions list.
    *   `GetCurrentInstruction(Guid gameId)` (ModeratorInstruction?): Retrieves the `PendingModeratorInstruction`. 
    *   `GetGameStateView(Guid gameId)` (IGameSession?): Returns the game state via the read-only `IGameSession` interface. This hides the internal mutation methods present on the concrete object, ensuring the UI cannot modify state. 
*   **Internal Logic:** 
    *   `EnsureInputTypeIsExpected(Guid gameId, ModeratorResponse input)`: Retrieves the pending instruction internally and validates input type matches expectation. Throws exception on mismatch.
    *   Relies on `GameFlowManager` for all state machine operations. 
    *   Victory condition checking is automatically handled by `GameFlowManager`.

## `ProcessResult` Structure

Represents the outcome of a processing operation.

*   `IsSuccess` (bool): True if processing completed successfully.
*   `ModeratorInstruction` (ModeratorInstruction?): The next instruction for the moderator.
*   **Factory Methods:**
    *   `Success(ModeratorInstruction)`: Creates a successful result.
    *   `Failure(ModeratorInstruction)`: Creates a failure result. **Note:** Only used by `GameService` when a game session cannot be found.

## `PhaseHandlerResult` Hierarchy

A hierarchy of records represents the outcome of a `SubPhaseStage`'s execution, signaling the intended next step to the `PhaseManager`.

*   `PhaseHandlerResult(ModeratorInstruction? ModeratorInstruction)`: Abstract base record.
*   `MajorNavigationPhaseHandlerResult`: Abstract record for results that cause a transition.
    *   `MainPhaseHandlerResult(ModeratorInstruction?, GamePhase)`: Signals a transition to a new main phase.
    *   `SubPhaseHandlerResult(ModeratorInstruction?, Enum)`: Signals a transition to a new sub-phase within the current main phase.
*   `StayInSubPhaseHandlerResult(ModeratorInstruction?)`: Signals that the state machine should remain in the current sub-phase. If the instruction is `null`, the `PhaseManager` will immediately attempt to execute the next stage in the sequence. If an instruction is provided, processing pauses until the moderator responds.

## Hook System Components
 
*   **`GameHook` Enum:** Defines all possible hook points in the game flow:
    *   `NightMainActionLoop`
    *   `PlayerRoleAssignedOnElimination`
    *   `OnVoteConcluded`
    *   `DawnMainActionLoop`

*   **`ListenerIdentifier` Record:** Unified identifier for different types of hook listeners: 
    ```csharp 
    public record ListenerIdentifier 
    { 
        public GameHookListenerType ListenerType { get; } // Enum: MainRole, Event, SecondaryRole 
        public string ListenerId { get; } // Stores the MainRoleType, SecondaryRoleType, or EventCardType enum value as string for better debugging/logging 
    } 
    ``` 

*   **`HookListenerActionResult` Class:** Standardized return type for `IGameHookListener.Execute`: 
    *   `NeedInput(instruction)`: Listener requires input, processing pauses.
    *   `Complete()`: Listener finished, processing continues.
    *   `Skip()`: Listener has no work to do.
 
## State Machine Validation
*   **Purpose:** The `GameFlowManager` implements comprehensive validation to ensure all phase transitions and state changes conform to the defined state machine rules. 
*   **Validation Features:** 
    *   **Phase Transition Validation:** Every phase transition is validated against the `PhaseTransitionInfo` defined in the source phase's `PossibleTransitions` list. 
    *   **Hook Dispatch Validation:** Ensures hooks are fired in correct sequence and listeners respond appropriately. 
    *   **State Cache Validation:** Validates that `IntraPhaseStateCache` operations are consistent with state machine rules. 
    *   **Internal Error Detection:** Catches internal state machine inconsistencies and provides detailed error messages for debugging. 
*   **Error Handling:** Validation failures result in exceptions, as they indicate unrecoverable logic errors.

 
## `ModeratorResponse` Class 
Data structure for communication FROM the moderator. 
*   `Type` (enum `ExpectedInputType`): Indicates which optional field below is populated. 
*   `SelectedPlayerIds` (List<Guid>?): IDs of players chosen. **Used for role identification (`PlayerSelectionMultiple`) and vote outcome (`PlayerSelectionSingle`, allowing 0 for tie).** 
*   `AssignedPlayerRoles` (Dictionary<Guid, MainRoleType>?): Player IDs mapped to the main role assigned to them. Used during setup/role assignment phases (e.g., Thief, initial role identification). 
*   `SelectedOption` (string?): Specific text option chosen. 
*   `Confirmation` (bool?): Boolean confirmation. 
*   **Construction:** Can only be instantiated via `ModeratorInstruction` subclass `CreateResponse()` methods.

**Design Note on Vote Input:** 
 
A key design principle for moderator input, especially during voting phases, is minimizing data entry to enhance usability during live gameplay. The application is designed to guide the moderator through the *process* of voting (whether standard or event-driven like Nightmare, Great Distrust, Punishment), reminding them of the relevant rules. However, the actual vote tallying is expected to happen physically among the players. 
 
Consequently, the `ModeratorResponse` structure requires the moderator to provide only the final *outcome* of the vote (e.g., who was eliminated via `SelectedPlayerIds`, where an empty list signifies a tie, or confirmation of other outcomes via `Confirmation`). This approach significantly reduces the moderator's interaction time and minimizes the potential for input errors. The application functions primarily as a streamlined state tracker and procedural guide, accepting the loss of granular vote data in its logs as an acceptable trade-off for improved real-time usability. 
 

 
## `ModeratorInstruction` Class Hierarchy
Polymorphic instruction system for communication TO the moderator. **Assembly Location:** The abstract base class `ModeratorInstruction` and all concrete implementations are located in `Werewolves.StateModels.Models.Instructions`. This placement allows `GameSession` to accept instructions as constructor parameters without circular dependencies.

*   **Abstract Base Class:** 
    *   `PublicAnnouncement` (string?): Text to be read aloud or displayed publicly to all players. 
    *   `PrivateInstruction` (string?): Text for moderator's eyes only, containing reminders, rules, or guidance. 
    *   `AffectedPlayerIds` (IReadOnlyList<Guid>?): Optional: Player(s) this instruction primarily relates to. 
*   **Concrete Implementations:** Each instruction type has its own `CreateResponse` method for validation and response creation:
*   **`ConfirmationInstruction`:** For yes/no confirmations.
*   **`SelectPlayersInstruction`:** For player selection with `NumberRangeConstraint` (defining min/max counts).
*   **`AssignRolesInstruction`:** For role assignment, validating that assignments match the available roles and player lists.
*   **`SelectOptionsInstruction`:** For option selection from a list of choices.

## Enums

### Core Game Flow Enums
*   `GamePhase`: `Night`, `Dawn`, `Day`.
*   `GameHook`: `NightMainActionLoop`, `PlayerRoleAssignedOnElimination`, `OnVoteConcluded`, `DawnMainActionLoop`.
*   `PlayerHealth`: `Alive`, `Dead`. 
*   `ExpectedInputType`: `PlayerSelection`, `PlayerSelectionSingle`, `PlayerSelectionMultiple`, `AssignPlayerRoles`, `OptionSelection`, `Confirmation`.

### Team Enum
*   `Team`: `Villagers`, `Werewolves`.
    *   **Planned values (not yet implemented):** `Lovers`, `Solo_WhiteWerewolf`, `Solo_Piper`, `Solo_Angel`, `Solo_PrejudicedManipulator`.

### Role Enums
*   `MainRoleType`: Comprehensive list of all roles (Werewolves, Villagers, Ambiguous, Loners, New Moon).
*   `SecondaryRoleType` (Flags enum): `None`, `Lovers`, `Charmed`, `TownCrier`, `Executioner`, `Sheriff`. Stackable roles on top of main roles that are linked to specific GameHooks.

### Night Action & Day Action Enums
*   `NightActionType`: `Unknown`, `WerewolfVictimSelection`, `BigBadWolfVictimSelection`, `WhiteWerewolfVictimSelection`, `AccursedWolfFatherInfection`, `SeerCheck`, `FoxCheck`, `WitchSave`, `WitchKill`, `DefenderProtect`, `PiperCharm`, `RustySword`, `ThiefSwap`, `ActorEmulate`, `WildChildModel`, `CupidLink`, `WolfHoundChoice`.
*   `DayPowerType`: `Unknown`, `JudgeExtraVote`, `DevotedServantSwap`, `TownCrierCardReveal`.
*   `StatusEffectTypes`: `None`, `ElderProtectionLost`, `LycanthropyInfection`, `WildChildChanged`, `LynchingImmunityUsed`.

### Elimination Enum
*   `EliminationReason`: `Unknown`, `WerewolfAttack`, `WitchKill`, `HunterShot`, `LoversHeartbreak`, `RustySword`, `ScapegoatSacrifice`, `EventElimination`, `DayVote`.

### Hook Listener Enums
*   `GameHookListenerType`: `MainRole`, `SpiritCard`, `SecondaryRole`. Used to distinguish between different categories of listeners.
*   `HookListenerOutcome`: `Skip`, `NeedInput`, `Complete`. Communicates listener state machine result back to GameFlowManager.

### Role State Machine Enums
*   `StandardNightRoleState`: `AwaitingAwakeConfirmation`, `AwaitingTargetSelection`, `AwaitingSleepConfirmation`, `Asleep`. Standard state machine for night roles with "wake → select target → sleep" flow.
*   `ImmediateFeedbackNightRoleState`: `AwaitingAwakeConfirmation`, `AwaitingTargetSelection`, `AwaitingModeratorFeedback`, `AwaitingSleepConfirmation`, `Asleep`. Extended state machine for roles requiring immediate moderator feedback during target selection.

### Sub-Phase Enums
*   `NightSubPhases`: `Start`.
*   `DawnSubPhases`: `CalculateVictims`, `AnnounceVictims`, `ProcessRoleReveals`, `Finalize`.
*   `DaySubPhases`: `Debate`, `DetermineVoteType`, `NormalVoting`, `AccusationVoting`, `FriendVoting`, `HandleNonTieVote`, `ProcessVoteOutcome`, `ProcessVoteDeathLoop`, `Finalize`.
*   `VictorySubPhases`: `Complete`.

# Game Loop Outline (Declarative Sub-Phase Architecture)

1.  **Bootstrap (Pre-Phase):**
    *   `GameService.StartNewGame` is called with player names and roles.
    *   `GameService` generates a unique `Guid` for the game session.
    *   `GameService` calls `GameFlowManager.GetInitialInstruction(rolesInPlay, gameId)` to obtain the startup instruction.
    *   `GameService` constructs `GameSession` with the ID and instruction, ensuring atomic validity.
    *   The initial instruction (`StartGameConfirmationInstruction`) is returned to the caller.
    *   When the moderator confirms this instruction, the game begins directly in the Night phase.

2.  **Night Phase (`GamePhase.Night`):**
    *   The `PhaseManager` for `Night` is activated. It begins executing the `NightSubPhases.Start` sub-phase.
    *   The `SubPhaseManager` for `Start` runs its sequence of atomic stages:
        1.  A `LogicSubPhaseStage` issues the "Village goes to sleep" instruction and increments the turn number.
        2.  A `HookSubPhaseStage` fires the `GameHook.NightMainActionLoop`. It iterates through all registered role listeners (`SimpleWerewolfRole`, `SeerRole`, etc.), calling `Execute` on each.
        3.  If a listener needs input, it returns `HookListenerActionResult.NeedInput`, which becomes a `StayInSubPhaseHandlerResult` with an instruction. The `PhaseManager` pauses.
        4.  Once all listeners complete, the `HookSubPhaseStage`'s `onComplete` delegate runs.
        5.  The final `EndNavigationSubPhaseStage` executes, returning a `MainPhaseHandlerResult` to transition to `GamePhase.Dawn`.

3.  **Dawn Phase (`GamePhase.Dawn`):**
    *   The `PhaseManager` for `Dawn` is activated, starting at `DawnSubPhases.CalculateVictims`.
    *   **Calculate Victims:** The `NightInteractionResolver` is invoked to process all night actions, resolving conflicts (Witch vs Defender vs Infection) and applying eliminations/status effects. Navigates either to `AnnounceVictims` or `Finalize`, depending on whether or not there were any night deaths.
    *   **Announce Victims:** If victims exist, the `AnnounceVictims` sub-phase requests role assignments for the victims, and fires `GameHook.PlayerRoleAssignedOnElimination`. Navigates to `Finalize`
    *   **Finalize:** The `Finalize` sub-phase transitions to `GamePhase.Day`.
    *   **Victory Check:** `GameFlowManager` checks for victory conditions.

4.  **Day Phase (`GamePhase.Day`):**
    *   The `PhaseManager` for `Day` starts at `DaySubPhases.Debate`.
    *   **Debate:** Issues an instruction for discussion, then transitions to `DetermineVoteType`.
    *   **Determine Vote Type:** Determines what's the appropriate vote type, checking for active events or modifiers (defaults to `NormalVoting` sub-phase).
    *   **Normal Voting:** Handles standard village voting to end the debate. Transitions to either `HandleNonTieVote` if there was no tie, or `ProcessVoteOutcome` if there was a tie.
    *   **Accusation Voting:** *(Not yet implemented)* Reserved for accusation-based voting mechanics.
    *   **Friend Voting:** *(Not yet implemented)* Reserved for friend-based voting mechanics (e.g., Angel event).
    *   **Handle Non Tie Vote:** Handles checking if the voted for player is susceptible to be actually lynched due to the vote (i.e. Village Idiot), eliminates it if they are, otherwise applies `LynchImmunityUsed` status effect. Transitions to `ProcessVoteOutcome`
    *   **Process Vote Outcome:** Fires `GameHook.OnVoteConcluded`, and then checks where to navigate. Can loop back to `DetermineVoteType` if a re-vote was triggered (i.e. stuttering judge), advance to `ProcessVoteDeathLoop` if there were any voting deaths, or `Finalize` if not.
    *   **Process Vote Death Loop:** Fires `GameHook.PlayerRoleAssignedOnElimination`.
    *   **Finalize:** Transitions to `GamePhase.Night`.
    *   **Victory Check:** `GameFlowManager` checks for victory conditions.

# Game Logs 

**Core Principle:** The `GameHistoryLog` serves as the single, canonical source of truth, containing an append-only record of events that determine the game state. All other game state is treated as derived and either cached or computed on-the-fly.

The chosen approach is an abstract base class (`GameLogEntryBase`) providing universal properties (`Timestamp`, `TurnNumber`, `CurrentPhase`) combined with distinct concrete derived types (preferably records) for each specific loggable event. This flat hierarchy significantly reduces boilerplate for universal fields via the base class while maintaining strong type safety, clarity, and maintainability through specific derived types.

## Mutation Mechanism

*   **`Apply(ISessionMutator)`:** Internal method called by the Kernel when a log entry is being committed. It invokes `InnerApply` and then appends the entry to the log via the mutator.
*   **`InnerApply(ISessionMutator)`:** Abstract protected method that each derived log entry must implement. This is where the actual state mutation logic resides. The method receives an `ISessionMutator` to perform changes and returns the log entry (potentially modified, e.g., if `TurnNumber` changed during application).

**Core Principle:** The `GameHistoryLog` serves as the single, canonical source of truth, containing an append-only record of events that determine the game state.

## Implemented Log Entries

1.  **`AssignRoleLogEntry`:** Records the batch assignment of a `MainRoleType` to one or more players via a `List<Guid>`. Used for initial identification (assigning the same role to multiple players at once, e.g., all Werewolves) and role reveals.
2.  **`DayActionLogEntry`:** Records actions taken during the day (e.g., Sheriff appointment, specific day powers).
3.  **`NightActionLogEntry`:** Records non-deterministic player choices made during the night (e.g., Seer check, Werewolf attack, Witch potion).
4.  **`PhaseTransitionLogEntry`:** Records the transition between main game phases (`Night` -> `Dawn`, etc.).
5.  **`PlayerEliminatedLogEntry`:** Records the elimination of a player and the reason (Vote, Attack, etc.).
6.  **`StatusEffectLogEntry`:** Records the application of a status effect (e.g., `ElderProtectionLost`, `LycanthropyInfection`, `WildChildChanged`, `LynchingImmunityUsed`). Note: Currently only application is implemented; removal is not handled.
7.  **`VictoryConditionMetLogEntry`:** Records that a specific team has met their win condition.
8.  **`VoteOutcomeReportedLogEntry`:** Records the result of a day vote (who was eliminated, or if it was a tie).

This list covers the distinct, loggable events derived from the rules. Each entry captures unique information critical for game logic, auditing, or moderator context.

# Victory Condition Checking:
 
The `GameFlowManager` implements automatic victory condition checking to ensure games end appropriately when win conditions are met: 
 
*   **Automatic Checking:** Victory conditions are automatically evaluated by `GameFlowManager.HandleInput` after specific resolution phases (`Dawn` and `Day`). This ensures immediate game termination when win conditions are achieved. 
*   **Basic Victory Logic (Phase 1):** The current implementation checks fundamental win conditions: 
    *   **Villager Win:** All werewolves eliminated and at least one non-werewolf player remains alive 
    *   **Werewolf Win:** Werewolves equal or outnumber non-werewolves, with at least one Werewolf alive 
*   **Victory Process:** When victory conditions are met: 
    1. `GameFlowManager.CheckVictoryConditions` returns the winning team and description 
    2. `VictoryConditionMetLogEntry` is logged with winning team and description 
    3. Final game over instruction is generated and set as `PendingModeratorInstruction` 
*   **Future Enhancements:** Later phases will expand victory checking to include: 
    *   Lovers win conditions (both lovers alive as last players) 
    *   Solo role win conditions (Angel, Piper, White Werewolf, Prejudiced Manipulator) 
    *   Event-specific win conditions 
    *   Complex role interactions (Charmed players, infected players, etc.) 