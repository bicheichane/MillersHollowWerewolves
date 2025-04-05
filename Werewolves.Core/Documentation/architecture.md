**Project:** `Werewolves.Core` (.NET Class Library)

**Goal:** To provide the core game logic and state management for a Werewolves of Miller's Hollow moderator helper application, handling rules and interactions based on the provided rulebook pages (excluding Buildings/Village expansion, but including specified New Moon events). The app **tracks the game state based on moderator input**. It assumes moderator input is accurate and provides deterministic state tracking and guidance based on that input.

**Core Components:**

We identified the need to track relative player seating positions due to rules involving neighbors (Fox, Bear Tamer) or sequential actions based on position (Knight/Backfire effects, Nightmare/Influences events). Several approaches were considered: a doubly linked list, baking neighbor IDs directly into PlayerState, and maintaining a central ordered list of player IDs (PlayerSeatingOrder) within GameSession. The central list approach was chosen as the preferred solution.

While baking IDs into PlayerState was functionally viable, the central PlayerSeatingOrder list in GameSession was favoured because it provides better architectural separation, treating the static seating arrangement as a structural property of the game session rather than mixing it with dynamic player status effects in PlayerState. It also offers a clearer single source of truth for the order. Given the typical player counts (10-15, max 40), performance differences between approaches are negligible, making architectural clarity the deciding factor. Helper methods within GameService will encapsulate the logic for retrieving neighbors, including handling skips over eliminated players.

1.  **`GameSession` Class:** Represents the tracked state of a single ongoing game, derived from moderator input.
    *   `Id` (Guid): Unique identifier for the game session.
    *   `Players` (Dictionary<Guid, Player>): Collection of all players in the game, keyed by their unique ID. Tracks player information provided by the moderator.
    *   `PlayerSeatingOrder` (IReadOnlyList<Guid>): Stores the Player IDs in clockwise seating order as reported by the Moderator during setup. Crucial for roles like Knight, Fox, Bear Tamer and events like Nightmare, Influences. Established once at game start.
    *   `GamePhase` (GamePhase Enum): Current stage of the game (e.g., Setup, Night, Day_ResolveNight, Day_Event, Day_Debate, Day_Vote, Day_ResolveVote, GameOver).
    *   `TurnNumber` (int): Tracks the number of full Night/Day cycles completed.
    *   `RolesInPlay` (List<RoleType>): List of role types included in the game (provided by Moderator at setup).
    *   `EventDeck` (List<EventCard>): Represents the set of event cards included in the physical deck.
    *   `DiscardPile` (List<EventCard>): Event cards reported as drawn by the moderator.
    *   `ActiveEvents` (List<ActiveEventState>): Tracks currently active New Moon events (input by Moderator when drawn) and their specific state data.
    *   `GameHistoryLog` (List<GameLogEntryBase>): A chronological record of all significant game events, moderator inputs, state changes, and action outcomes tracked during the session. Uses the `GameLogEntryBase` derived types for structured, strongly-typed entries (see "Setup & Initial State Logs" section for examples). This replaces the separate night/day temporary logs.
    *   `PendingModeratorInstruction` (ModeratorInstruction): The current prompt/instruction for the moderator, asking for input or guiding the next step.
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
        *   `VoteResultsCache` (Dictionary<Guid, int>?): Stores results from the current vote phase (input by Moderator).
        *   `AccusationResultsCache` (Dictionary<Guid, int>?): Stores results from Nightmare accusation phase (input by Moderator).
        *   `FriendVoteCache` (Dictionary<Guid, int>?): Stores results from Great Distrust friend vote phase (input by Moderator).
        *   `LastEliminatedPlayerId` (Guid?): Tracks the most recently eliminated player for event triggers.
        *   `PrejudicedManipulatorGroups` (Dictionary<Guid, int>?): Optional mapping of player ID to group number for PM.

---------------

