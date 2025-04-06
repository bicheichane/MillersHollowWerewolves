---
slug: architecture-v2
---
**Project:** `Werewolves.Core` (.NET Class Library)

**Goal:** To provide the core game logic and state management for a Werewolves of Miller's Hollow moderator helper application, handling rules and interactions based on the provided rulebook pages (excluding Buildings/Village expansion, but including specified New Moon events). The app **tracks the game state based on moderator input**. It assumes moderator input is accurate and provides deterministic state tracking and guidance based on that input.

**String Management Principle:** To ensure maintainability, localization capabilities, and type safety:
*   All user-facing strings (e.g., moderator instructions, log entry descriptions, error messages displayed to the user) **must** be defined in the `Resources/GameStrings.resx` file and accessed via the generated `GameStrings` class.
*   Internal identifiers or constants used purely for logic (e.g., specific action types for conditional checks) should strongly prefer the use of dedicated `enum` types over raw string literals to avoid weakly-typed comparisons and improve code clarity.

**Core Components:**

The central `PlayerSeatingOrder` list in `GameSession` provides architectural separation, treating the static seating arrangement as a structural property of the game session. It offers a clear single source of truth for the order. Helper methods within `GameService` encapsulate the logic for retrieving neighbors, including handling skips over eliminated players.

1.  **`GameSession` Class:** Represents the tracked state of a single ongoing game, derived from moderator input.
    *   `Id` (Guid): Unique identifier for the game session.
    *   `Players` (Dictionary<Guid, Player>): Collection of all players in the game, keyed by their unique ID. Tracks player information provided by the moderator.
    *   `PlayerSeatingOrder` (IReadOnlyList<Guid>): Stores the Player IDs in clockwise seating order as reported by the Moderator during setup. Crucial for roles like Knight, Fox, Bear Tamer and events like Nightmare, Influences. Established once at game start.
    *   `GamePhase` (GamePhase Enum): Current stage of the game (e.g., Setup, Night, Day_ResolveNight, Day_Event, Day_Debate, Day_Vote, Day_ResolveVote, GameOver).
    *   `TurnNumber` (int): Tracks the number of full Night/Day cycles completed. Starts at 1 for the first Night.
    *   `RolesInPlay` (List<RoleType>): List of role types included in the game (provided by Moderator at setup).
    *   `EventDeck` (List<EventCard>): Represents the set of event cards included in the physical deck.
    *   `DiscardPile` (List<EventCard>): Event cards reported as drawn by the moderator.
    *   `ActiveEvents` (List<ActiveEventState>): Tracks currently active New Moon events (input by Moderator when drawn) and their specific state data.
    *   `GameHistoryLog` (List<GameLogEntryBase>): A chronological record of all significant game events, moderator inputs, state changes, and action outcomes tracked during the session. Uses the `GameLogEntryBase` derived types for structured, strongly-typed entries (see "Setup & Initial State Logs" section for examples). This replaces the separate night/day temporary logs.
    *   `PendingModeratorInstruction` (ModeratorInstruction?): The current prompt/instruction for the moderator, asking for input or guiding the next step.
    *   `PendingNight1IdentificationForRole` (RoleType?): Stores the `RoleType` currently awaiting identification *during Night 1*. Used by `GameService` to manage the Identify->Act sequence specific to the first night. Null if no Night 1 identification is pending.
    *   `CurrentNightActingRoleIndex` (int): Tracks the index of the role currently acting (or pending identification) within the night wake-up order for the current night. Reset at the start of each Night phase.
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
    *   `IsRoleRevealed` (bool): Flag indicating if the moderator has input this player's role, **or if the role was assigned during Setup based on moderator identification**. `true` means the *application* knows the role.
    *   `State` (PlayerState): Encapsulates all dynamic states affecting the player (e.g., Sheriff status, protection, infection, charms, modifiers). This approach keeps the core Player class focused on identity and role, while grouping volatile states for better organization and potential future state management enhancements (like serialization or complex transitions).

--------------

