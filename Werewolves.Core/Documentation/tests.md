Given the complexity, stateful nature, and intricate rule interactions, a hybrid approach leaning heavily on **scenario-based integration tests** combined with **focused unit tests** for specific, isolated logic seems most effective. This aligns with your preference while ensuring critical low-level components are also verified.

**Core Philosophy:**

*   **Integration-First for Behavior:** The primary goal is to ensure the `GameService` correctly orchestrates the game flow, updates the `GameSession` state accurately based on moderator input, and handles the interactions between roles, events, and game phases according to the rules. Integration tests are best suited for this.
*   **Unit Tests for Isolation:** Use unit tests where logic can be effectively isolated and tested without the overhead of a full game state (e.g., utility functions, simple state transitions, validation helpers, basic instruction generation).
*   **Deterministic Confidence:** Leverage the deterministic nature of the core logic. Given the same initial state and sequence of moderator inputs, the outcome should always be identical.
*   **Focus on State and Transitions:** Tests should rigorously verify the `GameSession` and `PlayerState` after each significant action or phase change.
*   **Validate Moderator Guidance:** Ensure the `ModeratorInstruction` returned at each step is correct, providing the right prompts, options, and expected input types.
*   **Verify Error Handling:** Explicitly test scenarios that should result in `GameError` returns, ensuring the correct error type, code, and message are generated.

**1. Integration Testing Strategy (`GameService` & Interactions)**

This will form the bulk of the testing effort.

*   **Test Entry Point:** The `GameService` class will be the primary subject under test. We will interact with it via its public methods (`StartNewGame`, `ProcessModeratorInput`, `GetCurrentInstruction`, `GetGameStateView`).
*   **Test Structure:**
    *   Use a standard .NET testing framework (xUnit, NUnit, MSTest).
    *   Organize tests by feature/area (e.g., `NightPhaseTests`, `DayVoteTests`, `SeerRoleTests`, `FullMoonRisingEventTests`, `VictoryConditionTests`).
    *   Employ test fixtures or base classes to handle common setup (instantiating `GameService`, potentially pre-configuring simple game sessions).
*   **Scenario-Based Approach:**
    *   Define specific, reproducible game scenarios. Each test (or group of related tests) will represent a mini-gameplay sequence.
    *   **Scenario Definition:** Clearly document the initial setup (players, roles, events) and the sequence of moderator inputs for each test scenario.
    *   **Start Simple:** Begin with basic scenarios (e.g., 3 players - 1 WW, 2 Villagers; run one night/day cycle).
    *   **Increase Complexity:** Gradually add more roles, events, players, and interactions.
    *   **Target Specific Rules/Interactions:** Design scenarios specifically to exercise:
        *   **Core Roles:** Seer view, WW kill, Defender protect, Witch potions, Hunter shot, etc.
        *   **State Management:** Sheriff election/succession, Lover linking/death, Infection, Charm tracking, Potion/Power usage flags (`HasUsed...`), Elder survival counter.
        *   **Positional Logic:** Fox checks (WW nearby/not nearby), Bear Tamer growl condition, Knight curse target identification, Influences/Nightmare voting order.
        *   **Ambiguous Roles:** Thief choice validation, Wild Child transformation, Wolf Hound alignment effect, Devoted Servant swap mechanics (including state reset).
        *   **Loner Roles:** Angel early win check, Piper charm propagation and win condition, White WW kill logic, PM win condition logic.
        *   **Event Interactions:** Test how events modify night actions (Full Moon, Backfire, Specter), day votes (Nightmare, Influences, Great Distrust), debate rules (Eclipse, Good Manners, Not Me), role reveals (Burial, Executioner), and state (Miracle, Little Rascal).
        *   **Victory Conditions:** Create scenarios designed to end in specific ways (Villagers win, WW win, Lovers win, Piper win, etc.) and verify the `GameService` correctly identifies the outcome *based only on the known/revealed state*. Test edge cases like WW parity vs. total players.
        *   **Error Handling:** Scenarios involving invalid input (wrong player ID, invalid target, action out of phase, rule violation like Defender repeat target).
*   **Test Execution Flow (Typical Test):**
    1.  **Arrange:** Call `GameService.StartNewGame` with the scenario's player list (in order!), roles, and event deck. Process initial setup inputs if needed (e.g., PM groups, Night 1 identifications like Cupid).
    2.  **Act:** Call `GameService.ProcessModeratorInput` repeatedly, simulating the moderator's actions for the scenario.
    3.  **Assert:** After each significant `ProcessModeratorInput` call or phase transition:
        *   Check the returned `ProcessResult` (IsSuccess, Error details if applicable).
        *   Check the `ModeratorInstruction` (InstructionText, ExpectedInputType, SelectablePlayerIds, SelectableRoles, SelectableOptions).
        *   Query the `GameSession` state (via `GetGameStateView` or potentially internal access for testing): Verify `GamePhase`, `TurnNumber`, `Player.Status`, `Player.Role` (if revealed), `Player.State` properties (`IsSheriff`, `IsInLove`, `LoverId`, `VoteMultiplier`, `PotionsUsed`, `TimesAttackedByWerewolves`, etc.), `SheriffPlayerId`, `Lovers`, `PendingEliminations`, `ActiveEvents`, `DiscardPile`, `GameHistoryLog` count and latest entries.
        *   Verify specific outcomes (e.g., player eliminated, correct history log entry added, event turns remaining decremented).
*   **Helper Methods:** Develop helper methods within the test project to streamline setup and assertions (e.g., `CreateSessionWithRoles(params RoleType[] roles)`, `SimulateNightAction(gameId, actorId, targetId)`, `AssertPlayerState(gameId, playerId, stateChecker)`, `AssertInstructionAsksForPlayerSelection(instruction, expectedPlayers)`).

**2. Unit Testing Strategy (Focused Logic)**

These tests supplement the integration tests by focusing on smaller, isolated units.

*   **Target Areas:**
    *   **Positional Logic Helpers:** Test `GetLeftNeighbor`, `GetRightNeighbor`, `GetAdjacentLivingNeighbors` extensively with different `PlayerSeatingOrder` lists, player statuses (alive/dead), list lengths, and edge cases (first/last player). Mocking `GameSession` might be needed just to provide the list and player statuses.
    *   **`PlayerState`:** Test any complex logic *within* `PlayerState` itself, though most state changes will be driven by `GameService` and tested via integration. Test default values upon creation.
    *   **`IRole` / `EventCard` Implementations (Limited):**
        *   Test methods that don't require extensive game state, like `GetNightWakeUpOrder()`, `RequiresNight1Identification()`.
        *   Test basic `Generate...Instructions` logic if it primarily involves simple string formatting or checking basic player state flags (e.g., checking `PotionsUsed` before showing Witch options). Avoid mocking complex `GameSession` states here; test the interaction via integration tests.
    *   **Validation Logic:** If `GameService` delegates validation checks to private helper methods (e.g., `CanProtectTarget(session, defenderId, targetId)`), these helpers can be unit tested by passing carefully crafted (potentially mocked) `GameSession` and `PlayerState` objects representing specific conditions (e.g., target is dead, target is self, target was last protected).
    *   **`GameError` Creation:** Test utility methods or logic responsible for constructing `GameError` objects, ensuring correct Type, Code, Message, and Context are populated.
    *   **`GameLogEntry` Types:** Primarily structural validation (correct properties exist). The creation logic is tested via integration tests verifying the `GameHistoryLog`.
*   **Mocking:** Use mocking frameworks (like Moq or NSubstitute) judiciously, primarily where needed to isolate the unit under test (e.g., mocking `GameSession` state for a validation helper). Avoid excessive mocking, especially for `GameService` tests, as that defeats the purpose of integration testing.

**Conclusion:**

This hybrid strategy, heavily favoring scenario-based integration tests focused on the `GameService`, provides the best balance for this project. It directly tests the behavioral correctness and complex interactions that define the game logic, fulfilling the user's preference. Targeted unit tests provide focused verification for isolated algorithms and validation rules, improving confidence and potentially speeding up feedback for specific code areas. This approach maximizes the chances of catching subtle bugs arising from state changes and rule interactions while keeping the testing effort manageable.

--------------------

Okay, here is a comprehensive and exhaustive list of test scenarios for the `Werewolves.Core` library, following the requested format. This list focuses heavily on integration tests exercising the `GameService` and covers roles, events, state management, interactions, edge cases, and error handling based on the provided architecture and rules.

**Phase 1: Game Setup & Initialization**

*   **Test: Start New Game Successfully**
    *   //Setup: A list of 5 player names in order, a list of roles (e.g., 1 WW, 4 Villagers).
    *   //Act: Call `GameService.StartNewGame` with the player names and roles.
    *   //Assert:
        *   Method returns a valid Guid (GameSession Id).
        *   A new `GameSession` is created and retrievable.
        *   `GameSession.Players` contains 5 players with correct names, initial status `Alive`, null Role, default `PlayerState`.
        *   `GameSession.PlayerSeatingOrder` matches the input order of names/IDs.
        *   `GameSession.RolesInPlay` matches the input roles.
        *   `GameSession.GamePhase` is `Setup` or transitions immediately to asking for Night 1 roles/PM setup if applicable.
        *   `GameSession.TurnNumber` is 0 or 1 (depending on definition).
        *   `GameSession.PendingModeratorInstruction` is generated, likely prompting for Night 1 roles or PM setup if included.
        *   `GameSession.GameHistoryLog` contains a "Game Started" entry with initial setup details.

