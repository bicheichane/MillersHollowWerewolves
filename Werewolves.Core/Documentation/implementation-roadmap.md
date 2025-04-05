**Core Principles:**

*   **Iterative Building:** Each phase adds a distinct layer of functionality or complexity.
*   **Parallel Testing:** Each implementation step is paired with specific testing goals, allowing the test suite to grow alongside the code.
*   **Focus:** Steps concentrate on specific roles, mechanics, or structures to avoid overwhelming scope.

All tests should be built using the XUnit and Shouldly frameworks.
Constant string values should be stored in resx files for ease of future localization. whenever it is appropriate to do so.

---

**Phase 0: Project Foundation & Core Data Structures**

*Goal: Establish the basic project structure, essential data containers, and communication objects. No game logic yet.*

1.  **Implement: Project Setup & Basic Enums**
    *   Create the `Werewolves.Core` .NET Class Library project.
    *   Define core enums: `GamePhase` (initial values: Setup, Night, Day_ResolveNight, Day_Vote, Day_ResolveVote, GameOver), `PlayerStatus` (Alive, Dead), `Team` (Villagers, Werewolves - minimal initial set), `RoleType` (Unassigned, Unknown, SimpleVillager, SimpleWerewolf).
    *   Define basic error enums: `ErrorType`, `GameNotFoundCode`, `InvalidInputCode`, `RuleViolationCode`, `InvalidOperationCode`.
    *   Define the unified GameErrorCode enum with initial values using prefixes (e.g., GameNotFound_SessionNotFound, InvalidInput_InputTypeMismatch, RuleViolation_TargetIsAlly, InvalidOperation_ActionNotInCorrectPhase).
    *   *Test:* Basic compilation checks.

2.  **Implement: Core Data Classes (Structure Only)**
    *   Define `PlayerState` class with a few initial boolean properties (e.g., `IsSheriff`, `IsInLove` - default false). Use `internal set`.
    *   Define `Player` class with `Id`, `Name`, `Status`, `IsRoleRevealed`, and `State` (holding a `PlayerState` instance). No `Role` property yet.
    *   Define `GameSession` class with `Id`, `Players` (Dictionary<Guid, Player>), `GamePhase`, `TurnNumber`. Add empty/default collections for `PlayerSeatingOrder`, `RolesInPlay`, `EventDeck`, `DiscardPile`, `ActiveEvents`, `GameHistoryLog`. Add nullable state flags (`SheriffPlayerId`, `Lovers`).
    *   *Test:* Basic instantiation tests for `Player`, `PlayerState`, `GameSession`. Verify default values.

3.  **Implement: Communication & Result Classes**
    *   Define `ModeratorInstruction` class with `InstructionText`, `ExpectedInputType` enum (add `None`, `PlayerSelectionSingle`, `Confirmation`, `VoteCounts`), and potentially `SelectablePlayerIds`.
    *   Define `ModeratorInput` class with `InputTypeProvided`, `SelectedPlayerIds`, `Confirmation`, `VoteResults`.
    *   Define GameError class with Type (ErrorType), Code (GameErrorCode), Message, Context
    *   Define `ProcessResult` class with `IsSuccess`, `ModeratorInstruction?`, `GameError?`, and static factory methods (`Success`, `Failure`).
    *   *Test:* Basic instantiation and property access tests for these communication classes. Test `ProcessResult` factory methods.

4.  **Implement: Base Logging Structure**
    *   Define abstract base class `GameLogEntryBase` with `Timestamp`, `TurnNumber`, `Phase`.
    *   Define a concrete `GameStartedLogEntry` inheriting from `GameLogEntryBase`, adding `InitialRoles`, `InitialPlayers`, `InitialEvents`.
    *   *Test:* Basic instantiation of `GameStartedLogEntry`.

5.  **Implement: Basic `GameService` Structure & Setup**
    *   Define `GameService` class (constructor potentially taking a dependency for storing sessions, like `IDictionary<Guid, GameSession>`).
    *   Implement `StartNewGame(List<string> playerNamesInOrder, List<RoleType> rolesInPlay, ...)`:
        *   Create `GameSession`.
        *   Create `Player` objects for each name, populate `GameSession.Players`.
        *   Populate `GameSession.PlayerSeatingOrder` based on input order.
        *   Store `rolesInPlay` in `GameSession.RolesInPlay`.
        *   Set initial `GamePhase` to `Setup`.
        *   Create and add `GameStartedLogEntry` to `GameHistoryLog`.
        *   Generate a simple initial `ModeratorInstruction` (e.g., "Setup complete. Proceed to Night 1?"). Set `ExpectedInputType` to `Confirmation`. Store this in `GameSession.PendingModeratorInstruction`.
        *   Return `gameId`.
    *   Implement `GetCurrentInstruction(Guid gameId)` to retrieve `PendingModeratorInstruction`.
    *   Implement `GetGameStateView(Guid gameId)`: Add a basic implementation to return a view of the session state (even if just the session object itself initially) to support testing verification.
    *   Implement basic `ProcessModeratorInput(Guid gameId, ModeratorInput input)`:
        *   Find the `GameSession`. Return `GameNotFound` error if not found.
        *   (Minimal initial validation) Check if `input.InputTypeProvided` matches `PendingModeratorInstruction.ExpectedInputType`. Return `InputTypeMismatch` error if not.
        *   *Initially:* If input is the expected Confirmation for setup, transition `GamePhase` to `Night`. Generate placeholder "Night starts" instruction.
        *   Return `ProcessResult.Success(nextInstruction)`.
    *   *Test:*
        *   Integration Test: `StartNewGame` successfully creates a session with correct player count, names, seating order, roles, phase, and initial instruction. Verify `GameStartedLogEntry`.
        *   Integration Test: `GetCurrentInstruction` retrieves the initial instruction.
        *   Integration Test: `ProcessModeratorInput` with correct confirmation advances phase to Night.
        *   Integration Test: ProcessModeratorInput for non-existent game returns error with Code = `GameErrorCode.GameNotFound_SessionNotFound`.
        *   Integration Test: ProcessModeratorInput with wrong input type returns error with Code = `GameErrorCode.InvalidInput_InputTypeMismatch`.

---

**Phase 1: Minimal Game Loop & Core Mechanics**

*Goal: Implement the simplest possible Night -> Day -> Vote cycle with just Werewolves and Villagers.*

