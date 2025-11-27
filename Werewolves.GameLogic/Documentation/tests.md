# Integration Test Plan

This document defines the high-level integration test cases for the Werewolves moderator helper application. The initial scope focuses on a **simple game** consisting of:
- **Werewolves** (SimpleWerewolf)
- **Seer**
- **Simple Villagers**

Secondary roles (Sheriff, Lovers, etc.) and advanced roles are excluded from this initial test suite.

---

## Test Categories Overview

| Category | Description | Test File |
|----------|-------------|-----------|
| Game Lifecycle | Game creation, setup, and phase cycle completion | `GameLifecycleTests.cs` |
| Phase Transitions | State machine transitions and cache behavior | `PhaseTransitionTests.cs` |
| Night Actions | Werewolf attacks, Seer investigations, logging | `NightActionTests.cs` |
| Dawn Resolution | Victim calculation, eliminations, role reveals | `DawnResolutionTests.cs` |
| Day Voting | Vote outcomes, ties, elimination flow | `DayVotingTests.cs` |
| Victory Conditions | Both win conditions at correct moments | `VictoryConditionTests.cs` |
| Event Sourcing | Log integrity and state reconstruction | `EventSourcingTests.cs` |

---

## 1. Game Lifecycle Tests

Tests covering the complete game creation and lifecycle flow.

### 1.1 Game Creation
- **GL-001**: `StartNewGame_WithValidRolesAndPlayers_ReturnsStartGameConfirmationInstruction`
  - Given: Valid player names and matching roles (3 players: 1 WW, 1 Seer, 1 Villager)
  - When: `GameService.StartNewGame()` is called
  - Then: Returns `StartGameConfirmationInstruction` with valid `GameId`

- **GL-002**: `StartNewGame_WithEmptyRoles_ThrowsArgumentException`
  - Given: Valid player names but empty roles list
  - When: `GameService.StartNewGame()` is called
  - Then: Throws `ArgumentException`

- **GL-003**: `StartNewGame_CreatesSessionWithCorrectPlayerCount`
  - Given: 5 players with matching roles
  - When: Game is started and state retrieved
  - Then: Session contains exactly 5 players in correct seating order

### 1.2 Game Start Confirmation
- **GL-010**: `ConfirmGameStart_TransitionsToNightPhase`
  - Given: Game started, pending `StartGameConfirmationInstruction`
  - When: Moderator confirms and triggers Night phase execution
  - Then: Night phase begins, sub-phase is `Start`

- **GL-011**: `ConfirmGameStart_SetsCorrectTurnNumber`
  - Given: Game start confirmed
  - When: Night phase begins
  - Then: `TurnNumber` is 1

### 1.3 Full Game Cycle
- **GL-020**: `CompleteGameCycle_NightToDawnToDay_TransitionsCorrectly`
  - Given: Game in Night phase with simple setup (1 WW, 1 Seer, 2 Villagers)
  - When: All night actions completed
  - Then: Phase transitions Night → Dawn → Day in sequence

---

## 2. Phase Transition Tests

Tests validating the state machine transitions and cache behavior.

### 2.1 Valid Transitions
- **PT-001**: `NewGame_StartsInNightPhase`
  - Given: Game created and start confirmed
  - When: Game begins
  - Then: Initial phase is `Night.Start`

- **PT-002**: `NightStart_ToDawnCalculateVictims_IsValidTransition`
  - Given: Game in `Night.Start`, all actions complete
  - When: Night loop finishes
  - Then: Transitions to `Dawn.CalculateVictims`

- **PT-003**: `DawnFinalize_ToDayDebate_IsValidTransition`
  - Given: Game in `Dawn.Finalize`
  - When: Dawn hooks complete
  - Then: Transitions to `Day.Debate`

- **PT-004**: `DayFinalize_ToNightStart_IsValidTransition`
  - Given: Game in `Day.Finalize`, no victory condition met
  - When: Day completes
  - Then: Transitions to `Night.Start`, `TurnNumber` increments

### 2.2 Phase Cache Behavior
- **PT-010**: `MainPhaseTransition_ClearsSubPhaseCache`
  - Given: Game in `Dawn.AnnounceVictims` with cached sub-phase data
  - When: Transition to `Day` phase
  - Then: Sub-phase cache is cleared

- **PT-011**: `SubPhaseTransition_ClearsStageData`
  - Given: Game in `Day.NormalVoting` with stage data
  - When: Transition to `Day.ProcessVoteOutcome`
  - Then: Previous stage data is cleared

---

## 3. Night Action Tests

Tests for night phase role actions and logging.

### 3.1 Werewolf Actions
- **NA-001**: `Werewolves_WakeAndSelectVictim_LogsNightAction`
  - Given: Night phase, werewolf(s) active
  - When: Werewolves select a victim (Villager)
  - Then: `NightActionLogEntry` with `WerewolfVictimSelection` created

