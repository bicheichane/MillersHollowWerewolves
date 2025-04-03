**Project:** `Werewolves.Core` (.NET Class Library)

**Goal:** To provide the core game logic and state management for a Werewolves of Miller's Hollow moderator helper application, handling rules and interactions based on the provided rulebook pages (excluding Buildings/Village expansion, but including specified New Moon events).

**Core Components:**

1.  **`GameSession` Class:** Represents the state of a single ongoing game.
    *   `Id` (Guid): Unique identifier for the game session.
    *   `Players` (Dictionary<Guid, Player>): Collection of all players in the game, keyed by their unique ID.
    *   `GamePhase` (GamePhase Enum): Current stage of the game (e.g., Setup, Night, Day_ResolveNight, Day_Event, Day_Debate, Day_Vote, Day_ResolveVote, GameOver).
    *   `TurnNumber` (int): Tracks the number of full Night/Day cycles completed.
    *   `RolesInPlay` (List<IRole>): List of role instances corresponding to the character cards dealt in this game.
    *   `AvailableRolesForSetup` (List<IRole>): Temporary list holding cards for roles like Thief/Actor during setup.
    *   `EventDeck` (List<EventCard>): Shuffled deck of New Moon event cards to be used.
    *   `DiscardPile` (List<EventCard>): Used event cards.
    *   `ActiveEvents` (List<ActiveEventState>): Tracks currently active New Moon events and their specific state data.
    *   `NightActionsPerformed` (List<object>): Structured log of significant actions taken during the *current* night (e.g., Werewolf target choice, Witch potion use). Reset each night.
    *   `DayEventsOccurred` (List<object>): Structured log of significant events revealed during the *current* day (e.g., PlayerDeathEvent, RoleRevealedEvent). Reset each day.
    *   `PendingModeratorInstruction` (ModeratorInstruction): The current prompt/instruction for the moderator.
    *   **State Flags & Tracking:**
        *   `SheriffPlayerId` (Guid?): ID of the current Sheriff.
        *   `Lovers` (Tuple<Guid, Guid>?): IDs of the two players linked by Cupid.
        *   `InfectedPlayerIds` (HashSet<Guid>): IDs of players infected by the Accursed Wolf-Father.
        *   `ProtectedPlayerId` (Guid?): ID of the player protected by the Defender *this night*. Reset nightly.
        *   `CharmedPlayerIds` (HashSet<Guid>): IDs of players charmed by the Piper.
        *   `ExecutionerPlayerId` (Guid?): ID of the elected Executioner (New Moon).
        *   `DoubleAgentPlayerId` (Guid?): ID of the secret Double Agent (New Moon).
        *   `FirstWerewolfVictimId` (Guid?): Tracks the ID of the *first* player ever killed by Werewolves (for Spiritualism). Updated only once.
        *   `PlayerVoteModifiers` (Dictionary<Guid, int>): Tracks vote multipliers (e.g., Little Rascal). Default 1.
        *   `PendingEliminations` (Queue<Guid>): Players awaiting elimination resolution due to cascading effects (Hunter/Lovers).
        *   `VoteResultsCache` (Dictionary<Guid, int>?): Stores results from the current vote phase.
        *   `AccusationResultsCache` (Dictionary<Guid, int>?): Stores results from Nightmare accusation phase.
        *   `FriendVoteCache` (Dictionary<Guid, int>?): Stores results from Great Distrust friend vote phase.
        *   `LastEliminatedPlayerId` (Guid?): Tracks the most recently eliminated player for event triggers (e.g., Enthusiasm, Influences, Punishment).

