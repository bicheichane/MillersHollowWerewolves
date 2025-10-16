---
slug: architecture-v2
---
**Project:** `Werewolves.Core` (.NET Class Library)

**Goal:** To provide the core game logic and state management for a Werewolves of Miller's Hollow moderator helper application, handling rules and interactions based on the provided rulebook pages (excluding Buildings/Village expansion, but including specified New Moon events). The app **tracks the game state based on moderator input**. It assumes moderator input is accurate and provides deterministic state tracking and guidance based on that input.

**String Management Principle:** To ensure maintainability, localization capabilities, and type safety:
*   All user-facing strings (e.g., moderator instructions, log entry descriptions, error messages displayed to the user) **must** be defined in the `Resources/GameStrings.resx` file and accessed via the generated `GameStrings` class.
*   Internal identifiers or constants used purely for logic (e.g., specific action types for conditional checks) should strongly prefer the use of dedicated `enum` types over raw string literals to avoid weakly-typed comparisons and improve code clarity.

**State Management Philosophy:**

This architecture employs a hybrid approach:

*   **Current Persistent State:** The `Player` class and its contained `PlayerState` object store the *current, effective* status, assigned role, and persistent attributes of a player (e.g., `Status`, `IsSheriff`, `IsInLove`, `IsInfected`, `PotionsUsed`). These properties reflect the player's ongoing condition unless explicitly changed by a game event. They provide fast access to the "now" of the player's state.
*   **Event History & Transient State:** The `GameSession.GameHistoryLog` serves as the immutable, chronological record of *all* significant game events (actions, state changes, eliminations). It's the definitive source of truth for *how* the game state evolved. Crucially, it is also queried by specific phase handlers (like resolving night actions) to determine *transient* states needed only for immediate processing (e.g., identifying the werewolf target selected *this specific night*). This avoids cluttering `PlayerState` with temporary flags that become irrelevant immediately after the resolution step.

**Core Components:**

The central `PlayerSeatingOrder` list in `GameSession` provides architectural separation, treating the static seating arrangement as a structural property of the game session. It offers a clear single source of truth for the order. Helper methods within `GameService` encapsulate the logic for retrieving neighbors, including handling skips over eliminated players.

1.  **`GameSession` Class:** Represents the tracked state of a single ongoing game, derived from moderator input.
    *   `Id` (Guid): Unique identifier for the game session.
    *   `Players` (Dictionary<Guid, Player>): Collection of all players in the game, keyed by their unique ID. Tracks player information provided by the moderator.
    *   `PlayerSeatingOrder` (List<Guid>): Stores the Player IDs in clockwise seating order as reported by the Moderator during setup. Crucial for roles like Knight, Fox, Bear Tamer and events like Nightmare, Influences. Established once at game start. (Implemented as `List<Guid>`).
    *   `GamePhase` (GamePhase Enum): Current stage of the game (e.g., Setup, Night, Day_ResolveNight, Day_Event, Day_Debate, Day_Vote, Day_ResolveVote, GameOver).
    *   `TurnNumber` (int): Tracks the number of full Night/Day cycles completed. Initialized to 0 during `Setup`, increments to 1 at the start of the first `Night`.
    *   `RolesInPlay` (List<RoleType>): List of role types included in the game (provided by Moderator at setup).
    *   `EventDeck` (List<EventCard>): Represents the set of event cards included in the physical deck.
    *   `DiscardPile` (List<EventCard>): Event cards reported as drawn by the moderator.
    *   `ActiveEvents` (List<ActiveEventState>): Tracks currently active New Moon events (input by Moderator when drawn) and their specific state data.
    *   `GameHistoryLog` (List<GameLogEntryBase>): A chronological record of all significant game events, moderator inputs, state changes, and action outcomes tracked during the session. Uses the `GameLogEntryBase` derived types for structured, strongly-typed entries (see "Setup & Initial State Logs" section for examples). This replaces the separate night/day temporary logs. **This log serves as the definitive history and is also the source for resolving *transient* states required only during specific phase resolutions (e.g., querying the most recent Werewolf victim selection event during `Day_ResolveNight`).**
    *   `PendingModeratorInstruction` (ModeratorInstruction?): The current prompt/instruction for the moderator, asking for input or guiding the next step.
    *   `PendingNight1IdentificationForRole` (RoleType?): Stores the `RoleType` currently awaiting identification *during Night 1*. Used by `GameService` to manage the Identify->Act sequence specific to the first night. Null if no Night 1 identification is pending.
    *   `CurrentNightActingRoleIndex` (int): Tracks the index of the role currently acting (or pending identification) within the night wake-up order for the current night. Reset at the start of each Night phase.
    *   `WinningTeam` (Team?): Stores the winning team once determined by `GameService`. Null otherwise.
    *   **Helper Methods:**
        *   `FindLogEntries<TLogEntry>(...)`: Searches the `GameHistoryLog` for entries of a specific type `TLogEntry`, optionally filtering by relative turn number (`turnsAgo`), game phase (`phase`), or a custom predicate (`filter`).
        *   `GetRoleCount(RoleType roleType)`: Returns the total count of a specific role included in the game setup.
        *   `GetAliveRoleCount(RoleType roleType)`: Returns the count of living players known (or deduced) to have a specific role.
        *   `PreviousPhase`: Returns the `GamePhase` recorded in the most recent `PhaseTransitionLogEntry`.
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
    *   `Role` (IRole?): The player's character role instance. Null initially. **Set by `GameService` during the Setup phase (for roles requiring identification) or upon role reveal (death, etc.).**
    *   `Status` (PlayerStatus Enum): Current status (`Alive`, `Dead`).
    *   `IsRoleRevealed` (bool): Flag indicating if the moderator has input this player's role, **or if the role was assigned during Setup based on moderator identification**. `true` means the *application* knows the role. **Implemented as a computed property based on `Role` not being null.**
    *   `State` (PlayerState): Encapsulates all dynamic, *persistent* states affecting the player (e.g., Sheriff status, protection, infection, charms, modifiers). This approach keeps the core Player class focused on identity and role, while grouping volatile states for better organization and potential future state management enhancements (like serialization or complex transitions). **This reflects the player's current, ongoing condition.**