3.  **`IRole` Interface:** Defines the contract for character roles (represents the *rules* of the role).
    *   `Name` (string): Role name (e.g., "Seer").
    *   `RoleType` (RoleType Enum): The type identifier.
    *   `Description` (string): Brief role description.
    *   `GetNightWakeUpOrder()` (int): Priority for night actions (lower wakes first). Return `int.MaxValue` if no night action.
    *   `RequiresNight1Identification()` (bool): Does this role need to be identified by the moderator *during Night 1*? Crucial for roles like Werewolves, Seer, Cupid, etc., so the `GameService` knows who holds the role before their first action.
    *   `GenerateIdentificationInstructions(GameSession session)` (ModeratorInstruction?): Generates the prompt asking the moderator to identify the player(s) holding this role *during Night 1*. Returns `null` if `RequiresNight1Identification` is false. Expected input is typically `PlayerSelectionSingle` or `PlayerSelectionMultiple`.
    *   `ProcessIdentificationInput(GameSession session, ModeratorInput input)` (ProcessResult): Processes the moderator input provided for Night 1 role identification. Validates the input (e.g., correct player count). Updates the `Role` and `IsRoleRevealed` status for the identified players in the `session`. Returns a `ProcessResult` indicating success or failure.
    *   `GenerateNightInstructions(GameSession session)` (ModeratorInstruction?): Generates the prompt *if* this role acts at night. The role implementation uses the `session` to determine the context and find relevant player(s) if needed (e.g., finding living werewolves).
    *   `ProcessNightAction(GameSession session, ModeratorInput input)` (ProcessResult): Processes moderator input for the night action. The role implementation uses the `session` to identify the relevant actor(s) and target(s), validate the action based on game state and rules, and update the `session` accordingly (e.g., logging the action, updating player states).
    *   `GenerateDayInstructions(GameSession session)` (ModeratorInstruction?): Generates prompts for day-time actions (e.g., Hunter's last shot). The role implementation uses the `session` to determine if the action is relevant and find the necessary context (e.g., finding the dying Hunter).
    *   `ProcessDayAction(GameSession session, ModeratorInput input)` (ProcessResult): Processes moderator input for day actions. The role implementation uses the `session` to find the actor(s), validate the action, and update the `session`.
    *   **Concrete Implementations:** (`SimpleVillagerRole`, `SeerRole`, `WitchRole`, etc.) - These classes represent the *rules* of the role.

---------------------

4.  **`PlayerState` Class:** Wrapper class holding all dynamic state information for a `Player`. This improves organization and separation of concerns. Properties typically use `internal set` to allow modification primarily by the `GameService` or internal logic, maintaining state integrity.
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

7.  **`GameService` Class:** Orchestrates the game flow based on moderator input and tracked state.
    *   **Public Methods:**
        *   `StartNewGame(List<string> playerNamesInOrder, List<RoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)` (Guid): Creates a new `GameSession`, initializes players (with `Role = null`), records roles/events provided. Sets `GamePhase = Setup` and generates the initial instruction asking to confirm readiness for Night 1.
        *   `ProcessModeratorInput(Guid gameId, ModeratorInput input)` (ProcessResult): Takes moderator input, updates the tracked state in the specified `GameSession` (e.g., **assigns identified roles during Night 1 identification**, logs an action, records votes), advances the game state machine based on rules and tracked state, checks for game over conditions, and returns the next `ProcessResult`.
        *   `GetCurrentInstruction(Guid gameId)` (ModeratorInstruction): Retrieves the `PendingModeratorInstruction`.
        *   `GetGameStateView(Guid gameId)` (object): Returns a read-only view/DTO of the tracked game state.
    *   **Internal Logic:**
        *   Manages the game loop based on `GameSession.GamePhase` (`HandleSetupPhase`, `HandleNightPhase`, etc.).
        *   **Night Phase Management:**
            *   Uses `GenerateNextNightInstruction` helper to determine the next step: Identify a role (Night 1 only) or prompt for a role's action.
            *   **Handles Initial Night Start Confirmation:** Checks for the `GameStrings.NightStartsPrompt` confirmation first. If confirmed, proceeds to call `GenerateNextNightInstruction` for the first role's step.
            *   Handles pending identification input (`session.PendingNight1IdentificationForRole`): Calls `role.ProcessIdentificationInput`. If successful, logs `InitialRoleAssignmentLogEntry`, clears the pending state, and *immediately* generates the *action* instruction for the *same role* via `role.GenerateNightInstructions`.
            *   If no pending identification: Determines the current role using `session.CurrentNightActingRoleIndex` and `GetNightWakeUpOrder()`.
            *   Checks if identification is needed (`session.TurnNumber == 1 && role.RequiresNight1Identification() && role not assigned`). If yes, sets `session.PendingNight1IdentificationForRole` and returns the identification prompt from `role.GenerateIdentificationInstructions`.
            *   If identification is not needed, generates the action prompt using `role.GenerateNightInstructions`.
            *   Processes action input using `role.ProcessNightAction`. If successful, increments `CurrentNightActingRoleIndex` and calls `GenerateNextNightInstruction` to determine the subsequent step (next role's ID/action, or transition to Day).
        *   **Validates and Processes Role Actions:** Ensures actions comply with rules (e.g., checks `GameSession.LastProtectedPlayerId` before processing Defender action; checks `PlayerState.HasLostFoxPower` before Fox action; checks `PlayerState.HasUsedStutteringJudgePower` before allowing signal; checks `PlayerState.PotionsUsed` for Witch; **checks for ally targeting based on assigned `Player.Role` for Werewolves**).
        *   Processes reported night actions, applying protections, and calculating outcomes based on rules and tracked state (e.g., increments `PlayerState.TimesAttackedByWerewolves` for Elder target; sets `PlayerState.HasLostFoxPower` if Fox finds nothing).
        *   **Handles Delayed Effects:** Checks `GameSession.PendingKnightCurseTarget` during `Day_ResolveNight` and applies elimination if set. Schedules `PlayerState.IsTemporarilyRemoved` flag reset for Little Rascal's return.
        *   Prompts moderator to input event card draws. Applies event effects.
        *   Guides moderator through voting, calculates results based on reported votes.
        *   Prompts moderator to input revealed roles upon elimination (updates `Player.Role` and `IsRoleRevealed`).
        *   **Handles State Resets:** Explicitly resets relevant `PlayerState` flags/counters when the Devoted Servant successfully swaps roles.
        *   **Tracks Appointments:** Includes logic for Sheriff to appoint/change `GameSession.TownCrierPlayerId`.
        *   **Checks victory conditions:** Evaluates if any winning condition (`Team` enum value) has been met. **Relies on the assigned `Player.Role` (whether revealed or assigned during Night 1 identification or via Day reveal)**, `Player.Status`, and game state flags (`Lovers`, `InfectedPlayerIds`, etc.). It compares counts of living players per known/assigned faction against win conditions.
            *   **Ambiguous Role Alignment:** The victory check must correctly determine the *current effective team* of ambiguous roles based on game state (e.g., Thief's chosen role, Wolf Hound's choice, Wild Child based on model's status, **using assigned `Player.Role`**) when calculating faction counts.
            *   Informs the moderator of the specific winning condition(s) met if the game is over.
        *   **Victory Check Timing:** These checks should be performed after night resolution (`Day_ResolveNight`), after day-time eliminations resulting from events or revealed roles (`Day_Event`), and after vote resolution (`Day_ResolveVote`).
        *   **Manages Positional Logic:** Uses the `GameSession.PlayerSeatingOrder` list to determine relative player positions when required by rules (e.g., Fox, Bear Tamer, Knight, Nightmare, Influences). This is typically done via internal helper methods:
            *   `GetLeftNeighbor(Guid playerId, GameSession session, bool skipDead = true)`: Finds the ID of the neighbor to the left, optionally skipping dead players.
            *   `GetRightNeighbor(Guid playerId, GameSession session, bool skipDead = true)`: Finds the ID of the neighbor to the right, optionally skipping dead players.
            *   `GetAdjacentLivingNeighbors(Guid playerId, GameSession session)`: Returns a tuple of living left and right neighbor IDs.
            *   These helpers use the `PlayerSeatingOrder` list and modulo arithmetic, checking `Player.Status` when `skipDead` is true.

--------------------------

8.  **`ProcessResult` Class:**
    *   **Purpose:** Acts as a standard return type for operations like `GameService.ProcessModeratorInput` that can either succeed (yielding the next step) or fail (providing error details).
    *   **Structure:** Contains a boolean `IsSuccess` flag. If `true`, it holds the resulting `ModeratorInstruction?`. If `false`, it holds a `GameError` object detailing the failure. Also includes a `CheckVictory` flag (added via extension method) to signal when victory conditions should be re-evaluated after a successful operation.
    *   **Immutability:** Designed to be immutable after creation via static factory methods (`Success`, `Failure`) to ensure predictable state.
    *   **Usage:** Prevents the need for exception handling for expected validation/rule failures, allowing the calling layer (e.g., the UI or API endpoint) to gracefully handle errors and provide feedback based on the `GameError` details.

9.  **`GameError` Class:**
    *   **Purpose:** Provides structured information about a specific error that occurred during game logic processing.
    *   **Structure:**
        *   `Type` (`ErrorType` enum): Classifies the error into broad categories (e.g., invalid input, rule violation).
        *   `Code` (`GameErrorCode` enum): A specific, machine-readable code identifying the exact error.
        *   `Message` (string): A human-readable description of the error intended for the moderator.
        *   `Context` (Optional `IReadOnlyDictionary<string, object>`): Allows attaching relevant data to the error (e.g., the invalid player ID submitted, the conflicting state value) for richer feedback or debugging.
    *   **Usage:** Enables the calling layer to understand *why* an operation failed, display a relevant message, and potentially adjust its state or request corrected input. The `Code` allows for programmatic switching or specific handling if needed.

10. **`ModeratorInput` Class:** Data structure for communication FROM the moderator.
    *   `InputTypeProvided` (enum `ExpectedInputType`): Indicates which optional field below is populated.
    *   `SelectedPlayerIds` (List<Guid>?): IDs of players chosen. **Used for role identification (`PlayerSelectionMultiple`) and vote outcome (`PlayerSelectionSingle`, allowing 0 for tie).**
    *   `SelectedRole` (enum `RoleType`?): Role chosen (e.g., for role reveal).
    *   `SelectedOption` (string?): Specific text option chosen.
    *   `Confirmation` (bool?): Boolean confirmation.

**Design Note on Vote Input:**

A key design principle for moderator input, especially during voting phases, is minimizing data entry to enhance usability during live gameplay. The application is designed to guide the moderator through the *process* of voting (whether standard or event-driven like Nightmare, Great Distrust, Punishment), reminding them of the relevant rules. However, the actual vote tallying is expected to happen physically among the players.

Consequently, the `ModeratorInput` structure requires the moderator to provide only the final *outcome* of the vote (e.g., who was eliminated via `SelectedPlayerIds`, where an empty list signifies a tie, or confirmation of other outcomes via `Confirmation`). This approach significantly reduces the moderator's interaction time and minimizes the potential for input errors. The application functions primarily as a streamlined state tracker and procedural guide, accepting the loss of granular vote data in its logs as an acceptable trade-off for improved real-time usability.

--------------------------

11. **`ModeratorInstruction` Class:** Data structure for communication TO the moderator.
    *   `InstructionText` (string): The core message/question for the moderator.
    *   `ExpectedInputType` (ExpectedInputType Enum): Specifies the kind of input expected, and implies which `Selectable*` list might be populated.
    *   `AffectedPlayerIds` (List<Guid>?): Optional: Player(s) this instruction primarily relates to (for context, e.g., player needing role reveal).
    *   `SelectablePlayerIds` (List<Guid>?): Populated if `ExpectedInputType` involves selecting players (e.g., `PlayerSelectionSingle`, `PlayerSelectionMultiple`).
    *   `SelectableRoles` (List<RoleType>?): Populated if `ExpectedInputType` is `RoleSelection`. Note: Uses the `RoleType` enum directly.
    *   `SelectableOptions` (List<string>?): Populated if `ExpectedInputType` is `OptionSelection`.

--------------------------

12. **Enums:**
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
    *   `ExpectedInputType`: `None`, `PlayerSelectionSingle`, `PlayerSelectionMultiple`, `RoleSelection`, `OptionSelection`, `Confirmation`.
    *   `WitchPotionType`: `Healing`, `Poison`. (Could be flags).
    *   `NightActionType`: `Unknown`, `WerewolfVictimSelection`, `SeerCheck`, `WitchSave`, `WitchKill`, `DefenderProtect`, `PiperCharm`.
    *   `RoleType` (representing the intended values for a RoleType Enum):
        *   **System Types:** Unassigned, Unknown
        *   **Werewolves:** SimpleWerewolf, BigBadWolf, AccursedWolfFather, WhiteWerewolf
        *   **Villagers:** SimpleVillager, VillagerVillager, Seer, Cupid, Witch, Hunter, LittleGirl, Defender, Elder, Scapegoat, VillageIdiot, TwoSisters, ThreeBrothers, Fox, BearTamer, StutteringJudge, KnightWithRustySword
        *   **Ambiguous:** Thief, DevotedServant, Actor, WildChild, WolfHound
        *   **Loners:** Angel, Piper, PrejudicedManipulator
        *   **New Moon Roles:** Gypsy, TownCrier
    *    `ErrorType`: Defines the high-level categories of game errors.
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
    *   `GameService.StartNewGame` initializes `GameSession` with players (unidentified roles), records roles provided. Generates the initial instruction: "Setup complete. Proceed to Night 1?" (`ExpectedInputType.Confirmation`).
    *   `GameService.ProcessModeratorInput` handles this confirmation.
    *   If `true`: Transitions to `GamePhase.Night`, sets `TurnNumber = 1`, resets `CurrentNightActingRoleIndex = -1`, clears `NightActionsLog`, and generates the initial instruction for Night 1: "The village goes to sleep." (`GameStrings.NightStartsPrompt`, `ExpectedInputType.Confirmation`).

2.  **Night Phase (`GamePhase.Night`):**
    *   `GameService` manages the flow using `HandleNightPhase` and `GenerateNextNightInstruction`.
    *   **Handle Initial Confirmation:** `HandleNightPhase` first checks if the pending instruction is the initial "Night Starts" prompt. If it receives `true` confirmation, it proceeds by calling `GenerateNextNightInstruction` to get the *first* actual role prompt (identification or action). If confirmation is `false`, it re-issues the prompt.
    *   **Check for Pending Identification (Night 1 Only):** If `session.PendingNight1IdentificationForRole` is set, `HandleNightPhase` expects identification input. It calls `role.ProcessIdentificationInput`. On success, it logs the assignment (`InitialRoleAssignmentLogEntry`), clears the pending state, and *immediately* generates the *action* instruction for the *same role* via `role.GenerateNightInstructions`.
    *   **Determine Next Step:** If no identification is pending, `GenerateNextNightInstruction` finds the next role in the `GetNightWakeUpOrder()` sequence based on `CurrentNightActingRoleIndex`.
    *   **Check Need for Identification (Night 1 Only):** If it's Night 1, the role `RequiresNight1Identification`, and no player is assigned that role yet, `GenerateNextNightInstruction` sets `PendingNight1IdentificationForRole` and returns the identification prompt from `role.GenerateIdentificationInstructions`.
    *   **Generate Action Prompt:** If identification is not needed for the current role, `GenerateNextNightInstruction` returns the action prompt via `role.GenerateNightInstructions`.
    *   **Process Action:** `HandleNightPhase` expects action input corresponding to the generated instruction. It calls `role.ProcessNightAction`. On success, it increments `CurrentNightActingRoleIndex` and calls `GenerateNextNightInstruction` to determine the subsequent step (next role's ID/action, or transition to Day).
    *   Actions are logged to `GameSession.NightActionsLog` by the `ProcessNightAction` methods.
    *   When `GenerateNextNightInstruction` determines no more roles need to act, it transitions to `GamePhase.Day_ResolveNight`.

3.  **Night Resolution Phase (`GamePhase.Day_ResolveNight`):**
    *   `GameService` processes logged night actions:
        *   **Check for Knight's Curse:** Eliminate `PendingKnightCurseTarget` if set, then clear it.
        *   Determine Werewolf target(s).
        *   Apply Defender protection.
        *   Apply Witch actions.
        *   Apply Accursed Wolf-Father infection (if used).
        *   Increment `TimesAttackedByWerewolves` if Elder was targeted.
        *   *Event Check:* Apply active event modifications based on rules and state.
        *   Calculate deaths based on rules (considering Elder survival) and **assigned roles**. Generate instruction: "The following players were eliminated: [Names]. Please announce."
    *   Moderator uses this info. `ProcessModeratorInput` (Confirmation) triggers processing. The app updates internal state (e.g., `LastProtectedPlayerId`).
    *   Add deaths to `GameHistoryLog` (`PlayerEliminatedLogEntry`).
    *   **Check Game Over** based on assigned roles and game state.
    *   If game continues, transition to `GamePhase.Day_Event` (if eliminations) or `Day_Debate`. Generate appropriate instruction (e.g., Role Reveal prompt).

4.  **Day Event Phase (`GamePhase.Day_Event`):**
    *   `GameService` prompts moderator to announce victims (if instruction generated in previous phase).
    *   If an eliminated player needs role reveal: Prompt Moderator to input revealed role (`ExpectedInputType.RoleSelection`). `ProcessModeratorInput` updates `Player.Role` and `IsRoleRevealed`. Log `RoleRevealedLogEntry`.
    *   Handle death triggers based on the now-known role (Hunter's shot - prompt for target; Lovers - automatically mark).
    *   **Check Game Over** based on assigned roles and game state.
    *   Prompt Moderator if Bear Tamer is alive and adjacent to a player with an assigned Werewolf role.
    *   Prompt Moderator to draw and input the Event Card (if applicable). `drawnCard.ApplyEffect()` generates next instruction.
    *   Transition based on event or to `GamePhase.Day_Debate`.

5.  **Debate Phase (`GamePhase.Day_Debate`):**
    *   `GameService` reminds Moderator of any active event rules affecting debate.
    *   Await moderator confirmation (`ExpectedInputType.Confirmation`) to proceed to vote.
    *   Transition to appropriate voting phase.

6.  **Voting Phase (Standard: `GamePhase.Day_Vote`, etc.):**
    *   `GameService` guides Moderator based on standard rules or active events.
    *   Prompt Moderator to input the vote *outcome* (e.g., eliminated player ID via `PlayerSelectionSingle`, or empty selection for Tie).
    *   Process input via `ProcessModeratorInput`. Store outcome in `PendingVoteOutcome`.
    *   Transition to `GamePhase.Day_ResolveVote`.

7.  **Vote Resolution Phase (`GamePhase.Day_ResolveVote`):**
    *   `GameService` determines elimination based on the reported `PendingVoteOutcome` and tracked modifiers (Sheriff tie-break). Log vote outcome (`VoteResolvedLogEntry`).
    *   Generate Instruction: "[Player] was eliminated / The vote resulted in a tie."
    *   If eliminated, prompt Moderator to input the revealed role (`ExpectedInputType.RoleSelection`) - transition back to `Day_Event`.
    *   Update `Player.Status`. Log `PlayerEliminatedLogEntry`.
    *   Handle elimination triggers based on revealed role (Hunter - prompt; Idiot - update; Sheriff - prompt successor). Log these events.
    *   **Check Game Over** based on assigned roles and game state.
    *   If game continues (Tie): Increment `TurnNumber`, update event timers. Transition to `GamePhase.Night`. Generate first night instruction.

8.  **Game Over Phase (`GamePhase.GameOver`):**
    *   `GameService` reports winning team based on assigned roles and game state.
    *   Generate final instruction: "Game Over. Based on the tracked state, [Winning Team] wins."

---------------------------

**Setup & Initial State Logs:**

The chosen approach is an abstract base class (`GameLogEntryBase`) providing universal properties (`Timestamp`, `TurnNumber`, `Phase`) combined with distinct concrete derived types (preferably records) for each specific loggable event (`PlayerEliminatedLog`, `RoleRevealedLog`, etc.). This flat hierarchy significantly reduces boilerplate for universal fields via the base class while maintaining strong type safety, clarity, and maintainability through specific derived types.


1.  **Game Started:** Records the initial configuration provided by the moderator - the list of roles included in the deck, the list of players by name/ID, and the list of event cards included in the deck. *Uniqueness: Captures the baseline parameters of the game session.*
2.  **Initial Role Assignment (`InitialRoleAssignmentLogEntry`):** Records roles assigned during the *Night 1* identification process. Logs the `PlayerId` and the `AssignedRole` (`RoleType`). Generated by `GameService.HandleNightPhase` after successfully processing moderator input for identification. *Uniqueness: Captures the moderator's identification of key roles before Night 1.* (Replaces previous specific logs like Thief Choice, Cupid Choice etc. within this single log type for setup phase assignments).

**Night Action Logs (Inputs & Choices):**

3.  **Seer View Attempt:** Logs the Seer's ID and the ID of the player they chose to view. (The *result* might be logged separately or implicitly handled by resolution logic, especially with Somnambulism). *Uniqueness: Records the Seer's target choice.*
4.  **Fox Check Performed:** Logs the Fox's ID, the player they targeted, the IDs of the two neighbors checked, the Yes/No result given (WW nearby?), and whether the Fox lost their power as a result. *Uniqueness: Records the Fox's check details and outcome.*
5.  **Defender Protection Choice:** Logs the Defender's ID and the ID of the player they chose to protect for the night. *Uniqueness: Records the target of protection.*
6.  **Piper Charm Choice:** Logs the Piper's ID and the IDs of the two players they chose to charm. *Uniqueness: Records the targets of the charm effect.*
7.  **Witch Potion Use Attempt:** Logs the Witch's ID, the type of potion used (Healing or Poison), and the ID of the player targeted. *Uniqueness: Records the Witch's specific action and target.*
8.  **Night Action Log Entry (`NightActionLogEntry`):** A generic entry logging a specific action taken during the night. Includes:
    *   `ActorId` (Guid): ID of the player performing the action.
    *   `TargetId` (Guid?): ID of the player targeted, if applicable.
    *   `ActionType` (`NightActionType` Enum): Specifies the type of action performed (e.g., `WerewolfVictimSelection`, `SeerCheck`, `WitchSave`, `WitchKill`). *Using an enum ensures type safety and avoids string comparisons.*
    *   *(May include other relevant fields based on ActionType)*.
    *   *Uniqueness: Records the fundamental action performed by a role during the night.*

**Night & Day Resolution / Outcome Logs:**

14. **Player Eliminated (`PlayerEliminatedLogEntry`):** Logs the ID of the eliminated player and the specific *reason* for their elimination (e.g., `WerewolfAttack`, `WitchPoison`, `KnightCurse`, `HunterShot`, `LoversHeartbreak`, `DayVote`, `Scapegoat`, `GreatDistrust`, `PunishmentEvent`, `SpecterEvent`, etc.). *Uniqueness: The fundamental record of a player leaving the game and why.*
15. **Role Revealed (`RoleRevealedLogEntry`):** Logs the ID of a player whose role card was revealed (due to death, Village Idiot save, Devoted Servant swap, etc.) and the specific `RoleType` revealed. Generated after processing moderator input during `Day_Event`. *Uniqueness: Records the confirmation of a player's role.*
16. **Little Girl Caught:** Logs that the Little Girl spied, was caught, and became the Werewolves' target instead of their original choice. *Uniqueness: Records this specific night event outcome.*
17. **Elder Survived Attack:** Logs that the Elder was targeted (likely by Werewolves) but survived due to their ability (first time). *Uniqueness: Records the Elder rule interaction.*
18. **Knight Curse Activated:** Logs that the Knight was killed by Werewolves, activating the curse effect scheduled for the *next* night against a specific Werewolf (identified by proximity/logic). *Uniqueness: Signals the delayed curse effect is pending.*
19. **Wild Child Transformed:** Logs that the Wild Child's model was eliminated, causing the Wild Child to become a Werewolf. *Uniqueness: Records the role change of the Wild Child.*
20. **Player State Changed:** A generic log for various boolean flags or simple state updates on a player, detailing the player ID, the state that changed (e.g., `IsInfected`, `IsCharmed`, `IsMuted`, `CanVote` changed, `VoteMultiplier` applied, `HasUsedAWFInfection`, `HasLostFoxPower`, `HasUsedStutteringJudgePower`), and the new value. *Uniqueness: Captures miscellaneous status effects not covered by more specific logs. (Consider a dedicated `PlayerInfectedLog` if infection tracking proves complex).*
21. **Bear Tamer Growl Occurred:** Logs that the conditions were met for the Moderator to growl (Bear Tamer alive next to a player with assigned Werewolf role). *Uniqueness: Contextual indicator based on known state and positioning.*
22. **Devoted Servant Swap Executed:** Logs the Servant's ID, the ID of the player they saved from reveal, and the (hidden) role the Servant adopted. *Uniqueness: Records the role and player swap.*

**Day Phase Specific Logs:**

23. **Event Card Drawn:** Logs the specific New Moon Event Card ID and Name drawn at the start of the day. *Uniqueness: Records the active event modifying the day/upcoming night.*
24. **Gypsy Question Asked & Answered:** Logs the text of the Spiritualism question asked by the Medium and the "Yes" or "No" answer provided by the Moderator (as the spirit). *Uniqueness: Records the outcome of the Spiritualism event.*
25. **Town Crier Event Played:** Logs the specific Event Card ID and Name played by the Town Crier from their hand. *Uniqueness: Records an additional event activation.*
26. **Sheriff Appointed:** Logs the ID of the player who became Sheriff, the reason (Initial Election, Successor Appointment, Event), and the ID of the predecessor (if any). *Uniqueness: Tracks the Sheriff role holder.*
27. **Stuttering Judge Signaled Second Vote:** Logs that the Judge used their one-time ability to trigger a second vote this day. *Uniqueness: Records the use of the Judge's power.*
28. **Vote Outcome Reported (`VoteOutcomeReportedLogEntry`):** Logs the raw outcome (eliminated player ID or `Guid.Empty` for tie) reported by the Moderator during the `Day_Vote` phase. *Uniqueness: Captures moderator input for vote resolution.*
29. **Accusation Outcome Reported (Nightmare):** Logs the result of the Nightmare event vote, typically the ID of the player eliminated, as reported by the Moderator. *Uniqueness: Input for Nightmare resolution.*
30. **Friend Vote Outcome Reported (Great Distrust):** Logs the result of the Great Distrust event, typically the IDs of players eliminated (those receiving no friend votes), as reported by the Moderator. *Uniqueness: Input for Great Distrust resolution.*
31. **Vouching Outcome Reported (Punishment):** Logs the result of the Punishment event's vouching phase, indicating whether the target player was eliminated (due to insufficient vouches), as reported by the Moderator. *Uniqueness: Input for Punishment resolution.*
32. **Vote Resolved (`VoteResolvedLogEntry`):** Logs the final result of a voting phase *after* resolving the moderator-provided outcome during `Day_ResolveVote` - identifies who (if anyone) was eliminated and whether it was a tie. *Uniqueness: The final calculated result of a voting round.*
33. **Villager Powers Lost (Elder Died By Vote):** Logs that the Elder was eliminated by a day vote, causing all Villagers to lose their special abilities. *Uniqueness: Major game state change affecting multiple roles.*
34. **Scapegoat Voting Restrictions Set:** Logs the decision made by an eliminated Scapegoat regarding who can/cannot vote the following day. *Uniqueness: Records temporary voting rule changes.*

**Game End Log:**

35. **Victory Condition Met (`VictoryConditionMetLogEntry`):** Logs the determined winning team/player(s) and a brief description of the condition met (e.g., "All Werewolves eliminated," "Werewolves equal Villagers," "All survivors charmed," "Angel eliminated early"). *Uniqueness: Marks the end of the game and the outcome.*

This list aims to cover the distinct, loggable events derived from the rules. Each entry captures unique information critical for game logic, auditing, or moderator context.