1.  **Implement: `IRole` Interface & Simple Roles**
    *   Define `IRole` interface with initial methods: `RoleType`, `GetNightWakeUpOrder()`. Add `RequiresNight1Identification()` (default false).
    *   Implement `SimpleVillagerRole` (implements `IRole`, `RoleType`=SimpleVillager, `GetNightWakeUpOrder`=MaxValue).
    *   Implement `SimpleWerewolfRole` (implements `IRole`, `RoleType`=SimpleWerewolf, `GetNightWakeUpOrder`=e.g., 10). Add `GenerateNightInstructions`, `ProcessNightAction` stubs.
    *   Modify `Player` class to include `Role` (IRole?).
    *   Modify `GameService.StartNewGame`: When creating Players, initialize `Role` to null. (Role assignment will happen later).
    *   *Test:* Unit tests for `GetNightWakeUpOrder` on simple roles. Basic instantiation tests.

2.  **Implement: Night Phase Logic (WW Wakeup & Target)**
    *   Add `NightActionsLog` (temporary, maybe `List<Tuple<Guid, Guid>>` for ActorID, TargetID) to `GameSession` for tracking choices *during* the night phase.
    *   Modify `GameService.ProcessModeratorInput`: When transitioning to `Night`:
        *   Identify players with roles having `GetNightWakeUpOrder < int.MaxValue` (initially just WWs).
        *   Sort them by wake-up order.
        *   Generate instruction for the first role (Werewolves): Prompt for victim selection (`ExpectedInputType.PlayerSelectionSingle`, provide list of living non-WW players).
    *   Implement `SimpleWerewolfRole.GenerateNightInstructions`: Return instruction asking for victim.
    *   Implement `SimpleWerewolfRole.ProcessNightAction`: Takes input, validates target (is alive, not WW - basic checks), adds (actorId, targetId) to `GameSession.NightActionsLog`. Generates instruction indicating choice logged, transitioning phase if last night action. Returns error (e.g., `GameErrorCode.RuleViolation_TargetIsAlly`, `GameErrorCode.RuleViolation_TargetIsSelf`, `GameErrorCode.RuleViolation_TargetIsDead`) if invalid.
    *   Modify `GameService.ProcessModeratorInput` to handle `PlayerSelectionSingle` for the WW action: Call `role.ProcessNightAction`, clear `NightActionsLog` (if transitioning), transition `GamePhase` to `Day_ResolveNight`, generate placeholder "Resolve Night" instruction.
    *   Define `PlayerEliminatedLogEntry` and `WerewolfVictimChoiceLogEntry`.
    *   *Test:*
        *   Integration Test: Start game with 1 WW, 2 V. Assign roles manually for testing. Verify Night phase prompts for WW victim.
        *   Integration Test: Process WW input targeting a `Villager`. Verify `NightActionsLog` updated. Verify phase transitions to `Day_ResolveNight`. Verify `WerewolfVictimChoiceLogEntry` added to `GameHistoryLog`.
        *   Integration Test: Process WW input targeting self or another WW fails (`GameErrorCode.RuleViolation_TargetIsAlly` or `GameErrorCode.RuleViolation_TargetIsSelf`).
        *   Integration Test: Process WW input targeting dead player fails (`GameErrorCode.RuleViolation_TargetIsDead`).

3.  **Implement: Night Resolution Logic (WW Kill)**
    *   Modify `GameService.ProcessModeratorInput`: Add logic block for `GamePhase.Day_ResolveNight`:
        *   Iterate through `NightActionsLog` (just the WW action for now).
        *   Identify the victim.
        *   Update victim `Player.Status` to `Dead`.
        *   Add `PlayerEliminatedLogEntry` (Reason: `WerewolfAttack`) to `GameHistoryLog`.
        *   Clear `NightActionsLog`.
        *   Generate instruction: "Player [VictimName] was eliminated. Reveal role?" (`ExpectedInputType.RoleSelection` or `Confirmation` for now).
        *   Transition `GamePhase` to `Day_Event`.
    *   Define `RoleRevealedLogEntry`. Add `RoleSelection` to `ExpectedInputType` enum. Add `SelectedRoleName` to `ModeratorInput`.
    *   *Test:*
        *   Integration Test: Following previous test, trigger Night Resolution. Verify victim `Status` is `Dead`. Verify `PlayerEliminatedLogEntry` added. Verify instruction asks for role reveal. Verify phase is `Day_Event`.
        *   Integration Test: Process WW input targeting a Villager. Verify NightActionsLog updated. Verify phase transitions to Day_ResolveNight. Verify specific WerewolfVictimChoiceLogEntry added to GameHistoryLog.

4.  **Implement: Role Reveal on Death (Basic)**
    *   Modify `GameService.ProcessModeratorInput`: Add logic for `GamePhase.Day_Event`:
        *   Handle `RoleSelection` input (or Confirmation acting as reveal).
        *   Find the player corresponding to the last elimination.
        *   Instantiate the correct `IRole` based on the input `SelectedRoleName`.
        *   Set `player.Role` and `player.IsRoleRevealed = true`.
        *   Add `RoleRevealedLogEntry` to `GameHistoryLog`.
        *   Generate instruction: "Proceed to Vote?" (`ExpectedInputType.Confirmation`).
        *   Transition `GamePhase` to `Day_Vote`.
    *   *Test:*
        *   Integration Test: Following previous test, process role reveal input (e.g., "SimpleVillager"). Verify Player `Role` is set and `IsRoleRevealed` is true. Verify `RoleRevealedLogEntry`. Verify instruction asks to proceed to vote. Verify phase is `Day_Vote`.