- **NA-002**: `Werewolves_CannotSelectWerewolf_AsVictim`
  - Given: Night phase, multiple werewolves
  - When: Instruction for victim selection is generated
  - Then: Werewolves are excluded from selectable targets

- **NA-003**: `Werewolves_CompleteFlow_WakeSleepCycle`
  - Given: Night phase starting
  - When: Werewolf flow executes
  - Then: States transition: AwaitingAwake → AwaitingTarget → AwaitingSleep → Asleep

### 3.2 Seer Actions
- **NA-010**: `Seer_ChecksWerewolf_ReceivesThumbsUp`
  - Given: Night phase, Seer awake, known werewolf player
  - When: Seer selects the werewolf
  - Then: Moderator receives "thumbs up" feedback instruction

- **NA-011**: `Seer_ChecksVillager_ReceivesThumbsDown`
  - Given: Night phase, Seer awake, known villager player
  - When: Seer selects the villager
  - Then: Moderator receives "thumbs down" feedback instruction

- **NA-012**: `Seer_ActionLogged_WithCorrectDetails`
  - Given: Seer performs check
  - When: Action completes
  - Then: `NightActionLogEntry` with `SeerCheck` and target recorded

### 3.3 Edge Cases
- **NA-020**: `Seer_TargetedNight1_StillActsBeforeDawn`
  - Given: Werewolves select Seer as victim on Night 1
  - When: Seer's turn in the night action order
  - Then: Seer still performs their investigation (death resolves at Dawn)

- **NA-021**: `Seer_KilledNight1_CannotActNight2`
  - Given: Seer was killed Night 1 (eliminated at Dawn 1)
  - When: Night 2 begins
  - Then: Seer is skipped in the night action loop (Health = Dead)

- **NA-022**: `FirstNight_RoleIdentification_RecordsRoleCorrectly`
  - Given: First night, Seer wakes
  - When: Seer identified by moderator input
  - Then: Player's `MainRole` is set to `Seer`

---

## 4. Dawn Resolution Tests

Tests for victim calculation and elimination processing.

### 4.1 Victim Calculation
- **DR-001**: `WerewolfVictim_Unprotected_IsEliminated`
  - Given: Werewolves selected a villager victim
  - When: Dawn resolution occurs
  - Then: Victim is eliminated with `EliminationReason.WerewolfAttack`

- **DR-002**: `NoWerewolfVictim_NoElimination_Impossible`
  - Given: Valid victims exist (i.e. villagers)
  - When: Werewolves wake up
  - Then: Not possible to have no victim selected

### 4.2 Role Reveal Flow
- **DR-010**: `VictimEliminated_RoleRevealRequested`
  - Given: Player eliminated at dawn
  - When: Elimination processed
  - Then: Instruction requests role assignment for the victim

- **DR-011**: `VictimRole_Revealed_CreatesAssignRoleLogEntry`
  - Given: Moderator provides victim's role
  - When: Role assignment processed
  - Then: `AssignRoleLogEntry` created with correct player and role

- **DR-012**: `VictimHealthStatus_SetToDead`
  - Given: Player eliminated and role revealed
  - When: State checked
  - Then: Player's `Health` is `Dead`

---

## 5. Day Voting Tests

Tests for the voting phase and elimination outcomes.

### 5.1 Normal Vote Flow
- **DV-001**: `DebatePhase_TransitionsToVoting`
  - Given: Day phase, `Debate` sub-phase
  - When: Debate confirmation received
  - Then: Transitions to `DetermineVoteType` then `NormalVoting`

- **DV-002**: `VoteOutcome_SinglePlayer_RequestsRoleReveal`
  - Given: Voting sub-phase
  - When: Moderator reports single player received most votes
  - Then: Transitions to request role assignment for eliminated player

- **DV-003**: `VoteElimination_CreatesVoteOutcomeLogEntry`
  - Given: Player eliminated by vote
  - When: Vote processed
  - Then: `VoteOutcomeReportedLogEntry` created

- **DV-004**: `VoteElimination_PlayerHealthSetToDead`
  - Given: Player eliminated by vote
  - When: Elimination processed
  - Then: Player's `Health` is `Dead`

### 5.2 Tie Votes
- **DV-010**: `TieVote_NoPlayerSelected_NoElimination`
  - Given: Voting sub-phase
  - When: Moderator reports tie (empty player selection)
  - Then: No player eliminated, day proceeds normally

- **DV-011**: `TieVote_LogsCorrectOutcome`
  - Given: Tie vote occurred
  - When: Vote processed
  - Then: `VoteOutcomeReportedLogEntry` indicates tie

---

## 6. Victory Condition Tests

Tests for both victory paths.