Initial designs considered using a flexible Dictionary<Enum, object> within the Player class to hold various dynamic states, offering adaptability but sacrificing type safety and clarity. Alternative approaches explored included using a [Flags] enum for boolean states (improving efficiency but unable to store associated data like IDs or multipliers) and using dedicated properties directly on the Player class for each state (enhancing readability and type safety but potentially cluttering the Player class definition as states grew).

After evaluating the trade-offs, the chosen architecture utilizes a dedicated PlayerState wrapper class. This class contains individual properties (e.g., IsSheriff, LoverId, VoteMultiplier) for all dynamic boolean and data-carrying states, typically using internal set for controlled modification. The Player class then holds a single instance of PlayerState. This approach was selected because it provides the best balance of organization (grouping all volatile states together), strong typing, clear separation of concerns (keeping Player focused on identity/role), and scalability for future state additions, outweighing the minor syntactical overhead of accessing states via player.State.PropertyName.

2.  **`Player` Class:** Represents a participant and the tracked information about them.
    *   `Id` (Guid): Unique identifier.
    *   `Name` (string): Player's name.
    *   `Role` (IRole?): The player's character role instance, once identified by the moderator. Null initially, updated upon revelation (Night 1 action or death).
    *   `Status` (PlayerStatus Enum): Current status (`Alive`, `Dead`).
    *   `IsRoleRevealed` (bool): Flag indicating if the moderator has input this player's role.
    *   `State` (PlayerState): Encapsulates all dynamic states affecting the player (e.g., Sheriff status, protection, infection, charms, modifiers). This approach keeps the core Player class focused on identity and role, while grouping volatile states for better organization and potential future state management enhancements (like serialization or complex transitions).

--------------