5.  **Implement: Basic Day Vote & Resolution**
    *   Add `VoteResultsCache` (Dictionary<Guid, int>?) to `GameSession`.
    *   Modify `GameService.ProcessModeratorInput`:
        *   Handle `Confirmation` input in `Day_Event` to transition to `Day_Vote`. Generate instruction: "Collect votes. Input counts." (`ExpectedInputType.VoteCounts`). Provide list of living players.
        *   Handle `VoteCounts` input in `Day_Vote`:
            *   Basic validation: Check if `VoteResults` provided. Check if sum matches living players (add `GameErrorCode.InvalidInput_IncorrectVoteSum` error code). Store in `GameSession.VoteResultsCache`.
            *   Transition `GamePhase` to `Day_ResolveVote`. Generate placeholder "Resolve Vote" instruction.
    *   Add logic block for `GamePhase.Day_ResolveVote`:
        *   Retrieve `VoteResultsCache`. Find player(s) with max votes.
        *   Handle tie (for now: no elimination). Handle single max voter:
            *   Update voter `Player.Status` to `Dead`.
            *   Add `PlayerEliminatedLogEntry` (Reason: `DayVote`).
            *   Clear `VoteResultsCache`.
            *   Generate instruction: "Player [VoterName] eliminated. Reveal role?" (`ExpectedInputType.RoleSelection`). Transition back to `Day_Event` (for reveal).
        *   If no elimination (tie): Generate instruction: "Tie vote. Proceed to Night?". Transition to `Night`. Increment `TurnNumber`.
    *   Define `VoteCountsReportedLogEntry`, `VoteResolvedLogEntry`.
    *   *Test:*
        *   Integration Test: Process confirmation to start vote. Verify instruction asks for vote counts.
        *   Integration Test: Process valid `VoteCounts` input. Verify cache updated, phase transitions. Verify `VoteCountsReportedLogEntry`.
        *   Integration Test: Process invalid `VoteCounts` (wrong sum). Verify `GameErrorCode.InvalidInput_IncorrectVoteSum` error.
        *   Integration Test: Trigger vote resolution with single max voter. Verify player status updated, log entry added, instruction asks for role reveal, phase is `Day_Event`. Verify `VoteResolvedLogEntry`.
        *   Integration Test: Trigger vote resolution with a tie. Verify no elimination, instruction asks to proceed to night, phase is `Night`, `TurnNumber` incremented. Verify `VoteResolvedLogEntry`.

6.  **Implement: Basic Victory Condition Checks**
    *   Add internal helper method in `GameService`: `CheckVictoryConditions(GameSession session)`.
        *   *Initial Logic:*
            *   Count living WWs (based on revealed roles or initial `RolesInPlay` count minus revealed non-WWs - needs careful thought based *only* on known info).
            *   Count living non-WWs.
            *   If WW count >= non-WW count -> WW Win.
            *   If WW count == 0 -> Villager Win.
    *   Modify `GameService.ProcessModeratorInput`: Call `CheckVictoryConditions` after Night Resolution (`Day_ResolveNight`), after role reveal (`Day_Event`), and after Vote Resolution (`Day_ResolveVote`).
    *   If victory met: Transition `GamePhase` to `GameOver`. Generate instruction: "[Team] wins!". Add `VictoryConditionMetLogEntry`.
    *   Define `VictoryConditionMetLogEntry`.
    *   *Test:*
        *   Integration Test: Scenario leading to WW parity (1 WW, 1 V left). Verify WW win detected, phase is GameOver, correct instruction/log.
        *   Integration Test: Scenario leading to Villager win (last WW eliminated). Verify Villager win detected, phase is GameOver, correct instruction/log.

---

**Phase 2: Core Villager Roles**

*Goal: Add essential Villager roles with night actions.*

1.  **Implement: Seer Role**
    *   Define `SeerRole` class implementing `IRole`. `RequiresNight1Identification`=true. Wakeup order before WW.
    *   Implement `GenerateNightInstructions`: Prompt for target player.
    *   Implement `ProcessNightAction`: Log target (`SeerViewAttemptLogEntry`). Determine target's actual role (if known/revealed *or* potentially deduce based on game start info - simpler for now: report 'Unknown' if Role is null). Generate *private* instruction for Moderator reporting the role. Transition state.
    *   Modify `GameService` Night logic to handle Seer call order and private instruction.
    *   Modify `StartNewGame` / Setup logic to prompt for Night 1 Seer identification if Seer is in `RolesInPlay`. Add `InitialRoleAssignmentLogEntry`.
    *   Define `SeerViewAttemptLogEntry`, `InitialRoleAssignmentLogEntry`.
    *   *Test:*
        *   Integration Test: Start game with Seer. Verify Night 1 prompt for Seer ID. Process input. Verify `InitialRoleAssignmentLogEntry`.
        *   Integration Test: Night phase. Verify Seer called before WW. Verify prompt for target. Process input. Verify log entry. Verify private instruction shows correct role (if known) or 'Unknown'.

2.  **Implement: Defender Role**
    *   Add `ProtectedPlayerId` (Guid?) and `LastProtectedPlayerId` (Guid?) to `GameSession`.
    *   Define `DefenderRole` implementing `IRole`. `RequiresNight1Identification`=true. Wakeup order (e.g., after Seer, before WW).
    *   Implement `GenerateNightInstructions`: Prompt for target (allow self).
    *   Implement `ProcessNightAction`: Check if target is same as `LastProtectedPlayerId`. If yes, return `GameErrorCode.RuleViolation_DefenderRepeatTarget` error. If valid, set `GameSession.ProtectedPlayerId` to target ID. Log choice (`DefenderProtectionChoiceLogEntry`). Transition state.
    *   Modify `GameService` Night Resolution: Before processing WW kill, check if `victimId == session.ProtectedPlayerId`. If yes, skip elimination. *After* resolution logic, update `LastProtectedPlayerId = ProtectedPlayerId`, then clear `ProtectedPlayerId`.
    *   Define `DefenderProtectionChoiceLogEntry`. Add `DefenderRepeatTarget` error code.
    *   *Test:*
        *   Integration Test: Night 1 identification.
        *   Integration Test: Defender protects V1. WW targets V1. Verify V1 survives. Verify `ProtectedPlayerId` set/cleared and `LastProtectedPlayerId` updated. Log entry created.
        *   Integration Test: Defender protects V1 Turn 1. Attempts to protect V1 Turn 2. Verify `GameErrorCode.RuleViolation_DefenderRepeatTarget` error.
        *   Integration Test: Defender protects self. WW targets Defender. Verify Defender survives.

