**Project:** `Werewolves.Core` (.NET Class Library) 
 
**Goal:** To provide the core game logic and state management for a Werewolves of Miller's Hollow moderator helper application, handling rules and interactions based on the provided rulebook pages (excluding Buildings/Village expansion, but including specified New Moon events). The app **tracks the game state based on moderator input**. It assumes moderator input is accurate and provides deterministic state tracking and guidance based on that input. 
 
**String Management Principle:** To ensure maintainability, localization capabilities, and type safety: 
*   All user-facing strings (e.g., moderator instructions, log entry descriptions, error messages displayed to the user) **must** be defined in the `Resources/GameStrings.resx` file and accessed via the generated `GameStrings` class. 
*   Internal identifiers or constants used purely for logic (e.g., specific action types for conditional checks) should strongly prefer the use of dedicated `enum` types over raw string literals to avoid weakly-typed comparisons and improve code clarity. 
 
**State Management Philosophy:** 
 
This architecture employs a hook-based, delegated state machine approach with unified state caching: 
 
*   **Current Persistent State:** The `Player` class and its contained `PlayerState` object store the *current, effective* status, assigned role, and persistent attributes of a player (e.g., `Status`, `IsSheriff`, `IsInLove`, `IsInfected`, `PotionsUsed`). These properties reflect the player's ongoing condition unless explicitly changed by a game event. They provide fast access to the "now" of the player's state. 
*   **Event History & Transient State:** The `GameSession.GameHistoryLog` serves as the immutable, chronological record of *all* significant game events (actions, state changes, eliminations). It's the definitive source of truth for *how* the game state evolved.  
*   **Unified Execution State:** The `GamePhaseStateCache` provides a single source of truth for the game's current execution point, tracking the active phase, sub-phase, hook, and any listener that is paused awaiting input. This cache acts as a transient "program counter" and is automatically cleared between main phases to prevent state leakage. 
 
**Hook System Architecture:** 
 
The architecture uses a declarative hook-based system where the `GameFlowManager` acts as a dispatcher rather than an orchestrator: 
 
*   **Game Hooks:** Declarative events fired at specific moments in the game flow (e.g., `NightSequenceStart`, `OnPlayerEliminationFinalized`) 
*   **Hook Listeners:** Components (roles and events) that register to respond to specific hooks 
*   **Self-Contained State Machines:** Each listener manages its own state and logic, encapsulating all behavior 
*   **Unified State Cache:** Centralized state management for resuming paused operations and tracking execution progress 
 
**Core Components:** 
 
The central `PlayerSeatingOrder` list in `GameSession` provides architectural separation, treating the static seating arrangement as a structural property of the game session. It offers a clear single source of truth for the order. Helper methods within `GameService` encapsulate the logic for retrieving neighbors, including handling skips over eliminated players. 
 
