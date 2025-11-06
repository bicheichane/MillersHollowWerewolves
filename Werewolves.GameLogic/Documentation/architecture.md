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

This architecture employs a log-driven, encapsulated state management approach with compiler-enforced separation: 

*   **Canonical State Source:** The `GameSession.GameHistoryLog` is the **single, canonical source of truth** for all **persistent, non-deterministic** game events. This append-only event store drives all state mutations through the State Mutator Pattern. 
*   **Derived Cached State:** All in-memory representations of persistent state (e.g., `Player.Status`, `Player.State` properties, `TurnNumber`) are treated as **assembly-scoped derived state caches** that are mutated **exclusively** by applying events from the log. The primary concern here is **architectural integrity** - ensuring single source of truth, controlled mutation through the State Mutator Pattern, compiler-enforced separation between assemblies, and event-driven state reconstruction for correctness and maintainability.
*   **Transient Execution State:** The `GamePhaseStateCache` provides a single source of truth for the game's current execution point, tracking the active phase, sub-phase, hook, and any listener that is paused awaiting input. This cache acts as a transient "program counter" and is automatically cleared between main phases to prevent state leakage. 

# Hook System Architecture

The architecture uses a declarative hook-based system where the `GameFlowManager` acts as a dispatcher rather than an orchestrator: 

*   **Game Hooks:** Declarative events fired at specific moments in the game flow (e.g., `NightSequenceStart`, `OnPlayerEliminationFinalized`) 
*   **Hook Listeners:** Components (roles and events) that register to respond to specific hooks 
*   **Self-Contained State Machines:** Each listener manages its own state and logic, encapsulating all behavior 
*   **Unified State Cache:** Centralized state management for resuming paused operations and tracking execution progress 

# Two-Assembly Architecture

The architecture is split into two separate library projects to achieve compiler-enforced encapsulation: 

*   **`Werewolves.StateModels`:** This library contains the complete state representation of the game. This includes `GameSession`, `Player`, `PlayerState`, all `GameLogEntryBase` derived classes, and all shared `enums`. This project contains no game-specific rules logic (e.g., `GameFlowManager`, roles). Its purpose is to define the state and its mutation mechanisms. 
*   **`Werewolves.GameLogic`:** This library contains the stateless "rules engine," including the `GameFlowManager`, `GameService`, and all `IGameHookListener` implementations (roles and events). This project has a one-way reference to `Werewolves.StateModels` and can only interact with its `public` API. 

# Core Components

## `GameSession` Class (StateModel)

Represents the tracked state of a single ongoing game. 