3.  **Implement: Witch Role**
    *   Add `WitchPotionType` enum (Healing, Poison). Add `PotionsUsed` (WitchPotionType Flags Enum?) to `PlayerState`. Default None.
    *   Define `WitchRole` implementing `IRole`. `RequiresNight1Identification`=true. Wakeup order after WWs.
    *   Implement `GenerateNightInstructions`: Needs WW victim ID passed in. Show victim. Prompt for action: Heal Victim? Poison Whom? (provide targets excluding self/victim if applicable). Options depend on `PotionsUsed`.
    *   Implement `ProcessNightAction`: Validate potion use against `PotionsUsed`. If valid, log action (`WitchPotionUseAttemptLogEntry`), update `PotionsUsed`, store intended action (e.g., add to `NightActionsLog` with special type). Transition state. Return `GameErrorCode.RuleViolation_WitchPotionAlreadyUsed` error if invalid.
    *   Modify `GameService` Night Resolution:
        *   After determining initial WW victim, check for Witch Heal action on victim. If present, negate WW kill.
        *   Process Witch Poison action: mark target for elimination (`Reason: WitchPoison`).
    *   Define `WitchPotionUseAttemptLogEntry`. Add `WitchPotionAlreadyUsed` error code.
    *   *Test:*
        *   Integration Test: Night 1 identification.
        *   Integration Test: Witch uses Heal on WW victim. Verify victim survives, PotionUsed updated, log entry.
        *   Integration Test: Witch uses Poison on V2. Verify V2 eliminated, PotionUsed updated, log entry.
        *   Integration Test: Witch uses Heal and Poison in same night. Verify both work, PotionUsed updated.
        *   Integration Test: Witch attempts to use Heal twice. Verify `GameErrorCode.RuleViolation_WitchPotionAlreadyUsed` error.
        *   Integration Test: Witch attempts to use Poison twice. Verify error.

---

**Phase 3: Core Special Roles & State Management**

*Goal: Add roles with significant impact on game state and rules (Lovers, Hunter, Elder, Sheriff).*

1.  **Implement: Cupid & Lovers**
    *   Define `CupidRole` implementing `IRole`. `RequiresNight1Identification`=true. Wakeup order very early (Night 1 only).
    *   Implement `GenerateNightInstructions`: Prompt for 2 players (`ExpectedInputType.PlayerSelectionMultiple`, Count=2).
    *   Implement `ProcessNightAction`: Validate 2 players selected. Update `player.State.IsInLove`, `player.State.LoverId` for both selected players. Set `GameSession.Lovers` tuple. Log `CupidsLoversChoiceLogEntry`.
    *   Modify `GameService` Elimination Logic (Night Res & Vote Res): If an eliminated player `IsInLove`, find their `LoverId` and also eliminate that player (`Reason: LoversHeartbreak`). Handle potential cascades carefully (e.g., if second Lover is Hunter). Add `LoversHeartbreak` reason.
    *   Modify `GameService` Vote Input Validation: Add check: If voter `IsInLove`, ensure their vote target is not `LoverId`. Return `GameErrorCode.RuleViolation_LoverVotingAgainstLover` error.
    *   Modify `GameService` Victory Check: Add check for Lovers Win condition (only 2 survivors, are Lovers, opposing teams based on *known/revealed* roles or deduced alignment).
    *   Define `CupidsLoversChoiceLogEntry`. Add `LoverVotingAgainstLover` error code.
    *   *Test:*
        *   Integration Test: Night 1 Cupid action. Verify Player states and `GameSession.Lovers` updated. Log entry.
        *   Integration Test: Lover A killed by WW. Verify Lover B also dies (LoversHeartbreak).
        *   Integration Test: Lover A eliminated by vote. Verify Lover B also dies.
        *   Integration Test: Lover A tries to vote for Lover B. Verify `GameErrorCode.RuleViolation_LoverVotingAgainstLover` error.
        *   Integration Test: Scenario ending with V Lover and WW Lover as only survivors. Verify Lovers Win.

2.  **Implement: Hunter Role**
    *   Define `HunterRole` implementing `IRole`. No night action.
    *   Modify `GameService` Elimination Logic (Night Res & Vote Res): *After* a player is marked `Dead` and role revealed, check if `player.Role` is `HunterRole`.
    *   If Hunter, generate instruction: "Hunter [Name] eliminated. Choose target for final shot." (`ExpectedInputType.PlayerSelectionSingle`, provide living targets). Add state to track pending Hunter shot.
    *   Handle `PlayerSelectionSingle` input when pending Hunter shot: Eliminate target (`Reason: HunterShot`). Log `PlayerEliminatedLogEntry`. Trigger role reveal for the *new* victim (potentially cascading). Clear pending Hunter shot state.
    *   Add `HunterShot` reason.
    *   *Test:*
        *   Integration Test: Hunter killed by WW. Verify instruction asks for target. Process target input. Verify target eliminated (HunterShot), logged, and target role reveal prompted.
        *   Integration Test: Hunter killed by Vote. Verify same sequence as above.
        *   Integration Test: Cascade - Hunter Lover dies, other Lover dies, Hunter shoots. Verify sequence.

3.  **Implement: Elder Role & State**
    *   Add `TimesAttackedByWerewolves` (int) to `PlayerState`.
    *   Define `ElderRole` implementing `IRole`. `RequiresNight1Identification`=true? (Maybe not essential initially). No standard night action.
    *   Modify `GameService` Night Resolution (WW kill): If target is Elder, increment `TimesAttackedByWerewolves`. If count is now 1, *do not* eliminate the Elder. Log `ElderSurvivedAttackLogEntry`. If count > 1, eliminate normally.
    *   Modify `GameService` Elimination Logic (Vote Res, Witch Poison, Hunter Shot): If eliminated player is Elder:
        *   Log `VillagerPowersLostLogEntry`.
        *   Add a flag/state to `GameSession` indicating powers are lost.
    *   Modify `GameService` Night Logic: Before generating instructions for Villager roles (Seer, Defender, Witch, Fox etc.), check the "powers lost" flag. If set, skip their turn.
    *   Define `ElderSurvivedAttackLogEntry`, `VillagerPowersLostLogEntry`.
    *   *Test:*
        *   Integration Test: Elder attacked by WW first time. Verify survives, counter incremented, log entry.
        *   Integration Test: Elder attacked by WW second time. Verify eliminated.
        *   Integration Test: Elder eliminated by vote. Verify eliminated, log entry added, flag set. Subsequent night: Verify Seer/Defender/Witch skipped.
        *   Integration Test: Elder eliminated by Witch/Hunter. Verify powers lost.