1.  **`GameSession` Class:** Represents the tracked state of a single ongoing game, derived from moderator input. 
    *   `Id` (Guid): Unique identifier for the game session. 
    *   `Players` (Dictionary<Guid, Player>): Collection of all players in the game, keyed by their unique ID. Tracks player information provided by the moderator. 
    *   `PlayerSeatingOrder` (List<Guid>): Stores the Player IDs in clockwise seating order as reported by the Moderator during setup. Crucial for roles like Knight, Fox, Bear Tamer and events like Nightmare, Influences. Established once at game start. (Implemented as `List<Guid>`). 
    *   `GamePhaseStateCache` (GamePhaseStateCache): Unified state cache that tracks the current execution point, active hooks, and current listener states. This replaces the previous `GamePhase` tracking and `IntraPhaseRoleStates`. 
    *   `TurnNumber` (int): Tracks the number of full Night/Day cycles completed. Initialized to 0 during `Setup`, increments to 1 at the start of the first `Night`. 
    *   `RolesInPlay` (List<RoleType>): List of role types included in the game (provided by Moderator at setup). 
    *   `EventDeck` (List<EventCard>): Represents the set of event cards included in the physical deck. 
    *   `DiscardPile` (List<EventCard>): Event cards reported as drawn by the moderator. 
    *   `ActiveEvents` (List<ActiveEventState>): Tracks currently active New Moon events (input by Moderator when drawn) and their specific state data. 
    *   `GameHistoryLog` (List<GameLogEntryBase>): A chronological record of all significant game events, moderator inputs, state changes, and action outcomes tracked during the session. Uses the `GameLogEntryBase` derived types for structured, strongly-typed entries (see "Setup & Initial State Logs" section for examples). This remains the definitive history and source for resolving game state. 
    *   `PendingModeratorInstruction` (ModeratorInstruction?): The current prompt/instruction for the moderator, asking for input or guiding the next step. 
    *   `WinningTeam` (Team?): Stores the winning team once determined by `GameFlowManager`. Null otherwise. 
    *   **Helper Methods:** 
        *   `FindLogEntries<TLogEntry>(...)`: Searches the `GameHistoryLog` for entries of a specific type `TLogEntry`, optionally filtering by relative turn number (`turnsAgo`), game phase (`phase`), or a custom predicate (`filter`). 
        *   `GetRoleCount(RoleType roleType)`: Returns the total count of a specific role included in the game setup. 
        *   `GetAliveRoleCount(RoleType roleType)`: Returns the count of living players known (or deduced) to have a specific role. 
    *   **State Flags & Tracking (Based on Moderator Input):** 
        *   `SheriffPlayerId` (Guid?): ID of the current Sheriff (input by Moderator). 
        *   `Lovers` (Tuple<Guid, Guid>?): IDs of the two players linked by Cupid (input by Moderator). 
        *   `InfectedPlayerIds` (HashSet<Guid>): IDs of players identified as infected (input by Moderator). 
        *   `ProtectedPlayerId` (Guid?): ID of the player protected by the Defender *this night* (input by Moderator). 
        *   `LastProtectedPlayerId` (Guid?): ID of the player protected by the Defender on the *previous* night (to enforce the no-repeat rule). Updated during night resolution. 
        *   `CharmedPlayerIds` (HashSet<Guid>): IDs of players identified as charmed (input by Moderator). 
        *   `ExecutionerPlayerId` (Guid?): ID of the elected Executioner (input by Moderator when event drawn). 
        *   `DoubleAgentPlayerId` (Guid?): ID of the secret Double Agent (input by Moderator when event drawn). 
        *   `TownCrierPlayerId` (Guid?): ID of the player currently designated as Town Crier by the Sheriff. 
        *   `FirstWerewolfVictimId` (Guid?): Tracks the ID of the *first* player reported killed by Werewolves (for Spiritualism). 
        *   `PlayerVoteModifiers` (Dictionary<Guid, int>): Tracks vote multipliers (e.g., Little Rascal). 
        *   `PendingEliminations` (Queue<Guid>): Players awaiting elimination resolution due to cascading effects (calculated based on game rules and tracked state). 
        *   `PendingKnightCurseTarget` (Guid?): Stores the ID of the Werewolf targeted by the Knight's curse, to be eliminated the *following* night resolution phase. 
        *   `VoteResultsCache` (Dictionary<Guid, int>?): Stores results from the current vote phase (input by Moderator). **Removed/Replaced by PendingVoteOutcome in Phase 1.** 
        *   `PendingVoteOutcome` (Guid?): Stores the ID of the player reported eliminated in the vote, or `Guid.Empty` for a tie. Cleared after resolution. 
        *   `AccusationResultsCache` (Dictionary<Guid, int>?): Stores results from Nightmare accusation phase (input by Moderator). 
        *   `FriendVoteCache` (Dictionary<Guid, int>?): Stores results from Great Distrust friend vote phase (input by Moderator). 
        *   `LastEliminatedPlayerId` (Guid?): Tracks the most recently eliminated player for event triggers. 
        *   `PrejudicedManipulatorGroups` (Dictionary<Guid, int>?): Optional mapping of player ID to group number for PM. 
 
--------------- 
 
The chosen architecture utilizes a dedicated `PlayerState` wrapper class. This class contains individual properties (e.g., `IsSheriff`, `LoverId`, `VoteMultiplier`) for all dynamic boolean and data-carrying states, typically using `internal set` for controlled modification. The `Player` class then holds a single instance of `PlayerState`. This approach provides a balance of organization (grouping all volatile states together), strong typing, clear separation of concerns (keeping `Player` focused on identity/role), and scalability for future state additions. 
 
2.  **`Player` Class:** Represents a participant and the tracked information about them. 
    *   `Id` (Guid): Unique identifier. 
    *   `Name` (string): Player's name. 
    *   `Role` (IRole?): The player's character role instance. Null initially. **Set by hook listeners during the Setup phase (for roles requiring identification) or upon role reveal (death, etc.).** 
    *   `Status` (PlayerStatus Enum): Current status (`Alive`, `Dead`). 
    *   `IsRoleRevealed` (bool): Flag indicating if the moderator has input this player's role, **or if the role was assigned during Setup based on moderator identification**. `true` means the *application* knows the role. **Implemented as a computed property based on `Role` not being null.** 
    *   `State` (PlayerState): Encapsulates all dynamic, *persistent* states affecting the player (e.g., Sheriff status, protection, infection, charms, modifiers). This approach keeps the core Player class focused on identity and role, while grouping volatile states for better organization and potential future state management enhancements (like serialization or complex transitions). **This reflects the player's current, ongoing condition.** 
 
-------------- 
 
3.  **`IGameHookListener` Interface:** Defines the contract for components that respond to game hooks (represents the *rules* of roles and events). 
    *   **Method Signature:** 
        ```csharp 
        HookListenerActionResult AdvanceStateMachine(GameSession session, ModeratorInput input); 
        ``` 
    *   **Interaction Contract:**  
        *   The `GameFlowManager` dispatches to all listeners registered for a fired hook by calling `AdvanceStateMachine` 
        *   Each listener is responsible for determining if it should act based on game state and cached execution state 
        *   Listeners manage their own state machines and can pause/resume operations using the `IntraPhaseStateCache` 
        *   **Return Value Semantics:** The `HookListenerActionResult` communicates the outcome to the dispatcher: 
        *   `HookListenerActionResult.NeedInput(instruction)`: Listener requires further input; processing halts until next input 
        *   `HookListenerActionResult.Complete(optional_instruction)`: Listener completed all actions for this hook invocation 
        *   `HookListenerActionResult.Error(error)`: An error occurred during processing 
    *   **Concrete Implementations:** All role classes (`SimpleVillagerRole`, `SeerRole`, `WitchRole`, etc.) and event card classes implement this interface, containing their complete state machine logic. 
    *   **TurnNumber Pattern for First-Night-Only Roles:** Roles with actions exclusive to the first night (e.g., Cupid, Thief, WolfHound, WildChild) **must** include a check at the beginning of their `AdvanceStateMachine` method: `if (session.TurnNumber > 1) { return HookListenerActionResult.Complete(); }`. This delegates the responsibility for turn-specific behavior to the role itself, adhering to the "Self-Contained State Machines" principle.
 