*   **Test: Start New Game With Prejudiced Manipulator Requires Group Setup**
    *   //Setup: 5 player names, roles including `PrejudicedManipulator`.
    *   //Act: Call `GameService.StartNewGame`.
    *   //Assert:
        *   `GameSession` is created.
        *   `GameSession.PendingModeratorInstruction` prompts for PM identification and group assignments.
        *   `GameSession.GamePhase` is `Setup`.

*   **Test: Process Prejudiced Manipulator Group Setup**
    *   //Setup: Game started with PM role present, awaiting PM setup input. Input data defining the PM player ID and group assignments (e.g., Player A is PM, Players A, B belong to Group 1, Players C, D, E belong to Group 2).
    *   //Act: Call `ProcessModeratorInput` with the PM setup details.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   `GameSession.PrejudicedManipulatorGroups` is populated correctly.
        *   `GameHistoryLog` contains a "Prejudiced Manipulator Groups Defined" entry.
        *   `GameSession.PendingModeratorInstruction` advances to the next setup step (e.g., Night 1 roles).

*   **Test: Start New Game With Events Requires Event Deck Setup**
    *   //Setup: 5 player names, roles, a list of Event Card IDs (e.g., "FullMoonRising", "Somnambulism").
    *   //Act: Call `GameService.StartNewGame` with player names, roles, and event card IDs.
    *   //Assert:
        *   `GameSession` is created.
        *   `GameSession.EventDeck` is populated with `EventCard` instances matching the provided IDs.
        *   `GameSession.DiscardPile` is empty.
        *   `GameSession.ActiveEvents` is empty.

*   **Test: Start New Game With Thief Requires Night 1 Identification**
    *   //Setup: 5 player names, roles including `Thief`, plus 2 extra Villager roles.
    *   //Act: Call `StartNewGame`. Process any preceding setup steps (like PM if included).
    *   //Assert:
        *   `GameSession.PendingModeratorInstruction` eventually prompts the moderator to identify the Thief and provide the two offered roles.
        *   `GameSession.GamePhase` is `Setup` or `Night` (during Night 1 role calls).

*   **Test: Process Thief Role Choice**
    *   //Setup: Game in Night 1, Thief identification prompted. Moderator input identifying Player A as Thief, offered roles 'Seer' and 'SimpleVillager', and Thief chose 'Seer'.
    *   //Act: Call `ProcessModeratorInput` with the Thief choice details.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player A's `Role` property is now set to an instance of `SeerRole`.
        *   Player A's `IsRoleRevealed` is false (role is known internally but not publicly revealed).
        *   `GameSession.RolesInPlay` might be adjusted if needed (e.g., remove Thief, add Seer, remove SimpleVillager).
        *   `GameHistoryLog` contains a "Thief Role Choice" entry logging Player A, Seer chosen, SimpleVillager discarded.
        *   `GameSession.PendingModeratorInstruction` advances to the next Night 1 role.

*   **Test: Process Cupid Lover Choice**
    *   //Setup: Game in Night 1, Cupid identification prompted. Moderator input identifying Player B as Cupid, choosing Player C and Player D as Lovers.
    *   //Act: Call `ProcessModeratorInput` with Cupid choice details.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player B's `Role` is `CupidRole`. `IsRoleRevealed` is false.
        *   Player C's `State.IsInLove` is true. `State.LoverId` is Player D's ID.
        *   Player D's `State.IsInLove` is true. `State.LoverId` is Player C's ID.
        *   `GameSession.Lovers` tuple contains Player C and Player D's IDs.
        *   `GameHistoryLog` contains a "Cupid's Lovers Choice" entry logging Player B, C, and D.
        *   `GameSession.PendingModeratorInstruction` advances.

*   **Test: Process Wild Child Model Choice**
    *   //Setup: Game in Night 1, Wild Child identification prompted. Input: Player E is Wild Child, chooses Player A as model.
    *   //Act: Call `ProcessModeratorInput`.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player E's `Role` is `WildChildRole`. `IsRoleRevealed` is false.
        *   Player E's `State.WildChildModelId` is Player A's ID.
        *   `GameHistoryLog` contains a "Wild Child Model Choice" entry.
        *   `GameSession.PendingModeratorInstruction` advances.

*   **Test: Process Wolf Hound Alignment Choice (Villager)**
    *   //Setup: Game in Night 1, Wolf Hound identification prompted. Input: Player F is Wolf Hound, chooses Villager side.
    *   //Act: Call `ProcessModeratorInput`.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player F's `Role` is `WolfHoundRole`. `IsRoleRevealed` is false.
        *   Player F's `State.WolfHoundChoice` is `Team.Villagers`.
        *   `GameHistoryLog` contains a "Wolf Hound Alignment Choice" entry.
        *   `GameSession.PendingModeratorInstruction` advances.

*   **Test: Process Wolf Hound Alignment Choice (Werewolf)**
    *   //Setup: Game in Night 1, Wolf Hound identification prompted. Input: Player F is Wolf Hound, chooses Werewolf side.
    *   //Act: Call `ProcessModeratorInput`.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player F's `Role` is `WolfHoundRole`. `IsRoleRevealed` is false.
        *   Player F's `State.WolfHoundChoice` is `Team.Werewolves`.
        *   `GameHistoryLog` contains a "Wolf Hound Alignment Choice" entry.
        *   `GameSession.PendingModeratorInstruction` advances.