4.  **Implement: Sheriff Role & Mechanics**
    *   Add `IsSheriff` (bool) to `PlayerState`. `SheriffPlayerId` already in `GameSession`.
    *   Modify `GameService` Vote Resolution: When calculating max votes, check if voter `IsSheriff`. If true, count vote as 2 (or use `VoteMultiplier`).
    *   Modify `GameService` Elimination Logic (Night Res & Vote Res): If eliminated player `IsSheriff`:
        *   Generate instruction: "Sheriff [Name] eliminated. Choose successor." (`ExpectedInputType.SuccessorSelection` or `PlayerSelectionSingle`). Provide living targets. Add state to track pending successor choice.
    *   Handle SuccessorSelection input: Update `IsSheriff` (false for old, true for new). Update `GameSession.SheriffPlayerId`. Log `SheriffAppointedLogEntry`. Clear pending state.
    *   Add initial Sheriff Election step (e.g., after setup/Night 1). Prompt for vote, process result (simple majority for now), set initial Sheriff state. Log `SheriffAppointedLogEntry`.
    *   Define `SheriffAppointedLogEntry`. Add `SuccessorSelection` to `ExpectedInputType`.
    *   *Test:*
        *   Integration Test: Initial Sheriff Election. Verify state updated, logged.
        *   Integration Test: Sheriff votes. Verify vote counts double in resolution.
        *   Integration Test: Sheriff killed by WW. Verify successor prompt. Process successor choice. Verify state updated, logged.
        *   Integration Test: Sheriff killed by Vote. Verify same sequence.

5.  **Implement: Little Girl Role & Mechanics**
    * Modify GameService Night Logic (WW Action): Include logic for the Moderator to optionally provide input indicating the Little Girl was caught spying (e.g., a specific confirmation or secondary input after WW target selection).
    * Modify GameService Night Resolution: If the "Little Girl Caught" input was received for the night:
        * Identify the Little Girl player.
        * Override the Werewolves' chosen victim. The Little Girl becomes the only victim of the Werewolf attack for that night.
        * Mark Little Girl Player.Status to Dead (Reason: LittleGirlCaught).
        * Log LittleGirlCaughtLogEntry.
    * Define LittleGirlCaughtLogEntry. Add LittleGirlCaught reason.
    * *Test:*
        * Integration Test: WWs target V1. Moderator inputs Little Girl (LG) was caught. Verify LG is eliminated (LittleGirlCaught), V1 survives (relative to WW attack), log entry added.

---

**Phase 4: Positional Logic & Roles**

*Goal: Implement seating order mechanics and roles that depend on neighbors.*

1.  **Implement: Positional Helper Methods**
    *   Implement helper methods in `GameService` (or a dedicated utility class):
        *   `GetLeftNeighbor(Guid playerId, GameSession session, bool skipDead = true)`
        *   `GetRightNeighbor(Guid playerId, GameSession session, bool skipDead = true)`
        *   `GetAdjacentLivingNeighbors(Guid playerId, GameSession session)`
    *   These methods use `session.PlayerSeatingOrder` and `session.Players[id].Status`. Handle wrapping around the list ends.
    *   *Test:* Unit Test these helpers extensively with various seating orders, player statuses, and edge cases (first/last player, small/large lists, all dead neighbors).

2.  **Implement: Fox Role**
    *   Add `HasLostFoxPower` (bool) to `PlayerState`.
    *   Define `FoxRole` implementing `IRole`. `RequiresNight1Identification`=true. Wakeup order (e.g., after Seer).
    *   Implement `GenerateNightInstructions`: Prompt for target player (cannot be self).
    *   Implement `ProcessNightAction`: Check `HasLostFoxPower`. If true, return `GameErrorCode.RuleViolation_PowerLostOrUnavailable` error. If valid:
        *   Use positional helpers to find target and living neighbors.
        *   Check if any of the three have a *known/revealed* Werewolf role (or deduced WW alignment).
        *   Generate private instruction: "Result: Yes/No".
        *   If result is "No", set `player.State.HasLostFoxPower = true`.
        *   Log `FoxCheckPerformedLogEntry` (including target, neighbors, result, power lost state). Transition state.
    *   Define `FoxCheckPerformedLogEntry`. Add `PowerLostOrUnavailable` error code.
    *   *Test:*
        *   Integration Test: Night 1 identification.
        *   Integration Test: Fox checks group including a known/revealed WW. Verify "Yes" result, power NOT lost, log entry.
        *   Integration Test: Fox checks group with no known/revealed WWs. Verify "No" result, power IS lost, log entry.
        *   Integration Test: Fox attempts check after power lost. Verify `GameErrorCode.RuleViolation_PowerLostOrUnavailable` error.

3.  **Implement: Bear Tamer Role**
    *   Define `BearTamerRole` implementing `IRole`. `RequiresNight1Identification`=true? (Maybe not essential). No night action.
    *   Modify `GameService` Day Phase (`Day_Event`, after victim reveals):
        *   Check if Bear Tamer is alive and identified (`Role` is set).
        *   If yes, use positional helpers to get adjacent living neighbors.
        *   Check if either neighbor has a *known/revealed* Werewolf role. OR check if the Bear Tamer is Infected (`State.IsInfected`).
        *   If condition met, add reminder to `ModeratorInstruction`: "(Bear Tamer Growls)". Log `BearTamerGrowlOccurredLogEntry`.
    *   Define `BearTamerGrowlOccurredLogEntry`.
    *   *Test:*
        *   Integration Test: Bear Tamer alive next to revealed WW. Verify growl instruction generated, log entry added.
        *   Integration Test: Bear Tamer alive next to unrevealed WW or Villager. Verify no growl.
        *   Integration Test: Bear Tamer alive, neighbor is dead WW. Verify no growl.
        *   Integration Test: Bear Tamer is infected, neighbours are Villagers. Verify growl instruction generated.

4.  **Implement: Knight Role**
    *   Add `PendingKnightCurseTarget` (Guid?) to `GameSession`.
    *   Define `KnightWithRustySwordRole` implementing `IRole`. No night action.
    *   Modify `GameService` Night Resolution:
        *   *Before* processing WW kill: Check if `PendingKnightCurseTarget` is set. If yes, eliminate that player (`Reason: KnightCurse`). Log `PlayerEliminatedLogEntry`. Clear `PendingKnightCurseTarget`.
        *   *After* processing WW kill: If the victim was the Knight (`Role` is Knight), use positional helpers to find the first living Werewolf (known/revealed role) to the Knight's left. Set `PendingKnightCurseTarget` to that WW's ID. Log `KnightCurseActivatedLogEntry`.
    *   Define `KnightCurseActivatedLogEntry`. Add `KnightCurse` reason.
    *   *Test:*
        *   Integration Test: Knight killed by WWs. Verify `PendingKnightCurseTarget` set to correct WW, log entry added.
        *   Integration Test: Next night resolution. Verify `PendingKnightCurseTarget` player eliminated, log entry added, flag cleared.
        *   Integration Test: Knight killed by Vote/Witch/Hunter. Verify curse NOT activated.