*   **Canonical State Source:** The `_gameHistoryLog` is the **single, canonical source of truth** for all **persistent, non-deterministic** game events. This append-only event store drives all state mutations through the State Mutator Pattern. 
*   **Derived Cached State:** All in-memory representations of persistent state (e.g., `Player.Status`, `Player.State` properties, `TurnNumber`) are treated as **assembly-scoped derived state caches** that are mutated **exclusively** by applying events from the log. The primary concern here is **architectural integrity** - ensuring single source of truth, controlled mutation through the State Mutator Pattern, compiler-enforced separation between assemblies, and event-driven state reconstruction for correctness and maintainability.
*   **Transient Execution State:** The `GamePhaseStateCache` provides a single source of truth for the game's current execution point, tracking the active phase, sub-phase, hook, and any listener that is paused awaiting input. This cache acts as a transient "program counter" and is automatically cleared between main phases to prevent state leakage. 
*   **Encapsulated Public API:** `GameSession` provides a curated public API for state queries (e.g., `GetPlayerState(Guid)`) and controlled state mutations (e.g., `EliminatePlayer(Guid, EliminationReason)`). All state changes must go through log entries and their `Apply` methods. 
*   **State Mutator Pattern:** `GameLogEntryBase` defines an `internal abstract Apply(GameSession.IStateMutator mutator)` method. `GameSession` defines an `internal IStateMutator` interface and a `private nested` implementation class that has privileged access to `internal` setters of other classes in the same assembly. This ensures that only log entries can modify state. 
*   `Id` (Guid): Unique identifier for the game session. 
*   `Players` (Dictionary<Guid, Player>): Collection of all players in the game, keyed by their unique ID. Tracks player information provided by the moderator. 
*   `PlayerSeatingOrder` (List<Guid>): Stores the Player IDs in clockwise seating order as reported by the Moderator during setup. Crucial for roles like Knight, Fox, Bear Tamer and events like Nightmare, Influences. Established once at game start. 
*   `GamePhaseStateCache` (GamePhaseStateCache): Unified state cache that tracks the current execution point, active hooks, and current listener states. 
*   `TurnNumber` (int): Tracks the number of full Night/Day cycles completed. Initialized to 0 during `Setup`, increments to 1 at the start of the first `Night`. 
*   `RolesInPlay` (List<MainRoleType>): List of main role types included in the game (provided by Moderator at setup). 
*   `EventDeck` (List<EventCard>): Represents the set of event cards included in the physical deck. 
*   `DiscardPile` (List<EventCard>): Event cards reported as drawn by the moderator. 
*   `ActiveEvents` (List<ActiveEventState>): Tracks currently active New Moon events (input by Moderator when drawn) and their specific state data. 
*   `PendingVoteOutcome` (Guid?): Stores the ID of the player reported eliminated in the vote, or `Guid.Empty` for a tie. Cleared after resolution. 
*   `GameHistoryLog` (List<GameLogEntryBase>): A chronological record of all non-deterministic game events, like moderator inputs, event card draws, etc. Anything that cannot be calculated or derived deterministically from the known game state. Uses the `GameLogEntryBase` derived types for structured, strongly-typed entries (see "Setup & Initial State Logs" section for examples). This remains the definitive history and source for resolving game state. 
*   `PendingModeratorInstruction` (ModeratorInstruction?): The current prompt/instruction for the moderator, asking for input or guiding the next step. 
*   `WinningTeam` (Team?): Stores the winning team once determined by `GameFlowManager`. Null otherwise. 
*   **Helper Methods:** 
    *   `FindLogEntries<TLogEntry>(...)`: Searches the `GameHistoryLog` for entries of a specific type `TLogEntry`, optionally filtering by relative turn number (`turnsAgo`), game phase (`phase`), or a custom predicate (`filter`). 
    *   `GetRoleCount(MainRoleType roleType)`: Returns the total count of a specific main role included in the game setup. 
    *   `GetAliveRoleCount(MainRoleType roleType)`: Returns the count of living players known (or deduced) to have a specific main role. 
*   **State Flags & Tracking (Based on Moderator Input):** 
    *   **Derived Cached State (Deterministic Results):** 
        *   `AreVillagerPowersDisabled` (bool): Game-wide flag indicating whether all Villager special abilities have been lost due to Elder elimination by vote. *Causal Logs: `VoteOutcomeReportedLogEntry` (eliminating Elder) & `RoleRevealedLogEntry` (confirming Elder).* 

The chosen architecture utilizes a dedicated `PlayerState` wrapper class. This class contains individual properties (e.g., `IsSheriff`, `LoverId`, `VoteMultiplier`) for all dynamic boolean and data-carrying states, typically using `internal set` for controlled modification. The `Player` class then holds a single instance of `PlayerState`. This approach provides a balance of organization (grouping all volatile states together), strong typing, clear separation of concerns (keeping `Player` focused on identity/role), and scalability for future state additions. 

## `IPlayer` Interface & `Player` Class

Represents a participant and their core identity information. 

*   **Interface-Based Architecture:** The system uses an `IPlayer` interface with a `protected nested Player` implementation within `GameSession` to provide clean abstraction, support testing, and strong encapsulation.
*   **Enhanced Encapsulation through Nesting:** The `Player` class is implemented as a `protected nested class` within `GameSession`, ensuring that only `GameSession` and its `StateMutator` can directly access and modify player instances.
*   **`Player` Class Properties:**
    *   `Id` (Guid): Unique identifier. 
    *   `Name` (string): Player's name. 
    *   `State` (PlayerState): Encapsulates all dynamic, *persistent* states affecting the player. This approach keeps the core Player class focused on identity, while grouping volatile states for better organization and potential future state management enhancements (like serialization or complex transitions). **This reflects the player's current, ongoing condition.** 
*   **Design Philosophy:** The `Player` class maintains only identity information, while all game-related dynamic state is managed through the `PlayerState` property, ensuring clear separation of concerns. State mutations are controlled exclusively through the `StateMutator` pattern to maintain architectural integrity.
 
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