2.  **`Player` Class:** Represents a participant.
    *   `Id` (Guid): Unique identifier.
    *   `Name` (string): Player's name.
    *   `AssignedRole` (IRole): The player's secret character role instance.
    *   `Status` (PlayerStatus Enum): Current status (`Alive`, `Dead`).
    *   `StateFlags` (Dictionary<PlayerStateFlag, object>): Flexible storage for various temporary or permanent states affecting the player. Examples keys:
        *   `IsSheriff` (bool)
        *   `IsInLove` (bool)
        *   `LoverId` (Guid?)
        *   `IsProtectedTonight` (bool) - *Internal flag set during night resolution*
        *   `IsInfected` (bool) - *Redundant if using `GameSession.InfectedPlayerIds`*
        *   `IsCharmed` (bool) - *Redundant if using `GameSession.CharmedPlayerIds`*
        *   `IsTempWerewolf` (bool) - For Full Moon Rising
        *   `VoteMultiplier` (int) - For Little Rascal (can override GameSession default)
        *   `CanVote` (bool) - Affected by Village Idiot, Event Rules
        *   `IsMuted` (bool) - For event rule violations
        *   `IsIgnoringDebatePeers` (bool) - For Eclipse event
        *   `HasUsedAccursedInfection` (bool) - For Accursed Wolf-Father one-time use
        *   `WitchPotionsUsed` (WitchPotionType Flags Enum?) - Track potion usage