*   **Test: Process Actor Role Pool Set**
    *   //Setup: Game setup includes Actor role. GameService determines 3 valid non-WW roles (e.g., Seer, Defender, Hunter).
    *   //Act: Internal setup logic identifies the pool (may not require direct moderator input unless rules specify). Check state after `StartNewGame` or relevant setup step.
    *   //Assert:
        *   `GameSession` (or potentially the Actor's `PlayerState` if designed that way) stores the list of available roles for the Actor.
        *   `GameHistoryLog` contains an "Actor Role Pool Set" entry logging the available roles.

-------------------------------

**Phase 2: Core Game Flow & Night Phase**

*   **Test: Night Phase Role Order Execution**
    *   //Setup: Game session with multiple roles having night actions (e.g., Seer, Defender, Werewolves, Witch, Piper). All relevant players identified. Turn > 1.
    *   //Act: Transition game to `GamePhase.Night`. Repeatedly call `ProcessModeratorInput` with valid inputs for each role action when prompted.
    *   //Assert:
        *   `GetCurrentInstruction` prompts for roles in the correct order defined by `IRole.GetNightWakeUpOrder()` and rulebook overrides (e.g., Seer -> Defender -> WW -> Witch -> Piper).
        *   Game transitions correctly through prompts for each active role.
        *   After all night actions, `GamePhase` transitions to `Day_ResolveNight`.

*   **Test: Simple Werewolf Kill**
    *   //Setup: Game in Night phase. Players A (WW), B (Villager), C (Villager). Moderator prompted for WW action. Input: WW (Player A) targets Player B.
    *   //Act: Call `ProcessModeratorInput` with WW target choice. Proceed through remaining night actions (if any). Trigger Night Resolution.
    *   //Assert:
        *   `ProcessResult` is successful for WW input.
        *   `GameHistoryLog` contains "Werewolf Group Victim Choice" entry for Player B.
        *   During `Day_ResolveNight`, Player B's `Status` becomes `Dead`.
        *   `GameHistoryLog` contains a "Player Eliminated" entry for Player B with reason `WerewolfAttack`.
        *   `PendingModeratorInstruction` for Day phase indicates Player B was eliminated.

*   **Test: Big Bad Wolf Second Kill (Successful)**
    *   //Setup: Game in Night phase. Players A (BBW), B (WW), C (V), D (V). No WW/WC/WH previously eliminated. Input: WW group targets C. BBW separately targets D.
    *   //Act: Process WW group target C. Process BBW target D. Resolve Night.
    *   //Assert:
        *   `GameHistoryLog` contains entries for both "Werewolf Group Victim Choice" (C) and "Big Bad Wolf Victim Choice" (D).
        *   During resolution, Player C `Status` becomes `Dead` (reason `WerewolfAttack`).
        *   During resolution, Player D `Status` becomes `Dead` (reason `BigBadWolfAttack` or similar).
        *   Day instruction lists both C and D as eliminated.

*   **Test: Big Bad Wolf Second Kill (Prevented)**
    *   //Setup: Game in Night phase. Players A (BBW), C (V), D (V). A previous WW (Player B) was eliminated last turn. Input: WW group (just A) targets C.
    *   //Act: Process WW group target C. Check if BBW second kill is prompted. Resolve Night.
    *   //Assert:
        *   Moderator is *not* prompted for a second BBW victim.
        *   Only Player C is eliminated (reason `WerewolfAttack`).
        *   Day instruction lists only C as eliminated.

*   **Test: Accursed Wolf-Father Infection (Successful First Use)**
    *   //Setup: Game in Night phase. Player A (AWF), B (WW), C (V), D (V). AWF `State.HasUsedAccursedInfection` is false. Input: WW group targets C. AWF chooses to infect C instead.
    *   //Act: Process WW target C. Process AWF infection choice on C. Resolve Night.
    *   //Assert:
        *   `GameHistoryLog` contains "Accursed Wolf-Father Infection Attempt" entry for Player C.
        *   During resolution, Player C's `Status` remains `Alive`.
        *   Player C's `State.IsInfected` becomes true.
        *   Player A's `State.HasUsedAccursedInfection` becomes true.
        *   Day instruction does *not* list C as eliminated by WWs.
        *   On subsequent nights, Player C is prompted/included in WW actions.
        *   `GameHistoryLog` contains a "Player State Changed" entry for Player C (IsInfected).

*   **Test: Accursed Wolf-Father Infection (Attempt Second Use)**
    *   //Setup: Game in Night phase. Player A (AWF). AWF `State.HasUsedAccursedInfection` is true. Input: WW group targets D. AWF attempts to infect D.
    *   //Act: Process WW target D. Attempt to process AWF infection choice on D.
    *   //Assert:
        *   Moderator is *not* prompted for infection choice OR `ProcessModeratorInput` for infection returns `ProcessResult` failure with `RuleViolationCode.AccursedInfectionAlreadyUsed`.
        *   Player D is eliminated normally by WW attack.
        *   Player D's `State.IsInfected` remains false.

*   **Test: Seer Views Villager**
    *   //Setup: Game in Night phase. Player A (Seer), Player B (SimpleVillager). Seer prompted. Input: Seer (A) targets Player B.
    *   //Act: Process Seer input.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   `GameHistoryLog` contains "Seer View Attempt" entry for Player B.
        *   `PendingModeratorInstruction` to moderator *privately* indicates Player B is 'SimpleVillager' (or shows card representation).
        *   Instruction advances to next role.

*   **Test: Seer Views Werewolf**
    *   //Setup: Game in Night phase. Player A (Seer), Player B (SimpleWerewolf). Seer prompted. Input: Seer (A) targets Player B.
    *   //Act: Process Seer input.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   `GameHistoryLog` contains "Seer View Attempt" entry for Player B.
        *   `PendingModeratorInstruction` to moderator *privately* indicates Player B is 'SimpleWerewolf'.
        *   Instruction advances.

*   **Test: Defender Protects Target Successfully**
    *   //Setup: Game in Night phase. Player A (Defender), Player B (Villager), Player C (WW). Defender `State.LastProtectedPlayerId` is not Player B. Input: Defender (A) protects Player B. WW (C) targets Player B.
    *   //Act: Process Defender input. Process WW input. Resolve Night.
    *   //Assert:
        *   Defender input successful. `GameHistoryLog` contains "Defender Protection Choice" for B.
        *   WW input successful. `GameHistoryLog` contains "Werewolf Group Victim Choice" for B.
        *   During resolution, Player B's `Status` remains `Alive`.
        *   Player A's `State.LastProtectedPlayerId` becomes Player B's ID.
        *   Player B's `State.IsProtectedTonight` was true during resolution (then likely reset).
        *   Day instruction indicates no WW elimination.

*   **Test: Defender Protects Self Successfully**
    *   //Setup: Game in Night phase. Player A (Defender), Player C (WW). Defender `State.LastProtectedPlayerId` is not Player A. Input: Defender (A) protects Player A. WW (C) targets Player A.
    *   //Act: Process Defender input. Process WW input. Resolve Night.
    *   //Assert:
        *   Defender input successful.
        *   WW input successful.
        *   During resolution, Player A's `Status` remains `Alive`.
        *   Player A's `State.LastProtectedPlayerId` becomes Player A's ID.
        *   Day instruction indicates no WW elimination.

*   **Test: Defender Protects Target, Witch Poisons Same Target**
    *   //Setup: Game in Night phase. Player A (Defender), B (Villager), C (Witch), D (WW). Defender protects B. WW targets someone else (E). Witch poisons B. Witch `State.PotionsUsed` does not include Poison.
    *   //Act: Process Defender input (protect B). Process WW input (target E). Process Witch input (poison B). Resolve Night.
    *   //Assert:
        *   Defender, WW, Witch inputs successful.
        *   During resolution, Player B `Status` becomes `Dead` (reason `WitchPoison`). Protection does not save from poison.
        *   Player E `Status` becomes `Dead` (reason `WerewolfAttack`).
        *   Witch `State.PotionsUsed` now includes `Poison`.
        *   Day instruction lists B and E eliminated.

*   **Test: Defender Attempts to Protect Same Target Twice**
    *   //Setup: Game in Night phase (Turn 2+). Player A (Defender), Player B (Villager). On the previous night (Turn 1), A protected B. Player A's `State.LastProtectedPlayerId` is Player B's ID. Defender prompted. Input: Defender (A) tries to protect Player B again.
    *   //Act: Call `ProcessModeratorInput` with Defender targeting B.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `RuleViolation`.
        *   `GameError.Code` is `GameErrorCode.RuleViolation_DefenderRepeatTarget`.
        *   `GameSession.PendingModeratorInstruction` remains asking the Defender for a valid target.
        *   Player B's `State.IsProtectedTonight` remains false.

*   **Test: Witch Uses Healing Potion Successfully**
    *   //Setup: Game in Night phase. Player A (Witch), Player B (Villager), Player C (WW). Witch `State.PotionsUsed` does not include `Healing`. Input: WW (C) targets Player B. Witch prompted after seeing victim. Input: Witch uses Heal on B.
    *   //Act: Process WW input. Process Witch Heal input. Resolve Night.
    *   //Assert:
        *   Witch input successful. `GameHistoryLog` contains "Witch Potion Use Attempt" (Heal, B).
        *   During resolution, Player B `Status` remains `Alive`.
        *   Witch `State.PotionsUsed` now includes `Healing`.
        *   Day instruction indicates no WW elimination (or lists other victims if any).

*   **Test: Witch Uses Poison Potion Successfully**
    *   //Setup: Game in Night phase. Player A (Witch), Player B (Villager). Witch `State.PotionsUsed` does not include `Poison`. Input: Witch uses Poison on B.
    *   //Act: Process Witch Poison input. Resolve Night.
    *   //Assert:
        *   Witch input successful. `GameHistoryLog` contains "Witch Potion Use Attempt" (Poison, B).
        *   During resolution, Player B `Status` becomes `Dead` (reason `WitchPoison`).
        *   Witch `State.PotionsUsed` now includes `Poison`.
        *   Day instruction lists B as eliminated.

*   **Test: Witch Attempts to Use Heal Potion Twice**
    *   //Setup: Game in Night phase. Player A (Witch). Witch `State.PotionsUsed` already includes `Healing`. WW targets Player B. Witch prompted. Input: Witch attempts Heal on B.
    *   //Act: Attempt `ProcessModeratorInput` for Witch Heal.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `RuleViolation`.
        *   `GameError.Code` is `GameErrorCode.RuleViolation_WitchPotionAlreadyUsed`.
        *   Player B is eliminated by WWs (unless saved by other means).

*   **Test: Witch Attempts to Use Poison Potion Twice**
    *   //Setup: Game in Night phase. Player A (Witch). Witch `State.PotionsUsed` already includes `Poison`. Input: Witch attempts Poison on B.
    *   //Act: Attempt `ProcessModeratorInput` for Witch Poison.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `RuleViolation`.
        *   `GameError.Code` is `GameErrorCode.RuleViolation_WitchPotionAlreadyUsed`.
        *   Player B is not eliminated by the Witch.

*   **Test: Little Girl Gets Caught Spying**
    *   //Setup: Game in Night phase. Player A (LittleGirl), Player B (WW), Player C (WW). WWs B & C target Player D. Moderator indicates Little Girl (A) was caught.
    *   //Act: Process WWs targeting D. Moderator provides input indicating LG was caught (this might be a special input type or confirmation). Resolve Night.
    *   //Assert:
        *   During resolution, Player A (LittleGirl) `Status` becomes `Dead` (reason `LittleGirlCaught` or similar).
        *   Player D (original target) `Status` remains `Alive` (relative to this action).
        *   `GameHistoryLog` contains a "Little Girl Caught" entry.
        *   Day instruction indicates Player A was eliminated.

*   **Test: Fox Checks Neighbors - Finds Werewolf**
    *   //Setup: Game in Night phase. Seating: V1, WW1, Fox(A), V2, V3. Player A (Fox) `State.HasLostFoxPower` is false. Fox prompted. Input: Fox targets Player V2. Neighbors are Fox(A) and V3. Implicit check is on WW1, Fox(A), V2.
    *   //Act: Process Fox input targeting V2.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   `GameHistoryLog` contains "Fox Check Performed" entry (Target V2, Neighbors checked A, V3, Result: Yes, Power Lost: False). *Correction based on rules: Check is target + neighbors.* Setup: V1, WW1, Fox(A), V2, V3. Fox targets A. Neighbors are WW1, V2. Check is WW1, A, V2. Moderator gives affirmative.
    *   //Setup (Corrected): Seating Order [V1_ID, WW1_ID, FoxA_ID, V2_ID, V3_ID]. Player A (Fox) `State.HasLostFoxPower` is false. Fox prompted. Input: Fox (A) targets self (Player A). Living neighbors are WW1 and V2.
    *   //Act (Corrected): Process Fox input targeting Player A.
    *   //Assert (Corrected):
        *   `ProcessResult` is successful.
        *   `GameHistoryLog` contains "Fox Check Performed" entry (Target A, Neighbors WW1, V2, Result: Yes, Power Lost: False).
        *   `PendingModeratorInstruction` to moderator *privately* indicates "Yes" (WW nearby).
        *   Player A's `State.HasLostFoxPower` remains false.
        *   Instruction advances.

*   **Test: Fox Checks Neighbors - Finds No Werewolf (Loses Power)**
    *   //Setup (Corrected): Seating Order [V1_ID, V4_ID, FoxA_ID, V2_ID, V3_ID]. Player A (Fox) `State.HasLostFoxPower` is false. Fox prompted. Input: Fox (A) targets self (Player A). Living neighbors are V4 and V2.
    *   //Act (Corrected): Process Fox input targeting Player A.
    *   //Assert (Corrected):
        *   `ProcessResult` is successful.
        *   `GameHistoryLog` contains "Fox Check Performed" entry (Target A, Neighbors V4, V2, Result: No, Power Lost: True).
        *   `PendingModeratorInstruction` to moderator *privately* indicates "No".
        *   Player A's `State.HasLostFoxPower` becomes true.
        *   Instruction advances.

*   **Test: Fox Attempts Check After Losing Power**
    *   //Setup: Game in Night phase. Player A (Fox) `State.HasLostFoxPower` is true.
    *   //Act: Check if Fox role is prompted for action. If prompted, attempt to process Fox input.
    *   //Assert:
        *   Fox role is *not* prompted for action OR `ProcessModeratorInput` for Fox returns Failure with `GameErrorCode.RuleViolation_PowerLostOrUnavailable`.

*   **Test: Knight Curse Activates on Werewolf Death**
    *   //Setup: Game in Night phase. Seating: V1, Knight(A), WW1(B), V2. WWs (including B) target Knight (A).
    *   //Act: Process WW target A. Resolve Night.
    *   //Assert:
        *   During resolution, Player A (Knight) `Status` becomes `Dead` (reason `WerewolfAttack`).
        *   `GameSession.PendingKnightCurseTarget` is set to Player B's ID (first WW to Knight's left).
        *   `GameHistoryLog` contains "Knight Curse Activated" entry targeting Player B.
        *   Day instruction indicates Player A eliminated.