Wrapper class holding all dynamic state information for a `Player`. **Implemented with an `IPlayerState` interface and `protected nested PlayerState` class within `GameSession` to provide clean abstraction, support testing, and strong encapsulation.** This improves organization and separation of concerns. Properties use `internal set` to ensure they are managed exclusively through the `StateMutator` pattern as part of the derived cached state pattern, maintaining state integrity. **These properties represent the *persistent* or *longer-term* aspects of a player's current state (e.g., holding the Sheriff title, being in love, being infected, having used a specific potion). They reflect the player's ongoing status unless changed by a game event.** 

*   **Enhanced Encapsulation through Nesting:** The `PlayerState` class is implemented as a `protected nested class` within `GameSession`, ensuring that only `GameSession` and its `StateMutator` can directly access and modify player state instances.
*   **StateMutator Pattern Integration:** All state mutations are controlled exclusively through the `GameSession.IStateMutator` interface and its `protected StateMutator` implementation. This ensures that only log entries (through their `Apply` methods) can modify player state, maintaining architectural integrity.
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
    *   `IsTransformedToWerewolf` (bool): Indicates whether the Wild Child has transformed into a Werewolf due to their model being eliminated. *Causal Log: Any log leading to the elimination of the Wild Child's model.* 

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

## `GamePhaseStateCache` Record Struct

Unified state cache that serves as the single source of truth for the game's current execution point. Implemented as a `record struct` for value semantics and immutability benefits.

*   **GFM State Tracking:** 
    *   `GetCurrentPhase()` / `TransitionMainPhase(phase)`: Manages current game phase with automatic sub-phase state clearing on phase transitions
    *   `TransitionSubPhase<T>(subPhase)` / `GetSubPhase<T>()`: Manages and retrieves typed sub-phase state with automatic hook state clearing on sub-phase transitions
*   **Hook & Listener State:** 
    *   `TransitionHook(hook)` / `GetActiveHook()`: Manages currently executing hook with automatic listener state clearing on hook transitions
    *   `TransitionListenerAndState<T>(listener, state)` / `GetCurrentListenerState<T>(listener)`: Tracks current listener state
    *   `GetCurrentListener()`: Gets the identifier of the currently active listener
*   **Automatic State Management:** 
    *   All transition methods automatically clear related state to prevent leakage:
        *   `TransitionMainPhase()` clears sub-phase, hook, and listener state
        *   `TransitionSubPhase<T>()` clears hook and listener state
        *   `TransitionHook()` clears listener state
    *   Private cleanup methods ensure proper state isolation: `ClearCurrentSubPhaseState()`, `ClearCurrentHook()`, `ClearCurrentListener()`
*   **Access Strategy:** 
    *   Most methods are `internal` to maintain encapsulation between assemblies
    *   `GetCurrentPhase()` is `public` to allow external main game phase queries

## `GameFlowManager` Class

Acts as a high-level phase controller and reactive hook dispatcher. 

*   **Core Components:** 
    *   `_masterHookListeners` (static Dictionary<GameHook, List<ListenerIdentifier>>): Declarative mapping of hooks to registered listeners, defined at class level 
    *   `_listenerImplementations` (static Dictionary<ListenerIdentifier, IGameHookListener>): Lookup for concrete listener implementations, defined at class level 
    *   `PhaseDefinitions` (static Dictionary<GamePhase, IPhaseDefinition>): Declarative mapping of main phases to their phase definitions, supporting both legacy and new generic implementations
*   **Primary Methods:** 
    *   `HandleInput(GameService service, GameSession session, ModeratorInput input)` (ProcessResult): **The central state machine orchestrator.** 
        *   Retrieves current phase from `GamePhaseStateCache` and executes appropriate phase definition 
        *   Phase definitions dispatch to the correct sub-phase handler based on declarative stage maps 
        *   Sub-Phase handlers use `FireHook(GameHook)` to dispatch to registered listeners 
        *   Manages listener responses, handling `NeedInput` by pausing and `Complete` by continuing 
        *   Performs comprehensive state machine validation with detailed error reporting 
        *   Automatically checks victory conditions after resolution phases 
        *   Returns a `ProcessResult` with the next instruction or error details 
    *   `FireHook(GameHook)`: Dispatches to all registered listeners for the specified hook 
    *   `CheckVictoryConditions(GameSession session)` ((Team WinningTeam, string Description)?): Evaluates win conditions based on current game state and returns victory information if met. 