--------------------- 
 
4.  **`PlayerState` Class:** Wrapper class holding all dynamic state information for a `Player`. **Implemented as an inner class within `Player.cs`**. This improves organization and separation of concerns. Properties typically use `internal set` to allow modification primarily by hook listeners or internal logic, maintaining state integrity. **These properties represent the *persistent* or *longer-term* aspects of a player's current state (e.g., holding the Sheriff title, being in love, being infected, having used a specific potion). They reflect the player's ongoing status unless changed by a game event.** 
    *   **Boolean States:** 
        *   `IsSheriff` (bool): Indicates if the player currently holds the Sheriff title. 
        *   `IsInLove` (bool): Indicates if the player is part of the Lovers pair. 
        *   `IsProtectedTonight` (bool): True if the Defender chose to protect this player *this* night. Reset each night resolution. 
        *   `IsInfected` (bool): True if the player was successfully infected by the Accursed Wolf-Father. This is a permanent change towards the Werewolf team. (Ensure Werewolf night logic and Victory conditions correctly account for this). 
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
 
*Note on Devoted Servant:* When the Devoted Servant swaps roles, the responsible hook listener must explicitly reset any role-specific usage flags or counters (marked with *(Reset if...)* above) on the Servant's `PlayerState` to their default values.* 
 
---------------------- 
 
5.  **`EventCard` Abstract Class:** Base for New Moon event cards (represents the *rules* of the event). Implements `IGameHookListener`. 
    *   `Id` (string): Unique identifier (e.g., "FullMoonRising"). 
    *   `Name` (string): Event card name. 
    *   `Description` (string): Text description of the event. 
    *   `Timing` (EventTiming Enum): Primary trigger time (e.g., `NextNight`, `Immediate`, `PermanentAssignment`). 
    *   `Duration` (EventDuration Enum): How long the effect lasts (e.g., `OneNight`, `Permanent`, `UntilNextVote`). 
    *   `AdvanceStateMachine(GameSession session, ModeratorInput input)` (HookListenerActionResult): Implements the hook listener interface, managing the event's state machine and responding to relevant hooks. 
    *   **Concrete Implementations:** (`FullMoonRisingEvent`, `SomnambulismEvent`, `EnthusiasmEvent`, `BackfireEvent`, `NightmareEvent`, `InfluencesEvent`, `ExecutionerEvent`, `DoubleAgentEvent`, `GreatDistrustEvent`, `SpiritualismEvent` (potentially 5 variants or one class handling variants), `NotMeNorWolfEvent`, `MiracleEvent`, `DissatisfactionEvent`, `TheLittleRascalEvent`, `PunishmentEvent`, `EclipseEvent`, `TheSpecterEvent`, `GoodMannersEvent`, `BurialEvent`). Each manages its own state machine and responds to relevant hooks. 
 
--------------------- 
 
6.  **`ActiveEventState` Class:** Stores runtime state for an active event in `GameSession.ActiveEvents`. *known to be in play*. 
    *   `EventId` (string): Matches the `EventCard.Id`. 
    *   `CardReference` (EventCard): Reference to the static definition of the card for accessing its methods. 
    *   `TurnsRemaining` (int?): Countdown for temporary events. Null for permanent. 
    *   `StateData` (Dictionary<string, object>): Event-specific runtime data (e.g., who is muted, which question was asked in Spiritualism). 
 
----------------------- 
 
7.  **`GamePhaseStateCache` Class:** Unified state cache that serves as the single source of truth for the game's current execution point. 
    *   **GFM State Tracking:** 
        *   `GetCurrentPhase()` / `SetCurrentPhase(phase)`: Manages current game phase 
        *   `SetSubPhase<T>(subPhase)` / `GetGfmSubPhase<T>()`: Manages and retrieves typed sub-phase state 
    *   **Hook & Listener State:** 
        *   `SetActiveHook(hook)` / `GetActiveHook()` / `CompleteHook()`: Manages currently executing hook 
        *   `SetCurrentListenerState<T>(listener, state)` / `GetCurrentListenerState<T>(listener)`: Tracks current listener state 
        *   `CompleteCurrentListener()`: Clears current listener state 
    *   **Mandatory Cleanup:** 
        *   `ClearTransientState()`: Called by `GameFlowManager` between main phases to prevent state leakage 
 
----------------------- 
 