*   **Test: Knight Curse Resolves Next Night**
    *   //Setup: Game state from previous test. `GameSession.PendingKnightCurseTarget` is Player B's ID. Start next Night phase. Proceed through actions. Enter `Day_ResolveNight` phase for the *current* night.
    *   //Act: `GameService` executes Night Resolution logic.
    *   //Assert:
        *   During resolution, Player B (WW1) `Status` becomes `Dead` (reason `KnightCurse`).
        *   `GameSession.PendingKnightCurseTarget` is cleared (set to null).
        *   `GameHistoryLog` contains "Player Eliminated" entry for Player B with reason `KnightCurse`.
        *   Day instruction (for *this* turn) indicates Player B eliminated by curse (plus any other night victims).

*   **Test: Knight Killed by Vote Does Not Trigger Curse**
    *   //Setup: Game in Day Vote Resolution phase. Player A (Knight) is eliminated by vote. Seating: V1, Knight(A), WW1(B), V2.
    *   //Act: Process Knight elimination by vote. Reveal role.
    *   //Assert:
        *   Player A `Status` is `Dead`. Role is revealed.
        *   `GameSession.PendingKnightCurseTarget` remains null.
        *   No "Knight Curse Activated" log entry is generated.

*   **Test: Piper Charms Two Players**
    *   //Setup: Game in Night phase. Player A (Piper), B (V), C (WW). Piper prompted. Input: Piper targets B and C.
    *   //Act: Process Piper input.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   `GameHistoryLog` contains "Piper Charm Choice" entry for B and C.
        *   Player B's `State.IsCharmed` becomes true.
        *   Player C's `State.IsCharmed` becomes true.
        *   `GameSession.CharmedPlayerIds` contains B and C's IDs.
        *   Instruction advances. (Moderator is implicitly assumed to tap players).

*   **Test: Piper Attempts to Charm Self (Disallowed)**
    *   //Setup: Game in Night phase. Player A (Piper), B (V), C (WW). Piper prompted. Input: Piper targets Self (A) and B.
    *   //Act: Attempt `ProcessModeratorInput` for Piper charm.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `RuleViolation`.
        *   `GameError.Code` is `GameErrorCode.RuleViolation_TargetIsSelf` (or specific Piper rule).
        *   Player A and B `State.IsCharmed` remain false.

*   **Test: Actor Chooses and Uses Role Power (Seer Example)**
    *   //Setup: Game in Night phase. Player A (Actor). Available roles in pool: Seer, Defender, Hunter. Actor prompted. Input: Actor chooses Seer. Later, Actor (acting as Seer) prompted. Input: Actor targets Player B (WW).
    *   //Act: Process Actor role choice input. Process Actor's "Seer" action input.
    *   //Assert:
        *   Actor choice input successful. `GameHistoryLog` contains "Actor Role Choice" (Seer).
        *   Actor Seer action input successful. `GameHistoryLog` contains "Seer View Attempt" (by Actor A targeting B).
        *   `PendingModeratorInstruction` to moderator *privately* indicates Player B is 'SimpleWerewolf'.
        *   Next night, the 'Seer' role is removed from Actor's available pool.

*   **Test: White Werewolf Kills Werewolf (Correct Night)**
    *   //Setup: Game in Night phase (Turn 2, 4, 6...). Player A (WhiteWW), Player B (WW), Player C (WW). WWs target Player D (V). WhiteWW prompted for solo kill. Input: WhiteWW targets Player B.
    *   //Act: Process WW group target D. Process WhiteWW target B. Resolve Night.
    *   //Assert:
        *   WW group input successful.
        *   WhiteWW input successful. `GameHistoryLog` contains "White Werewolf Victim Choice" (B).
        *   During resolution, Player D `Status` becomes `Dead` (reason `WerewolfAttack`).
        *   During resolution, Player B `Status` becomes `Dead` (reason `WhiteWerewolfAttack` or similar).
        *   Day instruction lists both D and B eliminated.

*   **Test: White Werewolf Kill Attempt (Wrong Night)**
    *   //Setup: Game in Night phase (Turn 1, 3, 5...). Player A (WhiteWW), Player B (WW).
    *   //Act: Proceed through night actions.
    *   //Assert:
        *   White Werewolf is *not* prompted for a solo kill action.

*   **Test: White Werewolf Attempts to Kill Non-Werewolf**
    *   //Setup: Game in Night phase (Turn 2, 4, 6...). Player A (WhiteWW), Player B (WW), Player C (Villager). WhiteWW prompted for solo kill. Input: WhiteWW targets Player C.
    *   //Act: Attempt `ProcessModeratorInput` for WhiteWW targeting C.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `RuleViolation`.
        *   `GameError.Code` is `GameErrorCode.RuleViolation_TargetIsInvalid` (or specific WWWW rule).
        *   Player C is not eliminated by WhiteWW.


*   **Test: Witch Uses Both Heal and Poison Potions in Same Night**
    *   //Setup: Game in Night phase. Player A (Witch) `State.PotionsUsed` is None. Player B (Villager) was targeted by Werewolves. Player C (Villager) is alive.
    *   //Act: Witch is prompted after seeing victim B. Moderator input: Witch uses Heal on B AND uses Poison on C. Proceed to `Day_ResolveNight`.
    *   //Assert:
        *   `ProcessResult` is successful for Witch input.
        *   `GameHistoryLog` contains "Witch Potion Use Attempt" (Heal, B) and "Witch Potion Use Attempt" (Poison, C).
        *   During resolution, Player B `Status` remains `Alive`.
        *   During resolution, Player C `Status` becomes `Dead` (reason `WitchPoison`).
        *   Witch `State.PotionsUsed` now includes both `Healing` and `Poison`.
        *   Day instruction lists C as eliminated (but not B from WW attack).

*   **Test: Witch Successfully Heals Self**
    *   //Setup: Game in Night phase. Player A (Witch) `State.PotionsUsed` does not include `Healing`. Player A was targeted by Werewolves.
    *   //Act: Witch is prompted after seeing victim (self). Moderator input: Witch uses Heal on A (self). Proceed to `Day_ResolveNight`.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   `GameHistoryLog` contains "Witch Potion Use Attempt" (Heal, A).
        *   During resolution, Player A `Status` remains `Alive`.
        *   Witch `State.PotionsUsed` now includes `Healing`.
        *   Day instruction indicates no WW elimination (from this specific target).

*   **Test: Accursed Wolf-Father Infection Bypasses Defender Protection**
    *   //Setup: Game in Night phase. Player AWF (Accursed Wolf-Father) `State.HasUsedAccursedInfection` is false. Player D (Defender). Player V (Villager). Input: Defender protects V. WW group (including AWF) targets V. AWF chooses to infect V instead.
    *   //Act: Process Defender input (protect V). Process WW target V. Process AWF infection choice on V. Resolve Night.
    *   //Assert:
        *   Defender input successful.
        *   AWF infection input successful. `GameHistoryLog` contains "Accursed Wolf-Father Infection Attempt" entry for V.
        *   During resolution, Player V's `Status` remains `Alive`. Protection did not block infection.
        *   Player V's `State.IsInfected` becomes true.
        *   Player AWF's `State.HasUsedAccursedInfection` becomes true.
        *   Day instruction does *not* list V as eliminated.
------------------------

**Phase 3: Day Phase & Resolution**

*   **Test: Night Resolution Correctly Identifies Victims (Multiple Causes)**
    *   //Setup: Night actions logged: WW target A (Protected by Defender). Witch poisons B. WhiteWW kills WW C (Turn 2). Knight curse pending on WW D.
    *   //Act: Trigger `Day_ResolveNight` phase processing.
    *   //Assert:
        *   Player A remains Alive (Protected).
        *   Player B Status becomes Dead (Reason: WitchPoison).
        *   Player C Status becomes Dead (Reason: WhiteWerewolfAttack).
        *   Player D Status becomes Dead (Reason: KnightCurse).
        *   `PendingKnightCurseTarget` is cleared.
        *   `Defender.LastProtectedPlayerId` is updated.
        *   `Witch.PotionsUsed` includes Poison.
        *   `GameHistoryLog` contains correct "Player Eliminated" entries for B, C, D.
        *   `PendingModeratorInstruction` lists B, C, D as eliminated.
        *   Game phase transitions to `Day_Event`.