*   **Declarative Sub-Phase State Machine:** Complex phases use a declarative state machine approach:
    *   `PhaseDefinition<TSubPhaseEnum>`: Generic class that manages a declarative map of sub-phase stages
    *   `SubPhaseStage<TSubPhaseEnum>`: Records that define individual stages with their handlers and valid transitions
    *   Runtime validation ensures all sub-phase and main-phase transitions conform to declared rules
    *   Simple phases use single-state enums for consistency (e.g., `SetupSubPhases.Confirm`)
*   **State Machine Validation:** Comprehensive validation logic ensures: 
    *   All phase transitions match defined rules 
    *   Sub-phase transitions are validated against declarative `PossibleNextSubPhases` sets
    *   Main-phase transitions are validated against declarative `PossibleNextMainPhaseTransitions` sets
    *   Hook dispatching follows proper sequence 
    *   Listener responses are consistent with contracts 
    *   Detailed error messages for internal state machine errors

## `GameService` Class

Orchestrates the game flow based on moderator input and tracked state. **Delegates state machine management to `GameFlowManager` while handling high-level game logic and external interfaces.** 

*   **Public Methods:** 
    *   `StartNewGame(List<string> playerNamesInOrder, List<MainRoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)` (Guid): Creates a new `GameSession`, initializes players, records roles/events, **validates that all `rolesInPlay` are configured in the service's master hook listener lists**, sets initial state in `IntraPhaseStateCache`, logs initial state, and generates the first `ModeratorInstruction`. 
    *   `ProcessModeratorInput(Guid gameId, ModeratorInput input)` (ProcessResult): **The central entry point for processing moderator actions.** 
        *   Retrieves the current `GameSession` and delegates to `GameFlowManager.HandleInput`. 
        *   The `GameFlowManager` handles all state machine logic, validation, and transition management. 
        *   Returns the `ProcessResult` from the state machine containing the next instruction or error. 
    *   `GetCurrentInstruction(Guid gameId)` (ModeratorInstruction): Retrieves the `PendingModeratorInstruction`. 
    *   `GetGameStateView(Guid gameId)` (object): Returns a read-only view/DTO of the tracked game state. 
*   **Internal Logic:** 
    *   Relies on `GameFlowManager` for all state machine operations, phase transitions, and validation. 
    *   Provides hook listener implementations to `GameFlowManager` via dependency injection. 
    *   Handles high-level game setup and external interface concerns. 
    *   Phase-specific logic is now encapsulated within `GameFlowManager` handler methods. 
    *   Victory condition checking is automatically handled by `GameFlowManager` after resolution phases.
 
## `PhaseHandlerResult` Record

The standardized internal return type for all phase handler functions. It communicates the outcome of the handler's execution back to the main `GameFlowManager.HandleInput` loop. 

*   **Purpose:** Provides a structured way for phase handlers to indicate success/failure, request phase transitions, and specify the next moderator instruction. 
*   **Structure:** 
    *   `NextInstruction` (ModeratorInstruction?): The specific instruction for the moderator for the *next* step, if the handler determined it. Ignored if `UseDefaultInstructionForNextPhase` is true. 
    *   `TransitionReason` (PhaseTransitionReason? Enum): If a phase transition occurred, this holds the enum value identifying *why* the transition happened (e.g., `PhaseTransitionReason.VoteTied`, `PhaseTransitionReason.WwActionComplete`). This value **must** match the `ConditionOrReason` of a `PhaseTransitionInfo` defined in the *source* phase's `PossibleTransitions` list in `GameFlowManager`. Null if no transition occurred. 
    *   `UseDefaultInstructionForNextPhase` (bool): If true, signals `GameFlowManager` to ignore `NextInstruction` and instead use the `DefaultEntryInstruction` defined for the *target* phase in `GameFlowManager`. 
    *   `ShouldTransitionPhase` (bool): Indicates whether the handler has changed the game phase and the state machine should process a transition. 
