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

This architecture employs a **Kernel-Facade Pattern** with **Event Sourcing** to ensure strict separation between the mutable core and the read-only public API.

*   **Canonical State Source:** The `GameSessionKernel.GameHistoryLog` is the **single, canonical source of truth** for all **state-altering** game events. This includes both **non-deterministic inputs** (moderator choices) and **deterministic consequences** (rule resolutions like infection). This append-only event store drives all state mutations.
*   **The Kernel (Core):** The `GameSessionKernel` is the **sole owner of mutable memory**. It encapsulates the `GameHistoryLog`, `GamePhaseStateCache`, `Players`, `SeatingOrder`, and `RolesInPlay`. It is an internal, hermetically sealed component that cannot be accessed directly by the UI or external consumers.
*   **The Facade (Read-Only Projection & Mutation Gatekeeper):** The `GameSession` class acts as a **read-only projection** of the Kernel for the public API (via `IGameSession`). However, for the `Werewolves.GameLogic` assembly (via `internal` access), it acts as the **Mutation Gatekeeper**, exposing the methods necessary to dispatch commands to the Kernel.
*   **Transactional Mutation:** State mutation follows a strict transactional flow:
    1.  **Command Dispatch:** The Facade constructs a `GameLogEntryBase` (the command) and passes it to the Kernel.
    2.  **Proxy Mutator:** The Kernel creates a temporary `SessionMutator` (a private nested class implementing `ISessionMutator`) that has privileged access to the internal mutable state.
    3.  **Apply:** The `GameLogEntryBase.Apply(ISessionMutator)` method is called, allowing the entry to modify the state via the proxy.
    4.  **Commit:** If successful, the entry is appended to the `GameHistoryLog`. 

# Hook System Architecture

The architecture uses a declarative hook-based system where the `GameFlowManager` acts as a dispatcher rather than an orchestrator: 

*   **Game Hooks:** Declarative events fired at specific moments in the game flow (e.g., `NightSequenceStart`, `OnPlayerEliminationFinalized`) 
*   **Hook Listeners:** Components (roles and events) that register to respond to specific hooks 
*   **Self-Contained State Machines:** Each listener manages its own state and logic, encapsulating all behavior 
*   **Unified State Cache:** Centralized state management for resuming paused operations and tracking execution progress 

# Two-Assembly Architecture

The architecture is split into two separate library projects to achieve compiler-enforced encapsulation: 

*   **`Werewolves.StateModels`:** This library contains the complete state representation of the game. This includes `GameSession`, `Player`, `PlayerState`, all `GameLogEntryBase` derived classes, and all shared `enums`. This project contains no game-specific rules logic (e.g., `GameFlowManager`, roles). Its purpose is to define the state and its mutation mechanisms. 
*   **`Werewolves.GameLogic`:** This library contains the stateless "rules engine," including the `GameFlowManager`, `GameService`, and all `IGameHookListener` implementations (roles and events). This project has a one-way reference to `Werewolves.StateModels`. **Crucially, `Werewolves.StateModels` grants `[InternalsVisibleTo("Werewolves.GameLogic")]`.** This allows the Rules Engine to access the `internal` concrete `GameSession` and its mutation methods, while external consumers (UI) are restricted to the `public` read-only interfaces. 

# Core Components

## `GameSession` Class (Facade) & `GameSessionKernel` (Core)

The architecture separates the public API (`GameSession`) from the internal state container (`GameSessionKernel`) to enforce zero-leakage mutation and strict encapsulation.

**`GameSessionKernel` (Internal Core):**
The hermetically sealed kernel that owns the game's mutable memory. It is not visible to the public API.

*   **Sole Owner of Mutable State:** The Kernel holds the master references to:
    *   `GameHistoryLog` (The event source)
    *   `GamePhaseStateCache` (Transient execution state)
    *   `Players` (Dictionary of concrete `Player` objects)
    *   `SeatingOrder` (List of Guids)
    *   `RolesInPlay` (List of roles)
*   **Private Nested Classes:**
    *   **`Player` & `PlayerState`:** Concrete implementations of `IPlayer` and `IPlayerState` are defined as **private nested classes** within the Kernel. This ensures their setters are physically inaccessible to any code outside the Kernel file.
    *   **`SessionMutator`:** A **private nested class** implementing `ISessionMutator`. This is the "Proxy Mutator" that bridges the gap between the log entry and the private state. It exposes methods like `SetPlayerRole`, `EliminatePlayer`, etc., which internally modify the private `Player` objects.
*   **Transactional Apply Flow:** The Kernel exposes a single entry point for mutation: `RegisterLogEntry(GameLogEntryBase entry)`. This method instantiates a `SessionMutator`, calls `entry.Apply(mutator)`, and if successful, adds the entry to the log.

**`GameSession` (Facade):**
A lightweight, stateless wrapper that implements `IGameSession` and delegates all operations to an internal `_gameSessionKernel` instance. **This class is marked `internal` (or exposes its mutation API as `internal`), making it accessible only to the `Werewolves.GameLogic` assembly.**

*   **Three-Tier Visibility:**
    1.  **Private (Kernel):** The `GameSessionKernel` is completely hidden.
    2.  **Internal (Logic):** The concrete `GameSession` class and its **Internal Command API** (e.g., `AddLogEntry`) are visible to the GameLogic assembly.
    3.  **Public (UI):** External consumers interact exclusively via the `IGameSession` interface, which exposes only read-only properties.