--------------

3.  **`IRole` Interface:** Defines the contract for character roles (represents the *rules* of the role).
    *   `Name` (string): Role name (e.g., "Seer").
    *   `RoleType` (RoleType Enum): The type identifier.
    *   `Description` (string): Brief role description.
    *   `RequiresNight1Identification()` (bool): Does this role need to be identified by the moderator *during Night 1*? Crucial for roles like Werewolves, Seer, Cupid, Thief, Wild Child, Wolf Hound, Prejudiced Manipulator, so the `GameService` knows who holds the role before their first action.
    *   `GenerateIdentificationInstructions(GameSession session)` (ModeratorInstruction?): Generates the prompt asking the moderator to identify the player(s) holding this role *during Night 1*. Returns `null` if `RequiresNight1Identification` is false. Expected input is typically `PlayerSelectionSingle` or `PlayerSelectionMultiple`.
    *   `ProcessIdentificationInput(GameSession session, ModeratorInput input)` (ProcessResult): Processes the moderator input provided for Night 1 role identification. Validates the input (e.g., correct player count). Updates the `Role` and `IsRoleRevealed` status for the identified players in the `session`. Returns a `ProcessResult` indicating success or failure.
    *   `GenerateNightInstructions(GameSession session)` (ModeratorInstruction?): Generates the prompt *if* this role acts at night. The role implementation uses the `session` to determine the context and find relevant player(s) if needed (e.g., finding living werewolves).
    *   `ProcessNightAction(GameSession session, ModeratorInput input)` (ProcessResult): Processes moderator input for the night action. The role implementation uses the `session` to identify the relevant actor(s) and target(s), validate the action based on game state and rules, and update the `session` accordingly (e.g., logging the action, updating player states).
    *   `GenerateDayInstructions(GameSession session)` (ModeratorInstruction?): Generates prompts for day-time actions (e.g., Hunter's last shot). The role implementation uses the `session` to determine if the action is relevant and find the necessary context (e.g., finding the dying Hunter).
    *   `ProcessDayAction(GameSession session, ModeratorInput input)` (ProcessResult): Processes moderator input for day actions. The role implementation uses the `session` to find the actor(s), validate the action, and update the `session`.
    *   **Concrete Implementations:** (`SimpleVillagerRole`, `SeerRole`, `WitchRole`, etc.) - These classes represent the *rules* of the role.

---------------------

4.  **`PlayerState` Class:** Wrapper class holding all dynamic state information for a `Player`. **Implemented as an inner class within `Player.cs`**. This improves organization and separation of concerns. Properties typically use `internal set` to allow modification primarily by the `GameService` or internal logic, maintaining state integrity. **These properties represent the *persistent* or *longer-term* aspects of a player's current state (e.g., holding the Sheriff title, being in love, being infected, having used a specific potion). They reflect the player's ongoing status unless changed by a game event.**
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
        *   `IsTemporarilyRemoved` (bool): True if the player is currently out of the room due to the Little Rascal event. GameService must skip this player during this time.

    *   **Data-Carrying States:**
        *   `LoverId` (Guid?): Stores the ID of the other player in the Lovers pair, if `IsInLove` is true.
        *   `VoteMultiplier` (int): The multiplier applied to this player's vote (e.g., 1 for normal, 2 for Sheriff, 3 for Little Rascal). Default is 1.
        *   `PotionsUsed` (WitchPotionType Flags Enum?): Tracks which of the Witch's single-use potions have been reported as used by the moderator. Should be implemented as a `[Flags]` enum (e.g., `None=0, Healing=1, Poison=2`). *(Reset if Devoted Servant takes role).*
        *   `WildChildModelId` (Guid?): Stores the ID of the player chosen as the model by the Wild Child. Used to determine when/if the Wild Child transforms.
        *   `WolfHoundChoice` (Team?): Stores the alignment (Villagers or Werewolves) chosen by the Wolf Hound on Night 1.
        *   `TimesAttackedByWerewolves` (int): Counter for how many times this player has been the primary target of the Werewolves' night attack. Used specifically for the Elder's survival ability. *(Reset if Devoted Servant takes role).*

*Note on Devoted Servant:* When the Devoted Servant swaps roles, the `GameService` must explicitly reset any role-specific usage flags or counters (marked with *(Reset if...)* above) on the Servant's `PlayerState` to their default values.*

----------------------

5.  **`EventCard` Abstract Class:** Base for New Moon event cards (represents the *rules* of the event).
    *   `Id` (string): Unique identifier (e.g., "FullMoonRising").
    *   `Name` (string): Event card name.
    *   `Description` (string): Text description of the event.
    *   `Timing` (EventTiming Enum): Primary trigger time (e.g., `NextNight`, `Immediate`, `PermanentAssignment`).
    *   `Duration` (EventDuration Enum): How long the effect lasts (e.g., `OneNight`, `Permanent`, `UntilNextVote`).
    *   `ApplyEffect(GameSession session, GameService service)` (ModeratorInstruction): Applies the *initial* effect when the moderator inputs that this card was drawn. Returns the instruction.
    *   **Optional Override Methods (for Active Events):**
        *   `ModifyNightActionResolution(GameSession session, NightActionResolution currentResolution)` (NightActionResolution): Allows active events to alter the outcome of night actions (e.g., Backfire, Specter, Miracle).
        *   `ModifyDayVoteProcess(GameSession session)` (ModeratorInstruction?): Allows events to change the voting mechanics (e.g., Nightmare, Influences, Great Distrust). Returns instruction if it takes over, null otherwise.
        *   `ModifyDebateRules(GameSession session)` (ModeratorInstruction?): Allows events to impose rules during debate (e.g., Eclipse, Good Manners, Not Me).
        *   `ModifyInstruction(GameSession session, ModeratorInstruction originalInstruction)` (ModeratorInstruction): Allows events to change the text/options of standard instructions (e.g., Somnambulism, Burial, Executioner).
        *   `ModifyVictoryConditions(GameSession session)` (void): Allows events to alter win conditions (less common, maybe Double Agent indirectly).
        *   `OnTurnEnd(GameSession session, GamePhase endingPhase)` (void): Hook for temporary events to decrement counters or clean up state.
    *   **Concrete Implementations:** (`FullMoonRisingEvent`, `SomnambulismEvent`, `EnthusiasmEvent`, `BackfireEvent`, `NightmareEvent`, `InfluencesEvent`, `ExecutionerEvent`, `DoubleAgentEvent`, `GreatDistrustEvent`, `SpiritualismEvent` (potentially 5 variants or one class handling variants), `NotMeNorWolfEvent`, `MiracleEvent`, `DissatisfactionEvent`, `TheLittleRascalEvent`, `PunishmentEvent`, `EclipseEvent`, `TheSpecterEvent`, `GoodMannersEvent`, `BurialEvent`).

---------------------

6.  **`ActiveEventState` Class:** Stores runtime state for an active event in `GameSession.ActiveEvents`. *known to be in play*.
    *   `EventId` (string): Matches the `EventCard.Id`.
    *   `CardReference` (EventCard): Reference to the static definition of the card for accessing its methods.
    *   `TurnsRemaining` (int?): Countdown for temporary events. Null for permanent.
    *   `StateData` (Dictionary<string, object>): Event-specific runtime data (e.g., who is muted, which question was asked in Spiritualism).

-----------------------

The core principle of this application is to accurately track the game state as known by the Moderator. Roles that require Moderator knowledge from the start (e.g., Werewolves, Seer, Cupid, Thief, Wild Child, Wolf Hound, Prejudiced Manipulator) **are identified during Night 1**. The `GameService` manages this process by checking `IRole.RequiresNight1Identification()` for roles acting during the night. If identification is needed and hasn't happened yet, it prompts the moderator using `IRole.GenerateIdentificationInstructions()`. Upon receiving the moderator's input, `IRole.ProcessIdentificationInput()` validates it and updates the `Player.Role` and `Player.IsRoleRevealed` flags. The `GameService` then immediately prompts for that same role's action. This ensures the application state reflects the Moderator's crucial knowledge before the role's first action occurs, synchronizing the tracked state with the information available to the human Moderator.

7.  **`GameFlowManager` Class:** Holds the state machine configuration and provides access to phase definitions. This class implements a comprehensive declarative state machine that manages all game phase transitions, validates transitions, and orchestrates the overall game flow.
    *   **Core Components:**
        *   `_phaseDefinitions` (Dictionary<GamePhase, PhaseDefinition>): Stores the complete state machine configuration with all phase definitions, transitions, and handler references.
        *   `_roleImplementations` (Dictionary<RoleType, IRole>): Dependency-injected role implementations used by handlers for role-specific logic.
    *   **Primary Methods:**
        *   `HandleInput(GameService service, GameSession session, ModeratorInput input)` (ProcessResult): **The central state machine orchestrator.** 
            *   Retrieves the current phase definition and executes the appropriate handler.
            *   Validates phase transitions against defined `PhaseTransitionInfo` rules.
            *   Determines the next instruction (either handler-provided or default for target phase).
            *   Performs comprehensive state machine validation with detailed error reporting.
            *   Automatically checks victory conditions after resolution phases.
            *   Returns a `ProcessResult` with the next instruction or error details.
        *   `CheckVictoryConditions(GameSession session)` ((Team WinningTeam, string Description)?): Evaluates win conditions based on current game state and returns victory information if met.
    *   **Phase Handler Methods:** Each phase has a dedicated handler method (`HandleSetupPhase`, `HandleNightPhase`, `HandleDayVotePhase`, etc.) that:
        *   Processes moderator input specific to that phase.
        *   Updates game state and logs relevant events.
        *   Returns `PhaseHandlerResult` indicating success/failure, transition information, and next instruction.
    *   **State Machine Validation:** Comprehensive validation logic ensures:
        *   All phase transitions match defined `PhaseTransitionInfo` rules.
        *   Expected input types match transition requirements.
        *   Handler results are consistent with state machine configuration.
        *   Detailed error messages for internal state machine errors.

8.  **`GameService` Class:** Orchestrates the game flow based on moderator input and tracked state. **Delegates state machine management to `GameFlowManager` while handling high-level game logic and external interfaces.**
    *   **Public Methods:**
        *   `StartNewGame(List<string> playerNamesInOrder, List<RoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)` (Guid): Creates a new `GameSession`, initializes players, records roles/events, **validates that all `rolesInPlay` are configured in the service's master night order lists**, sets `GamePhase = Setup`, logs initial state, and generates the first `ModeratorInstruction`.
        *   `ProcessModeratorInput(Guid gameId, ModeratorInput input)` (ProcessResult): **The central entry point for processing moderator actions.**
            *   Retrieves the current `GameSession` and delegates to `GameFlowManager.HandleInput`.
            *   The `GameFlowManager` handles all state machine logic, validation, and transition management.
            *   Returns the `ProcessResult` from the state machine containing the next instruction or error.
        *   `GetCurrentInstruction(Guid gameId)` (ModeratorInstruction): Retrieves the `PendingModeratorInstruction`.
        *   `GetGameStateView(Guid gameId)` (object): Returns a read-only view/DTO of the tracked game state.
    *   **Internal Logic:**
        *   Relies on `GameFlowManager` for all state machine operations, phase transitions, and validation.
        *   Provides role implementations to `GameFlowManager` via dependency injection.
        *   Handles high-level game setup and external interface concerns.
        *   Phase-specific logic is now encapsulated within `GameFlowManager` handler methods.
        *   Victory condition checking is automatically handled by `GameFlowManager` after resolution phases.

--------------------------

9.  **`PhaseHandlerResult` Record:** The standardized internal return type for all phase handler functions (`PhaseDefinition.ProcessInputAndUpdatePhase`). It communicates the outcome of the handler's execution back to the main `GameFlowManager.HandleInput` loop.
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

10.  **State Machine Validation:**
    *   **Purpose:** The `GameFlowManager` implements comprehensive validation to ensure all phase transitions and state changes conform to the defined state machine rules.
    *   **Validation Features:**
        *   **Phase Transition Validation:** Every phase transition is validated against the `PhaseTransitionInfo` defined in the source phase's `PossibleTransitions` list. The validation checks:
            *   Target phase matches a defined transition
            *   `PhaseTransitionReason` matches the transition's `ConditionOrReason`
            *   Expected input type matches the transition's `ExpectedInputOnArrival`
        *   **Handler Result Validation:** Validates that `PhaseHandlerResult` objects are consistent with state machine configuration:
            *   Transition reasons must match defined transitions
            *   Next instruction expectations are consistent with target phase requirements
            *   Required data (like transition reasons) is provided when needed
        *   **Internal Error Detection:** Catches internal state machine inconsistencies and provides detailed error messages for debugging.
    *   **Error Handling:** Validation failures result in `GameError` objects with specific error codes and descriptive messages, ensuring robust error reporting and debugging capabilities.

11.  **`GameError` Class:**
    *   **Purpose:** Provides structured information about a specific error that occurred during game logic processing.
    *   **Structure:**
        *   `Type` (`ErrorType` enum): Classifies the error.
        *   `Code` (`GameErrorCode` enum): Specific error identifier.
        *   `Message` (string): Human-readable description.
        *   `Context` (Optional `IReadOnlyDictionary<string, object>`): Relevant data.
    *   **Usage:** Returned within a `PhaseHandlerResult` (if `IsSuccess` is false) or wrapped in the final `ProcessResult` by `GameFlowManager.HandleInput`. Allows the calling layer to handle errors gracefully.

12.  **`ModeratorInput` Class:** Data structure for communication FROM the moderator.
    *   `InputTypeProvided` (enum `ExpectedInputType`): Indicates which optional field below is populated.
    *   `SelectedPlayerIds` (List<Guid>?): IDs of players chosen. **Used for role identification (`PlayerSelectionMultiple`) and vote outcome (`PlayerSelectionSingle`, allowing 0 for tie).**
    *   `AssignedPlayerRoles` (Dictionary<Guid, RoleType>?): Player IDs mapped to the role assigned to them. Used during setup/role assignment phases (e.g., Thief, initial role identification).
    *   `SelectedOption` (string?): Specific text option chosen.
    *   `Confirmation` (bool?): Boolean confirmation.

**Design Note on Vote Input:**

A key design principle for moderator input, especially during voting phases, is minimizing data entry to enhance usability during live gameplay. The application is designed to guide the moderator through the *process* of voting (whether standard or event-driven like Nightmare, Great Distrust, Punishment), reminding them of the relevant rules. However, the actual vote tallying is expected to happen physically among the players.

Consequently, the `ModeratorInput` structure requires the moderator to provide only the final *outcome* of the vote (e.g., who was eliminated via `SelectedPlayerIds`, where an empty list signifies a tie, or confirmation of other outcomes via `Confirmation`). This approach significantly reduces the moderator's interaction time and minimizes the potential for input errors. The application functions primarily as a streamlined state tracker and procedural guide, accepting the loss of granular vote data in its logs as an acceptable trade-off for improved real-time usability.

--------------------------

13. **`ModeratorInstruction` Class:** Data structure for communication TO the moderator.
    *   `InstructionText` (string): The core message/question for the moderator.
    *   `ExpectedInputType` (ExpectedInputType Enum): Specifies the kind of input expected, and implies which `Selectable*` list might be populated.
    *   `AffectedPlayerIds` (List<Guid>?): Optional: Player(s) this instruction primarily relates to (for context, e.g., player needing role reveal).
    *   `SelectablePlayerIds` (List<Guid>?): Populated if `ExpectedInputType` involves selecting players (e.g., `PlayerSelectionSingle`, `PlayerSelectionMultiple`).
    *   `SelectableRoles` (List<RoleType>?): Populated if `ExpectedInputType` is `RoleAssignment`. Provides the list of possible roles the moderator can assign via the `AssignedPlayerRoles` field in `ModeratorInput`.
    *   `SelectableOptions` (List<string>?): Populated if `ExpectedInputType` is `OptionSelection`.

--------------------------

14. **Enums:**
    *   `GamePhase`: `Setup`, `Night`, `Day_ResolveNight`, `Day_Event`, `Day_Debate`, `Day_Vote`, `Day_ResolveVote`, `AccusationVoting` (Nightmare), `FriendVoting` (Great Distrust), `GameOver`.
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
        *   `SetupConfirmed`: Transition from Setup to Night_Start when moderator confirms setup is complete
        *   `NightStarted`: Transition from Night_Start to Night_RoleAction when night begins
        *   `IdentifiedRoleAndProceedToAction`: Transition within Night_RoleAction after Night 1 role identification
        *   `RoleActionComplete`: Transition from Night_RoleAction to Night_RoleSleep after role action
        *   `RoleSleep`: Transition from Night_RoleSleep to either Night_RoleAction (more roles) or Day_ResolveNight (night ends)
        *   `NightResolutionConfirmedProceedToReveal`: Transition from Day_ResolveNight to Day_Event when there are victims to reveal
        *   `NightResolutionConfirmedNoVictims`: Transition from Day_ResolveNight to Day_Debate when no victims
        *   `RoleRevealedProceedToDebate`: Transition from Day_Event to Day_Debate after role reveal from night resolution
        *   `RoleRevealedProceedToNight`: Transition from Day_Event to Night_RoleAction after role reveal from vote resolution
        *   `DebateConfirmedProceedToVote`: Transition from Day_Debate to Day_Vote when moderator confirms debate end
        *   `VoteOutcomeReported`: Transition from Day_Vote to Day_ResolveVote when vote outcome is reported
        *   `VoteResolvedProceedToReveal`: Transition from Day_ResolveVote to Day_Event when player eliminated by vote
        *   `VoteResolvedTieProceedToNight`: Transition from Day_ResolveVote to Night_Start when vote ends in tie
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

**Game Loop Outline (Moderator Helper Perspective):**

1.  **Setup Phase (`GamePhase.Setup`):**
    *   `GameService.StartNewGame` initializes `GameSession`, logs `GameStartedLogEntry`, sets initial `PendingModeratorInstruction` ("Setup complete. Proceed to Night 1?", `ExpectedInputType.Confirmation`).
    *   `GameService.ProcessModeratorInput` delegates to `GameFlowManager.HandleInput`.
    *   `GameFlowManager` executes the `HandleSetupPhase` handler.
    *   `HandleSetupPhase` processes the `Confirmation` input.
    *   If `true`: Updates `session.GamePhase` to `Night_Start`, logs the transition (`PhaseTransitionLogEntry`, Reason: `SetupConfirmed`), and returns a `PhaseHandlerResult` with the next instruction ("The village goes to sleep.", `ExpectedInputType.Confirmation`) and the `SetupConfirmed` reason.
    *   `GameFlowManager` validates the transition and updates `PendingModeratorInstruction`, returns the `ProcessResult`.

2.  **Night Start Phase (`GamePhase.Night_Start`):**
    *   `GameFlowManager.HandleInput` executes the `HandleNewNightLogic` handler.
    *   Handler processes the `Confirmation` for "Night Starts". If `true`, it starts a new turn (`session.StartNewTurn()`), advances to the first night role (`session.AdvanceToNextNightRole()`), and transitions to `Night_RoleAction`.
    *   Returns a `PhaseHandlerResult` with transition to `Night_RoleAction` and the first role's wake-up instruction.

3.  **Night Role Action Phase (`GamePhase.Night_RoleAction`):**
    *   `GameFlowManager.HandleInput` executes the `HandleNightPhase` handler.
    *   **Handle Night 1 Identification:** If it's the first night and the current role requires identification (`currentNightRole.RequiresNight1Identification()`), the handler calls `HandleNightRoleIdentification`.
        *   On success: Logs `InitialRoleAssignmentLogEntry`, then immediately calls `HandleNightActionPhase` for the same role's action.
        *   On failure: Returns `PhaseHandlerResult.Failure` with the error.
    *   **Handle Regular Night Action:** If no identification needed, calls `HandleNightActionPhase` which calls `currentNightRole.ProcessNightAction`.
        *   On success: The `IRole` implementation logs the action (`NightActionLogEntry`) and returns a `PhaseHandlerResult`.
        *   After successful action, transitions to `Night_RoleSleep` phase.
        *   On failure: Returns `PhaseHandlerResult.Failure` with the error.

4.  **Night Role Sleep Phase (`GamePhase.Night_RoleSleep`):**
    *   `GameFlowManager.HandleInput` executes the `HandleNightRoleGoesToSleep` handler.
    *   Handler processes the `Confirmation` for the role going to sleep.
    *   Advances to the next night role (`session.AdvanceToNextNightRole()`).
    *   **If more roles remain:** Transitions back to `Night_RoleAction` with the next role's wake-up instruction.
    *   **If no more roles:** Transitions to `Day_ResolveNight` for night resolution.

5.  **Night Resolution Phase (`GamePhase.Day_ResolveNight`):**
    *   `GameFlowManager.HandleInput` executes the `HandleResolveNightPhase` handler.
    *   Handler expects `Confirmation` input.
    *   Processes logged night actions (Werewolf victim selection, applying protection effects).
    *   Calculates deaths. Logs `PlayerEliminatedLogEntry`.
    *   **If eliminations:** Updates `session.GamePhase` to `Day_Event`, logs transition (Reason: `NightResolutionConfirmedProceedToReveal`), generates reveal prompt instruction, and returns `PhaseHandlerResult` with the instruction and reason.
    *   **If no eliminations:** Updates `session.GamePhase` to `Day_Debate`, logs transition (Reason: `NightResolutionConfirmedNoVictims`), and returns `PhaseHandlerResult` requesting default instruction for `Day_Debate`.
    *   `GameFlowManager` automatically checks victory conditions after resolution, updates `PendingModeratorInstruction`, returns the `ProcessResult`.

6.  **Day Event Phase (`GamePhase.Day_Event`):**
    *   `GameFlowManager.HandleInput` executes the `HandleDayEventPhase` handler.
    *   Handles `AssignPlayerRoles` input for role reveals. Updates `Player.Role`, `IsRoleRevealed`, logs `RoleRevealedLogEntry`.
    *   Handles death triggers based on revealed role (Hunter - Phase 3+, Lovers - Phase 3+).
    *   **If coming from night resolution:** Updates `session.GamePhase` to `Day_Debate`, logs transition (Reason: `RoleRevealedProceedToDebate`), returns `PhaseHandlerResult` requesting default instruction.
    *   **If coming from vote resolution:** Updates `session.GamePhase` to `Night_RoleAction`, logs transition (Reason: `RoleRevealedProceedToNight`), returns `PhaseHandlerResult` requesting default instruction.
    *   (Future phases: Handle event card drawing/application here).
    *   `GameFlowManager` automatically checks victory conditions, updates `PendingModeratorInstruction`, returns the `ProcessResult`.

7.  **Debate Phase (`GamePhase.Day_Debate`):**
    *   `GameFlowManager.HandleInput` executes the `HandleDayDebatePhase` handler.
    *   Handler expects `Confirmation`. If `true`: Updates `session.GamePhase` to `Day_Vote`, logs transition (Reason: `DebateConfirmedProceedToVote`), generates vote prompt instruction, returns `PhaseHandlerResult` with instruction and reason.
    *   `GameFlowManager` updates `PendingModeratorInstruction`, returns the `ProcessResult`.

8.  **Voting Phase (Standard: `GamePhase.Day_Vote`):**
    *   `GameFlowManager.HandleInput` executes the `HandleDayVotePhase` handler.
    *   Handler expects `PlayerSelectionSingle` (outcome). Validates input, stores outcome in `PendingVoteOutcome`, logs `VoteOutcomeReportedLogEntry`.
    *   Updates `session.GamePhase` to `Day_ResolveVote`, logs transition (Reason: `VoteOutcomeReported`), generates confirmation prompt for resolution, returns `PhaseHandlerResult` with instruction and reason.
    *   `GameFlowManager` updates `PendingModeratorInstruction`, returns the `ProcessResult`.

9.  **Vote Resolution Phase (`GamePhase.Day_ResolveVote`):**
    *   `GameFlowManager.HandleInput` executes the `HandleDayResolveVotePhase` handler.
    *   Handler expects `Confirmation`. Retrieves `PendingVoteOutcome`. Logs `VoteResolvedLogEntry`.
    *   **If player eliminated:** Updates `Player.Status`, logs `PlayerEliminatedLogEntry`. Updates `session.GamePhase` to `Day_Event`, logs transition (Reason: `VoteResolvedProceedToReveal`), generates role reveal prompt, returns `PhaseHandlerResult` with instruction and reason.
    *   **If tie:** Updates `session.GamePhase` to `Night_Start`, increments `TurnNumber`. Logs transition (Reason: `VoteResolvedTieProceedToNight`), returns `PhaseHandlerResult` with instruction and reason.
    *   Clears `PendingVoteOutcome`.
    *   `GameFlowManager` automatically checks victory conditions after resolution, updates `PendingModeratorInstruction`, returns the `ProcessResult`.

10. **Game Over Phase (`GamePhase.GameOver`):**
    *   `GameFlowManager.HandleInput` executes `HandleGameOverPhase`.
    *   Handler returns `PhaseHandlerResult.Failure` for any input, as the game has ended.
    *   The final "Game Over" instruction was set when victory was first detected by the automatic victory checking.

---------------------------

**Setup & Initial State Logs:**

The chosen approach is an abstract base class (`GameLogEntryBase`) providing universal properties (`Timestamp`, `TurnNumber`, `Phase`) combined with distinct concrete derived types (preferably records) for each specific loggable event (`PlayerEliminatedLog`, `RoleRevealedLog`, etc.). This flat hierarchy significantly reduces boilerplate for universal fields via the base class while maintaining strong type safety, clarity, and maintainability through specific derived types.


1.  **Game Started:** Records the initial configuration provided by the moderator - the list of roles included in the deck, the list of players by name/ID, and the list of event cards included in the deck. *Uniqueness: Captures the baseline parameters of the game session.*
2.  **Initial Role Assignment (`InitialRoleAssignmentLogEntry`):** Records roles assigned during the *Night 1* identification process. Logs the `PlayerId` and the `AssignedRole` (`RoleType`). Generated by `GameService.HandleNightPhase` after successfully processing moderator input for identification. *Uniqueness: Captures the moderator's identification of key roles before Night 1.*
3.  **Phase Transition (`PhaseTransitionLogEntry`):** Records when the game moves from one phase to another. Logs the `PreviousPhase`, `CurrentPhase`, and the `Reason` string identifying the specific condition that triggered the transition (matching the `ConditionOrReason` from `PhaseTransitionInfo`). *Uniqueness: Tracks the flow of the game through its defined states.*

**Night Action Logs (Inputs & Choices):**

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

*   **Automatic Checking:** Victory conditions are automatically evaluated by `GameFlowManager.HandleInput` after specific resolution phases (`Day_ResolveNight` and `Day_ResolveVote`). This ensures immediate game termination when win conditions are achieved.
*   **Basic Victory Logic (Phase 1):** The current implementation checks fundamental win conditions:
    *   **Villager Win:** All werewolves eliminated and at least one non-werewolf player remains alive
    *   **Werewolf Win:** Werewolves equal or outnumber non-werewolves, with at least one werewolf alive
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