*   **Immutability:** Designed as an immutable record. 
*   **Usage:** Enables the central `GameFlowManager.HandleInput` logic to understand the outcome of a phase handler, validate the resulting state transition against the declared state machine rules, determine the correct next instruction, and validate the expected input for that instruction. **Includes static factory methods for common results:** 
    *   `SuccessTransition()`: For successful phase transitions
    *   `SuccessInternalGeneric()`: For successful handling without phase changes
    *   `Failure()`: For error conditions
 +++++++ REPLACE
 
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
    *   **`ConfirmationInstruction`:** For yes/no confirmations 
    *   **`SelectPlayersInstruction`:** For player selection with `SelectionConstraint` (minimum/maximum counts) 
    *   **`AssignRolesInstruction`:** For role assignment with per-player selectable role lists 
    *   **`SelectOptionsInstruction`:** For option selection from a list of choices 

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
*   `PhaseTransitionReason` (Enum used internally by `GameFlowManager` and `PhaseHandlerResult`): 
    *   `SetupConfirmed`: Transition from Setup to Night when moderator confirms setup is complete 
    *   `NightStarted`: Transition from Night to Night sub-phases 
    *   `NightActionLoopComplete`: Transition from Night to Day_Dawn when all night actions complete 
    *   `DawnVictimsCalculated`: Transition within Day_Dawn phase after victim calculation 
    *   `DawnVictimsAnnounced`: Transition within Day_Dawn phase after victim announcement 
    *   `DawnRoleRevealsComplete`: Transition within Day_Dawn phase after role reveals 
    *   `DawnNoVictimsProceedToDebate`: Transition from Day_Dawn to Day_Debate when no victims 
    *   `DawnVictimsProceedToDebate`: Transition from Day_Dawn to Day_Debate when victims present 
    *   `DebateConfirmedProceedToVote`: Transition from Day_Debate to Day_Vote when moderator confirms debate end 
    *   `VoteOutcomeReported`: Transition from Day_Vote to Day_Dusk when vote outcome is reported 
    *   `VoteResolvedProceedToReveal`: Transition from Day_Dusk to Day_Dawn when player eliminated by vote 
    *   `VoteResolvedTieProceedToNight`: Transition from Day_Dusk to Night when vote ends in tie 
    *   `VictoryConditionMet`: Transition to GameOver when victory conditions are met 
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
    *   `GameService.StartNewGame` initializes `GameSession`, logs `GameStartedLogEntry`, sets initial state in `GamePhaseStateCache` (`Setup` phase), and generates the first `ModeratorInstruction`. 
    *   `GameService.ProcessModeratorInput` delegates to `GameFlowManager.HandleInput`. 
    *   `GameFlowManager` executes the appropriate phase definition, which dispatches to the `HandleSetupConfirmation` sub-phase handler. 
    *   `HandleSetupConfirmation` processes the `Confirmation` input. 
    *   If `true`: Updates cache state to transition to `Night`, logs the transition (`PhaseTransitionLogEntry`, Reason: `SetupConfirmed`), calls `cache.ClearTransientState()`, and returns a `PhaseHandlerResult` with the next instruction and the `SetupConfirmed` reason. 
    *   `GameFlowManager` validates the transition and updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
2.  **Night Phase (`GamePhase.Night`):** 
    *   `GameFlowManager.HandleInput` executes the `PhaseDefinition<NightSubPhases>` which manages declarative sub-phase stages. 
    *   Phase definition retrieves current sub-phase from `GamePhaseStateCache` and routes to the appropriate sub-handler: 
        *   **NightSubPhase.Start**: Issues "Village goes to sleep" instruction, increments turn number, transitions to `ActionLoop`. 
        *   **NightSubPhase.ActionLoop**: Fires `GameHook.NightActionLoop` for registered listeners, manages pause/resume via `GamePhaseStateCache`, transitions to `Day_Dawn` when complete. 
    *   Hook dispatch iterates through registered listeners (e.g., `SimpleWerewolfRole`, `SeerRole`), calling `AdvanceStateMachine` on each. 
    *   Listeners determine if they should act based on game state and their cached state. First listener needing input returns `HookListenerActionResult.NeedInput`, causing processing to pause. 
    *   Roles with first-night-only behavior (e.g., Cupid, Thief, WolfHound, WildChild) check `session.TurnNumber > 1` and return `HookListenerActionResult.Complete()` immediately if true.
    *   Phase definition returns `PhaseHandlerResult` with transition to `Day_Dawn` when all night actions complete. 
    *   `GameFlowManager` validates the transition and updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