*   **Test: Role Reveal on Death (Standard)**
    *   //Setup: Game in `Day_Event` phase. Player B (Seer) was just announced eliminated. Moderator prompted to reveal role. Input: Role is 'Seer'.
    *   //Act: Call `ProcessModeratorInput` with revealed role 'Seer'.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player B's `Role` is set to `SeerRole` instance.
        *   Player B's `IsRoleRevealed` becomes true.
        *   `GameHistoryLog` contains "Role Revealed" entry for Player B as Seer.
        *   Instruction advances (e.g., check Bear Tamer, draw Event).

*   **Test: Hunter Shoots on Night Death**
    *   //Setup: Game in `Day_Event`. Player B (Hunter) eliminated overnight. Role revealed as Hunter. Moderator prompted for Hunter's target. Input: Hunter targets Player C (Alive).
    *   //Act: Call `ProcessModeratorInput` with Hunter's target C.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player C's `Status` becomes `Dead` (reason `HunterShot`).
        *   `GameHistoryLog` contains "Player Eliminated" entry for Player C with reason `HunterShot`.
        *   Moderator prompted to reveal Player C's role.

*   **Test: Hunter Shoots on Day Vote Death**
    *   //Setup: Game in `Day_ResolveVote`. Player B (Hunter) eliminated by vote. Role revealed as Hunter. Moderator prompted for Hunter's target. Input: Hunter targets Player C (Alive).
    *   //Act: Call `ProcessModeratorInput` with Hunter's target C.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player C's `Status` becomes `Dead` (reason `HunterShot`).
        *   `GameHistoryLog` contains "Player Eliminated" entry for Player C with reason `HunterShot`.
        *   Moderator prompted to reveal Player C's role (potentially leading to cascade if C is also Hunter or Lover).

*   **Test: Lovers Die Together (Night Kill)**
    *   //Setup: Game in `Day_ResolveNight`. Player C and Player D are Lovers (`GameSession.Lovers` set). Player C was killed by WWs.
    *   //Act: Process Night Resolution.
    *   //Assert:
        *   Player C `Status` becomes `Dead` (reason `WerewolfAttack`).
        *   Player D `Status` *also* becomes `Dead` (reason `LoversHeartbreak`).
        *   `GameHistoryLog` contains elimination entries for both C and D with correct reasons.
        *   Day instruction lists both C and D eliminated.

*   **Test: Lovers Die Together (Day Vote Kill)**
    *   //Setup: Game in `Day_ResolveVote`. Player C and Player D are Lovers. Player C is eliminated by vote. Role revealed (if applicable).
    *   //Act: Process vote elimination of Player C.
    *   //Assert:
        *   Player C `Status` becomes `Dead` (reason `DayVote`).
        *   Player D `Status` *also* becomes `Dead` (reason `LoversHeartbreak`).
        *   `GameHistoryLog` contains elimination entries for both C and D with correct reasons.
        *   Moderator prompted to reveal Player D's role.

*   **Test: Bear Tamer Growl Triggered**
    *   //Setup: Game in `Day_Event`. Seating: V1, BearTamer(A), WW1(B), V2. Player A (Bear Tamer) is Alive. Player B (WW) is Alive. Player B's role *has been revealed* (e.g., died and revived by Miracle as WW, or somehow revealed earlier).
    *   //Act: Game logic proceeds past victim announcement/reveal.
    *   //Assert:
        *   `PendingModeratorInstruction` includes a specific instruction/reminder for the moderator to make a growling sound.
        *   `GameHistoryLog` contains a "Bear Tamer Growl Occurred" entry.

*   **Test: Bear Tamer Growl Not Triggered (Neighbor Not Revealed WW)**
    *   //Setup: Game in `Day_Event`. Seating: V1, BearTamer(A), WW1(B), V2. Player A (Bear Tamer) is Alive. Player B (WW) is Alive. Player B's role is *not* revealed.
    *   //Act: Game logic proceeds past victim announcement/reveal.
    *   //Assert:
        *   No growl instruction is generated.
        *   No "Bear Tamer Growl Occurred" log entry.

*   **Test: Bear Tamer Growl Not Triggered (Neighbor Dead)**
    *   //Setup: Game in `Day_Event`. Seating: V1, BearTamer(A), WW1(B), V2. Player A is Alive. Player B is Dead (but role was WW and revealed).
    *   //Act: Game logic proceeds past victim announcement/reveal.
    *   //Assert:
        *   No growl instruction is generated.
        *   No "Bear Tamer Growl Occurred" log entry.

*   **Test: Bear Tamer Growl Triggered (Tamer Infected)**
        *   //Setup: Player BT (Bear Tamer) `State.IsInfected` is true. Neighbors P1, P2 are Villagers. Game enters `Day_Event`.
        *   //Act: Check for Bear Tamer growl logic trigger.
        *   //Assert: `PendingModeratorInstruction` includes the Bear Tamer's growl indication. `GameHistoryLog` contains "Bear Tamer Growl Occurred".
    *   **(REFINED)** **Test: Bear Tamer Growl Triggered (Adjacent Revealed WW, Tamer Not Infected)**
        *   //Setup: Player BT (Bear Tamer) `State.IsInfected` is false. Player W (Werewolf) is seated adjacent, Alive, and `IsRoleRevealed` is true.
        *   //Act: Check for Bear Tamer growl logic trigger in `Day_Event`.
        *   //Assert: `PendingModeratorInstruction` includes the growl indication. `GameHistoryLog` contains "Bear Tamer Growl Occurred".
    *   **(CONFIRMED)** **Test: Bear Tamer No Growl (No Adjacent Revealed WW, Tamer Not Infected)**
        *   //Setup: Player BT (Bear Tamer) `State.IsInfected` is false. Adjacent neighbors are Villagers or unrevealed Werewolves.
        *   //Act: Check for Bear Tamer growl logic trigger in `Day_Event`.
        *   //Assert: No growl instruction is generated.

*   **Test: Elder Survives First Werewolf Attack**
    *   //Setup: Game in Night phase. Player A (Elder) `State.TimesAttackedByWerewolves` is 0. Player B (WW) targets Player A.
    *   //Act: Process WW target A. Resolve Night.
    *   //Assert:
        *   During resolution, Player A `Status` remains `Alive`.
        *   Player A `State.TimesAttackedByWerewolves` becomes 1.
        *   `GameHistoryLog` contains an "Elder Survived Attack" entry.
        *   Day instruction does not list A as eliminated by WWs.

*   **Test: Elder Dies on Second Werewolf Attack**
    *   //Setup: Game in Night phase. Player A (Elder) `State.TimesAttackedByWerewolves` is 1. Player B (WW) targets Player A.
    *   //Act: Process WW target A. Resolve Night.
    *   //Assert:
        *   During resolution, Player A `Status` becomes `Dead` (reason `WerewolfAttack`).
        *   Player A `State.TimesAttackedByWerewolves` becomes 2.
        *   `GameHistoryLog` contains "Player Eliminated" entry for A.
        *   Day instruction lists A as eliminated.

*   **Test: Elder Dies By Vote - Villager Powers Lost**
    *   //Setup: Game in `Day_ResolveVote`. Player A (Elder) is eliminated by vote. Player B (Seer), Player C (Defender) are alive.
    *   //Act: Process Elder elimination by vote. Reveal role.
    *   //Assert:
            *   Player E (Elder) `Status` is Dead. Role is revealed.
            *   `GameHistoryLog` contains "Villager Powers Lost..." entry.
            *   **Positive Checks (Powers Lost):**
                *   Subsequent attempt by Player S (Seer) to use power fails or is not prompted.
                *   Subsequent attempt by Player D (Defender) to use power fails or is not prompted.
                *   Subsequent attempt by Player W (Witch) to use remaining potions fails or is not prompted.
                *   Subsequent attempt by Player F (Fox) to use power fails or is not prompted (if not already lost).
                *   (Include other defined Villager roles like Two Sisters, Three Brothers recognition, Stuttering Judge signal).
            *   **Negative Checks (Powers Retained):**
                *   Subsequent Night Phase: Player WW1 (Werewolf) is still prompted for WW action.
                *   Subsequent Night Phase: Player P (Piper) is still prompted for charm action.
                *   Subsequent Night Phase: Player WH (Wolf Hound who chose WW) still wakes with WWs.
                *   Subsequent Night Phase: Player WC (Wild Child, transformed to WW) still wakes with WWs.
                *   Subsequent Night Phase: Player AWF (Accursed Wolf-Father) can still attempt infection if unused (and not Elder-disabled).
                *   Subsequent Night Phase: Player WhWW (White Werewolf) can still perform solo kill on correct night.
                *   (Add checks for other non-Villager roles as needed).

*   **Test: Elder Dies By Witch Poison - Villager Powers Lost**
    *   //Setup: Game in `Day_ResolveNight`. Player A (Elder) was poisoned by Witch. Player B (Seer), C (Defender) alive.
    *   //Act: Process night resolution including Elder death by poison.
    *   //Assert:
        *   Player A `Status` is `Dead`.
        *   `GameHistoryLog` contains "Villager Powers Lost (Elder Died By Poison)" entry. (Or combine logic with vote death).
        *   Subsequent night phase: Seer, Defender are not prompted.