*   **Read-Only Projection (Public):** `GameSession` does not hold state itself. It forwards all property accesses to the Kernel.
    *   `Players`: Returns `IEnumerable<IPlayer>` (projected from Kernel's private players).
    *   `SeatingOrder`: Delegated to Kernel.
    *   `RolesInPlay`: Delegated to Kernel.
    *   `TurnNumber`: Delegated to Kernel.
    *   `GameHistoryLog`: Returns `IEnumerable<GameLogEntryBase>` from the Kernel.
*   **Internal Command API:** `GameSession` provides methods to perform actions (e.g., `AddLogEntry`). These methods do not modify state directly; they pass the request to the Kernel's `RegisterLogEntry` method.
*   **API Surface:**
    *   `Id` (Guid): Unique identifier.
    *   `GamePhaseStateCache` (IGamePhaseStateCache): Exposes the Kernel's cache for reading execution state.
    *   `PendingModeratorInstruction` (ModeratorInstruction?): Delegated to Kernel.
    *   `WinningTeam` (Team?): Delegated to Kernel.
    *   **Helper Methods:** Exposes read-only helpers like `GetPlayer(Guid id)`, `GetAlivePlayers()`, etc., which query the Kernel's state. 

The chosen architecture utilizes a dedicated `PlayerState` wrapper class. This class contains individual properties (e.g., `IsSheriff`, `LoverId`, `VoteMultiplier`) for all dynamic boolean and data-carrying states, typically using `internal set` for controlled modification. The `Player` class then holds a single instance of `PlayerState`. This approach provides a balance of organization (grouping all volatile states together), strong typing, clear separation of concerns (keeping `Player` focused on identity/role), and strict encapsulation. 

## `IPlayer` Interface & `Player` Class

Represents a participant and their core identity information. 

*   **Interface-Based Architecture:** The system uses a `public IPlayer` interface with a `private nested Player` implementation within `GameSessionKernel`. The `GameSession` exposes these instances as `IPlayer` to the UI (read-only). The `Werewolves.GameLogic` assembly cannot interact with `internal` members if necessary as it lacks access to the `private nested PlayerState` class.
*   **Enhanced Encapsulation through Nesting:** The `Player` class is implemented as a `private nested class` within `GameSessionKernel`, ensuring that only `GameSessionKernel` and its `SessionMutator` can directly access and modify player instances.
*   **`Player` Class Properties:**
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
        HookListenerActionResult AdvanceStateMachine(GameSession session, ModeratorResponse input);
        ListenerIdentifier Role { get; }
    }
    ``` 
*   **Accessibility:** The interface is marked as `internal` to hide implementation details from UI clients and ensure these components are only used within the game logic assembly.
*   **Interaction Contract:**  
    *   The `GameFlowManager` dispatches to all listeners registered for a fired hook by calling `AdvanceStateMachine` 
    *   Each listener is responsible for determining if it should act based on game state and cached execution state 
    *   Listeners manage their own state machines and can pause/resume operations using the `GamePhaseStateCache` 
    *   **Return Value Semantics:** The `HookListenerActionResult` communicates the outcome to the dispatcher: 
    *   `HookListenerActionResult.NeedInput(instruction)`: Listener requires further input; processing halts until next input 
    *   `HookListenerActionResult.Complete(optional_instruction)`: Listener completed all actions for this hook invocation 
    *   `HookListenerActionResult.Error(error)`: An error occurred during processing 
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

Wrapper class holding all dynamic state information for a `Player`. **Implemented with an `IPlayerState` interface and `private nested PlayerState` class within `GameSessionKernel` to provide clean abstraction, support testing, and strict encapsulation.** This improves organization and separation of concerns. Properties use `internal set` to ensure they are managed exclusively through the `StateMutator` pattern as part of the derived cached state pattern, maintaining state integrity. **These properties represent the *persistent* or *longer-term* aspects of a player's current state (e.g., holding the Sheriff title, being in love, being infected, having used a specific potion). They reflect the player's ongoing status unless changed by a game event.** 

*   **Enhanced Encapsulation through Nesting:** The `PlayerState` class is implemented as a `private nested class` within `GameSessionKernel`, ensuring that only `GameSessionKernel` and its `SessionMutator` can directly access and modify player state instances.
*   **StateMutator Pattern Integration:** All state mutations are controlled exclusively through the `ISessionMutator` interface and its `private SessionMutator` implementation. This ensures that only log entries (through their `Apply` methods) can modify player state, maintaining architectural integrity.
*   **Core Identity & Role Properties:**
    *   `MainRole` (MainRoleType?): The player's main character role type. Null initially. **Set by hook listeners during the Setup phase (for roles requiring identification) or upon role reveal (death, etc.).** 
    *   `SecondaryRoles` (SecondaryRoleType?): Stackable secondary roles that provide additional abilities linked to specific GameHooks. These can be combined with main roles (e.g., Lovers, Charmed, TownCrier). 
    *   `Health` (PlayerHealth Enum): Current health status (`Alive`, `Dead`). 
    *   `IsMainRoleRevealed` (bool): Flag indicating if the moderator has input this player's main role, **or if the main role was assigned during Setup based on moderator identification**. `true` means the *application* knows the main role. **Implemented as a computed property based on `MainRole` not being null.** 
*   **Boolean States:** 
    *   `IsSheriff` (bool): Indicates if the player currently holds the Sheriff title. 
    *   `IsInLove` (bool): Indicates if the player is part of the Lovers pair. 
    *   `IsInfected` (bool): True if the player was successfully infected by the Accursed Wolf-Father. This is a permanent change towards the Werewolf team. (Ensure Werewolf night logic and Victory conditions correctly account for this). 
    *   `IsProtectedTonight` (bool): True if the Defender chose to protect this player *this* night. Reset each night resolution. 
    *   `IsCharmed` (bool): True if the player has been targeted by the Piper. Does not prevent normal actions but affects Piper's win condition. 
    *   `IsTempWerewolf` (bool): True if the player is temporarily acting as a Werewolf due to an event like Full Moon Rising. Reset when the event expires. 
    *   `CanVote` (bool): Determines if the player can participate in the current vote. Default is true, modified by roles (Village Idiot revealed) or events. 
    *   `IsMuted` (bool): True if the player is prevented from speaking/participating in debate due to event rule violations (e.g., Good Manners). 
    *   `IsIgnoringDebatePeers` (bool): True if the player must ignore others during debate due to an event (e.g., Eclipse). 
    *   `HasUsedAccursedInfection` (bool): Specific flag for the Accursed Wolf-Father player to track if their one-time infection power has been used. *(Reset if Devoted Servant takes role).* 
    *   `HasLostFoxPower` (bool): True if the Fox performed a check that resulted in no Werewolves being detected, permanently disabling their power. *(Reset if Devoted Servant takes role).* 
    *   `HasUsedStutteringJudgePower` (bool): True if the Stuttering Judge has successfully used their once-per-game ability to trigger a second vote. *(Reset if Devoted Servant takes role).* 
    *   `IsTemporarilyRemoved` (bool): True if the player is currently out of the room due to the Little Rascal event. Hook listeners must skip this player during this time. 

*   **Data-Carrying States:** 
    *   `LoverId` (Guid?): Stores the ID of the other player in the Lovers pair, if `IsInLove` is true. 
    *   `VoteMultiplier` (int): The multiplier applied to this player's vote (e.g., 1 for normal, 2 for Sheriff, 3 for Little Rascal). Default is 1. 
    *   `PotionsUsed` (WitchPotionType Flags Enum?): Tracks which of the Witch's single-use potions have been reported as used by the moderator. Should be implemented as a `[Flags]` enum (e.g., `None=0, Healing=1, Poison=2`). *(Reset if Devoted Servant takes role).* 
    *   `WildChildModelId` (Guid?): Stores the ID of the player chosen as the model by the Wild Child. Used to determine when/if the Wild Child transforms. 
    *   `WolfHoundChoice` (Team?): Stores the alignment (Villagers or Werewolves) chosen by the Wolf Hound on Night 1. 
    *   `TimesAttackedByWerewolves` (int): Counter for how many times this player has been the primary target of the Werewolves' night attack. Used specifically for the Elder's survival ability. *(Reset if Devoted Servant takes role).* 
    *   `IsTransformedToWerewolf` (bool): Indicates whether the Wild Child has transformed into a Werewolf due to their model being eliminated. *Causal Log: `PlayerStatusEffectLogEntry` (WildChildTransformation).* 

*Note on Devoted Servant:* When the Devoted Servant swaps roles, the responsible hook listener must explicitly reset any role-specific usage flags or counters (marked with *(Reset if...)* above) on the Servant's `PlayerState` to their default values.* 

## `EventCard` Abstract Class

Base for New Moon event cards (represents the *rules* of the event). Implements `IGameHookListener`. 

*   `Id` (string): Unique identifier (e.g., "FullMoonRising"). 
*   `Name` (string): Event card name. 
*   `Description` (string): Text description of the event. 
*   `Timing` (EventTiming Enum): Primary trigger time (e.g., `NextNight`, `Immediate`, `PermanentAssignment`). 
*   `Duration` (EventDuration Enum): How long the effect lasts (e.g., `OneNight`, `Permanent`, `UntilNextVote`). 
*   `AdvanceStateMachine(GameSession session, ModeratorInput input)` (HookListenerActionResult): Implements the hook listener interface, managing the event's state machine and responding to relevant hooks. 
*   **Concrete Implementations:** (`FullMoonRisingEvent`, `SomnambulismEvent`, `EnthusiasmEvent`, `BackfireEvent`, `NightmareEvent`, `InfluencesEvent`, `ExecutionerEvent`, `DoubleAgentEvent`, `GreatDistrustEvent`, `SpiritualismEvent` (potentially 5 variants or one class handling variants), `NotMeNorWolfEvent`, `MiracleEvent`, `DissatisfactionEvent`, `TheLittleRascalEvent`, `PunishmentEvent`, `EclipseEvent`, `TheSpecterEvent`, `GoodMannersEvent`, `BurialEvent`). Each manages its own state machine and responds to relevant hooks. 

## `ActiveEventState` Class

Stores runtime state for an active event in `GameSession.ActiveEvents`. *known to be in play*. 

*   `EventId` (string): Matches the `EventCard.Id`. 
*   `CardReference` (EventCard): Reference to the static definition of the card for accessing its methods. 
*   `TurnsRemaining` (int?): Countdown for temporary events. Null for permanent. 
*   `StateData` (Dictionary<string, object>): Event-specific runtime data (e.g., who is muted, which question was asked in Spiritualism). 

## `IGamePhaseStateCache` Interface & `GamePhaseStateCache` Struct

Unified state cache that serves as the single source of truth for the game's current execution point. Implemented as a `private record struct` within `GameSessionKernel`, exposed via the `IGamePhaseStateCache` interface. Its primary role is to track the "program counter" of the state machine and enforce atomic execution of sub-phase stages.

*   **Phase and Sub-Phase Tracking:**
    *   `GetCurrentPhase()` / `TransitionMainPhase(phase)`: Manages the current main game phase. Transitioning to a new main phase automatically clears all sub-phase and stage history.
    *   `TransitionSubPhase(subPhase)` / `GetSubPhase<T>()`: Manages the current sub-phase. Transitioning to a new sub-phase clears all stage execution history for that sub-phase.
*   **Atomic Stage Execution:** The cache enforces that each sub-phase stage is an atomic unit of work that executes exactly once per sub-phase entry.
    *   `_currentSubPhaseStage` (private string?): Tracks the stage currently executing, acting as a mutex.
    *   `_previousSubPhaseStages` (private List<string>): Records all stages that have already completed within the current sub-phase.
    *   `TryEnterSubPhaseStage(string)`: Checks if a stage can be executed (i.e., it's not already running and hasn't already completed).
    *   `CompleteSubPhaseStage()`: Marks the current stage as completed.
*   **Listener State:**
    *   `TransitionListenerAndState<T>(listener, state)` / `GetCurrentListenerState<T>(listener)`: Tracks the state of a specific hook listener when it pauses for input.
    *   `GetCurrentListener()`: Gets the identifier of the currently paused listener.
*   **Automatic State Cleanup:**
    *   Transitioning to a new main phase clears all sub-phase and stage data.
    *   Transitioning to a new sub-phase clears all stage execution history, ensuring a clean slate for the new sub-phase's workflow.

## `GameFlowManager` Class

Acts as a high-level phase controller and reactive hook dispatcher. It contains the complete, declarative definition of the game's state machine.

*   **Core Components:**
    *   `HookListeners` (static Dictionary<GameHook, List<ListenerIdentifier>>): Declarative mapping of hooks to the ordered list of listeners that respond to them.
    *   `ListenerImplementations` (static Dictionary<ListenerIdentifier, IGameHookListener>): Lookup for concrete listener implementations (i.e., the role classes).
    *   `PhaseDefinitions` (static Dictionary<GamePhase, IPhaseDefinition>): Declarative mapping of each main `GamePhase` to its corresponding `PhaseManager`.
*   **Primary Methods:**
    *   `HandleInput(GameSession session, ModeratorResponse input)` (ProcessResult): **The central state machine orchestrator.**
        *   Retrieves the current phase and delegates to the appropriate `IPhaseDefinition` (`PhaseManager`).
        *   The `PhaseManager` loops, executing the current `SubPhaseManager`'s sequence of atomic stages until an instruction is generated for the moderator.
        *   It validates all transitions (both sub-phase and main-phase) against the declarative rules.
        *   After a phase handler completes, it checks for victory conditions.
        *   Returns a `ProcessResult` with the next instruction or an error.
    *   `CheckVictoryConditions(GameSession session)`: Evaluates win conditions based on the current game state.
*   **Declarative State Machine Architecture:** The game flow is defined by a hierarchy of declarative components:
    *   **`PhaseManager<TSubPhaseEnum>`**: Manages the flow between sub-phases for a single main `GamePhase`. It contains a dictionary of `SubPhaseManager`s.
    *   **`SubPhaseManager<TSubPhase>`**: Defines a single sub-phase. It contains a linear sequence of `SubPhaseStage`s that are executed in order. It also declares all valid transitions to other sub-phases or main phases.
    *   **`SubPhaseStage`**: An abstract class representing a single, **atomic, non-re-entrant** unit of work. The `GamePhaseStateCache` ensures each stage is executed at most once per sub-phase entry.
        *   `LogicSubPhaseStage`: Executes a custom logic handler.
        *   `HookSubPhaseStage`: Fires a `GameHook` and dispatches to all registered listeners.
        *   `NavigationSubPhaseStage`: A stage that results in a transition to a new sub-phase or main phase. `EndNavigationSubPhaseStage` is a required final stage for any sub-phase.
*   **State Machine Validation:** The architecture provides strong runtime guarantees:
    *   **Transition Validation:** All transitions are validated against the `PossibleNextSubPhases` and `PossibleNextMainPhaseTransitions` sets defined in the `SubPhaseManager`. An illegal transition throws an `InvalidOperationException`.
    *   **Stage Atomicity:** The `GamePhaseStateCache` and `SubPhaseStage.TryExecute` method prevent any stage from being executed more than once within a single sub-phase activation, eliminating the need for idempotent handlers.

## `GameService` Class

Orchestrates the game flow based on moderator input and tracked state. **Delegates state machine management to `GameFlowManager` while handling high-level game logic and external interfaces.** 

*   **Public Methods:** 
    *   `StartNewGame(List<string> playerNamesInOrder, List<MainRoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)` (Guid): Creates a new `GameSession`, initializes players, records roles/events, **validates that all `rolesInPlay` are configured in the service's master hook listener lists**, sets initial state in `IntraPhaseStateCache`, logs initial state, and generates the first `ModeratorInstruction`. 
    *   `ProcessModeratorInput(Guid gameId, ModeratorInput input)` (ProcessResult): **The central entry point for processing moderator actions.** 
        *   Retrieves the current `GameSession` and delegates to `GameFlowManager.HandleInput`. 
        *   The `GameFlowManager` handles all state machine logic, validation, and transition management. 
        *   Returns the `ProcessResult` from the state machine containing the next instruction or error. 
    *   `GetCurrentInstruction(Guid gameId)` (ModeratorInstruction): Retrieves the `PendingModeratorInstruction`. 
    *   `GetGameStateView(Guid gameId)` (IGameSession): Returns the game state via the read-only `IGameSession` interface. This hides the internal mutation methods present on the concrete object, ensuring the UI cannot modify state. 
*   **Internal Logic:** 
    *   Relies on `GameFlowManager` for all state machine operations, phase transitions, and validation. 
    *   Provides hook listener implementations to `GameFlowManager` via dependency injection. 
    *   Handles high-level game setup and external interface concerns. 
    *   Phase-specific logic is now encapsulated within `GameFlowManager` handler methods. 
    *   Victory condition checking is automatically handled by `GameFlowManager` after resolution phases.
 
## `PhaseHandlerResult` Hierarchy

A hierarchy of records represents the outcome of a `SubPhaseStage`'s execution, signaling the intended next step to the `PhaseManager`.

*   `PhaseHandlerResult(ModeratorInstruction? ModeratorInstruction)`: Abstract base record.
*   `MajorNavigationPhaseHandlerResult`: Abstract record for results that cause a transition.
    *   `MainPhaseHandlerResult(ModeratorInstruction?, GamePhase)`: Signals a transition to a new main phase.
    *   `SubPhaseHandlerResult(ModeratorInstruction?, Enum)`: Signals a transition to a new sub-phase within the current main phase.
*   `StayInSubPhaseHandlerResult(ModeratorInstruction?)`: Signals that the state machine should remain in the current sub-phase. If the instruction is `null`, the `PhaseManager` will immediately attempt to execute the next stage in the sequence. If an instruction is provided, processing pauses until the moderator responds.

## Hook System Components
 
*   **`GameHook` Enum:** Defines all possible hook points in the game flow. Examples include: 
    *   `NightSequenceStart`: Fired when night actions begin 
    *   `OnPlayerEliminationFinalized`: Fired when a player elimination is confirmed 
    *   `DayVoteStarted`: Fired when voting phase begins 
    *   `NightResolutionStarted`: Fired when night resolution begins 
    *   `RoleRevealRequested`: Fired when a role needs to be revealed 

*   **`ListenerIdentifier` Struct:** Unified identifier for different types of hook listeners: 
    ```csharp 
    public readonly struct ListenerIdentifier 
    { 
        public GameHookListenerType ListenerType { get; } // Enum: MainRole, Event, SecondaryRole 
        public string ListenerId { get; } // Stores the MainRoleType, SecondaryRoleType, or EventCardType enum value as string for better debugging/logging 
    } 
    ``` 

*   **`HookListenerActionResult` Class:** Standardized return type for `IGameHookListener.AdvanceStateMachine`: 
    *   `NeedInput(instruction)`: Listener requires input, processing pauses 
    *   `Complete(optional_instruction)`: Listener finished, processing continues 
    *   `Error(error)`: Processing failed with error 
 
## State Machine Validation
*   **Purpose:** The `GameFlowManager` implements comprehensive validation to ensure all phase transitions and state changes conform to the defined state machine rules. 
*   **Validation Features:** 
    *   **Phase Transition Validation:** Every phase transition is validated against the `PhaseTransitionInfo` defined in the source phase's `PossibleTransitions` list. 
    *   **Hook Dispatch Validation:** Ensures hooks are fired in correct sequence and listeners respond appropriately. 
    *   **State Cache Validation:** Validates that `IntraPhaseStateCache` operations are consistent with state machine rules. 
    *   **Internal Error Detection:** Catches internal state machine inconsistencies and provides detailed error messages for debugging. 
*   **Error Handling:** Validation failures result in `GameError` objects with specific error codes and descriptive messages, ensuring robust error reporting and debugging capabilities. 

 
## `ModeratorResponse` Class 
Data structure for communication FROM the moderator. 
*   `Type` (enum `ExpectedInputType`): Indicates which optional field below is populated. 
*   `SelectedPlayerIds` (List<Guid>?): IDs of players chosen. **Used for role identification (`PlayerSelectionMultiple`) and vote outcome (`PlayerSelectionSingle`, allowing 0 for tie).** 
*   `AssignedPlayerRoles` (Dictionary<Guid, MainRoleType>?): Player IDs mapped to the main role assigned to them. Used during setup/role assignment phases (e.g., Thief, initial role identification). 
*   `SelectedOption` (string?): Specific text option chosen. 
*   `Confirmation` (bool?): Boolean confirmation. 
 
**Design Note on Vote Input:** 
 
A key design principle for moderator input, especially during voting phases, is minimizing data entry to enhance usability during live gameplay. The application is designed to guide the moderator through the *process* of voting (whether standard or event-driven like Nightmare, Great Distrust, Punishment), reminding them of the relevant rules. However, the actual vote tallying is expected to happen physically among the players. 
 
Consequently, the `ModeratorResponse` structure requires the moderator to provide only the final *outcome* of the vote (e.g., who was eliminated via `SelectedPlayerIds`, where an empty list signifies a tie, or confirmation of other outcomes via `Confirmation`). This approach significantly reduces the moderator's interaction time and minimizes the potential for input errors. The application functions primarily as a streamlined state tracker and procedural guide, accepting the loss of granular vote data in its logs as an acceptable trade-off for improved real-time usability. 
 

 
## `ModeratorInstruction` Class Hierarchy
Polymorphic instruction system for communication TO the moderator. 
*   **Abstract Base Class:** 
    *   `PublicAnnouncement` (string?): Text to be read aloud or displayed publicly to all players. 
    *   `PrivateInstruction` (string?): Text for moderator's eyes only, containing reminders, rules, or guidance. 
    *   `AffectedPlayerIds` (IReadOnlyList<Guid>?): Optional: Player(s) this instruction primarily relates to. 
*   **Concrete Implementations:** Each instruction type has its own `CreateResponse` method for validation and response creation:
*   **`ConfirmationInstruction`:** For yes/no confirmations.
*   **`SelectPlayersInstruction`:** For player selection with `SelectionCountConstraint` (defining min/max counts).
*   **`AssignRolesInstruction`:** For role assignment, validating that assignments match the available roles and player lists.
*   **`SelectOptionsInstruction`:** For option selection from a list of choices.

## Enums
*   `GamePhase`: `Setup`, `Night`, `Day_Dawn`, `Day_Debate`, `Day_Vote`, `Day_Dusk`, `AccusationVoting` (Nightmare), `FriendVoting` (Great Distrust), `GameOver`. 
*   `PlayerHealth`: `Alive`, `Dead`. 
*   `Team` (Represents the fundamental winning factions/conditions): 
    *   Villagers 
    *   Werewolves 
    *   Lovers (Opposing team lovers win condition) 
    *   Solo_WhiteWerewolf 
    *   Solo_Piper 
    *   Solo_Angel (Early win condition) 
    *   Solo_PrejudicedManipulator 
    *   *(Note: This enum defines potential winning states. Determining if one of these states has actually been achieved requires runtime logic within the `GameService`. The `GameService`'s victory condition check compares the current, moderator-known game state (player counts per known faction **based on assigned roles**, revealed roles, specific statuses like Lovers, Charmed, Infected, Angel timing, PM group status) against the requirements for each potential `Team` outcome.)* 
*   `EventTiming`: `Immediate`, `NextNight`, `NextDayVote`, `VictimEffect`, `PermanentAssignment`, `DayAction`. 
*   `EventDuration`: `OneTurn`, `OneNight`, `OneDayCycle`, `Permanent`, `UntilNextVote`. 
*   `ExpectedInputType`: `PlayerSelection`, `AssignPlayerRoles`, `OptionSelection`, `Confirmation`. Corresponds to the populated field in `ModeratorInput`. `AssignPlayerRoles` expects input via `ModeratorInput.AssignedPlayerRoles`. 
*   `WitchPotionType`: `Healing`, `Poison`. (Could be flags). 
*   `NightActionType`: `Unknown`, `WerewolfVictimSelection`, `SeerCheck`, `WitchSave`, `WitchKill`, `DefenderProtect`, `PiperCharm`. 
*   `EliminationReason` (Enum used in `PlayerEliminatedLogEntry`): 
    *   `Unknown` 
    *   `WerewolfAttack` 
    *   `DayVote` 
    *   *(Additional values added in later phases)* 
*   `MainRoleType` (representing the intended values for a MainRoleType Enum): 
    *   **System Types:** Unassigned, Unknown 
    *   **Werewolves:** SimpleWerewolf, BigBadWolf, AccursedWolfFather, WhiteWerewolf 
    *   **Villagers:** SimpleVillager, VillagerVillager, Seer, Cupid, Witch, Hunter, LittleGirl, Defender, Elder, Scapegoat, VillageIdiot, TwoSisters, ThreeBrothers, Fox, BearTamer, StutteringJudge, KnightWithRustySword 
    *   **Ambiguous:** Thief, DevotedServant, Actor, WildChild, WolfHound 
    *   **Loners:** Angel, Piper, PrejudicedManipulator 
    *   **New Moon Roles:** Gypsy, TownCrier 
*   `SecondaryRoleType` (Flags enum for stackable secondary roles): 
    *   **Secondary Roles:** Lovers, Charmed, TownCrier 
*   **Sub-Phase Enums:**
    *   `NightSubPhases`: `Start`, `ActionLoop` - Internal sub-phases for consolidated Night phase
    *   `DawnSubPhases`: `CalculateVictims`, `AnnounceVictims`, `ProcessRoleReveals`, `Finalize` - Internal sub-phases for consolidated Day_Dawn phase
    *   `DayDuskSubPhases`: `ResolveVote`, `TransitionToNext` - Internal sub-phases for Day_Dusk phase
*   `ErrorType`: Defines the high-level categories of game errors. 
    *   `Unknown` 
    *   `GameNotFound` 
    *   `InvalidInput` 
    *   `RuleViolation` 
    *   `InvalidOperation` 
*   `GameErrorCode`: Defines specific error codes, grouped by their `ErrorType` using prefixes. 
    *   **Game Not Found:** 
        *   `GameNotFound_SessionNotFound` 
    *   **Invalid Input:** 
        *   `InvalidInput_TypeMismatch` 
        *   `InvalidInput_RequiredDataMissing` 
        *   `InvalidInput_PlayerIdNotFound` 
        *   `InvalidInput_RoleNameNotFound` 
        *   `InvalidInput_OptionNotAvailable` 
        *   `InvalidInput_InvalidPlayerSelectionCount` 
    *   **Rule Violation:** 
        *   `RuleViolation_TargetIsDead` 
        *   `RuleViolation_TargetIsInvalid` 
        *   `RuleViolation_TargetIsSelf` 
        *   `RuleViolation_TargetIsAlly` 
        *   `RuleViolation_DefenderRepeatTarget` 
        *   `RuleViolation_WitchPotionAlreadyUsed` 
        *   `RuleViolation_AccursedInfectionAlreadyUsed` 
        *   `RuleViolation_PowerLostOrUnavailable` 
        *   `RuleViolation_LoverVotingAgainstLover` 
        *   `RuleViolation_VoterIsInvalid` 
        *   `RuleViolation_EventRuleConflict` 
        *   `RuleViolation_PlayerAlreadyHasRole` 
    *   **Invalid Operation:** 
        *   `InvalidOperation_GameIsOver` 
        *   `InvalidOperation_ActionNotInCorrectPhase` 
        *   `InvalidOperation_UnexpectedInput` 
    *   **Unknown/Internal:** 
        *   `Unknown_InternalError` 

# Game Loop Outline (Declarative Sub-Phase Architecture)

1.  **Setup Phase (`GamePhase.Setup`):**
    *   `GameService.StartNewGame` initializes the `GameSession` and sets the initial phase to `Setup`.
    *   The `PhaseManager` for `Setup` executes its single `SubPhaseManager`.
    *   Its `EndNavigationSubPhaseStage` runs, processing the moderator's confirmation.
    *   If confirmed, it returns a `MainPhaseHandlerResult` to transition to `GamePhase.Night`.
    *   The `PhaseManager` validates this transition and the `GameFlowManager` updates the session.

2.  **Night Phase (`GamePhase.Night`):**
    *   The `PhaseManager` for `Night` is activated. It begins executing the `NightSubPhases.Start` sub-phase.
    *   The `SubPhaseManager` for `Start` runs its sequence of atomic stages:
        1.  A `LogicSubPhaseStage` issues the "Village goes to sleep" instruction and increments the turn number.
        2.  A `HookSubPhaseStage` fires the `GameHook.NightActionLoop`. It iterates through all registered role listeners (`SimpleWerewolfRole`, `SeerRole`, etc.), calling `AdvanceStateMachine` on each.
        3.  If a listener needs input, it returns `HookListenerActionResult.NeedInput`, which becomes a `StayInSubPhaseHandlerResult` with an instruction. The `PhaseManager` pauses.
        4.  Once all listeners complete, the `HookSubPhaseStage`'s `onComplete` delegate runs.
        5.  The final `EndNavigationSubPhaseStage` executes, returning a `MainPhaseHandlerResult` to transition to `GamePhase.Dawn`.

3.  **Dawn Phase (`GamePhase.Dawn`):**
    *   The `PhaseManager` for `Dawn` is activated, starting at `DawnSubPhases.CalculateVictims`.
    *   The `SubPhaseManager` for `CalculateVictims` runs its `EndNavigationSubPhaseStage`. This stage queries the log for night actions, determines victims, and eliminates them. It returns a `SubPhaseHandlerResult` to transition to either `AnnounceVictims` (if there are victims) or `Finalize` (if not).
    *   If `AnnounceVictims`, its `SubPhaseManager` executes a sequence of `LogicSubPhaseStage`s to first request role assignments for the victims and then process the moderator's response. It then transitions to `Finalize`.
    *   The `Finalize` sub-phase's `EndNavigationSubPhaseStage` transitions to `GamePhase.Day`.
    *   After the `Dawn` phase logic completes, `GameFlowManager` automatically checks for victory conditions.

4.  **Day Phase (`GamePhase.Day`):**
    *   The `PhaseManager` for `Day` starts at `DaySubPhases.Debate`.
    *   The `Debate` sub-phase issues an instruction and transitions to `NormalVoting`.
    *   The `NormalVoting` sub-phase is a two-stage process:
        1.  A `LogicSubPhaseStage` issues a `SelectPlayersInstruction` to get the vote outcome.
        2.  An `EndNavigationSubPhaseStage` processes the moderator's response, eliminates the player (if any), and transitions to either `ProcessVoteRoleReveal` (on elimination) or `Finalize` (on a tie).
    *   The `ProcessVoteRoleReveal` sub-phase follows the same two-stage request/response pattern to reveal the eliminated player's role, then transitions to `Finalize`.
    *   The `Finalize` sub-phase transitions to `GamePhase.Night`.
    *   After the `Day` phase logic completes (specifically after vote resolution), `GameFlowManager` automatically checks for victory conditions.

5.  **Game Over Phase (`GamePhase.GameOver`):**
    *   This phase is entered when `CheckVictoryConditions` returns a winner.
    *   The `GameFlowManager` issues a final `FinishedGameConfirmationInstruction`. Any subsequent input is effectively ignored as the game is over.
 

# Game Logs 

**Core Principle:** The `GameHistoryLog` serves as the single, canonical source of truth, containing an append-only record of events that determine the game state. All other game state is treated as derived and either cached or computed on-the-fly.

The chosen approach is an abstract base class (`GameLogEntryBase`) providing universal properties (`Timestamp`, `TurnNumber`, `Phase`) combined with distinct concrete derived types (preferably records) for each specific loggable event. This flat hierarchy significantly reduces boilerplate for universal fields via the base class while maintaining strong type safety, clarity, and maintainability through specific derived types.

## Non-Deterministic Event Logs (Canonical Source of Truth)

These entries represent the canonical source of truth for the game.

### Setup & Initial State Logs

1.  **Game Started:** Records the initial configuration provided by the moderator - the list of roles included in the deck, the list of players by name/ID, and the list of event cards included in the deck. *Represents: The initial, non-deterministic setup parameters from the moderator.* 

2.  **Initial Role Assignment (`InitialRoleAssignmentLogEntry`):** Records main roles assigned during the *Night 1* identification process. Logs the `PlayerId` and the `AssignedMainRole` (`MainRoleType`). Generated by hook listeners during the `NightSequenceStart` hook when processing moderator input for identification. *Represents: A non-deterministic moderator input identifying a player's role during Night 1 setup.* 

### Player Action & Choice Logs

3.  **Night Action Log Entry (`NightActionLogEntry`):** The primary log for all non-deterministic player choices made during the night. This entry consolidates the specific actions for Seer, Fox, Defender, Piper, Witch, and Werewolf roles. Includes: 
    *   `ActorId` (Guid): ID of the player performing the action. 
    *   `TargetId` (Guid?): ID of the player targeted, if applicable. 
    *   `ActionType` (`NightActionType` Enum): Specifies the type of action performed (e.g., `WerewolfVictimSelection`, `SeerCheck`, `WitchSave`, `WitchKill`, `DefenderProtect`, `PiperCharm`). 
    *   *Represents: The fundamental non-deterministic player choices made during night actions.* 

4.  **Role Assignment (`AssignRoleLogEntry`):** Logs the assignment of a `MainRoleType` to one or more `PlayerId`s. It is used for both Night 1 identification and any subsequent role reveals (e.g., after death). *Represents: A non-deterministic moderator input that provides new, previously unknown information to the system.* 

5.  **Devoted Servant Swap Executed:** Logs the Servant's ID, the ID of the player they saved from reveal, and the (hidden) role the Servant adopted. *Represents: A non-deterministic player decision.* 

### Consequential State Logs (Deterministic Resolutions)

These entries record the **result** of a rule resolution. While technically deterministic based on the logic at the time of execution, they are logged to explicitly freeze the state change and decouple history from logic versions.

6.  **Player Status Effect (`PlayerStatusEffectLogEntry`):** Records the application of a unary state mutation (defined by `StatusEffectType`) to a specific player. Used for persistent effects like `ElderProtectionLost`, `LycanthropyInfection`, `WildChildTransformation`, etc. *Represents: The frozen consequence of a rule resolution.*

### Event & Vote Logs

7.  **Event Card Drawn:** Logs the specific New Moon Event Card ID and Name drawn at the start of the day. *Represents: A random event reported by the moderator.* 

8.  **Gypsy Question Asked & Answered:** Logs the text of the Spiritualism question asked by the Medium and the "Yes" or "No" answer provided by the Moderator (as the spirit). *Represents: A non-deterministic player choice and moderator response.* 

9.  **Town Crier Event Played:** Logs the specific Event Card ID and Name played by the Town Crier from their hand. *Represents: A non-deterministic player decision.* 

10. **Sheriff Appointed:** Logs the ID of the player who became Sheriff, the reason (Initial Election, Successor Appointment, Event), and the ID of the predecessor (if any). *Represents: A non-deterministic outcome of a player vote or a predecessor's choice.* 

11. **Stuttering Judge Signaled Second Vote:** Logs that the Judge used their one-time ability to trigger a second vote this day. *Represents: A non-deterministic player decision.* 

12. **Vote Outcome Reported (`VoteOutcomeReportedLogEntry`):** The core moderator input for any vote resolution. This generic entry consolidates the outcomes for standard votes, Nightmare accusations, Great Distrust, and Punishment events. Logs the raw outcome (eliminated player ID or `Guid.Empty` for tie) reported by the Moderator. *Represents: The core moderator input for vote resolution.* 

13. **Scapegoat Voting Restrictions Set:** Logs the decision made by an eliminated Scapegoat regarding who can/cannot vote the following day. *Represents: A non-deterministic player decision.* 

### Game End Log

14. **Victory Condition Met (`VictoryConditionMetLogEntry`):** Logs the determined winning team/player(s) and a brief description of the condition met (e.g., "All Werewolves eliminated," "Werewolves equal Villagers," "All survivors charmed," "Angel eliminated early"). *Represents: While technically a deterministic outcome, this log serves as the explicit, terminal event of the game, which is valuable for auditing and clearly defining the end of the event stream.* 
 
# Victory Condition Checking:
 
The `GameFlowManager` implements automatic victory condition checking to ensure games end appropriately when win conditions are met: 
 
*   **Automatic Checking:** Victory conditions are automatically evaluated by `GameFlowManager.HandleInput` after specific resolution phases (`Day_Dawn` and `Day_Dusk`). This ensures immediate game termination when win conditions are achieved. 
*   **Basic Victory Logic (Phase 1):** The current implementation checks fundamental win conditions: 
    *   **Villager Win:** All werewolves eliminated and at least one non-werewolf player remains alive 
    *   **Werewolf Win:** Werewolves equal or outnumber non-werewolves, with at least one Werewolf alive 
*   **Victory Process:** When victory conditions are met: 
    1. `GameFlowManager.CheckVictoryConditions` returns the winning team and description 
    2. `session.WinningTeam` is set to the determined winner 
    3. Game transitions to `GameOver` phase with `VictoryConditionMet` reason 
    4. `VictoryConditionMetLogEntry` is logged with winning team and description 
    5. Final game over instruction is generated and set as `PendingModeratorInstruction` 
*   **Future Enhancements:** Later phases will expand victory checking to include: 
    *   Lovers win conditions (both lovers alive as last players) 
    *   Solo role win conditions (Angel, Piper, White Werewolf, Prejudiced Manipulator) 
    *   Event-specific win conditions 
    *   Complex role interactions (Charmed players, infected players, etc.) 
 
This list aims to cover the distinct, loggable events derived from the rules. Each entry captures unique information critical for game logic, auditing, or moderator context.