---

**Phase 5: Ambiguous & Transforming Roles**

*Goal: Implement roles that can change alignment or powers.*

1.  **Implement: Thief Role**
    *   Define `ThiefRole` implementing `IRole`. `RequiresNight1Identification`=true. Night 1 only, very early wake order.
    *   Modify `StartNewGame`: If Thief in `RolesInPlay`, ensure 2 extra Villager roles are conceptualized/tracked.
    *   Implement `GenerateNightInstructions`: Needs the 2 available roles passed in. Prompt for choice (`ExpectedInputType.RoleSelection`, provide the 2 role names).
    *   Implement `ProcessNightAction`: Validate choice. Update `player.Role` to the chosen `IRole` instance. Log `ThiefRoleChoiceLogEntry` (Thief ID, chosen role, discarded role). Remove Thief/Discarded role from `RolesInPlay` conceptually, add chosen role.
    *   *Test:*
        *   Integration Test: Night 1 Thief action. Verify prompt shows 2 roles. Process choice. Verify Player `Role` updated, log entry created. Verify player acts as new role subsequently.
        *   Integration Test: Thief offered 2 WW roles. Verify must choose one, becomes WW.

2.  **Implement: Wild Child Role**
    *   Add `WildChildModelId` (Guid?) to `PlayerState`.
    *   Define `WildChildRole` implementing `IRole`. `RequiresNight1Identification`=true. Night 1 only.
    *   Implement `GenerateNightInstructions`: Prompt for model selection (`ExpectedInputType.PlayerSelectionSingle`).
    *   Implement `ProcessNightAction`: Set `player.State.WildChildModelId`. Log `WildChildModelChoiceLogEntry`.
    *   Modify `GameService` Elimination Logic (Night Res & Vote Res): If eliminated player's ID matches any living Wild Child's `WildChildModelId`:
        *   Log `WildChildTransformedLogEntry`.
        *   Update Wild Child's internal state/alignment to Werewolf (e.g., add to a WW group list, or change effective team flag).
    *   Modify `GameService` Night Logic (WW wakeup): Include transformed Wild Children in the WW group.
    *   Modify `GameService` Victory Check: Consider transformed Wild Children as Werewolves.
    *   Define `WildChildModelChoiceLogEntry`, `WildChildTransformedLogEntry`.
    *   *Test:*
        *   Integration Test: Night 1 Wild Child chooses model. Verify state updated, log entry.
        *   Integration Test: Model killed by WW. Verify WC transforms (log entry), wakes with WWs next night, counts as WW for victory.
        *   Integration Test: Model killed by Vote. Verify WC transforms.

3.  **Implement: Wolf Hound Role**
    *   Add `WolfHoundChoice` (Team?) to `PlayerState`.
    *   Define `WolfHoundRole` implementing `IRole`. `RequiresNight1Identification`=true. Night 1 only.
    *   Implement `GenerateNightInstructions`: Prompt for choice (Villager or Werewolf) (`ExpectedInputType.OptionSelection`).
    *   Implement `ProcessNightAction`: Set `player.State.WolfHoundChoice`. Log `WolfHoundAlignmentChoiceLogEntry`.
    *   Modify `GameService` Night Logic (WW wakeup): If Wolf Hound chose Werewolf, include them.
    *   Modify `GameService` Victory Check: Determine Wolf Hound's alignment based on `WolfHoundChoice` for counting teams.
    *   Define `WolfHoundAlignmentChoiceLogEntry`. Add `OptionSelection` to `ExpectedInputType`.
    *   *Test:*
        *   Integration Test: Night 1 Wolf Hound chooses WW. Verify state updated, log entry. Verify wakes with WWs, counts as WW for victory.
        *   Integration Test: Night 1 Wolf Hound chooses Villager. Verify state updated, log entry. Verify does NOT wake with WWs, counts as Villager for victory.

4.  **Implement: Devoted Servant Role & State Reset**
    *   Add role-specific usage flags to `PlayerState`: `HasUsedAccursedInfection`, `HasLostFoxPower`, `HasUsedStutteringJudgePower`. Reset `TimesAttackedByWerewolves`. Ensure `PotionsUsed` exists.
    *   Define `DevotedServantRole` implementing `IRole`. No night action.
    *   Modify `GameService` Elimination Logic (Vote Res): *Before* revealing role/triggering death effects (like Hunter/Sheriff/Elder), check if Devoted Servant is alive and not a Lover. If yes, generate instruction: "[EliminatedName] eliminated. Devoted Servant, reveal to swap?" (`ExpectedInputType.Confirmation`). Add pending state.
    *   Handle Confirmation input for Servant swap:
        *   If Yes: Log `DevotedServantSwapExecutedLogEntry`. Prevent original victim's elimination/role reveal/triggers. Set Servant's `Role` to victim's *original* role (instance). Set Servant's `IsRoleRevealed`=false. **Crucially:** Reset role-specific flags (`HasUsed...`, `PotionsUsed`, `TimesAttacked...`) on the Servant's `PlayerState` to defaults. Mark original victim as `Dead` but maybe with a special status/log indicating swap occurred.
        *   If No: Proceed with normal elimination, reveal, triggers for the original victim.
    *   Define `DevotedServantSwapExecutedLogEntry`.
    *   *Test:*
        *   Integration Test: Witch (used Heal) eliminated by vote. Servant swaps. Verify Servant becomes Witch, `PotionsUsed` is reset, log entry. Verify original Witch marked dead (swapped). Verify no Sheriff pass triggered if Witch was Sheriff.
        *   Integration Test: AWF (used infection) eliminated. Servant swaps. Verify Servant becomes AWF, `HasUsedAccursedInfection` is reset.
        *   Integration Test: Fox (lost power) eliminated. Servant swaps. Verify Servant becomes Fox, `HasLostFoxPower` is reset.
        *   Integration Test: Judge (used power) eliminated. Servant swaps. Verify Servant becomes Judge, `HasUsedStutteringJudgePower` is reset.
        *   Integration Test: Elder (attacked once) eliminated. Servant swaps. Verify Servant becomes Elder, `TimesAttackedByWerewolves` is reset.
        *   Integration Test: Servant is Lover. Verify swap attempt fails or is not offered.