*   **Test: Elder Dies By Hunter Shot - Villager Powers Lost**
    *   //Setup: Game state where Hunter shot Elder. Player B (Seer), C (Defender) alive.
    *   //Act: Process Hunter shot elimination of Elder. Reveal role.
    *   //Assert:
        *   Player A `Status` is `Dead`.
        *   `GameHistoryLog` contains "Villager Powers Lost (Elder Died By Hunter Shot)" entry.
        *   Subsequent night phase: Seer, Defender are not prompted.

*   **Test: Wild Child Transforms When Model Dies (Night)**
    *   //Setup: Game in `Day_ResolveNight`. Player A (Wild Child), Player B (Model). Player A `State.WildChildModelId` is B's ID. Player B was killed overnight by WWs.
    *   //Act: Process night resolution including B's death.
    *   //Assert:
        *   Player B `Status` is `Dead`.
        *   Player A's effective team/role considered Werewolf for subsequent actions/checks. (Internal state change, maybe logged).
        *   `GameHistoryLog` contains "Wild Child Transformed" entry.
        *   Subsequent night phase: Player A wakes with Werewolves.

*   **Test: Wild Child Transforms When Model Dies (Day Vote)**
    *   //Setup: Game in `Day_ResolveVote`. Player A (Wild Child), Player B (Model). Player A `State.WildChildModelId` is B's ID. Player B is eliminated by vote.
    *   //Act: Process vote elimination of B. Reveal role.
    *   //Assert:
        *   Player B `Status` is `Dead`.
        *   Player A's effective team/role considered Werewolf.
        *   `GameHistoryLog` contains "Wild Child Transformed" entry.
        *   Subsequent night phase: Player A wakes with Werewolves.

*   **Test: Village Idiot Revealed on Vote**
    *   //Setup: Game in `Day_ResolveVote`. Player A (Village Idiot) receives most votes.
    *   //Act: Process vote results indicating A is eliminated. Moderator reveals role as Village Idiot. Process role reveal.
    *   //Assert:
        *   Player A `Status` remains `Alive`.
        *   Player A `Role` is `VillageIdiotRole`. `IsRoleRevealed` is true.
        *   Player A `State.CanVote` becomes false.
        *   No player is eliminated this vote round. `GameHistoryLog` contains "Role Revealed" for A, potentially a specific log for Idiot save.
        *   Game proceeds to next phase (e.g., check game over, start night).
        *   Subsequent votes: Player A cannot vote (verify by checking vote calculation or input validation).

*   **Test: Scapegoat Eliminated on Tie**
    *   //Setup: Game in `Day_ResolveVote`. Player A (Scapegoat) is alive. Vote results in a tie between Player B and Player C.
    *   //Act: Process vote results. Logic identifies tie and Scapegoat.
    *   //Assert:
        *   Player A (Scapegoat) `Status` becomes `Dead` (reason `Scapegoat`).
        *   Player B and C `Status` remain `Alive`.
        *   `GameHistoryLog` contains "Player Eliminated" for A.
        *   Moderator is prompted to reveal A's role (Scapegoat) and then prompted for Scapegoat's decision on next day's voters.

*   **Test: Scapegoat Chooses Next Day Voters**
    *   //Setup: State following Scapegoat elimination. Moderator prompted for decision. Input: Scapegoat allows Players D, E to vote; others cannot.
    *   //Act: Process Scapegoat decision input. Proceed to next day's vote phase.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   `GameHistoryLog` contains "Scapegoat Voting Restrictions Set" entry.
        *   Game state tracks the restriction (e.g., in `GameSession` or temporary flags).
        *   Next day vote: Only D and E can vote (verify via vote input validation or calculation). Restriction is removed after that vote.

*   **Test: Stuttering Judge Signals Second Vote**
    *   //Setup: Game in Day Vote phase. Player A (Stuttering Judge) `State.HasUsedStutteringJudgePower` is false. Judge signals moderator (represented by specific input). Vote proceeds, Player B eliminated.
    *   //Act: Moderator provides input indicating Judge signaled. Process first vote elimination.
    *   //Assert:
        *   `GameHistoryLog` contains "Stuttering Judge Signaled Second Vote" entry.
        *   Player A's `State.HasUsedStutteringJudgePower` becomes true.
        *   After first elimination resolves, `PendingModeratorInstruction` prompts for a *second* vote in the same Day phase.
        *   Game phase loops back to `Day_Vote` (or a specific second vote phase).

*   **Test: Stuttering Judge Attempts Signal Twice**
    *   //Setup: Game in Day Vote phase. Player A (Stuttering Judge) `State.HasUsedStutteringJudgePower` is true. Judge attempts to signal again.
    *   //Act: Moderator provides input indicating Judge signaled.
    *   //Assert:
        *   Input is ignored OR `ProcessResult` is Failure with `GameErrorCode.RuleViolation_PowerLostOrUnavailable`.
        *   Only one vote occurs (unless triggered by other means like events).

*   **Test: Cascade - Hunter Lover Dies By Vote, Other Lover Dies, Hunter Kills Target**
    *   //Setup: Player H (Hunter) and Player L (Villager) are Lovers. Player T (Villager) is alive. Game in `Day_ResolveVote`. Player H receives most votes.
    *   //Act:
        *   1. Process vote elimination for H. H `Status` becomes `Dead`.
        *   2. Trigger Lover cascade: L `Status` becomes `Dead` (reason `LoversHeartbreak`).
        *   3. Trigger Hunter shot (H is the Hunter). Prompt for target. Moderator input targets T.
        *   4. Process Hunter shot: T `Status` becomes `Dead` (reason `HunterShot`).
    *   //Assert:
        *   Final Statuses: H=Dead, L=Dead, T=Dead.
        *   `GameHistoryLog` contains elimination entries for H (Vote), L (LoversHeartbreak), T (HunterShot).
        *   Moderator prompted to reveal roles for H, L, T in sequence.

*   **Test: Cascade - Hunter Lover Dies By Night, Other Lover Dies, Hunter Kills Target**
    *   //Setup: Player H (Hunter) and Player L (Villager) are Lovers. Player T (Villager) is alive. Game in `Day_ResolveNight`. Player H was killed by Werewolves.
    *   //Act:
        *   1. Process H's death from WW. H `Status` becomes `Dead` (reason `WerewolfAttack`).
        *   2. Trigger Lover cascade: L `Status` becomes `Dead` (reason `LoversHeartbreak`).
        *   3. Prompt for role reveals (H revealed as Hunter, L revealed).
        *   4. Trigger Hunter shot (H is the Hunter). Prompt for target. Moderator input targets T.
        *   5. Process Hunter shot: T `Status` becomes `Dead` (reason `HunterShot`).
    *   //Assert:
        *   Final Statuses: H=Dead, L=Dead, T=Dead.
        *   `GameHistoryLog` contains elimination entries for H (WerewolfAttack), L (LoversHeartbreak), T (HunterShot).
        *   Moderator prompted to reveal roles for H, L, T in sequence (if not already handled in step 3).

--------------------------------

**Phase 4: Events (New Moon)**

*(General Test Structure for each Event Card)*
*   **Test: [EventName] Event Drawn and Applied**
    *   //Setup: Game in `Day_Event` phase. Event deck contains [EventName]. Moderator prompted to draw. Input: [EventName] drawn.
    *   //Act: Process event draw input.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   [EventName] moved from `EventDeck` to `DiscardPile`.
        *   `GameHistoryLog` contains "Event Card Drawn" entry for [EventName].
        *   If event is temporary/active: Entry added to `GameSession.ActiveEvents` with correct `EventId`, `CardReference`, `TurnsRemaining` (if applicable).
        *   `PendingModeratorInstruction` reflects the immediate effect or next step according to the event rule.
        *   Game state updated according to event's `ApplyEffect`.

*   **Test: [EventName] Effect on Night Actions** (For events like FullMoon, Backfire, Specter)
    *   //Setup: Game state with [EventName] active in `ActiveEvents`. Enter Night Phase.
    *   //Act: Execute night actions relevant to the event's modification. Resolve Night.
    *   //Assert:
        *   `GameService` logic correctly applies `ModifyNightActionResolution` from the active event.
        *   Outcomes match the event rule (e.g., FullMoon: WWs see roles, Vills act as WWs; Backfire: WW target transforms or WW dies; Specter: Victim becomes WW, kills WW).
        *   Verify state changes and log entries specific to the event outcome.

*   **Test: [EventName] Effect on Day Vote** (For events like Nightmare, Influences, Great Distrust, Enthusiasm, Dissatisfaction)
    *   //Setup: Game state with [EventName] active. Enter Day phase, proceed towards vote.
    *   //Act: Attempt to initiate standard vote OR follow event-specific vote instructions. Process event-specific vote *outcome* input. Resolve vote.
    *   //Assert:
        *   `GameService` logic correctly calls `ModifyDayVoteProcess`.
        *   `PendingModeratorInstruction` requests the correct type of input (`PlayerSelectionMultiple` for Great Distrust outcome, `PlayerSelectionSingle`/`Confirmation` for Nightmare outcome, etc.).
        *   Input validation matches the event requirements (e.g., valid player selection for elimination, confirmation for tie).
        *   Vote resolution logic uses the event's rules based on the reported outcome.
        *   Enthusiasm/Dissatisfaction trigger second vote correctly based on outcome.

*   **Test: [EventName] Effect on Debate** (For events like Eclipse, Good Manners, Not Me)
    *   //Setup: Game state with [EventName] active. Enter `Day_Debate` phase.
    *   //Act: Proceed through debate phase. (Simulate moderator acknowledging rules).
    *   //Assert:
        *   `PendingModeratorInstruction` during debate reminds moderator of the active rule (Eclipse: turn backs, GoodManners: speak in turn, NotMe: forbidden words).
        *   *Testing actual violation/vote loss might be hard without specific input, focus on the instruction being present.*