8.  **`GameFlowManager` Class:** Acts as a high-level phase controller and reactive hook dispatcher. 
    *   **Core Components:** 
        *   `_masterHookListeners` (static Dictionary<GameHook, List<ListenerIdentifier>>): Declarative mapping of hooks to registered listeners, defined at class level 
        *   `_listenerImplementations` (static Dictionary<ListenerIdentifier, IGameHookListener>): Lookup for concrete listener implementations, defined at class level 
    *   **Primary Methods:** 
        *   `HandleInput(GameService service, GameSession session, ModeratorInput input)` (ProcessResult): **The central state machine orchestrator.** 
            *   Retrieves current phase from `GamePhaseStateCache` and executes appropriate phase handler 
            *   Phase handlers use `FireHook(GameHook)` to dispatch to registered listeners 
            *   Manages listener responses, handling `NeedInput` by pausing and `Complete` by continuing 
            *   Performs comprehensive state machine validation with detailed error reporting 
            *   Automatically checks victory conditions after resolution phases 
            *   Returns a `ProcessResult` with the next instruction or error details 
        *   `FireHook(GameHook hook)`: Dispatches to all registered listeners for the specified hook 
        *   `CheckVictoryConditions(GameSession session)` ((Team WinningTeam, string Description)?): Evaluates win conditions based on current game state and returns victory information if met. 
    *   **Phase Handler Methods:** Each phase has a dedicated handler method (`HandleSetupPhase`, `HandleNightPhase`, etc.) that: 
        *   Uses re-entrant `switch` statements based on `GamePhaseStateCache` sub-phase 
        *   Fires appropriate hooks at specific moments 
        *   Manages phase transitions through the state cache 
        *   Returns `PhaseHandlerResult` indicating success/failure and transition information 
    *   **State Machine Validation:** Comprehensive validation logic ensures: 
        *   All phase transitions match defined rules 
        *   Hook dispatching follows proper sequence 
        *   Listener responses are consistent with contracts 
        *   Detailed error messages for internal state machine errors 
 
9.  **`GameService` Class:** Orchestrates the game flow based on moderator input and tracked state. **Delegates state machine management to `GameFlowManager` while handling high-level game logic and external interfaces.** 
    *   **Public Methods:** 
        *   `StartNewGame(List<string> playerNamesInOrder, List<RoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)` (Guid): Creates a new `GameSession`, initializes players, records roles/events, **validates that all `rolesInPlay` are configured in the service's master hook listener lists**, sets initial state in `IntraPhaseStateCache`, logs initial state, and generates the first `ModeratorInstruction`. 
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
 
-------------------------- 
 
10. **`PhaseHandlerResult` Record:** The standardized internal return type for all phase handler functions. It communicates the outcome of the handler's execution back to the main `GameFlowManager.HandleInput` loop. 
    *   **Purpose:** Provides a structured way for phase handlers to indicate success/failure, request phase transitions, and specify the next moderator instruction. 
    *   **Structure:** 
        *   `IsSuccess` (bool): Indicates if the handler processed the input successfully. 
        *   `NextInstruction` (ModeratorInstruction?): The specific instruction for the moderator for the *next* step, if the handler determined it. Ignored if `UseDefaultInstructionForNextPhase` is true. 
        *   `TransitionReason` (PhaseTransitionReason? Enum): If a phase transition occurred, this holds the enum value identifying *why* the transition happened (e.g., `PhaseTransitionReason.VoteTied`, `PhaseTransitionReason.WwActionComplete`). This value **must** match the `ConditionOrReason` of a `PhaseTransitionInfo` defined in the *source* phase's `PossibleTransitions` list in `GameFlowManager`. Null if no transition occurred. 
        *   `UseDefaultInstructionForNextPhase` (bool): If true, signals `GameFlowManager` to ignore `NextInstruction` and instead use the `DefaultEntryInstruction` defined for the *target* phase in `GameFlowManager`. 
        *   `ShouldTransitionPhase` (bool): Indicates whether the handler has changed the game phase and the state machine should process a transition. 
        *   `Error` (GameError?): Contains error details if `IsSuccess` is false. 
    *   **Immutability:** Designed as an immutable record. 
    *   **Usage:** Enables the central `GameFlowManager.HandleInput` logic to understand the outcome of a phase handler, validate the resulting state transition against the declared state machine rules, determine the correct next instruction, and validate the expected input for that instruction. **Includes static factory methods for common results:** 
        *   `SuccessTransition()`: For successful phase transitions 
        *   `SuccessInternalGeneric()`: For successful handling without phase changes 
        *   `Failure()`: For error conditions 
 