5.  **Implement: Actor Role**
    *   Define `ActorRole` implementing `IRole`. `RequiresNight1Identification`=true? (To know who it is). Wakeup order early.
    *   Modify `GameService` Setup: Select 3 non-WW roles for the Actor pool. Store these (e.g., in `GameSession` or `PlayerState`). Log `ActorRolePoolSetLogEntry`.
    *   Implement `GenerateNightInstructions`: Show available roles from pool. Prompt for choice (`ExpectedInputType.RoleSelection`).
    *   Implement `ProcessNightAction`: Record chosen role for the night (e.g., in temp state). Log `ActorRoleChoiceLogEntry`.
    *   Modify `GameService` Night Logic: After Actor chooses, if the chosen role has a night action (e.g., Seer), call the Actor *again* at the appropriate time, passing the chosen role's logic. Process the action as if the Actor *is* that role. Mark the chosen role as used/removed from the pool for next night.
    *   Define `ActorRolePoolSetLogEntry`, `ActorRoleChoiceLogEntry`.
    *   *Test:*
        *   Integration Test: Setup with Actor. Verify pool logged.
        *   Integration Test: Actor chooses Seer. Verify log. Verify Actor called again as Seer, performs Seer action. Verify Seer removed from pool for next night.
        *   Integration Test: Actor chooses Defender. Verify log. Verify Actor called as Defender, performs action.

---

**Phase 6: Loner Roles & Complex Win Conditions**

*Goal: Implement solo roles with unique win conditions.*

1.  **Implement: Piper Role**
    *   Add `IsCharmed` (bool) to `PlayerState`. Add `CharmedPlayerIds` (HashSet<Guid>) to `GameSession`.
    *   Define `PiperRole` implementing `IRole`. `RequiresNight1Identification`=true. Wakeup order late.
    *   Implement `GenerateNightInstructions`: Prompt for 2 targets (`PlayerSelectionMultiple`, Count=2, cannot target self).
    *   Implement `ProcessNightAction`: Update `IsCharmed` for targets. Add IDs to `CharmedPlayerIds`. Log `PiperCharmChoiceLogEntry`.
    *   Modify `GameService` Victory Check: Add check for Piper Win: If Piper alive and all other *living* players have `IsCharmed` == true.
    *   Define `PiperCharmChoiceLogEntry`.
    *   *Test:*
        *   Integration Test: Night 1 Piper ID.
        *   Integration Test: Piper charms V1, V2. Verify states updated, log entry.
        *   Integration Test: Piper attempts to charm self. Verify error.
        *   Integration Test: Scenario ending with Piper and 2 charmed survivors. Verify Piper Win detected.

2.  **Implement: White Werewolf Role**
    *   Define `WhiteWerewolfRole` implementing `IRole`. Part of WW group. Wakeup order: Wakes with WWs, then wakes *again* alone on even nights.
    *   Modify `GameService` Night Logic:
        *   Include WhiteWW in standard WW wakeup.
        *   On even `TurnNumber` nights, after standard WW action, call WhiteWW again.
    *   Implement WhiteWW `GenerateNightInstructions` (Solo): Prompt for target Werewolf (`PlayerSelectionSingle`, provide list of living WWs - revealed or deduced).
    *   Implement WhiteWW `ProcessNightAction` (Solo): Validate target is WW. Log `WhiteWerewolfVictimChoiceLogEntry`. Add action to `NightActionsLog`.
    *   Modify `GameService` Night Resolution: Process WhiteWW solo kill (`Reason: WhiteWerewolfAttack`).
    *   Modify `GameService` Victory Check: Add check for WhiteWW Win: If WhiteWW is the only survivor.
    *   Define `WhiteWerewolfVictimChoiceLogEntry`. Add `WhiteWerewolfAttack` reason.
    *   *Test:*
        *   Integration Test: WhiteWW wakes with WWs.
        *   Integration Test: Turn 2 Night: WhiteWW called solo. Prompts for WW target. Processes kill. Verify log. Verify WW target eliminated in resolution.
        *   Integration Test: Turn 1/3 Night: WhiteWW NOT called solo.
        *   Integration Test: WhiteWW attempts to kill Villager solo. Verify error.
        *   Integration Test: Scenario ending with only WhiteWW alive. Verify WhiteWW Win.

3.  **Implement: Angel Role**
    *   Define `AngelRole` implementing `IRole`. No night action.
    *   Modify `GameService` Victory Check: Add check for Angel Win: If Angel (`Role` is Angel) is eliminated (Vote or Night) AND `TurnNumber <= 2` (needs precise definition - before Day 2 vote completes?).
    *   *Test:*
        *   Integration Test: Angel eliminated by Vote on Day 1 (`TurnNumber`=1). Verify Angel Win.
        *   Integration Test: Angel eliminated by WW on Night 1 (revealed Day 1, `TurnNumber`=1). Verify Angel Win.
        *   Integration Test: Angel eliminated by WW on Night 2 (revealed Day 2, `TurnNumber`=2). Verify Angel Win.
        *   Integration Test: Angel eliminated by Vote on Day 2 (`TurnNumber`=2). Verify Angel does NOT win.

4.  **Implement: Prejudiced Manipulator Role**
    *   Add `PrejudicedManipulatorGroups` (Dictionary<Guid, int>?) to `GameSession`.
    *   Define `PrejudicedManipulatorRole` implementing `IRole`. No night action.
    *   Modify `GameService` Setup: If PM in `RolesInPlay`, prompt Moderator for PM player ID and group assignments for all players. Store in `PrejudicedManipulatorGroups`. Log `PrejudicedManipulatorGroupsDefinedLogEntry`.
    *   Modify `GameService` Victory Check: Add check for PM Win: If PM is alive AND all living players belong to the PM's group (check `PrejudicedManipulatorGroups`).
    *   Define `PrejudicedManipulatorGroupsDefinedLogEntry`.
    *   *Test:*
        *   Integration Test: Setup with PM. Verify prompts for groups. Process input. Verify state updated, log entry.
        *   Integration Test: Scenario ending with PM and only members of their group alive. Verify PM Win detected.

---

**Phase 7: New Moon Event Framework**

*Goal: Build the infrastructure to handle event cards.*