*   **Test: [EventName] Effect on Instructions/State** (For events like Somnambulism, Executioner, DoubleAgent, Miracle, LittleRascal, Burial, Punishment, Spiritualism)
    *   //Setup: Game state with [EventName] active. Trigger the relevant game situation.
    *   //Act: Execute the relevant action (Seer view, vote elimination, WW kill, Gypsy action).
    *   //Assert:
        *   `GameService` logic correctly calls `ModifyInstruction` or applies state changes.
        *   Verify specific outcomes:
            *   Somnambulism: Seer result announced publicly (role only).
            *   Executioner: Role not revealed on vote death; Executioner state tracks role; Successor appointed on death.
            *   DoubleAgent: DA player identified, state set, wakes with WWs (verify instruction)? No, doesn't wake. Wins with WWs (verify victory condition check).
            *   Miracle: WW victim revived, becomes Simple Villager, state updated, log entry.
            *   LittleRascal: Player state `IsTemporarilyRemoved`, returns next day, `VoteMultiplier` becomes 3, logs updated.
            *   Burial: Role not revealed on WW night death.
            *   Punishment: Outcome input requested (Confirmation or PlayerSelectionSingle?), elimination based on outcome.
            *   Spiritualism: Gypsy action prompts Medium choice, Day phase prompts Medium to ask question (OptionSelection), Moderator inputs answer (OptionSelection?), Log records Q&A.

*   **Test: [EventName] Expiration** (For temporary events)
    *   //Setup: Game state with temporary [EventName] active (`TurnsRemaining` > 0).
    *   //Act: Complete a full game cycle (or relevant duration). Trigger `OnTurnEnd` logic.
    *   //Assert:
        *   `ActiveEventState.TurnsRemaining` is decremented.
        *   When `TurnsRemaining` reaches 0, the event is removed from `ActiveEvents`.
        *   The event's effect no longer applies in subsequent phases.