3.  **Day Dawn Phase (`GamePhase.Day_Dawn`):** 
    *   `GameFlowManager.HandleInput` executes the `PhaseDefinition<DawnSubPhases>` which manages declarative sub-phase stages. 
    *   Phase definition retrieves current sub-phase from `GamePhaseStateCache` and routes to the appropriate sub-handler: 
        *   **DawnSubPhase.CalculateVictims**: Queries `GameHistoryLog` for night actions, calculates final list of eliminated players, processes cascading effects (Hunter, Lovers), handles moderator input for targets if needed. 
        *   **DawnSubPhase.AnnounceVictims**: Issues single instruction to announce all victims, awaits moderator confirmation. 
        *   **DawnSubPhase.ProcessRoleReveals**: Iterates through eliminated players, issues instruction for each role reveal, pauses/resumes as input received. 
        *   **DawnSubPhase.Finalize**: Transitions to `Day_Debate` with appropriate reason (`DawnNoVictimsProceedToDebate` or `DawnVictimsProceedToDebate`). 
    *   Fires `GameHook.OnPlayerEliminationFinalized` for each eliminated player. 
    *   Hook dispatch allows listeners (e.g., `HunterRole`) to respond to eliminations. 
    *   `GameFlowManager` automatically checks victory conditions after resolution, updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
4.  **Debate Phase (`GamePhase.Day_Debate`):** 
    *   `GameFlowManager.HandleInput` executes the `PhaseDefinition<DayDebateSubPhases>` which routes to the `HandleDayDebateConfirmation` sub-phase handler. 
    *   `HandleDayDebateConfirmation` processes `Confirmation` input. 
    *   If `true`: Updates cache state to transition to `Day_Vote`, fires `GameHook.DayVoteStarted`, logs transition (Reason: `DebateConfirmedProceedToVote`), generates vote prompt instruction, returns `PhaseHandlerResult`. 
    *   `GameFlowManager` updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
5.  **Voting Phase (Standard: `GamePhase.Day_Vote`):** 
    *   `GameFlowManager.HandleInput` executes the `PhaseDefinition<DayVoteSubPhases>` which routes to the `HandleDayVoteProcessOutcome` sub-phase handler. 
    *   `HandleDayVoteProcessOutcome` expects `PlayerSelectionSingle` (outcome). Validates input, stores outcome in `PendingVoteOutcome`, logs `VoteOutcomeReportedLogEntry`. 
    *   Updates cache state to transition to `Day_Dusk`, logs transition (Reason: `VoteOutcomeReported`), generates confirmation prompt for resolution, returns `PhaseHandlerResult`. 
    *   `GameFlowManager` updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
6.  **Vote Resolution Phase (`GamePhase.Day_Dusk`):** 
    *   `GameFlowManager.HandleInput` executes the `PhaseDefinition<DayDuskSubPhases>` which manages declarative sub-phase stages. 
    *   Phase definition retrieves current sub-phase from `GamePhaseStateCache` and routes to the appropriate sub-handler: 
        *   **DayDuskSubPhase.ResolveVote**: Processes vote outcome and handles eliminations. 
        *   **DayDuskSubPhase.TransitionToNext**: Determines next phase based on vote outcome (elimination → Day_Dawn for role reveals, tie → Night for new turn). 
    *   Calls `GameSession.EliminatePlayer`and fires `GameHook.OnPlayerEliminationFinalized` and for each eliminated player. 
    *   `GameFlowManager` automatically checks victory conditions after resolution, updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
7.  **Game Over Phase (`GamePhase.GameOver`):** 
    *   `GameFlowManager.HandleInput` executes the legacy `PhaseDefinition` which routes to the `HandleGameOverPhase`. 
    *   `HandleGameOverPhase` returns `PhaseHandlerResult.Failure` for any input, as the game has ended. 
    *   The final "Game Over" instruction was set when victory was first detected by the automatic victory checking. 
 

# Game Logs 

**Core Principle:** The `GameHistoryLog` serves as the single, canonical source of truth, containing an append-only record of events that cannot be deterministically calculated from prior states. All other game state is treated as derived and either cached or computed on-the-fly.