### 6.1 Villager Victory

- **VC-001**: `WerewolfEliminated_AtDawn_VillagerVictory`
  - Given: Last werewolf killed by special ability at dawn (future: Knight's sword, witch's poison, etc.)
  - When: Dawn resolution completes
  - Then: Victory detected, `WinningTeam` is `Villagers`

- **VC-002**: `WerewolfEliminated_AtDay_VillagerVictory`
  - Given: Last werewolf voted out during day
  - When: Vote elimination processed
  - Then: Victory detected, `WinningTeam` is `Villagers`

### 6.2 Werewolf Victory
- **VC-010**: `WerewolvesEqualVillagers_WerewolvesWin`
  - Given: 1 werewolf, 1 villager remaining
  - When: Victory check performed
  - Then: `WinningTeam` is `Werewolves`

- **VC-011**: `WerewolvesOutnumberVillagers_WerewolvesWin`
  - Given: 2 werewolves, 1 villager remaining
  - When: Victory check performed
  - Then: `WinningTeam` is `Werewolves`

- **VC-012**: `VillagerKilled_AtDawn_WerewolfVictory`
  - Given: 1 werewolf, 2 villagers; werewolf kills 1 villager
  - When: Dawn resolution completes (now 1 WW, 1 Villager)
  - Then: Victory detected, `WinningTeam` is `Werewolves`

- **VC-013**: `VillagerKilled_AtDay_WerewolfVictory`
  - Given: 1 werewolf, 3 villagers; werewolf kills 1 villager during the night; a villager is voted for lynching
  - When: Day resolution completes (now 1 WW, 1 Villager)
  - Then: Victory detected, `WinningTeam` is `Werewolves

### 6.3 Victory Timing
- **VC-020**: `VictoryCondition_CheckedAtDawn`
  - Given: Victory condition met after night kills
  - When: Dawn phase completes victim processing
  - Then: Victory detected before Day phase starts

- **VC-021**: `VictoryCondition_CheckedAfterVote`
  - Given: Victory condition met after day vote
  - When: Vote elimination processed
  - Then: Victory detected, game ends

- **VC-022**: `NoVictoryCondition_GameContinues`
  - Given: Multiple werewolves and villagers alive
  - When: Victory check performed
  - Then: Game continues to next phase

---

## 7. Event Sourcing Tests

Tests for log integrity and state reconstruction.

### 7.1 Log Integrity
- **ES-001**: `AllActions_CreateLogEntries`
  - Given: Complete game cycle (Night → Dawn → Day)
  - When: Game history queried
  - Then: Log contains entries for all major actions

- **ES-002**: `LogEntries_ContainCorrectTurnNumber`
  - Given: Multi-turn game
  - When: Log entries examined
  - Then: Each entry has correct `TurnNumber` for when it occurred

- **ES-003**: `LogEntries_ContainCorrectPhase`
  - Given: Actions in different phases
  - When: Log entries examined
  - Then: Each entry has correct `CurrentPhase`

### 7.2 State Reconstruction
- **ES-010**: `ReplayLog_ReconstructsPlayerHealth`
  - Given: Game log with eliminations
  - When: State reconstructed from log
  - Then: Eliminated players have `Health = Dead`

- **ES-011**: `ReplayLog_ReconstructsKnownRoles`
  - Given: Game log with role assignments
  - When: State reconstructed from log
  - Then: Players have correct `MainRole` values

- **ES-012**: `DerivedState_MatchesCachedState`
  - Given: Active game session
  - When: Compare cached state vs. derived-from-log state
  - Then: States are equivalent

---

## Test Helpers

### GameTestBuilder
Fluent builder for creating test scenarios:
```csharp
var game = GameTestBuilder.Create()
    .WithSimpleGame(playerCount: 5, werewolfCount: 1, includeSeer: true)
    .StartGame();
```

### ResponseFactory
Factory methods for creating `ModeratorResponse` instances:
```csharp
ResponseFactory.Confirm(true);
ResponseFactory.SelectPlayer(playerId);
ResponseFactory.AssignRole(playerId, MainRoleType.Seer);
```

---

## Future Expansion

When adding support for additional roles, add corresponding test sections:
- **Witch Tests**: Save/kill potion interactions
- **Hunter Tests**: Shot on death trigger
- **Defender Tests**: Protection mechanics
- **Elder Tests**: Multi-life handling
- **Sheriff Tests**: Double vote, succession

---

## Conventions

- Test method names follow: `MethodUnderTest_Scenario_ExpectedResult`
- Test IDs use prefixes: GL (Lifecycle), PT (Transitions), NA (Night), DR (Dawn), DV (Day), VC (Victory), ES (Event Sourcing)
- Each test should be independent and not rely on other tests' execution order
- Use `GameTestBuilder` for consistent setup across tests