3.  **`IRole` Interface:** Defines the contract for character roles (represents the *rules* of the role).
    *   `Name` (string): Role name (e.g., "Seer").
    *   `RoleType` (RoleType Enum): The type identifier.
    *   `Description` (string): Brief role description.
    *   `GetNightWakeUpOrder()` (int): Priority for night actions (lower wakes first). Return `int.MaxValue` if no night action.
    *   `RequiresNight1Identification()` (bool): Does this role need to be identified by the moderator on Night 1?
    *   `GenerateNightInstructions(GameSession session, Player currentPlayer)` (ModeratorInstruction?): Generates the prompt *if* this role is identified and needs to act.
    *   `ProcessNightAction(GameSession session, Player actor, ModeratorInput input)` (void): Processes moderator input for the night action, updating `GameSession` state (e.g., logging the target).
    *   `GenerateDayInstructions(GameSession session, Player currentPlayer)` (ModeratorInstruction?): Generates prompts for day-time actions (e.g., Hunter's last shot, if role is identified).
    *   `ProcessDayAction(GameSession session, Player actor, ModeratorInput input)` (void): Processes moderator input for day actions.
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

The core principle of this application is to accurately track the game state as known by the Moderator. Standard Werewolves gameplay dictates that the Moderator identifies players holding active night roles (like Seer, Werewolves, Piper, Cupid, etc.) during the first night's call sequence to manage subsequent turns. Consequently, the application architecture mandates that the GameService must prompt the Moderator to input the specific player holding such roles when IRole.RequiresNight1Identification() returns true for that role during the initial night cycle. This ensures the application state immediately reflects the Moderator's crucial knowledge.

Furthermore, roles identified during physical pre-game setup, notably the Prejudiced Manipulator and their group assignments, must also be captured by the application. The GameService's setup phase (before Night 1 begins) must include prompts for the Moderator to input this information. This approach guarantees the application's tracked state remains consistently synchronized with the information available to the human Moderator from the earliest possible moments.

7.  **`GameService` Class:** Orchestrates the game flow based on moderator input and tracked state.
    *   **Public Methods:**
        *   `StartNewGame(List<string> playerNamesInOrder, List<RoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)` (Guid): Creates a new `GameSession`, initializes players based on the provided list of names (which **must** be in clockwise seating order), populates the `PlayerSeatingOrder`, records roles/events provided, generates the first prompt, and returns the `GameSession.Id`.
        *   `ProcessModeratorInput(Guid gameId, ModeratorInput input)` (ModeratorInstruction): Takes moderator input, updates the tracked state in the specified `GameSession` (e.g., identifies a role, logs an action, records votes), advances the game state machine based on rules and tracked state, checks for game over conditions, and returns the next `ProcessResult`.
        *   `GetCurrentInstruction(Guid gameId)` (ModeratorInstruction): Retrieves the `PendingModeratorInstruction`.
        *   `GetGameStateView(Guid gameId)` (object): Returns a read-only view/DTO of the tracked game state.
    *   **Internal Logic:**
        *   Manages the game loop based on `GameSession.GamePhase`.
        *   Prompts moderator to identify players for roles that act in order (`GetNightWakeUpOrder`).
        *   **Validates and Processes Role Actions:** Ensures actions comply with rules (e.g., checks `GameSession.LastProtectedPlayerId` before processing Defender action; checks `PlayerState.HasLostFoxPower` before Fox action; checks `PlayerState.HasUsedStutteringJudgePower` before allowing signal; checks `PlayerState.PotionsUsed` for Witch).
        *   Processes reported night actions, applying protections, and calculating outcomes based on rules and tracked state (e.g., increments `PlayerState.TimesAttackedByWerewolves` for Elder target; sets `PlayerState.HasLostFoxPower` if Fox finds nothing).
        *   **Handles Delayed Effects:** Checks `GameSession.PendingKnightCurseTarget` during `Day_ResolveNight` and applies elimination if set. Schedules `PlayerState.IsTemporarilyRemoved` flag reset for Little Rascal's return.
        *   Prompts moderator to input event card draws. Applies event effects.
        *   Guides moderator through voting, calculates results based on reported votes.
        *   Prompts moderator to input revealed roles upon elimination.
        *   **Handles State Resets:** Explicitly resets relevant `PlayerState` flags/counters when the Devoted Servant successfully swaps roles.
        *   **Tracks Appointments:** Includes logic for Sheriff to appoint/change `GameSession.TownCrierPlayerId`.
        *   **Checks victory conditions:** Evaluates if any winning condition (`Team` enum value) has been met. **Crucially, this check relies *solely* on the tracked state available to the moderator:** the initial `RolesInPlay` list, the `Status` (`Alive`/`Dead`) and *revealed* `Role` of all players, current player counts per known faction (e.g., known living Villagers vs. maximum possible living Werewolves), and specific game states tracked in `GameSession` (e.g., `Lovers`, `InfectedPlayerIds`, `CharmedPlayerIds`, `Angel` status and timing, `PrejudicedManipulator` group status, `Piper` charm saturation, `WhiteWerewolf` survival). This check involves deducing if a win condition *must* be met given this known information, rather than requiring knowledge of every living player's hidden role.
            *   **Ambiguous Role Alignment:** The victory check must correctly determine the *current effective team* of ambiguous roles based on game state (e.g., Thief's chosen role, Wolf Hound's choice, Wild Child based on model's status) when calculating faction counts.
            *   Informs the moderator of the specific winning condition(s) met if the game is over.
        *   **Victory Check Timing:** These checks should be performed after night resolution (`Day_ResolveNight`), after day-time eliminations resulting from events or revealed roles (`Day_Event`), and after vote resolution (`Day_ResolveVote`).
            *   Contains internal helper methods to deduce counts and possibilities based *only* on known information (e.g., calculate max possible living WWs based on initial roles minus revealed non-WWs, calculate known living Villagers, check if all living players are Charmed, check if PM target group is eliminated, etc.).
        *   **Manages Positional Logic:** Uses the `GameSession.PlayerSeatingOrder` list to determine relative player positions when required by rules (e.g., Fox, Bear Tamer, Knight, Nightmare, Influences). This is typically done via internal helper methods:
            *   `GetLeftNeighbor(Guid playerId, GameSession session, bool skipDead = true)`: Finds the ID of the neighbor to the left, optionally skipping dead players.
            *   `GetRightNeighbor(Guid playerId, GameSession session, bool skipDead = true)`: Finds the ID of the neighbor to the right, optionally skipping dead players.
            *   `GetAdjacentLivingNeighbors(Guid playerId, GameSession session)`: Returns a tuple of living left and right neighbor IDs.
            *   These helpers use the `PlayerSeatingOrder` list and modulo arithmetic, checking `Player.Status` when `skipDead` is true.

--------------------------

1.   **`ProcessResult` Class:**
    *   **Purpose:** Acts as a standard return type for operations like `GameService.ProcessModeratorInput` that can either succeed (yielding the next step) or fail (providing error details).
    *   **Structure:** Contains a boolean `IsSuccess` flag. If `true`, it holds the resulting `ModeratorInstruction`. If `false`, it holds a `GameError` object detailing the failure.
    *   **Immutability:** Designed to be immutable after creation via static factory methods (`Success`, `Failure`) to ensure predictable state.
    *   **Usage:** Prevents the need for exception handling for expected validation/rule failures, allowing the calling layer (e.g., the UI or API endpoint) to gracefully handle errors and provide feedback based on the `GameError` details.

2.   **`GameError` Class:**
    *   **Purpose:** Provides structured information about a specific error that occurred during game logic processing.
    *   **Structure:**
        *   `Type` (`ErrorType` enum): Classifies the error into broad categories (e.g., invalid input, rule violation).
        *   `Code` (string): A specific, machine-readable code identifying the exact error. Populated from dedicated enum values (e.g., `InvalidInputCode.PlayerIdNotFound.ToString()`). Using enums for definition provides strong typing and discoverability, while the string representation is useful for logging or serialization.
        *   `Message` (string): A human-readable description of the error intended for the moderator.
        *   `Context` (Optional `IReadOnlyDictionary<string, object>`): Allows attaching relevant data to the error (e.g., the invalid player ID submitted, the conflicting state value) for richer feedback or debugging.
    *   **Usage:** Enables the calling layer to understand *why* an operation failed, display a relevant message, and potentially adjust its state or request corrected input. The `Code` allows for programmatic switching or specific handling if needed.

3.   **`ModeratorInput` Class:** Data structure for communication FROM the moderator.
    *   `InputTypeProvided` (ExpectedInputType Enum): Matches the type expected by the instruction.
    *   `SelectedPlayerIds` (List<Guid>?): IDs of player(s) chosen.
    *   `SelectedRoleName` (string?): Name of role chosen.
    *   `SelectedOption` (string?): Text option chosen.
    *   `VoteResults` (Dictionary<Guid, int>?): Player ID -> Vote Count.
    *   `AccusationResults` (Dictionary<Guid, int>?): Player ID -> Accusation Count (Nightmare).
    *   `FriendVoteResults` (Dictionary<Guid, List<Guid>>?): Voter ID -> List of Friend IDs (Great Distrust).
    *   `Confirmation` (bool?): Yes/No confirmation value.
    *   `VouchedPlayerIds` (List<Guid>?): Players vouching (Punishment).

--------------------------

11.  **`ModeratorInstruction` Class:** Data structure for communication TO the moderator.
    *   `InstructionText` (string): The message to display.
    *   `ExpectedInputType` (ExpectedInputType Enum): Specifies the type of input required next.
    *   `AffectedPlayerIds` (List<Guid>?): Player(s) this instruction primarily relates to.
    *   `SelectablePlayerIds` (List<Guid>?): Valid player choices for selection input.
    *   `SelectableRoleNames` (List<string>?): Valid role choices (e.g., for Thief).
    *   `SelectableOptions` (List<string>?): Generic text options (e.g., Spiritualism questions).
    *   `RequiresConfirmation` (bool): Simple yes/no confirmation needed.

--------------------------

12.  **Enums:**
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
        *   *(Note: This enum defines potential winning states. Determining if one of these states has actually been achieved requires runtime logic within the `GameService`. The `GameService`'s victory condition check compares the current, moderator-known game state (player counts per known faction, revealed roles, specific statuses like Lovers, Charmed, Infected, Angel timing, PM group status) against the requirements for each potential `Team` outcome, without relying on knowledge of hidden roles.)*
    *   `EventTiming`: `Immediate`, `NextNight`, `NextDayVote`, `VictimEffect`, `PermanentAssignment`, `DayAction`.
    *   `EventDuration`: `OneTurn`, `OneNight`, `OneDayCycle`, `Permanent`, `UntilNextVote`.
    *   `ExpectedInputType`: `None`, `PlayerSelectionSingle`, `PlayerSelectionMultiple`, `RoleSelection`, `OptionSelection`, `VoteCounts`, `AccusationCounts`, `FriendVotes`, `Confirmation`, `VoucherSelection`, `SuccessorSelection`.
    *   `WitchPotionType`: `Healing`, `Poison`. (Could be flags).
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
            *   `InvalidInput_MalformedVoteData`
            *   `InvalidInput_IncorrectVoteSum`
            *   `InvalidInput_IncorrectFriendVoteCount`
            *   `InvalidInput_InvalidFriendVoteTarget`
            *   `InvalidInput_InsufficientVouchers`
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
        *   **Invalid Operation:**
            *   `InvalidOperation_GameIsOver`
            *   `InvalidOperation_ActionNotInCorrectPhase`
            *   `InvalidOperation_UnexpectedInput`

---------------------------------

**Game Loop Outline (Moderator Helper Perspective):**

1.  **Setup Phase (`GamePhase.Setup`):**
    *   Before transitioning to the first night, the GameService (or a dedicated setup step) must also prompt the Moderator for any role information identified during the physical pre-game setup. This specifically includes identifying the player assigned the Prejudiced Manipulator role and inputting the player group assignments.
    *   `GameService.StartNewGame` initializes `GameSession` with players (unidentified roles), records roles provided. Generates the first prompt (e.g., input Prejudiced Manipulator details if applicable, then identify Night 1 roles like Thief/Cupid), and returns the `GameSession.Id`.
    *   Prompt Moderator for Night 1 roles needing identification based on `IRole.RequiresNight1Identification()`. Process input via `ProcessModeratorInput` to update `Player.Role`.
    *   Transition to `GamePhase.Night`. Generate first night instruction (e.g., "Call the Seer").

2.  **Night Phase (`GamePhase.Night`):**
    *   `GameService` guides Moderator through roles based on `IRole.GetNightWakeUpOrder()`. Filter by roles included in the game.
    *   For each role identified to act: Prompt Moderator for action details (target, choice) via `role.GenerateNightInstructions`. Process input via `role.ProcessNightAction`, logging the action.
    *   Store actions in `GameSession.NightActionsLog`.
    *   Transition to `GamePhase.Day_ResolveNight`.

3.  **Night Resolution Phase (`GamePhase.Day_ResolveNight`):**
    *   `GameService` processes logged night actions:
        *   **Check for Knight's Curse:** Eliminate `PendingKnightCurseTarget` if set, then clear it.
        *   Determine Werewolf target(s).
        *   Apply Defender protection.
        *   Apply Witch actions.
        *   Apply Accursed Wolf-Father infection (if used).
        *   Increment `TimesAttackedByWerewolves` if Elder was targeted.
        *   *Event Check:* Apply active event modifications based on rules and state.
        *   Calculate deaths based on rules (considering Elder survival) and tracked state. Generate instruction: "The following players were eliminated: [Names]. Please announce."
    *   Moderator uses this info. `ProcessModeratorInput` is not typically needed here unless confirming an ambiguous outcome, but the app updates internal state (e.g., `LastProtectedPlayerId`).
    *   Add deaths to `GameHistoryLog`.
    *   **Check Game Over** based on tracked state (e.g., Lovers died, WW parity achieved overnight).
    *   If game continues, transition to `GamePhase.Day_Event`.

4.  **Day Event Phase (`GamePhase.Day_Event`):**
    *   `GameService` prompts moderator to announce victims.
    *   Prompt Moderator to input revealed roles for eliminated players. `ProcessModeratorInput` updates `Player.Role` and logs to `GameHistoryLog`.
    *   Handle death triggers based on identified roles (Hunter's shot - prompt for target; Lovers - automatically mark).
    *   **Check Game Over** based on tracked state (e.g., after Hunter shot, after Angel elimination timing check).
    *   Prompt Moderator if Bear Tamer is identified and adjacent to an identified Werewolf.
    *   Prompt Moderator to draw and input the Event Card. `drawnCard.ApplyEffect()` generates next instruction.
    *   Transition based on event or to `GamePhase.Day_Debate`.

5.  **Debate Phase (`GamePhase.Day_Debate`):**
    *   `GameService` reminds Moderator of any active event rules affecting debate.
    *   Await moderator confirmation (`ExpectedInputType.Confirmation`) to proceed to vote.
    *   Transition to appropriate voting phase.

6.  **Voting Phase (Standard: `GamePhase.Day_Vote`, etc.):**
    *   `GameService` guides Moderator based on standard rules or active events.
    *   Prompt Moderator to input vote counts/accusations/friend votes.
    *   Process input via `ProcessModeratorInput`. Store results in `VoteResultsCache`.
    *   Transition to `GamePhase.Day_ResolveVote`.

7.  **Vote Resolution Phase (`GamePhase.Day_ResolveVote`):**
    *   `GameService` calculates elimination based on reported votes and tracked modifiers (Sheriff). Log vote outcome.
    *   Generate Instruction: "[Player] received the most votes and is eliminated. Player reveals their card."
    *   Prompt Moderator to input the revealed role.
    *   Update `Player.Status` and `Player.Role`. Log role reveal.
    *   Handle elimination triggers based on identified roles (Hunter - prompt for target; Idiot - update status; Sheriff - prompt for successor). Log these events.
    *   **Check Game Over** based on tracked state (e.g., after vote elimination, after potential Hunter shot).
    *   If game continues: Increment `TurnNumber`, update event timers.
    *   Transition to `GamePhase.Night`. Generate first night instruction.

8.  **Game Over Phase (`GamePhase.GameOver`):**
    *   `GameService` reports winning team based on tracked state.
    *   Generate final instruction: "Game Over. Based on the tracked state, [Winning Team] wins."
  
---------------------------

**Setup & Initial State Logs:**

We explored several approaches for modeling the distinct game log entries required for logic resolution, auditing, and moderator context. Options considered included using a base interface, a single base class with an enum type discriminator, introducing intermediate abstract classes for grouping, and a flat hierarchy with an abstract base class and distinct concrete derived types.

The chosen approach is an abstract base class (GameLogEntryBase) providing universal properties (Timestamp, TurnNumber, Phase) combined with distinct concrete derived types (preferably records) for each specific loggable event (PlayerEliminatedLog, RoleRevealedLog, etc.). This flat hierarchy was selected because it offers the best balance: it significantly reduces boilerplate for universal fields via the base class while maintaining strong type safety, clarity, and maintainability through specific derived types. Alternatives like enum discriminators were rejected due to loss of type safety and potential for bloated base classes, while intermediate base classes offered marginal code reuse benefits that didn't outweigh the added complexity and potential for awkward abstractions in this specific domain.


1.  **Game Started:** Records the initial configuration provided by the moderator - the list of roles included in the deck, the list of players by name/ID, and the list of event cards included in the deck. *Uniqueness: Captures the baseline parameters of the game session.*
2.  **Initial Role Assignment (Special Cases):** Records roles assigned during Night 1 setup actions that permanently define a player's role or initial state *before* regular turns begin.
    *   **Thief Role Choice:** Logs the player ID identified as the Thief, the role they chose, and the role they discarded. *Uniqueness: Records the Thief's role transformation.*
    *   **Cupid's Lovers Choice:** Logs the player ID identified as Cupid and the IDs of the two players designated as Lovers. *Uniqueness: Establishes the Lover pair and their unique win condition/link.*
    *   **Wild Child Model Choice:** Logs the player ID identified as the Wild Child and the ID of the player they chose as their model. *Uniqueness: Establishes the Wild Child's dependency.*
    *   **Wolf Hound Alignment Choice:** Logs the player ID identified as the Wolf Hound and the side they chose (Villager or Werewolf). *Uniqueness: Defines the Wolf Hound's permanent team alignment.*
    *   **Actor Role Pool Set:** Logs the three specific non-Werewolf roles made available for the Actor to choose from. *Uniqueness: Defines the Actor's available powers.*
    *   **Prejudiced Manipulator Groups Defined:** Logs the assignment of each player to one of the two groups. *Uniqueness: Establishes the PM's win condition targets.*

**Night Action Logs (Inputs & Choices):**

3.  **Seer View Attempt:** Logs the Seer's ID and the ID of the player they chose to view. (The *result* might be logged separately or implicitly handled by resolution logic, especially with Somnambulism). *Uniqueness: Records the Seer's target choice.*
4.  **Fox Check Performed:** Logs the Fox's ID, the player they targeted, the IDs of the two neighbors checked, the Yes/No result given (WW nearby?), and whether the Fox lost their power as a result. *Uniqueness: Records the Fox's check details and outcome.*
5.  **Defender Protection Choice:** Logs the Defender's ID and the ID of the player they chose to protect for the night. *Uniqueness: Records the target of protection.*
6.  **Piper Charm Choice:** Logs the Piper's ID and the IDs of the two players they chose to charm. *Uniqueness: Records the targets of the charm effect.*
7.  **Witch Potion Use Attempt:** Logs the Witch's ID, the type of potion used (Healing or Poison), and the ID of the player targeted. *Uniqueness: Records the Witch's specific action and target.*
8.  **Werewolf Group Victim Choice:** Logs the consensus victim ID chosen by the Werewolf team (excluding BBW/White WW special kills). Optionally logs the IDs of participating WWs. *Uniqueness: Records the primary Werewolf target.*
9.  **Big Bad Wolf Victim Choice:** Logs the second victim ID chosen by the Big Bad Wolf (if conditions allow). *Uniqueness: Records the BBW's separate kill target.*
10. **White Werewolf Victim Choice:** Logs the Werewolf player ID targeted by the White Werewolf (if conditions allow). *Uniqueness: Records the White Werewolf's special kill target.*
11. **Accursed Wolf-Father Infection Attempt:** Logs the AWF's ID and the ID of the player they chose to *infect* instead of kill. *Uniqueness: Records the intent to infect, replacing the kill.*
12. **Actor Role Choice:** Logs the Actor's ID and which of the available roles they chose to emulate for the night. *Uniqueness: Records the Actor's active power for the night.*
13. **Gypsy Medium Choice:** Logs the Gypsy's ID and the ID of the player designated to ask the Spiritualism question the next day. *Uniqueness: Designates the Medium for the day's event.*

**Night & Day Resolution / Outcome Logs:**

14. **Player Eliminated:** Logs the ID of the eliminated player and the specific *reason* for their elimination (e.g., `WerewolfAttack`, `WitchPoison`, `KnightCurse`, `HunterShot`, `LoversHeartbreak`, `DayVote`, `Scapegoat`, `GreatDistrust`, `PunishmentEvent`, `SpecterEvent`, etc.). *Uniqueness: The fundamental record of a player leaving the game and why.*
15. **Role Revealed:** Logs the ID of a player whose role card was revealed (due to death, Village Idiot save, Devoted Servant swap, etc.) and the specific `RoleType` revealed. *Uniqueness: Records the confirmation of a player's role.*
16. **Little Girl Caught:** Logs that the Little Girl spied, was caught, and became the Werewolves' target instead of their original choice. *Uniqueness: Records this specific night event outcome.*
17. **Elder Survived Attack:** Logs that the Elder was targeted (likely by Werewolves) but survived due to their ability (first time). *Uniqueness: Records the Elder rule interaction.*
18. **Knight Curse Activated:** Logs that the Knight was killed by Werewolves, activating the curse effect scheduled for the *next* night against a specific Werewolf (identified by proximity/logic). *Uniqueness: Signals the delayed curse effect is pending.*
19. **Wild Child Transformed:** Logs that the Wild Child's model was eliminated, causing the Wild Child to become a Werewolf. *Uniqueness: Records the role change of the Wild Child.*
20. **Player State Changed:** A generic log for various boolean flags or simple state updates on a player, detailing the player ID, the state that changed (e.g., `IsInfected`, `IsCharmed`, `IsMuted`, `CanVote` changed, `VoteMultiplier` applied, `HasUsedAWFInfection`, `HasLostFoxPower`, `HasUsedStutteringJudgePower`), and the new value. *Uniqueness: Captures miscellaneous status effects not covered by more specific logs. (Consider a dedicated `PlayerInfectedLog` if infection tracking proves complex).*
21. **Bear Tamer Growl Occurred:** Logs that the conditions were met for the Moderator to growl (Bear Tamer alive next to a known Werewolf). *Uniqueness: Contextual indicator based on known state and positioning.*
22. **Devoted Servant Swap Executed:** Logs the Servant's ID, the ID of the player they saved from reveal, and the (hidden) role the Servant adopted. *Uniqueness: Records the role and player swap.*

**Day Phase Specific Logs:**

23. **Event Card Drawn:** Logs the specific New Moon Event Card ID and Name drawn at the start of the day. *Uniqueness: Records the active event modifying the day/upcoming night.*
24. **Gypsy Question Asked & Answered:** Logs the text of the Spiritualism question asked by the Medium and the "Yes" or "No" answer provided by the Moderator (as the spirit). *Uniqueness: Records the outcome of the Spiritualism event.*
25. **Town Crier Event Played:** Logs the specific Event Card ID and Name played by the Town Crier from their hand. *Uniqueness: Records an additional event activation.*
26. **Sheriff Appointed:** Logs the ID of the player who became Sheriff, the reason (Initial Election, Successor Appointment, Event), and the ID of the predecessor (if any). *Uniqueness: Tracks the Sheriff role holder.*
27. **Stuttering Judge Signaled Second Vote:** Logs that the Judge used their one-time ability to trigger a second vote this day. *Uniqueness: Records the use of the Judge's power.*
28. **Vote Counts Reported (Standard Vote):** Logs the raw vote counts received by each player during a standard day vote, as reported by the Moderator. *Uniqueness: Raw input for standard vote resolution.*
29. **Accusation Counts Reported (Nightmare):** Logs the number of accusations received by each player during the Nightmare event vote. *Uniqueness: Raw input for Nightmare resolution.*
30. **Friend Votes Reported (Great Distrust):** Logs the full mapping of who designated whom as a "friend" during the Great Distrust event. *Uniqueness: Raw input for Great Distrust resolution.*
31. **Vouchers Reported (Punishment):** Logs the IDs of the players who vouched for the target during the Punishment event. *Uniqueness: Raw input for Punishment resolution.*
32. **Vote Resolved (Outcome):** Logs the results of a voting phase *after* calculations - who (if anyone) was eliminated, whether it was a tie, if the Scapegoat was eliminated instead, and potentially the vote counts for verification. Needs variations or flags for standard, Nightmare, Great Distrust, Punishment outcomes. *Uniqueness: The final calculated result of a voting round.*
33. **Villager Powers Lost (Elder Died By Vote):** Logs that the Elder was eliminated by a day vote, causing all Villagers to lose their special abilities. *Uniqueness: Major game state change affecting multiple roles.*
34. **Scapegoat Voting Restrictions Set:** Logs the decision made by an eliminated Scapegoat regarding who can/cannot vote the following day. *Uniqueness: Records temporary voting rule changes.*

**Game End Log:**

35. **Victory Condition Met:** Logs the determined winning team/player(s) and a brief description of the condition met (e.g., "All Werewolves eliminated," "Werewolves equal Villagers," "All survivors charmed," "Angel eliminated early"). *Uniqueness: Marks the end of the game and the outcome.*

This list aims to cover the distinct, loggable events derived from the rules. Each entry captures unique information critical for game logic, auditing, or moderator context.