The chosen approach is an abstract base class (`GameLogEntryBase`) providing universal properties (`Timestamp`, `TurnNumber`, `Phase`) combined with distinct concrete derived types (preferably records) for each specific loggable event. This flat hierarchy significantly reduces boilerplate for universal fields via the base class while maintaining strong type safety, clarity, and maintainability through specific derived types.

## Non-Deterministic Event Logs (Canonical Source of Truth)

These entries represent the canonical source of truth for the game and cannot be deterministically calculated from prior states.

### Setup & Initial State Logs

1.  **Game Started:** Records the initial configuration provided by the moderator - the list of roles included in the deck, the list of players by name/ID, and the list of event cards included in the deck. *Represents: The initial, non-deterministic setup parameters from the moderator.* 

2.  **Initial Role Assignment (`InitialRoleAssignmentLogEntry`):** Records main roles assigned during the *Night 1* identification process. Logs the `PlayerId` and the `AssignedMainRole` (`MainRoleType`). Generated by hook listeners during the `NightSequenceStart` hook when processing moderator input for identification. *Represents: A non-deterministic moderator input identifying a player's role during Night 1 setup.* 

### Player Action & Choice Logs

3.  **Night Action Log Entry (`NightActionLogEntry`):** The primary log for all non-deterministic player choices made during the night. This entry consolidates the specific actions for Seer, Fox, Defender, Piper, Witch, and Werewolf roles. Includes: 
    *   `ActorId` (Guid): ID of the player performing the action. 
    *   `TargetId` (Guid?): ID of the player targeted, if applicable. 
    *   `ActionType` (`NightActionType` Enum): Specifies the type of action performed (e.g., `WerewolfVictimSelection`, `SeerCheck`, `WitchSave`, `WitchKill`, `DefenderProtect`, `PiperCharm`). 
    *   *Represents: The fundamental non-deterministic player choices made during night actions.* 

4.  **Role Revealed (`RoleRevealedLogEntry`):** Logs the ID of a player whose main role card was revealed (due to death, Village Idiot save, Devoted Servant swap, etc.) and the specific `MainRoleType` revealed. Generated after processing moderator input during role reveal events. *Represents: A non-deterministic moderator input that provides new, previously unknown information to the system.* 

5.  **Devoted Servant Swap Executed:** Logs the Servant's ID, the ID of the player they saved from reveal, and the (hidden) role the Servant adopted. *Represents: A non-deterministic player decision.* 

### Event & Vote Logs

6.  **Event Card Drawn:** Logs the specific New Moon Event Card ID and Name drawn at the start of the day. *Represents: A random event reported by the moderator.* 

7.  **Gypsy Question Asked & Answered:** Logs the text of the Spiritualism question asked by the Medium and the "Yes" or "No" answer provided by the Moderator (as the spirit). *Represents: A non-deterministic player choice and moderator response.* 

8.  **Town Crier Event Played:** Logs the specific Event Card ID and Name played by the Town Crier from their hand. *Represents: A non-deterministic player decision.* 

9.  **Sheriff Appointed:** Logs the ID of the player who became Sheriff, the reason (Initial Election, Successor Appointment, Event), and the ID of the predecessor (if any). *Represents: A non-deterministic outcome of a player vote or a predecessor's choice.* 

10. **Stuttering Judge Signaled Second Vote:** Logs that the Judge used their one-time ability to trigger a second vote this day. *Represents: A non-deterministic player decision.* 

11. **Vote Outcome Reported (`VoteOutcomeReportedLogEntry`):** The core moderator input for any vote resolution. This generic entry consolidates the outcomes for standard votes, Nightmare accusations, Great Distrust, and Punishment events. Logs the raw outcome (eliminated player ID or `Guid.Empty` for tie) reported by the Moderator. *Represents: The core moderator input for vote resolution.* 

12. **Scapegoat Voting Restrictions Set:** Logs the decision made by an eliminated Scapegoat regarding who can/cannot vote the following day. *Represents: A non-deterministic player decision.* 

### Game End Log

13. **Victory Condition Met (`VictoryConditionMetLogEntry`):** Logs the determined winning team/player(s) and a brief description of the condition met (e.g., "All Werewolves eliminated," "Werewolves equal Villagers," "All survivors charmed," "Angel eliminated early"). *Represents: While technically a deterministic outcome, this log serves as the explicit, terminal event of the game, which is valuable for auditing and clearly defining the end of the event stream.* 
 
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