1.  **Implement: Event Card Base & State**
    *   Define `EventTiming`, `EventDuration` enums.
    *   Define abstract `EventCard` class with `Id`, `Name`, `Description`, `Timing`, `Duration`. Add virtual methods: `ApplyEffect`, `ModifyNightActionResolution`, `ModifyDayVoteProcess`, etc. (return base/no-op).
    *   Define `ActiveEventState` class with `EventId`, `CardReference`, `TurnsRemaining`, `StateData`.
    *   Add `EventDeck` (List<EventCard>), `DiscardPile` (List<EventCard>), `ActiveEvents` (List<ActiveEventState>) to `GameSession`.
    *   *Test:* Basic instantiation tests.

2.  **Implement: Event Drawing & Activation**
    *   Modify `GameService` Day Phase (`Day_Event`, after role reveals): If Events are enabled and `TurnNumber > 1` (or as per rules):
        *   Check if `EventDeck` has cards.
        *   Generate instruction: "Draw Event Card. Input card ID/Name." (`ExpectedInputType.OptionSelection`, provide card names from deck?).
    *   Handle Event Selection input:
        *   Find `EventCard` in `EventDeck`. Move to `DiscardPile`. Log `EventCardDrawnLogEntry`.
        *   Call `drawnCard.ApplyEffect(session, this)`. This method updates `GameSession` state directly or adds an entry to `ActiveEvents`. It returns the next `ModeratorInstruction`.
    *   Define `EventCardDrawnLogEntry`.
    *   *Test:*
        *   Integration Test: Start game with event deck. Proceed to Day 2. Verify prompt to draw event. Process input (e.g., select a placeholder event). Verify card moved to discard, log entry added, and a basic instruction returned from `ApplyEffect`.

3.  **Implement: Event Hooks in `GameService`**
    *   In relevant `GameService` logic points (Night Resolution, Start of Vote, Start of Debate, Instruction Generation, Victory Check):
        *   Iterate through `session.ActiveEvents`.
        *   Call the corresponding `Modify...` method on the `eventState.CardReference`, passing the current state/data.
        *   Use the return value to alter the standard game flow (e.g., replace standard vote logic, modify night victims, change instruction text).
    *   *Test:* (Conceptual for now) Verify the hooks are called if an event is active (can test with placeholder events later).

4.  **Implement: Event Expiration**
    *   In `GameService` (e.g., end of Day Resolution or start of Night):
        *   Iterate through `ActiveEvents`.
        *   Decrement `TurnsRemaining` if it's not null.
        *   Remove events where `TurnsRemaining` reaches 0.
    *   *Test:* Integration test: Activate a temporary placeholder event. Run a game cycle. Verify `TurnsRemaining` decremented. Run another cycle. Verify event removed.

---

**Phase 8: Implement Specific Events (Grouped)**

*Goal: Implement the logic for each event card, testing its specific impact.*

*(Structure for each event:)*
*   **Implement: [EventName]Event class** inheriting `EventCard`. Implement `ApplyEffect` and override relevant `Modify...` methods. Define specific `StateData` keys if needed.
*   **Test: [EventName]Event.** Integration test scenario: Draw the event. Verify `ApplyEffect` sets up correct state/instruction. Trigger the relevant game phase modified by the event. Verify the `Modify...` logic works correctly (e.g., Backfire changes kill outcome, Nightmare changes vote type, Eclipse adds instruction). Verify expiration if temporary.

1.  **Implement & Test: Night Modifiers** (FullMoonRising, Backfire, Specter, Miracle)
2.  **Implement & Test: Vote Modifiers** (Nightmare, Influences, Great Distrust, Enthusiasm, Dissatisfaction)
3.  **Implement & Test: State Changers / Role Assigner** (Executioner, Double Agent, Little Rascal, Punishment)
4.  **Implement & Test: Rule Modifiers** (Somnambulism, NotMeNorWolf, Eclipse, Good Manners, Burial)
5.  **Implement & Test: Information Events** (Spiritualism - requires Gypsy role implemented first)

*(Implement Gypsy and Town Crier roles alongside Spiritualism and relevant events requiring them).*

---

**Phase 9: Refinement & Complex Interactions**

*Goal: Polish, test edge cases, and ensure robustness.*

1.  **Implement & Test: Remaining Roles** 
    *   Implement roles mostly affecting setup or having minor logic:
    *   `Villager-Villager`: Primarily affects Seer results or role reveal logic (proves innocence). No special actions.
    *   `Two Sisters` / `Three Brothers`: Add Night 1 wake-up logic (after `Cupid`/`Lovers`). Log recognition event (`SiblingRecognitionLogEntry`). No subsequent special actions required by the app core.
    *   `Stuttering Judge`: Add `HasUsedStutteringJudgePower` to `PlayerState`. Implement Night 1 signal mechanism (log confirmation). Modify `Day Vote` phase to accept `Judge` signal input (if power unused). If signaled, set flag, log `StutteringJudgeSignaledSecondVoteLogEntry`, and modify vote resolution to loop back for a second vote after the first resolves.
    *   `Scapegoat`: Add tie-breaking logic to `Vote Resolution`. If tie and `Scapegoat` alive, eliminate `Scapegoat` (Reason: Scapegoat). Prompt `Scapegoat` (via Moderator) for next day's voter restrictions (ExpectedInputType.PlayerSelectionMultiple?). Store restrictions, log ScapegoatVotingRestrictionsSetLogEntry. Apply restrictions during next day's vote validation/setup.
    *   Define `SiblingRecognitionLogEntry`, `StutteringJudgeSignaledSecondVoteLogEntry`, `ScapegoatVotingRestrictionsSetLogEntry`. Add `Scapegoat` reason.

    *   **Test**: Corresponding tests from tests.md for `Judge` signal/second vote, `Scapegoat` tie break/voter choice. Test `Sibling` log entry.
2.  **Implement & Test: Complex Interactions**
    *   Scenarios with multiple events active.
    *   Scenarios where role abilities interact with event effects (e.g., Defender protecting during Backfire, Witch healing during Miracle).
    *   Scenarios involving complex cascades (Lover + Hunter + Devoted Servant possibility?).
3.  **Refine: Error Handling & Instructions**
    *   Review all error codes and messages for clarity. Add context where helpful.
    *   Review all `ModeratorInstruction` text for clarity and accuracy based on game state.
4.  **Refine: Victory Condition Logic**
    *   Thoroughly review victory condition checks, especially regarding deduced roles vs revealed roles and ambiguous role alignments (Thief, WildChild, WolfHound, Infected). Ensure checks rely *only* on moderator-knowable state.
5.  **Code Review & Final Testing:** Perform holistic code review and run the full test suite. Add any missing edge case tests identified.