3.  **`IRole` Interface:** Defines the contract for character roles.
    *   `Name` (string): Role name (e.g., "Seer").
    *   `Team` (Team Enum): Affiliation (`Villager`, `Werewolf`, `Loner`, `Ambiguous`).
    *   `Description` (string): Brief role description.
    *   `GetNightWakeUpOrder()` (int): Priority for night actions (lower wakes first). Return `int.MaxValue` if no night action.
    *   `GenerateNightInstructions(GameSession session, Player currentPlayer)` (ModeratorInstruction?): Generates the prompt for this role's night action.
    *   `ProcessNightAction(GameSession session, Player actor, ModeratorInput input)` (void): Processes moderator input for the night action, updating `GameSession` state or `NightActionsPerformed`.
    *   `GenerateDayInstructions(GameSession session, Player currentPlayer)` (ModeratorInstruction?): Generates prompts for day-time actions (e.g., Hunter's last shot).
    *   `ProcessDayAction(GameSession session, Player actor, ModeratorInput input)` (void): Processes moderator input for day actions.
    *   `CheckVictoryCondition(GameSession session, Player currentPlayer)` (bool): Checks if this player (or their team) meets their win condition based on the current `GameSession` state.
    *   **Concrete Implementations:** (`SimpleVillagerRole`, `VillagerVillagerRole`, `SeerRole`, `WitchRole`, `HunterRole`, `CupidRole`, `LittleGirlRole`, `DefenderRole`, `ElderRole`, `ScapegoatRole`, `VillageIdiotRole`, `SimpleWerewolfRole`, `BigBadWolfRole`, `AccursedWolfFatherRole`, `WhiteWerewolfRole`, `ThiefRole`, `ActorRole`, `DevotedServantRole`, `WildChildRole`, `WolfHoundRole`, `PiperRole`, `PrejudicedManipulatorRole`, `AngelRole`, `TwoSistersRole`, `ThreeBrothersRole`, `FoxRole`, `BearTamerRole`, `StutteringJudgeRole`, `KnightWithRustySwordRole`). *Note: Some roles might be simpler and share logic via a base class.*

4.  **`EventCard` Abstract Class:** Base for New Moon event cards.
    *   `Id` (string): Unique identifier (e.g., "FullMoonRising").
    *   `Name` (string): Event card name.
    *   `Description` (string): Text description of the event.
    *   `Timing` (EventTiming Enum): Primary trigger time (e.g., `NextNight`, `Immediate`, `PermanentAssignment`).
    *   `Duration` (EventDuration Enum): How long the effect lasts (e.g., `OneNight`, `Permanent`, `UntilNextVote`).
    *   `ApplyEffect(GameSession session, GameService service)` (ModeratorInstruction): Applies the *initial* effect when the card is drawn (returns the instruction for the moderator). May modify `GameSession.ActiveEvents` or other state.
    *   **Optional Override Methods (for Active Events):**
        *   `ModifyNightActionResolution(GameSession session, NightActionResolution currentResolution)` (NightActionResolution): Allows active events to alter the outcome of night actions (e.g., Backfire, Specter, Miracle).
        *   `ModifyDayVoteProcess(GameSession session)` (ModeratorInstruction?): Allows events to change the voting mechanics (e.g., Nightmare, Influences, Great Distrust). Returns instruction if it takes over, null otherwise.
        *   `ModifyDebateRules(GameSession session)` (ModeratorInstruction?): Allows events to impose rules during debate (e.g., Eclipse, Good Manners, Not Me).
        *   `ModifyInstruction(GameSession session, ModeratorInstruction originalInstruction)` (ModeratorInstruction): Allows events to change the text/options of standard instructions (e.g., Somnambulism, Burial, Executioner).
        *   `ModifyVictoryConditions(GameSession session)` (void): Allows events to alter win conditions (less common, maybe Double Agent implicitly).
        *   `OnTurnEnd(GameSession session, GamePhase endingPhase)` (void): Hook for temporary events to decrement counters or clean up state.
    *   **Concrete Implementations:** (`FullMoonRisingEvent`, `SomnambulismEvent`, `EnthusiasmEvent`, `BackfireEvent`, `NightmareEvent`, `InfluencesEvent`, `ExecutionerEvent`, `DoubleAgentEvent`, `GreatDistrustEvent`, `SpiritualismEvent` (potentially 5 variants or one class handling variants), `NotMeNorWolfEvent`, `MiracleEvent`, `DissatisfactionEvent`, `TheLittleRascalEvent`, `PunishmentEvent`, `EclipseEvent`, `TheSpecterEvent`, `GoodMannersEvent`, `BurialEvent`).

5.  **`ActiveEventState` Class:** Stores runtime state for an active event in `GameSession.ActiveEvents`.
    *   `EventId` (string): Matches the `EventCard.Id`.
    *   `CardReference` (EventCard): Reference to the static definition of the card for accessing its methods.
    *   `TurnsRemaining` (int?): Countdown for temporary events. Null for permanent.
    *   `StateData` (Dictionary<string, object>): Event-specific runtime data (e.g., who is muted, which question was asked in Spiritualism).

6.  **`GameService` Class:** Orchestrates the game flow and manages sessions.
    *   **Public Methods:**
        *   `StartNewGame(List<string> playerNames, List<string> selectedRoleNames, List<string>? selectedEventCardIds = null)` (Guid): Creates a new `GameSession`, deals roles, shuffles events (if provided), handles initial setup (Thief, Cupid), sets phase to Night, and returns the `GameSession.Id`.
        *   `ProcessModeratorInput(Guid gameId, ModeratorInput input)` (ModeratorInstruction): Takes moderator input, updates the specified `GameSession`, advances the game state machine (calling role/event methods), checks for game over, and returns the next `ModeratorInstruction`.
        *   `GetCurrentInstruction(Guid gameId)` (ModeratorInstruction): Retrieves the `PendingModeratorInstruction` for the specified game.
        *   `GetGameStateView(Guid gameId)` (object): Returns a read-only view/DTO of the current game state for UI display (optional).
    *   **Internal Logic:**
        *   Manages the game loop based on `GameSession.GamePhase`.
        *   Calls roles in the correct night order (`GetNightWakeUpOrder`).
        *   Resolves night actions, applying protections and *checking `ActiveEvents` for modifications* (`ModifyNightActionResolution`).
        *   Initiates Day phase, reveals victims (*checking `ActiveEvents` for modifications* like Burial/Miracle), handles death triggers.
        *   Draws and applies event cards (`ApplyEffect`).
        *   Manages debate phase (*checking `ActiveEvents` for rule modifications*).
        *   Manages voting phase (*checking `ActiveEvents` for process modifications*).
        *   Resolves votes, handles eliminations and triggers (*checking `ActiveEvents` for consequence modifications* like Enthusiasm/Dissatisfaction/Executioner).
        *   Checks victory conditions (`IRole.CheckVictoryCondition`) after state changes.
        *   Generates appropriate `ModeratorInstruction` at each step.
        *   Handles event turn counters (`ActiveEventState.TurnsRemaining`) at phase transitions.

7.  **`ModeratorInstruction` Class:** Data structure for communication TO the moderator.
    *   `InstructionText` (string): The message to display.
    *   `ExpectedInputType` (ExpectedInputType Enum): Specifies the type of input required next.
    *   `AffectedPlayerIds` (List<Guid>?): Player(s) this instruction primarily relates to.
    *   `SelectablePlayerIds` (List<Guid>?): Valid player choices for selection input.
    *   `SelectableRoleNames` (List<string>?): Valid role choices (e.g., for Thief).
    *   `SelectableOptions` (List<string>?): Generic text options (e.g., Spiritualism questions).
    *   `RequiresConfirmation` (bool): Simple yes/no confirmation needed.

8.  **`ModeratorInput` Class:** Data structure for communication FROM the moderator.
    *   `InputTypeProvided` (ExpectedInputType Enum): Matches the type expected by the instruction.
    *   `SelectedPlayerIds` (List<Guid>?): IDs of player(s) chosen.
    *   `SelectedRoleName` (string?): Name of role chosen.
    *   `SelectedOption` (string?): Text option chosen.
    *   `VoteResults` (Dictionary<Guid, int>?): Player ID -> Vote Count.
    *   `AccusationResults` (Dictionary<Guid, int>?): Player ID -> Accusation Count (Nightmare).
    *   `FriendVoteResults` (Dictionary<Guid, List<Guid>>?): Voter ID -> List of Friend IDs (Great Distrust).
    *   `Confirmation` (bool?): Yes/No confirmation value.
    *   `VouchedPlayerIds` (List<Guid>?): Players vouching (Punishment).

9.  **Enums:**
    *   `GamePhase`: `Setup`, `Night`, `Day_ResolveNight`, `Day_Event`, `Day_Debate`, `Day_Vote`, `Day_ResolveVote`, `AccusationVoting` (Nightmare), `FriendVoting` (Great Distrust), `GameOver`.
    *   `PlayerStatus`: `Alive`, `Dead`.
    *   `Team`: `Villager`, `Werewolf`, `Loner`, `Ambiguous`.
    *   `PlayerStateFlag`: `IsSheriff`, `IsInLove`, `LoverId`, `IsProtectedTonight`, `IsInfected`, `IsCharmed`, `IsTempWerewolf`, `VoteMultiplier`, `CanVote`, `IsMuted`, `IsIgnoringDebatePeers`, `HasUsedAccursedInfection`, `WitchPotionUsed`. (Use string keys in dictionary for more flexibility if preferred over enum).
    *   `EventTiming`: `Immediate`, `NextNight`, `NextDayVote`, `VictimEffect`, `PermanentAssignment`, `DayAction`.
    *   `EventDuration`: `OneTurn`, `OneNight`, `OneDayCycle`, `Permanent`, `UntilNextVote`.
    *   `ExpectedInputType`: `None`, `PlayerSelectionSingle`, `PlayerSelectionMultiple`, `RoleSelection`, `OptionSelection`, `VoteCounts`, `AccusationCounts`, `FriendVotes`, `Confirmation`, `VoucherSelection`, `SuccessorSelection`.
    *   `WitchPotionType`: `Healing`, `Poison`. (Could be flags).

**Game Loop Outline (Incorporating Events):**

1.  **Setup Phase (`GamePhase.Setup`):**
    *   `GameService.StartNewGame` initializes `GameSession`, deals roles.
    *   Handle Thief (if present): Generate instruction, process input, swap roles.
    *   Handle Cupid (if present): Generate instruction, process input, set `GameSession.Lovers`.
    *   Assign initial Sheriff (if using rule variant, otherwise happens later).
    *   Transition to `GamePhase.Night`. Generate first night instruction.

2.  **Night Phase (`GamePhase.Night`):**
    *   `GameService` iterates through roles based on `IRole.GetNightWakeUpOrder()`.
    *   *Event Check:* Check `ActiveEvents` for modifications to the night sequence (e.g., `FullMoonRising`).
    *   For each role to act: Generate instruction via `role.GenerateNightInstructions`, wait for input, process via `role.ProcessNightAction`.
    *   Store actions in `GameSession.NightActionsPerformed`.
    *   Transition to `GamePhase.Day_ResolveNight`.

3.  **Night Resolution Phase (`GamePhase.Day_ResolveNight`):**
    *   `GameService` processes `NightActionsPerformed`:
        *   Determine initial Werewolf target(s).
        *   Apply Defender protection.
        *   Apply Witch actions (heal/poison).
        *   Apply Accursed Wolf-Father infection (if used).
        *   *Event Check:* Check `ActiveEvents` for modifications (`BackfireEvent.ModifyNightActionResolution`, `TheSpecterEvent.ModifyNightActionResolution`, `MiracleEvent.ModifyNightActionResolution`, `FullMoonRisingEvent.ModifyNightActionResolution`). Apply changes.
        *   Finalize deaths, update `Player.Status`. Set `FirstWerewolfVictimId` if needed. Add deaths to `DayEventsOccurred`.
        *   Transition to `GamePhase.Day_Event`.

4.  **Day Event Phase (`GamePhase.Day_Event`):**
    *   `GameService` reveals victims from `DayEventsOccurred`.
    *   *Event Check:* Check `ActiveEvents` for modifications to reveal (`BurialEvent.ModifyInstruction`).
    *   Handle immediate death triggers (Lovers' suicide, Hunter's shot - may require input via `GenerateDayInstructions`/`ProcessDayAction`). Add deaths to `DayEventsOccurred`.
    *   Check Game Over.
    *   Handle Bear Tamer growl.
    *   Draw top card from `GameSession.EventDeck`.
    *   `instruction = drawnCard.ApplyEffect(session, service);`
    *   Update `ActiveEvents` list based on card duration/type.
    *   Check if event changed phase immediately (e.g., `Nightmare`). If so, transition.
    *   Otherwise, set `PendingModeratorInstruction = instruction`.
    *   Transition to `GamePhase.Day_Debate` (or specific event phase if applicable).

5.  **Debate Phase (`GamePhase.Day_Debate`):**
    *   *Event Check:* Check `ActiveEvents` for rules (`EclipseEvent.ModifyDebateRules`, `GoodMannersEvent.ModifyDebateRules`, `NotMeNorWolfEvent.ModifyDebateRules`). Generate appropriate instructions.
    *   Await moderator confirmation (`ExpectedInputType.Confirmation`) to proceed to vote.
    *   Transition to appropriate voting phase (standard or event-driven).

6.  **Voting Phase (Standard: `GamePhase.Day_Vote`, others possible):**
    *   *Event Check:* Check `ActiveEvents` if an event dictates the vote process (`NightmareEvent.ModifyDayVoteProcess`, `InfluencesEvent.ModifyDayVoteProcess`, `GreatDistrustEvent.ModifyDayVoteProcess`). Execute that process.
    *   If standard vote: Generate instruction, expect `ExpectedInputType.VoteCounts`.
    *   Process votes (consider Sheriff, `PlayerVoteModifiers`). Store in `VoteResultsCache`.
    *   Transition to `GamePhase.Day_ResolveVote`.

7.  **Vote Resolution Phase (`GamePhase.Day_ResolveVote`):**
    *   Determine eliminated player(s) based on vote results (handle ties, Scapegoat).
    *   *Event Check:* Check `ActiveEvents` for reveal modifications (`ExecutionerEvent.ModifyInstruction`). Reveal eliminated player/role accordingly.
    *   Update `Player.Status`. Set `LastEliminatedPlayerId`.
    *   Handle elimination triggers (Village Idiot reveal, Hunter's shot, Lovers' suicide, Sheriff passing badge - may require input).
    *   *Event Check:* Check `ActiveEvents` for consequences (`EnthusiasmEvent`, `DissatisfactionEvent`). If triggered, loop back to a second vote (`GamePhase.Day_Vote`).
    *   *Event Check:* Check `ActiveEvents` for other consequences (`PunishmentEvent`). Initiate if needed.
    *   Check Game Over.
    *   If game continues: Increment `TurnNumber`, call `OnTurnEnd` for active events, remove expired events.
    *   Transition to `GamePhase.Night`. Generate first night instruction.

8.  **Game Over Phase (`GamePhase.GameOver`):**
    *   Determine winning team(s) based on final state and `IRole.CheckVictoryCondition`.
    *   *Event Check:* Check `ActiveEvents` for victory modifications (`DoubleAgentEvent` indirectly).
    *   Generate final game over message instruction.