*   **Sub-Section: Sheriff Mechanics**
    *   **Test: Sheriff Election Process and State Update**
        *   //Setup: Game state suitable for Sheriff election. 5 Players A, B, C, D, E.
        *   //Act: Generate instruction for Sheriff election vote. Moderator inputs the *outcome*: Player A is elected Sheriff (e.g., via `PlayerSelectionSingle`).
        *   //Assert:
            *   `ProcessResult` is successful.
            *   Player A's `State.IsSheriff` becomes true.
            *   `GameSession.SheriffPlayerId` is set to Player A's ID.
            *   `GameHistoryLog` contains "Sheriff Appointed" entry for A (reason Initial Election).
            *   Game proceeds to next appropriate step.

    *   **Test: Sheriff Vote Correctly Counts Double During Resolution**
        *   //Setup: Game in `Day_Vote`. Player A (Sheriff), Player B (Villager), Player C (Villager). Vote results in a tie between B and C reported by moderator.
        *   //Act: Process vote outcome input (e.g., `Confirmation`=true for tie, or specific 'Tie' `OptionSelection`). Resolve vote.
        *   //Assert:
            *   `GameService` identifies the tie and applies Sheriff tie-breaking logic (Sheriff A's vote breaks tie, C is eliminated).
            *   Player C `Status` becomes `Dead` (reason `DayVote`).
            *   `GameHistoryLog` contains "Vote Resolved (Outcome)" reflecting the Sheriff breaking the tie.

    *   **Test: Sheriff Successfully Passes Role to Successor on Night Death**
        *   //Setup: Player A (Sheriff) killed overnight. Player B (Villager) is alive. Game in `Day_Event` after revealing A's death.
        *   //Act: Moderator prompted for A's successor choice. Moderator input selects Player B.
        *   //Assert:
            *   `ProcessResult` is successful.
            *   Player A's `State.IsSheriff` becomes false (or remains true but Status is Dead).
            *   Player B's `State.IsSheriff` becomes true.
            *   `GameSession.SheriffPlayerId` is updated to Player B's ID.
            *   `GameHistoryLog` contains "Sheriff Appointed" entry for B (reason Successor Appointment, predecessor A).

    *   **Test: Sheriff Successfully Passes Role to Successor on Vote Elimination**
        *   //Setup: Player A (Sheriff) eliminated by vote. Player B (Villager) is alive. Game in `Day_ResolveVote` after resolving A's elimination.
        *   //Act: Moderator prompted for A's successor choice. Moderator input selects Player B.
        *   //Assert:
            *   `ProcessResult` is successful.
            *   Player A's `State.IsSheriff` becomes false.
            *   Player B's `State.IsSheriff` becomes true.
            *   `GameSession.SheriffPlayerId` is updated to Player B's ID.
            *   `GameHistoryLog` contains "Sheriff Appointed" entry for B (reason Successor Appointment, predecessor A).

--------------------------

**Phase 5: Roles with Complex State / Interactions**

*   **Test: Devoted Servant Swaps Successfully (Resets State)**
    *   //Setup: Game in `Day_ResolveVote`. Player B (Witch, `State.PotionsUsed` = Heal, `State.IsSheriff` = true) eliminated. Player A (Devoted Servant, `State.IsCharmed` = true) is alive, not a Lover. Servant prompted/signals swap.
    *   //Act: Process Servant swap input.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player A's `Role` becomes `WitchRole`. `IsRoleRevealed` becomes false (for the new role).
        *   Player A's *original* state affecting the *Servant* role persists if applicable (e.g., `IsCharmed` remains true). Any state tied to the *eliminated player B* is removed from A (A's `IsSheriff` becomes false).
        *   Player A's *new* Witch state is RESET: `State.PotionsUsed` becomes None.
        *   Player B `Status` remains `Dead`, but their `Role` might be marked as 'Swapped' or similar, `IsRoleRevealed` might become true for the original role.
        *   `GameHistoryLog` contains "Devoted Servant Swap Executed" entry.
        *   Game prompts for Sheriff successor (since B lost Sheriff status *before* the swap technically prevented B's reveal/death triggers). *Or does swap happen before Sheriff loss trigger? Clarify rule interaction.* Assuming swap prevents B's death triggers: Game proceeds without asking for B's successor.

*   **Test: Devoted Servant Swap Resets AWF/Fox/Judge/Elder State**
    *   //Setup: Similar to above, but Player B has used AWF infection / lost Fox power / used Judge power / been attacked once as Elder. Player A swaps.
    *   //Act: Process Servant swap.
    *   //Assert:
        *   Player A (now AWF/Fox/Judge/Elder) has `HasUsedAccursedInfection` / `HasLostFoxPower` / `HasUsedStutteringJudgePower` flags reset to false, or `TimesAttackedByWerewolves` reset to 0.

*   **Test: Devoted Servant Cannot Swap if Lover**
    *   //Setup: Player B eliminated. Player A (Devoted Servant) is a Lover (`State.IsInLove` = true). Servant prompted/signals swap.
    *   //Act: Attempt `ProcessModeratorInput` for Servant swap.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `RuleViolation`.
        *   `GameError.Code` is specific to Servant/Lover conflict.
        *   Player B is eliminated normally, role revealed, triggers proceed.

*   **Test: Thief Forced to Choose Werewolf**
    *   //Setup: Game in Night 1. Player A (Thief). Offered roles are 'SimpleWerewolf' and 'BigBadWolf'.
    *   //Act: Process Thief choice input (must choose one). Assume chooses 'SimpleWerewolf'.
    *   //Assert:
        *   `ProcessResult` is successful.
        *   Player A's `Role` becomes `SimpleWerewolfRole`.
        *   Log entry reflects choice.
        *   Player A wakes with Werewolves on subsequent nights.

*   **Test: Infected Player Retains Night Ability (Seer Example)**
    *   //Setup: Player C (Seer) was infected by AWF. Game in subsequent Night phase.
    *   //Act: Proceed through night call order.
    *   //Assert:
        *   Player C is prompted for Seer action.
        *   Player C also wakes/acts with Werewolves.

*   **Test: Infected Player contributes to Werewolf Win Condition**
    *   //Setup: Game state: Player A (WW), Player C (Seer, Infected), Player D (Villager).
    *   //Act: Trigger victory condition check.
    *   //Assert:
        *   Game correctly counts 2 Werewolf-aligned players (A and C) vs 1 Villager-aligned player (D).
        *   Werewolves win condition is met. `PendingModeratorInstruction` indicates WW win.

-------------------------

**Phase 6: Victory Conditions**

*   **Test: Victory Condition - Villagers Win (Standard)**
    *   //Setup: Game state: Player A (Villager), Player B (Villager). All Werewolves previously eliminated and revealed.
    *   //Act: Trigger victory condition check (e.g., after last WW eliminated).
    *   //Assert:
        *   `GameService` determines Villagers win.
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces Villager victory.
        *   `GameHistoryLog` contains "Victory Condition Met" (Villagers).

*   **Test: Victory Condition - Werewolves Win (Parity)**
    *   //Setup: Game state: Player A (WW), Player B (Villager). Both alive.
    *   //Act: Trigger victory check (e.g., after a Villager death leads to this state).
    *   //Assert:
        *   `GameService` determines Werewolves win (count >= Villagers).
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces Werewolf victory.
        *   `GameHistoryLog` contains "Victory Condition Met" (Werewolves).

*   **Test: Victory Condition - Werewolves Win (Elimination)**
    *   //Setup: Game state: Player A (WW), Player B (WW). All non-Werewolves eliminated.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   `GameService` determines Werewolves win.
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces Werewolf victory.
        *   `GameHistoryLog` contains "Victory Condition Met" (Werewolves).

*   **Test: Victory Condition - Lovers Win (Opposing Teams)**
    *   //Setup: Game state: Player A (Villager), Player B (WW). A and B are Lovers. All other players eliminated.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   `GameService` determines Lovers win.
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces Lover victory.
        *   `GameHistoryLog` contains "Victory Condition Met" (Lovers).

*   **Test: Victory Condition - Lovers Do Not Win (Same Team)**
    *   //Setup: Game state: Player A (Villager), Player B (Villager). A and B are Lovers. All WWs eliminated.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   `GameService` determines Villagers win (standard Villager win).
        *   `PendingModeratorInstruction` announces Villager victory.

*   **Test: Victory Condition - Piper Wins**
    *   //Setup: Game state: Player A (Piper), Player B (Charmed), Player C (Charmed). All players B, C have `State.IsCharmed` = true. All are alive.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   `GameService` determines Piper wins (all survivors charmed).
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces Piper victory.
        *   `GameHistoryLog` contains "Victory Condition Met" (Solo_Piper).

*   **Test: Victory Condition - White Werewolf Wins**
    *   //Setup: Game state: Player A (WhiteWW) is the only player alive.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   `GameService` determines White Werewolf wins.
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces White Werewolf victory.
        *   `GameHistoryLog` contains "Victory Condition Met" (Solo_WhiteWerewolf).

*   **Test: Victory Condition - Angel Wins (Early Vote)**
    *   //Setup: Game state: Day 1 Vote resolution. Player A (Angel) is eliminated. TurnNumber is 1.
    *   //Act: Process Angel elimination and role reveal. Trigger victory check.
    *   //Assert:
        *   `GameService` determines Angel wins.
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces Angel victory.
        *   `GameHistoryLog` contains "Victory Condition Met" (Solo_Angel).

*   **Test: Victory Condition - Angel Wins (Night 1 Kill)**
    *   //Setup: Game state: Day 1 Resolve Night. Player A (Angel) killed overnight. TurnNumber is 1.
    *   //Act: Process Angel elimination and role reveal. Trigger victory check.
    *   //Assert:
        *   `GameService` determines Angel wins.
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces Angel victory.

*   **Test: Victory Condition - Angel Wins (Night 2 Kill)**
    *   //Setup: Game state: Day 2 Resolve Night. Player A (Angel) killed overnight. TurnNumber is 2.
    *   //Act: Process Angel elimination and role reveal. Trigger victory check.
    *   //Assert:
        *   `GameService` determines Angel wins.
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces Angel victory.

*   **Test: Victory Condition - Angel Does NOT Win (Day 2 Vote)**
    *   //Setup: Game state: Day 2 Vote resolution. Player A (Angel) is eliminated. TurnNumber is 2.
    *   //Act: Process Angel elimination and role reveal. Trigger victory check.
    *   //Assert:
        *   `GameService` does *not* determine Angel win based on timing.
        *   Game continues (assuming other win conditions not met). Player A becomes effectively a Simple Villager for any remaining checks? No, just doesn't win.
        *   `GamePhase` does not transition to `GameOver` based on Angel win.

*   **Test: Victory Condition - Prejudiced Manipulator Wins**
    *   //Setup: Game state: Player A (PM, Group 1), Player B (Villager, Group 1). All players in Group 2 eliminated. `GameSession.PrejudicedManipulatorGroups` is set.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   `GameService` determines Prejudiced Manipulator wins.
        *   `GamePhase` transitions to `GameOver`.
        *   `PendingModeratorInstruction` announces PM victory.
        *   `GameHistoryLog` contains "Victory Condition Met" (Solo_PrejudicedManipulator).

*   **Test: Victory Check Considers Wolf Hound Alignment**
    *   //Setup: Game state: Player A (WW), Player B (Wolf Hound - Chose WW), Player C (Villager). Player D (Villager) just eliminated.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   Counts 2 WW-aligned (A, B) vs 1 Villager-aligned (C). Werewolves win.
        *   `PendingModeratorInstruction` announces WW victory.

*   **Test: Victory Check Considers Wild Child Transformation**
    *   //Setup: Game state: Player A (WW), Player B (Wild Child - Transformed), Player C (Villager). Player D (Villager) just eliminated.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   Counts 2 WW-aligned (A, B) vs 1 Villager-aligned (C). Werewolves win.
        *   `PendingModeratorInstruction` announces WW victory.

*   **Test: Victory Check Considers Double Agent**
    *   //Setup: Game state: Player A (WW), Player B (Double Agent), Player C (Villager). Player C just eliminated.
    *   //Act: Trigger victory check.
    *   //Assert:
        *   Counts 1 WW vs 0 Villagers. Player B is DA. Werewolves win.
        *   `PendingModeratorInstruction` announces WW victory (implicitly includes DA).

**Phase 7: Error Handling & Validation**

*   **Test: Process Input for NonExistent Game**
    *   //Setup: No active game session with Guid X.
    *   //Act: Call `ProcessModeratorInput` with Guid X and any valid input data.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `GameNotFound`.
        *   `GameError.Code` is `GameErrorCode.GameNotFound_SessionNotFound`.

*   **Test: Process Input - Invalid Player ID**
    *   //Setup: Active game. Instruction expects single player selection. Input provides a Guid not matching any player in `GameSession.Players`.
    *   //Act: Call `ProcessModeratorInput` with the invalid Player ID.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `InvalidInput`.
        *   `GameError.Code` is `GameErrorCode.InvalidInput_PlayerIdNotFound`.

*   **Test: Process Input - Incorrect Input Type**
    *   //Setup: Active game. Instruction expects `ExpectedInputType.Confirmation`. Input provides `SelectedPlayerIds`.
    *   //Act: Call `ProcessModeratorInput`.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `InvalidInput`.
        *   `GameError.Code` is `GameErrorCode.InvalidInput_InputTypeMismatch`.

*   **Test: Process Input - Action Out Of Phase**
    *   //Setup: Active game in `Day_Debate` phase. Input represents a night action (e.g., Seer view).
    *   //Act: Call `ProcessModeratorInput`.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `InvalidOperation`.
        *   `GameError.Code` is `GameErrorCode.InvalidOperation_ActionNotInCorrectPhase`.

*   **Test: Process Input - Target is Dead**
    *   //Setup: Active game in Night. Player A (WW) prompted. Player B is Dead. Input: WW targets Player B.
    *   //Act: Call `ProcessModeratorInput`.
    *   //Assert:
        *   `ProcessResult` is Failure.
        *   `GameError.Type` is `RuleViolation`.
        *   `GameError.Code` is `GameErrorCode.RuleViolation_TargetIsDead`.

*   **Test: Process Input - Malformed Vote Data** - *REMOVED as vote data is no longer input*

*   **Test: Process Input - Vote Sum Incorrect** - *REMOVED as vote counts are no longer input*

*   **Test: Process Input - Lover Voting Against Lover** - *REMOVED as individual votes are not tracked*

*   **Sub-Section: Role & Event Interactions**
    *   **Test: Interaction - Witch Heals Target Already Protected by Defender (Potion Used)**
        *   //Setup: Game in Night phase. Player D (Defender) protects Player V. Player W (Witch) has Healing potion. Player V targeted by WW.
        *   //Act: Process Defender action. Process WW action. Process Witch action (Heal V). Resolve Night.
        *   //Assert:
            *   Player V `Status` remains `Alive`.
            *   Witch `State.PotionsUsed` now includes `Healing` (potion consumed despite protection).
            *   Defender `State.LastProtectedPlayerId` is V's ID.

    *   **Test: Event Interaction - Enthusiasm Does Not Trigger Second Vote After Nightmare Elimination (Even if WW Died)**
        *   //Setup: `EnthusiasmEvent` is active (pending next standard vote). `NightmareEvent` is drawn, replacing the standard vote. Player W (Werewolf) is eliminated during Nightmare accusations.
        *   //Act: Resolve Nightmare elimination of W. Check game flow.
        *   //Assert:
            *   Player W `Status` is `Dead`.
            *   Game proceeds directly to the next phase (e.g., Night) *without* triggering a second vote.
            *   `EnthusiasmEvent` state is removed/discarded.

    *   **Test: Event Interaction - Backfire Transforms Villager, Preventing Miracle Effect**
        *   //Setup: `BackfireEvent` active. `MiracleEvent` active (applicable to next WW victim). WWs target Player V (Simple Villager).
        *   //Act: Resolve Night actions, considering both events.
        *   //Assert:
            *   Backfire logic applies first: Player V `Status` remains `Alive`.
            *   Player V's `Role` becomes `SimpleWerewolfRole` (or equivalent state change).
            *   `MiracleEvent` does not trigger as V was not technically eliminated.
            *   Backfire and Miracle event states are removed/consumed as appropriate.

    *   **Test: Event Interaction - Specter Transforms Victim and Kills WW, Preventing Miracle Effect**
        *   //Setup: `SpecterEvent` active. `MiracleEvent` active. WWs target Player V. Player V chooses original WW1 to be eliminated.
        *   //Act: Resolve Night actions, considering both events.
        *   //Assert:
            *   Specter logic applies: Player V `Status` remains `Alive`. Player V's `Role` becomes `SimpleWerewolfRole`. Player WW1 `Status` becomes `Dead`.
            *   `MiracleEvent` does not trigger as V was not eliminated by the initial WW attack sequence.
            *   Specter and Miracle event states are removed/consumed as appropriate.

-------------