11. **Hook System Components:** 
 
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
            public GameHookListenerType ListenerType { get; } // Enum: Role, Event 
            public string ListenerId { get; } // Stores the RoleType or EventCardType enum value as string for better debugging/logging 
        } 
        ``` 
 
    *   **`HookListenerActionResult` Class:** Standardized return type for `IGameHookListener.AdvanceStateMachine`: 
        *   `NeedInput(instruction)`: Listener requires input, processing pauses 
        *   `Complete(optional_instruction)`: Listener finished, processing continues 
        *   `Error(error)`: Processing failed with error 
 
12. **State Machine Validation:** 
    *   **Purpose:** The `GameFlowManager` implements comprehensive validation to ensure all phase transitions and state changes conform to the defined state machine rules. 
    *   **Validation Features:** 
        *   **Phase Transition Validation:** Every phase transition is validated against the `PhaseTransitionInfo` defined in the source phase's `PossibleTransitions` list. 
        *   **Hook Dispatch Validation:** Ensures hooks are fired in correct sequence and listeners respond appropriately. 
        *   **State Cache Validation:** Validates that `IntraPhaseStateCache` operations are consistent with state machine rules. 
        *   **Internal Error Detection:** Catches internal state machine inconsistencies and provides detailed error messages for debugging. 
    *   **Error Handling:** Validation failures result in `GameError` objects with specific error codes and descriptive messages, ensuring robust error reporting and debugging capabilities. 
 
13. **`GameError` Class:** 
    *   **Purpose:** Provides structured information about a specific error that occurred during game logic processing. 
    *   **Structure:** 
        *   `Type` (`ErrorType` enum): Classifies the error. 
        *   `Code` (`GameErrorCode` enum): Specific error identifier. 
        *   `Message` (string): Human-readable description. 
        *   `Context` (Optional `IReadOnlyDictionary<string, object>`): Relevant data. 
    *   **Usage:** Returned within a `PhaseHandlerResult` (if `IsSuccess` is false) or wrapped in the final `ProcessResult` by `GameFlowManager.HandleInput`. Allows the calling layer to handle errors gracefully. 
 
14. **`ModeratorInput` Class:** Data structure for communication FROM the moderator. 
    *   `InputTypeProvided` (enum `ExpectedInputType`): Indicates which optional field below is populated. 
    *   `SelectedPlayerIds` (List<Guid>?): IDs of players chosen. **Used for role identification (`PlayerSelectionMultiple`) and vote outcome (`PlayerSelectionSingle`, allowing 0 for tie).** 
    *   `AssignedPlayerRoles` (Dictionary<Guid, RoleType>?): Player IDs mapped to the role assigned to them. Used during setup/role assignment phases (e.g., Thief, initial role identification). 
    *   `SelectedOption` (string?): Specific text option chosen. 
    *   `Confirmation` (bool?): Boolean confirmation. 
 
**Design Note on Vote Input:** 
 
A key design principle for moderator input, especially during voting phases, is minimizing data entry to enhance usability during live gameplay. The application is designed to guide the moderator through the *process* of voting (whether standard or event-driven like Nightmare, Great Distrust, Punishment), reminding them of the relevant rules. However, the actual vote tallying is expected to happen physically among the players. 
 
Consequently, the `ModeratorInput` structure requires the moderator to provide only the final *outcome* of the vote (e.g., who was eliminated via `SelectedPlayerIds`, where an empty list signifies a tie, or confirmation of other outcomes via `Confirmation`). This approach significantly reduces the moderator's interaction time and minimizes the potential for input errors. The application functions primarily as a streamlined state tracker and procedural guide, accepting the loss of granular vote data in its logs as an acceptable trade-off for improved real-time usability. 
 
-------------------------- 
 
15. **`ModeratorInstruction` Class:** Data structure for communication TO the moderator. 
    *   `InstructionText` (string): The core message/question for the moderator. 
    *   `ExpectedInputType` (ExpectedInputType Enum): Specifies the kind of input expected, and implies which `Selectable*` list might be populated. 
    *   `AffectedPlayerIds` (List<Guid>?): Optional: Player(s) this instruction primarily relates to (for context, e.g., player needing role reveal). 
    *   `SelectablePlayerIds` (List<Guid>?): Populated if `ExpectedInputType` involves selecting players (e.g., `PlayerSelectionSingle`, `PlayerSelectionMultiple`). 
    *   `SelectableRoles` (List<RoleType>?): Populated if `ExpectedInputType` is `RoleAssignment`. Provides the list of possible roles the moderator can assign via the `AssignedPlayerRoles` field in `ModeratorInput`. 
    *   `SelectableOptions` (List<string>?): Populated if `ExpectedInputType` is `OptionSelection`. 
 
-------------------------- 
 
16. **Enums:** 
    *   `GamePhase`: `Setup`, `Night`, `Day_Dawn`, `Day_Debate`, `Day_Vote`, `Day_Dusk`, `AccusationVoting` (Nightmare), `FriendVoting` (Great Distrust), `GameOver`. 
    *   `PlayerStatus`: `Alive`, `Dead`. 
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
    *   `ExpectedInputType`: `None`, `PlayerSelectionSingle`, `PlayerSelectionMultiple`, `AssignPlayerRoles`, `OptionSelection`, `Confirmation`. Corresponds to the populated field in `ModeratorInput`. `AssignPlayerRoles` expects input via `ModeratorInput.AssignedPlayerRoles`. 
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
    *   `RoleType` (representing the intended values for a RoleType Enum): 
        *   **System Types:** Unassigned, Unknown 
        *   **Werewolves:** SimpleWerewolf, BigBadWolf, AccursedWolfFather, WhiteWerewolf 
        *   **Villagers:** SimpleVillager, VillagerVillager, Seer, Cupid, Witch, Hunter, LittleGirl, Defender, Elder, Scapegoat, VillageIdiot, TwoSisters, ThreeBrothers, Fox, BearTamer, StutteringJudge, KnightWithRustySword 
        *   **Ambiguous:** Thief, DevotedServant, Actor, WildChild, WolfHound 
        *   **Loners:** Angel, Piper, PrejudicedManipulator 
        *   **New Moon Roles:** Gypsy, TownCrier 
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
 
--------------------------------- 
 
**Game Loop Outline (Hook-Based Architecture):** 
 
1.  **Setup Phase (`GamePhase.Setup`):** 
    *   `GameService.StartNewGame` initializes `GameSession`, logs `GameStartedLogEntry`, sets initial state in `GamePhaseStateCache` (`Setup` phase), and generates the first `ModeratorInstruction`. 
    *   `GameService.ProcessModeratorInput` delegates to `GameFlowManager.HandleInput`. 
    *   `GameFlowManager` executes the `HandleSetupPhase` handler using re-entrant `switch` based on cache state. 
    *   `HandleSetupPhase` processes the `Confirmation` input. 
    *   If `true`: Updates cache state to transition to `Night`, logs the transition (`PhaseTransitionLogEntry`, Reason: `SetupConfirmed`), calls `cache.ClearTransientState()`, and returns a `PhaseHandlerResult` with the next instruction and the `SetupConfirmed` reason. 
    *   `GameFlowManager` validates the transition and updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
2.  **Night Phase (`GamePhase.Night`):** 
    *   `GameFlowManager.HandleInput` executes the `HandleNightPhase` handler with internal sub-phase management.
    *   Handler retrieves current sub-phase from `GamePhaseStateCache` and routes to appropriate sub-handler: 
        *   **NightSubPhase.Start**: Issues "Village goes to sleep" instruction, increments turn number, transitions to `ActionLoop`. 
        *   **NightSubPhase.ActionLoop**: Iterates through complete ordered list of night roles (including those that only require first-night identification), fires `GameHook.NightActionLoop` for each, manages pause/resume via `GamePhaseStateCache`, transitions to `Day_Dawn` when complete. 
    *   Hook dispatch iterates through registered listeners (e.g., `SimpleWerewolfRole`, `SeerRole`), calling `AdvanceStateMachine` on each. 
    *   Listeners determine if they should act based on game state and their cached state. First listener needing input returns `HookListenerActionResult.NeedInput`, causing processing to pause. 
    *   Roles with first-night-only behavior (e.g., Cupid, Thief) check `session.TurnNumber > 1` and return `HookListenerActionResult.Complete()` immediately if true.
    *   Handler returns `PhaseHandlerResult` with transition to `Day_Dawn` when all night actions complete. 
 
3.  **Day Dawn Phase (`GamePhase.Day_Dawn`):** 
    *   `GameFlowManager.HandleInput` executes the `HandleDayDawnPhase` handler with internal sub-phase management. 
    *   Handler retrieves current sub-phase from `GamePhaseStateCache` and routes to appropriate sub-handler: 
        *   **DawnSubPhase.CalculateVictims**: Queries `GameHistoryLog` for night actions, calculates final list of eliminated players, processes cascading effects (Hunter, Lovers), handles moderator input for targets if needed. 
        *   **DawnSubPhase.AnnounceVictims**: Issues single instruction to announce all victims, awaits moderator confirmation. 
        *   **DawnSubPhase.ProcessRoleReveals**: Iterates through eliminated players, issues instruction for each role reveal, pauses/resumes as input received. 
        *   **DawnSubPhase.Finalize**: Transitions to `Day_Debate` with appropriate reason (`DawnNoVictimsProceedToDebate` or `DawnVictimsProceedToDebate`). 
    *   Fires `GameHook.OnPlayerEliminationFinalized` for each eliminated player. 
    *   Hook dispatch allows listeners (e.g., `HunterRole`) to respond to eliminations. 
    *   `GameFlowManager` automatically checks victory conditions after resolution, updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
4.  **Debate Phase (`GamePhase.Day_Debate`):** 
    *   `GameFlowManager.HandleInput` executes the `HandleDayDebatePhase` handler. 
    *   Handler expects `Confirmation`. If `true`: Updates cache state to transition to `Day_Vote`, fires `GameHook.DayVoteStarted`, logs transition (Reason: `DebateConfirmedProceedToVote`), generates vote prompt instruction, returns `PhaseHandlerResult`. 
    *   `GameFlowManager` updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
5.  **Voting Phase (Standard: `GamePhase.Day_Vote`):** 
    *   `GameFlowManager.HandleInput` executes the `HandleDayVotePhase` handler. 
    *   Handler expects `PlayerSelectionSingle` (outcome). Validates input, stores outcome in `PendingVoteOutcome`, logs `VoteOutcomeReportedLogEntry`. 
    *   Updates cache state to transition to `Day_Dusk`, logs transition (Reason: `VoteOutcomeReported`), generates confirmation prompt for resolution, returns `PhaseHandlerResult`. 
    *   `GameFlowManager` updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
6.  **Vote Resolution Phase (`GamePhase.Day_Dusk`):** 
    *   `GameFlowManager.HandleInput` executes the `HandleDayDuskPhase` handler. 
    *   Handler expects `Confirmation`. Retrieves `PendingVoteOutcome`. Logs `VoteResolvedLogEntry`. 
    *   **If player eliminated:** Updates `Player.Status`, logs `PlayerEliminatedLogEntry`. Fires `GameHook.OnPlayerEliminationFinalized`. Updates cache state to transition to `Day_Dawn`, logs transition (Reason: `VoteResolvedProceedToReveal`), generates role reveal prompt, returns `PhaseHandlerResult`. 
    *   **If tie:** Updates cache state to transition to `Night`, increments `TurnNumber`, calls `cache.ClearTransientState()`. Logs transition (Reason: `VoteResolvedTieProceedToNight`), returns `PhaseHandlerResult`. 
    *   Clears `PendingVoteOutcome`. 
    *   `GameFlowManager` automatically checks victory conditions after resolution, updates `PendingModeratorInstruction`, returns the `ProcessResult`. 
 
7.  **Game Over Phase (`GamePhase.GameOver`):** 
    *   `GameFlowManager.HandleInput` executes the `HandleGameOverPhase`. 
    *   Handler returns `PhaseHandlerResult.Failure` for any input, as the game has ended. 
    *   The final "Game Over" instruction was set when victory was first detected by the automatic victory checking. 
 
--------------------------- 
 
**Setup & Initial State Logs:** 
 
The chosen approach is an abstract base class (`GameLogEntryBase`) providing universal properties (`Timestamp`, `TurnNumber`, `Phase`) combined with distinct concrete derived types (preferably records) for each specific loggable event (`PlayerEliminatedLog`, `RoleRevealedLog`, etc.). This flat hierarchy significantly reduces boilerplate for universal fields via the base class while maintaining strong type safety, clarity, and maintainability through specific derived types. 
 
 
1.  **Game Started:** Records the initial configuration provided by the moderator - the list of roles included in the deck, the list of players by name/ID, and the list of event cards included in the deck. *Uniqueness: Captures the baseline parameters of the game session.* 
2.  **Initial Role Assignment (`InitialRoleAssignmentLogEntry`):** Records roles assigned during the *Night 1* identification process. Logs the `PlayerId` and the `AssignedRole` (`RoleType`). Generated by hook listeners during the `NightSequenceStart` hook when processing moderator input for identification. *Uniqueness: Captures the moderator's identification of key roles before Night 1.* 
3.  **Phase Transition (`PhaseTransitionLogEntry`):** Records when the game moves from one phase to another. Logs the `PreviousPhase`, `CurrentPhase`, and the `Reason` string identifying the specific condition that triggered the transition (matching the `ConditionOrReason` from `PhaseTransitionInfo`). *Uniqueness: Tracks the flow of the game through its defined states.* 
 
**Hook Action Logs (Inputs & Choices):** 
 
4.  **Seer View Attempt:** Logs the Seer's ID and the ID of the player they chose to view. (The *result* might be logged separately or implicitly handled by resolution logic, especially with Somnambulism). *Uniqueness: Records the Seer's target choice.* 
5.  **Fox Check Performed:** Logs the Fox's ID, the player they targeted, the IDs of the two neighbors checked, the Yes/No result given (WW nearby?), and whether the Fox lost their power as a result. *Uniqueness: Records the Fox's check details and outcome.* 
6.  **Defender Protection Choice:** Logs the Defender's ID and the ID of the player they chose to protect for the night. *Uniqueness: Records the target of protection.* 
7.  **Piper Charm Choice:** Logs the Piper's ID and the IDs of the two players they chose to charm. *Uniqueness: Records the targets of the charm effect.* 
8.  **Witch Potion Use Attempt:** Logs the Witch's ID, the type of potion used (Healing or Poison), and the ID of the player targeted. *Uniqueness: Records the Witch's specific action and target.* 
9.  **Night Action Log Entry (`NightActionLogEntry`):** A generic entry logging a specific action taken during the night. Includes: 
    *   `ActorId` (Guid): ID of the player performing the action. 
    *   `TargetId` (Guid?): ID of the player targeted, if applicable. 
    *   `ActionType` (`NightActionType` Enum): Specifies the type of action performed (e.g., `WerewolfVictimSelection`, `SeerCheck`, `WitchSave`, `WitchKill`). *Using an enum ensures type safety and avoids string comparisons.* 
    *   *(May include other relevant fields based on ActionType)*. 
    *   *Uniqueness: Records the fundamental action performed by a role during the night.* 
 
**Night & Day Resolution / Outcome Logs:** 
 
10. **Player Eliminated (`PlayerEliminatedLogEntry`):** Logs the ID of the eliminated player and the specific *reason* for their elimination (e.g., `WerewolfAttack`, `WitchPoison`, `KnightCurse`, `HunterShot`, `LoversHeartbreak`, `DayVote`, `Scapegoat`, `GreatDistrust`, `PunishmentEvent`, `SpecterEvent`, etc.). *Uniqueness: The fundamental record of a player leaving the game and why.* 
11. **Role Revealed (`RoleRevealedLogEntry`):** Logs the ID of a player whose role card was revealed (due to death, Village Idiot save, Devoted Servant swap, etc.) and the specific `RoleType` revealed. Generated after processing moderator input during `Day_Event`. *Uniqueness: Records the confirmation of a player's role.*12. **Little Girl Caught:** Logs that the Little Girl spied, was caught, and became the Werewolves' target instead of their original choice. *Uniqueness: Records this specific night event outcome.* 
13. **Elder Survived Attack:** Logs that the Elder was targeted (likely by Werewolves) but survived due to their ability (first time). *Uniqueness: Records the Elder rule interaction.* 
14. **Knight Curse Activated:** Logs that the Knight was killed by Werewolves, activating the curse effect scheduled for the *next* night against a specific Werewolf (identified by proximity/logic). *Uniqueness: Signals the delayed curse effect is pending.* 
15. **Wild Child Transformed:** Logs that the Wild Child's model was eliminated, causing the Wild Child to become a Werewolf. *Uniqueness: Records the role change of the Wild Child.* 
16. **Player State Changed:** A generic log for various boolean flags or simple state updates on a player, detailing the player ID, the state that changed (e.g., `IsInfected`, `IsCharmed`, `IsMuted`, `CanVote` changed, `VoteMultiplier` applied, `HasUsedAWFInfection`, `HasLostFoxPower`, `HasUsedStutteringJudgePower`), and the new value. *Uniqueness: Captures miscellaneous status effects not covered by more specific logs. (Consider a dedicated `PlayerInfectedLog` if infection tracking proves complex).* 
17. **Bear Tamer Growl Occurred:** Logs that the conditions were met for the Moderator to growl (Bear Tamer alive next to a player with assigned Werewolf role). *Uniqueness: Contextual indicator based on known state and positioning.* 
18. **Devoted Servant Swap Executed:** Logs the Servant's ID, the ID of the player they saved from reveal, and the (hidden) role the Servant adopted. *Uniqueness: Records the role and player swap.* 
 
**Day Phase Specific Logs:** 
 
19. **Event Card Drawn:** Logs the specific New Moon Event Card ID and Name drawn at the start of the day. *Uniqueness: Records the active event modifying the day/upcoming night.* 
20. **Gypsy Question Asked & Answered:** Logs the text of the Spiritualism question asked by the Medium and the "Yes" or "No" answer provided by the Moderator (as the spirit). *Uniqueness: Records the outcome of the Spiritualism event.* 
21. **Town Crier Event Played:** Logs the specific Event Card ID and Name played by the Town Crier from their hand. *Uniqueness: Records an additional event activation.* 
22. **Sheriff Appointed:** Logs the ID of the player who became Sheriff, the reason (Initial Election, Successor Appointment, Event), and the ID of the predecessor (if any). *Uniqueness: Tracks the Sheriff role holder.* 
23. **Stuttering Judge Signaled Second Vote:** Logs that the Judge used their one-time ability to trigger a second vote this day. *Uniqueness: Records the use of the Judge's power.* 
24. **Vote Outcome Reported (`VoteOutcomeReportedLogEntry`):** Logs the raw outcome (eliminated player ID or `Guid.Empty` for tie) reported by the Moderator during the `Day_Vote` phase. *Uniqueness: Captures moderator input for vote resolution.* 
25. **Accusation Outcome Reported (Nightmare):** Logs the result of the Nightmare event vote, typically the ID of the player eliminated, as reported by the Moderator. *Uniqueness: Input for Nightmare resolution.* 
26. **Friend Vote Outcome Reported (Great Distrust):** Logs the result of the Great Distrust event, typically the IDs of players eliminated (those receiving no friend votes), as reported by the Moderator. *Uniqueness: Input for Great Distrust resolution.* 
27. **Vouching Outcome Reported (Punishment):** Logs the result of the Punishment event's vouching phase, indicating whether the target player was eliminated (due to insufficient vouches), as reported by the Moderator. *Uniqueness: Input for Punishment resolution.* 
28. **Vote Resolved (`VoteResolvedLogEntry`):** Logs the final result of a voting phase *after* resolving the moderator-provided outcome during `Day_ResolveVote` - identifies who (if anyone) was eliminated and whether it was a tie. *Uniqueness: The final calculated result of a voting round.* 
29. **Villager Powers Lost (Elder Died By Vote):** Logs that the Elder was eliminated by a day vote, causing all Villagers to lose their special abilities. *Uniqueness: Major game state change affecting multiple roles.* 
30. **Scapegoat Voting Restrictions Set:** Logs the decision made by an eliminated Scapegoat regarding who can/cannot vote the following day. *Uniqueness: Records temporary voting rule changes.* 
31. **Phase Transition:** *(See entry #3 above)* 
 
**Game End Log:** 
 
32. **Victory Condition Met (`VictoryConditionMetLogEntry`):** Logs the determined winning team/player(s) and a brief description of the condition met (e.g., "All Werewolves eliminated," "Werewolves equal Villagers," "All survivors charmed," "Angel eliminated early"). *Uniqueness: Marks the end of the game and the outcome.* 
 
**Victory Condition Checking:** 
 
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
