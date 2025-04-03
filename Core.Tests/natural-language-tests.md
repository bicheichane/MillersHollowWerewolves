**Test Suite Structure:**

Tests are described using a **Setup -> Action -> Assertion** format.

*   **Setup:** Defines the initial state of the `GameSession` (players, roles, specific statuses, active events if applicable).
*   **Action:** Describes the sequence of moderator inputs or game phase progressions being tested.
*   **Assertion:** Specifies the expected state of the `GameSession` or the expected `ModeratorInstruction` after the actions.

---

**Integration Test Suite: Werewolves.Core**

**I. Basic Game Flow & Setup**

1.  **Test: Game Initialization**
    *   **Setup:** List of player names, list of standard roles (e.g., 2 WW, 1 Seer, 1 Witch, 4 Villagers).
    *   **Action:** Call `GameService.StartNewGame`.
    *   **Assertion:** A `GameSession` ID is returned. The game phase is `Night`. The correct number of players exist with assigned roles. The `PendingModeratorInstruction` prompts for the first night role (e.g., Seer or Thief if included). `TurnNumber` is 1.
2.  **Test: Thief Role - Swap with Villager**
    *   **Setup:** Game includes Thief and 2 extra Simple Villager cards. Player A is Thief. Card X (Simple Villager) and Card Y (Werewolf) are the unused cards.
    *   **Action:** Start game. Moderator input confirms Thief sees Card X and Card Y. Moderator input selects Card X (Villager) for the Thief.
    *   **Assertion:** Player A's `AssignedRole` is now `SimpleVillagerRole`. The game proceeds to the next night role.
3.  **Test: Thief Role - Swap with Werewolf**
    *   **Setup:** Game includes Thief. Player A is Thief. Card X (Simple Villager) and Card Y (Werewolf) are the unused cards.
    *   **Action:** Start game. Moderator input confirms Thief sees Card X and Card Y. Moderator input selects Card Y (Werewolf) for the Thief.
    *   **Assertion:** Player A's `AssignedRole` is now `SimpleWerewolfRole`. Player A wakes with Werewolves.
4.  **Test: Thief Role - Forced Swap with Werewolf**
    *   **Setup:** Game includes Thief. Player A is Thief. Card X (Werewolf) and Card Y (Werewolf) are the unused cards.
    *   **Action:** Start game. Moderator input confirms Thief sees Card X and Card Y. Moderator input selects Card Y (Werewolf) for the Thief (or indicates a choice).
    *   **Assertion:** Player A's `AssignedRole` is now `SimpleWerewolfRole`.
5.  **Test: Cupid Role - Linking Two Villagers**
    *   **Setup:** Game includes Cupid (Player C). Player A and Player B are Villagers.
    *   **Action:** Start game. Call Cupid. Moderator input selects Player A and Player B as lovers.
    *   **Assertion:** `GameSession.Lovers` contains IDs of A and B. Player A and B `StateFlags` indicate `IsInLove` and link to each other. Game proceeds.
6.  **Test: Cupid Role - Linking Villager and Werewolf**
    *   **Setup:** Game includes Cupid (Player C). Player A is Villager, Player B is Werewolf.
    *   **Action:** Start game. Call Cupid. Moderator input selects Player A and Player B as lovers.
    *   **Assertion:** `GameSession.Lovers` contains IDs of A and B. Player A and B `StateFlags` indicate `IsInLove`. (Victory condition for them changes implicitly).
7.  **Test: Cupid Role - Linking Self**
    *   **Setup:** Game includes Cupid (Player C). Player A is Villager.
    *   **Action:** Start game. Call Cupid. Moderator input selects Player C (Cupid) and Player A as lovers.
    *   **Assertion:** `GameSession.Lovers` contains IDs of C and A. Player C and A `StateFlags` indicate `IsInLove`.

**II. Night Phase Role Actions**

1.  **Test: Seer - Sees Villager**
    *   **Setup:** Player A is Seer, Player B is Simple Villager. Game phase is Night.
    *   **Action:** Call Seer. Moderator input selects Player B.
    *   **Assertion:** `PendingModeratorInstruction` (privately for Seer) indicates Player B is a Simple Villager.
2.  **Test: Seer - Sees Werewolf**
    *   **Setup:** Player A is Seer, Player B is Simple Werewolf. Game phase is Night.
    *   **Action:** Call Seer. Moderator input selects Player B.
    *   **Assertion:** `PendingModeratorInstruction` indicates Player B is a Simple Werewolf.
3.  **Test: Werewolves - Standard Kill**
    *   **Setup:** Player A and B are Werewolves. Player C is Villager. Game phase is Night.
    *   **Action:** Call Werewolves. Moderator input indicates they target Player C. Advance to Day_ResolveNight.
    *   **Assertion:** `DayEventsOccurred` includes Player C's death. Player C `Status` is Dead.
4.  **Test: Witch - Heals Werewolf Victim**
    *   **Setup:** Player A targeted by Werewolves. Player B is Witch with Healing potion available.
    *   **Action:** Resolve WW action (target A). Call Witch. Moderator input selects Heal on Player A. Advance to Day_ResolveNight.
    *   **Assertion:** Player A `Status` remains Alive. Witch's Healing potion is marked used. `DayEventsOccurred` does *not* include Player A's death from Werewolves.
5.  **Test: Witch - Poisons a Player**
    *   **Setup:** Player A is Villager. Player B is Witch with Poison potion available.
    *   **Action:** Call Witch. Moderator input selects Poison on Player A. Advance to Day_ResolveNight.
    *   **Assertion:** `DayEventsOccurred` includes Player A's death. Player A `Status` is Dead. Witch's Poison potion is marked used.
6.  **Test: Witch - Uses Both Potions**
    *   **Setup:** Player A targeted by WW. Player B is Witch. Player C is Villager. Both potions available.
    *   **Action:** Resolve WW action (target A). Call Witch. Moderator input selects Heal on Player A AND Poison on Player C. Advance to Day_ResolveNight.
    *   **Assertion:** Player A `Status` is Alive. Player C `Status` is Dead. Both Witch potions marked used.
7.  **Test: Witch - Cannot Use Potion Twice**
    *   **Setup:** Player B is Witch, Healing potion used in previous turn. Player A targeted by WW.
    *   **Action:** Resolve WW action (target A). Call Witch. Moderator input attempts to Heal Player A.
    *   **Assertion:** Action fails or instruction indicates potion unavailable. Player A `Status` becomes Dead during resolution. Healing potion remains marked used.
8.  **Test: Witch - Self-Heal**
    *   **Setup:** Player A is Witch, targeted by WW. Healing potion available.
    *   **Action:** Resolve WW action (target A). Call Witch. Moderator input selects Heal on Player A (self). Advance to Day_ResolveNight.
    *   **Assertion:** Player A `Status` remains Alive. Witch's Healing potion marked used.
9.  **Test: Defender - Protects Successfully**
    *   **Setup:** Player A is Defender. Player B targeted by WW.
    *   **Action:** Call Defender. Moderator input selects Player B. Resolve WW action (target B). Advance to Day_ResolveNight.
    *   **Assertion:** Player B `Status` remains Alive. `GameSession.ProtectedPlayerId` was B during resolution.
10. **Test: Defender - Cannot Protect Same Target Twice Consecutively**
    *   **Setup:** Player A is Defender. Protected Player B on Night 1. Game is now Night 2.
    *   **Action:** Call Defender. Moderator input attempts to select Player B.
    *   **Assertion:** Action fails or instruction indicates target is invalid. Defender must choose someone else or skip.
11. **Test: Defender - Protects Self**
    *   **Setup:** Player A is Defender, targeted by WW.
    *   **Action:** Call Defender. Moderator input selects Player A (self). Resolve WW action (target A). Advance to Day_ResolveNight.
    *   **Assertion:** Player A `Status` remains Alive.
12. **Test: Defender vs Witch Poison**
    *   **Setup:** Player A is Defender. Player B is protected by Defender. Player C is Witch, poisons Player B.
    *   **Action:** Resolve night actions.
    *   **Assertion:** Player B `Status` is Dead. (Witch's poison bypasses Defender's protection).
13. **Test: Big Bad Wolf - Extra Kill Active**
    *   **Setup:** Game includes Big Bad Wolf (Player BBW), Simple Werewolf (Player SW), 2 Villagers (V1, V2). No WW/WildChild/WolfHound previously eliminated. Night phase.
    *   **Action:** Call Werewolves (BBW, SW). Moderator input targets V1. Call Big Bad Wolf. Moderator input targets V2. Advance to Day_ResolveNight.
    *   **Assertion:** Both V1 and V2 `Status` are Dead. `DayEventsOccurred` includes both deaths.
14. **Test: Big Bad Wolf - Extra Kill Inactive (WW Died)**
    *   **Setup:** Simple Werewolf was eliminated last turn. Player BBW is Big Bad Wolf. V1, V2 are Villagers. Night phase.
    *   **Action:** Call Werewolves (BBW). Moderator input targets V1. (BBW is not called separately). Advance to Day_ResolveNight.
    *   **Assertion:** V1 `Status` is Dead. V2 `Status` is Alive.
15. **Test: Accursed Wolf-Father - Infection**
    *   **Setup:** Player AWF is Accursed Wolf-Father. Player V1 is Villager. AWF infection power available.
    *   **Action:** Call Werewolves. AWF indicates intent to infect. Moderator input targets V1. Advance to Day_ResolveNight.
    *   **Assertion:** V1 `Status` remains Alive. V1 secretly becomes a Werewolf (`AssignedRole` changes or state flag set, `InfectedPlayerIds` updated). AWF infection power marked used. V1 wakes with WW next night. Victim is *not* revealed in `DayEventsOccurred`.
16. **Test: Accursed Wolf-Father - Normal Kill (After Infection Used)**
    *   **Setup:** Player AWF used infection previously. Player V2 is Villager.
    *   **Action:** Call Werewolves. Moderator input targets V2 (AWF participates normally). Advance to Day_ResolveNight.
    *   **Assertion:** V2 `Status` is Dead.
17. **Test: Accursed Wolf-Father - Infection Attempt on Protected Target**
    *   **Setup:** Player AWF tries to infect Player V1. Player V1 is protected by Defender.
    *   **Action:** Resolve night actions.
    *   **Assertion:** Player V1 remains Villager and Alive. AWF infection power is *not* marked used.
18. **Test: White Werewolf - Kills Villager (Normal)**
    *   **Setup:** Player WW is White Werewolf. Player SW is Simple Werewolf. V1 is Villager. Night phase (odd turn number, e.g., Night 1 or 3).
    *   **Action:** Call Werewolves (WW, SW). Moderator input targets V1. (White WW not called separately). Advance to Day_ResolveNight.
    *   **Assertion:** V1 `Status` is Dead.
19. **Test: White Werewolf - Kills Werewolf (Special)**
    *   **Setup:** Player WW is White Werewolf. Player SW is Simple Werewolf. Night phase (even turn number, e.g., Night 2 or 4).
    *   **Action:** Call Werewolves normally (target V1). Call White Werewolf. Moderator input targets Player SW. Advance to Day_ResolveNight.
    *   **Assertion:** V1 `Status` is Dead (from main WW action). SW `Status` is Dead (from White WW action). `DayEventsOccurred` includes both deaths.
20. **Test: Wolf Hound - Chooses Villager**
    *   **Setup:** Player WH is Wolf Hound. Night 1.
    *   **Action:** Call Wolf Hound (if applicable in sequence). Moderator input indicates WH chooses Villager side.
    *   **Assertion:** WH internal state set to Villager team alignment. WH does not wake with WW.
21. **Test: Wolf Hound - Chooses Werewolf**
    *   **Setup:** Player WH is Wolf Hound. Night 1.
    *   **Action:** Call Wolf Hound. Moderator input indicates WH chooses Werewolf side.
    *   **Assertion:** WH internal state set to Werewolf team alignment. WH wakes with WW from now on.
22. **Test: Piper - Charms Players**
    *   **Setup:** Player P is Piper. Players V1, V2 are alive. Night phase.
    *   **Action:** Call Piper. Moderator input selects V1 and V2.
    *   **Assertion:** `CharmedPlayerIds` includes V1 and V2.
23. **Test: Fox - Detects Werewolf**
    *   **Setup:** Player F is Fox. Players P1(Villager), P2(Werewolf), P3(Villager) are neighbours. Night Phase.
    *   **Action:** Call Fox. Moderator input selects center Player P2.
    *   **Assertion:** `PendingModeratorInstruction` (privately for Fox) indicates a Werewolf is present in the group {P1, P2, P3}. Fox retains power for future nights.
24. **Test: Fox - Detects No Werewolf**
    *   **Setup:** Player F is Fox. Players P1(Villager), P2(Villager), P3(Villager) are neighbours. Night Phase.
    *   **Action:** Call Fox. Moderator input selects center Player P2.
    *   **Assertion:** `PendingModeratorInstruction` indicates no Werewolf present. Fox role state updated to indicate power is lost permanently.
25. **Test: Actor - Uses Seer Power**
    *   **Setup:** Player ACT is Actor. Seer, Witch, Hunter roles available for Actor to mimic. Player V1 is Villager. Night Phase.
    *   **Action:** Call Actor. Moderator input selects 'Seer' power and targets V1.
    *   **Assertion:** `PendingModeratorInstruction` (privately for Actor) reveals V1's role. The 'Seer' power is marked as used/removed from Actor's available choices for subsequent nights.

**III. Day Phase & Triggers**

1.  **Test: Hunter - Dies by Werewolf, Kills Target**
    *   **Setup:** Player H is Hunter. Player V1 is Villager. Player H is killed during the night.
    *   **Action:** Enter Day_Event phase. Reveal H's death. Generate instruction for Hunter's shot. Moderator input selects V1.
    *   **Assertion:** V1 `Status` becomes Dead. `DayEventsOccurred` includes V1's death trigger. Game proceeds.
2.  **Test: Hunter - Dies by Vote, Kills Target**
    *   **Setup:** Player H is Hunter. Player V1 is Villager.
    *   **Action:** Proceed to vote. Player H is eliminated. Generate instruction for Hunter's shot. Moderator input selects V1.
    *   **Assertion:** V1 `Status` becomes Dead immediately after H's elimination resolution.
3.  **Test: Lovers - Villager Lover Dies (WW Kill)**
    *   **Setup:** Player A (Villager) and Player B (Villager) are Lovers. Player A killed by WW.
    *   **Action:** Enter Day_Event phase. Reveal A's death.
    *   **Assertion:** Player B `Status` becomes Dead immediately due to lover's death. `DayEventsOccurred` includes both deaths.
4.  **Test: Lovers - Werewolf Lover Dies (Vote)**
    *   **Setup:** Player A (Villager) and Player B (Werewolf) are Lovers. Player B eliminated by vote.
    *   **Action:** Resolve vote elimination for B.
    *   **Assertion:** Player A `Status` becomes Dead immediately due to lover's death.
5.  **Test: Lovers - Hunter Lover Dies, Triggers Hunter Shot, Other Lover Dies**
    *   **Setup:** Player H (Hunter) and Player V (Villager) are Lovers. Player H killed by WW. Player T is target for Hunter.
    *   **Action:** Reveal H's death. Trigger Lover death for V. Trigger Hunter shot. Moderator input selects T.
    *   **Assertion:** H, V, and T `Status` are all Dead.
6.  **Test: Village Idiot - Voted Out**
    *   **Setup:** Player VI is Village Idiot.
    *   **Action:** Proceed to vote. Player VI receives most votes.
    *   **Assertion:** Instruction reveals VI is the Idiot. VI `Status` remains Alive. VI `StateFlags` updated `CanVote = false`. The vote turn ends immediately (no second elimination even if there was a tie for second place).
7.  **Test: Village Idiot - Killed by Werewolves**
    *   **Setup:** Player VI is Village Idiot. Killed by WW during the night.
    *   **Action:** Reveal deaths in Day_Event phase.
    *   **Assertion:** VI `Status` is Dead. Role is revealed.
8.  **Test: Elder - Survives First Werewolf Attack**
    *   **Setup:** Player E is Elder. Night 1.
    *   **Action:** Elder targeted by WW. Resolve night.
    *   **Assertion:** Player E `Status` remains Alive. Elder state flag indicates one life lost. Role is *not* revealed.
9.  **Test: Elder - Dies to Second Werewolf Attack**
    *   **Setup:** Player E is Elder, survived first WW attack. Night 2.
    *   **Action:** Elder targeted by WW again. Resolve night.
    *   **Assertion:** Player E `Status` is Dead. Role is revealed.
10. **Test: Elder - Dies to Vote, Disables Powers**
    *   **Setup:** Player E is Elder. Player S is Seer, Player W is Witch.
    *   **Action:** Player E is eliminated by vote.
    *   **Assertion:** Player E `Status` is Dead. Role is revealed. Instruction indicates special powers are lost. Subsequent attempts to use Seer/Witch powers fail or instructions indicate unavailability. (*Verify exact interpretation - does it disable ALL special powers or just Villager ones? Assume ALL for now.*)
11. **Test: Elder - Dies to Witch Poison, Disables Powers**
    *   **Setup:** Player E is Elder. Poisoned by Witch.
    *   **Action:** Resolve night actions.
    *   **Assertion:** Player E `Status` is Dead. Role is revealed. Powers disabled as above.
12. **Test: Bear Tamer - Growl Trigger (WW Adjacent)**
    *   **Setup:** Player BT is Bear Tamer. Player W (Werewolf) is seated adjacent and Alive. Player V (Villager) adjacent other side.
    *   **Action:** Enter Day_Event phase.
    *   **Assertion:** `PendingModeratorInstruction` includes the Bear Tamer's growl indication.
13. **Test: Bear Tamer - No Growl Trigger (No WW Adjacent)**
    *   **Setup:** Player BT is Bear Tamer. Player V1, V2 (Villagers) are seated adjacent.
    *   **Action:** Enter Day_Event phase.
    *   **Assertion:** No growl instruction is generated.
14. **Test: Bear Tamer - Growl Trigger (If BT Infected)**
    *   **Setup:** Player BT is Bear Tamer, infected by Accursed Wolf-Father (is now secretly a WW). Players V1, V2 adjacent.
    *   **Action:** Enter Day_Event phase.
    *   **Assertion:** Growl instruction is generated every day phase from now until BT is eliminated.
15. **Test: Knight - Kills Werewolf on Death**
    *   **Setup:** Player K is Knight. Player W is Werewolf seated to K's left. Player V is Villager seated to W's left. K is killed by WW (from other side).
    *   **Action:** Resolve night actions. Reveal K's death.
    *   **Assertion:** K `Status` is Dead. W `Status` becomes Dead due to rusty sword disease *on the following night's resolution*. Instruction on Day 2 morning indicates W died from disease.
16. **Test: Stuttering Judge - Triggers Second Vote**
    *   **Setup:** Player SJ is Stuttering Judge. Night 1.
    *   **Action:** Call Stuttering Judge. Moderator input indicates SJ uses power via sign. Proceed through Day 1 vote. Player A eliminated.
    *   **Assertion:** After Player A's elimination is resolved, the game immediately triggers a *second* vote phase without debate.
17. **Test: Devoted Servant - Takes Over Eliminated Role**
    *   **Setup:** Player DS is Devoted Servant. Player S (Seer) is eliminated by vote.
    *   **Action:** Before S's role is revealed publicly, DS uses power. Moderator input confirms DS takes role.
    *   **Assertion:** DS `AssignedRole` becomes `SeerRole` (reset state, e.g., sees first time). S's role is not revealed publicly. DS is called as Seer next night.

**IV. Voting & Elimination Mechanics**

1.  **Test: Sheriff Election**
    *   **Setup:** Start of game or designated point.
    *   **Action:** Generate instruction for Sheriff election. Moderator input provides votes. Player S receives most votes.
    *   **Assertion:** Player S `StateFlags` updated `IsSheriff = true`. `GameSession.SheriffPlayerId` is set.
2.  **Test: Sheriff - Double Vote**
    *   **Setup:** Player S is Sheriff. Player A, Player B also voting.
    *   **Action:** Vote phase. Player S votes for Player X. Player A votes for Player Y. Player B votes for Player X. Moderator input provides votes {X: 1+2=3, Y: 1}.
    *   **Assertion:** Player X is eliminated.
3.  **Test: Sheriff - Passes Badge on Death (WW Kill)**
    *   **Setup:** Player S is Sheriff. Player P is another player. Sheriff S is killed by WW.
    *   **Action:** Reveal S's death. Generate instruction for S (moderator acting for dead player) to choose successor. Moderator input selects P.
    *   **Assertion:** S `Status` is Dead. P `StateFlags` updated `IsSheriff = true`. `GameSession.SheriffPlayerId` is updated to P's ID.
4.  **Test: Sheriff - Passes Badge on Death (Vote)**
    *   **Setup:** Player S is Sheriff. Player P is another player. S is eliminated by vote.
    *   **Action:** Resolve S's elimination. Generate instruction for S to choose successor. Moderator input selects P.
    *   **Assertion:** S `Status` is Dead. P `StateFlags` updated `IsSheriff = true`. `GameSession.SheriffPlayerId` is updated to P's ID.
5.  **Test: Scapegoat - Eliminated on Tie**
    *   **Setup:** Player SG is Scapegoat. Player A, Player B tie for most votes.
    *   **Action:** Resolve vote.
    *   **Assertion:** Player SG `Status` is Dead (instead of A or B). Instruction generated for SG to choose who can vote next day.
6.  **Test: Scapegoat - Chooses Voters**
    *   **Setup:** Player SG eliminated on tie. Players X, Y, Z remain.
    *   **Action:** Moderator input indicates SG allows only X and Y to vote next day.
    *   **Assertion:** `GameSession` state updated to reflect only X and Y can vote. Next day's vote instructions reflect this. Z cannot vote.

**V. Victory Conditions**

1.  **Test: Villagers Win**
    *   **Setup:** Only Villager team members (Simple Villagers, Seer, Witch, etc.) remain alive. Last Werewolf just died.
    *   **Action:** Check victory conditions after last WW death/elimination.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Villagers win.
2.  **Test: Werewolves Win - Equal Numbers**
    *   **Setup:** 1 Werewolf, 1 Villager remain alive.
    *   **Action:** Check victory conditions (e.g., start of day).
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Werewolves win.
3.  **Test: Werewolves Win - More Werewolves**
    *   **Setup:** 2 Werewolves, 1 Villager remain alive.
    *   **Action:** Check victory conditions.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Werewolves win.
4.  **Test: Lovers Win (Different Teams)**
    *   **Setup:** Player A (Villager), Player B (Werewolf) are Lovers. All other players eliminated.
    *   **Action:** Check victory conditions after last non-lover eliminated.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Lovers (A & B) win.
5.  **Test: Lovers Win (Same Team - Implicitly Team Win)**
    *   **Setup:** Player A (Villager), Player B (Villager) are Lovers. Last Werewolf eliminated.
    *   **Action:** Check victory conditions.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Villagers win. (Lovers win *with* their team).
6.  **Test: White Werewolf Wins**
    *   **Setup:** Player WW (White Werewolf) is the only player remaining alive.
    *   **Action:** Check victory conditions after last other player eliminated.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares White Werewolf wins.
7.  **Test: Piper Wins**
    *   **Setup:** Player P is Piper. All *currently living* players are in `CharmedPlayerIds`.
    *   **Action:** Check victory conditions after last non-charmed player dies OR after Piper charms the last living non-charmed player.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Piper wins.
8.  **Test: Prejudiced Manipulator Wins**
    *   **Setup:** Player PM is Prejudiced Manipulator, hating Group B. All players in Group B eliminated. PM is alive.
    *   **Action:** Check victory conditions after last Group B member eliminated.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Prejudiced Manipulator wins.
9.  **Test: Angel Wins - Killed Night 1**
    *   **Setup:** Player ANG is Angel.
    *   **Action:** Player ANG killed by WW during Night 1. Resolve night actions.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Angel wins.
10. **Test: Angel Wins - Killed Day 1 Vote**
    *   **Setup:** Player ANG is Angel. Survives Night 1.
    *   **Action:** Player ANG eliminated by Day 1 vote. Resolve vote.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Angel wins.
11. **Test: Angel Wins - Killed Night 2**
    *   **Setup:** Player ANG is Angel. Survives Night 1 and Day 1.
    *   **Action:** Player ANG killed by WW during Night 2. Resolve night actions for Night 2.
    *   **Assertion:** `GamePhase` becomes `GameOver`. `PendingModeratorInstruction` declares Angel wins.
12. **Test: Angel Loses - Survives Past Night 2**
    *   **Setup:** Player ANG is Angel. Survives Night 1, Day 1, Night 2.
    *   **Action:** Enter Day 2 phase.
    *   **Assertion:** Player ANG `AssignedRole` changes to `SimpleVillagerRole` (or effectively becomes one). Angel can no longer win via special condition.

**VI. Event Card Interactions**

*(Structure: Setup involves game state AND having a specific event active or drawn)*

1.  **Test: Event - Full Moon Rising (Night Effect)**
    *   **Setup:** Game state normal. `FullMoonRisingEvent` is activated for the upcoming Night 2. Player S(Seer), W(Witch), H(Hunter) are alive. WW1, WW2 are alive. V1 is Villager.
    *   **Action:** Proceed through Night 2.
        *   WW1 inputs target V_WW1_SeerTarget. WW2 inputs target V_WW2_SeerTarget. (Acting as Seers)
        *   S, W, H are called *together*. Moderator input targets V1 for temporary WW kill.
        *   Normal Seer, Witch, Hunter calls are skipped.
    *   **Assertion:** Night 2 resolution: V1 is marked dead (subject to other saves). V_WW1_SeerTarget and V_WW2_SeerTarget are *not* marked dead by WWs. (Private reveals for WWs about seer targets are optional complexity). Day 2 starts, roles S, W, H revert to normal. Event state removed.
2.  **Test: Event - Somnambulism (Seer Output)**
    *   **Setup:** `SomnambulismEvent` is permanently active. Player S is Seer. Player V1 is Villager. Night phase.
    *   **Action:** Call Seer. Moderator input targets V1.
    *   **Assertion:** `PendingModeratorInstruction` (for public announcement) is "The Seer saw the role: Simple Villager" (or equivalent). It does *not* name V1.
3.  **Test: Event - Enthusiasm (Triggers Second Vote)**
    *   **Setup:** `EnthusiasmEvent` activated for this Day's vote. Player W (Werewolf) is alive.
    *   **Action:** Proceed through vote. Player W is eliminated.
    *   **Assertion:** After W's elimination resolved, game *immediately* enters a second vote phase (`GamePhase.Day_Vote` again, skipping debate). Enthusiasm event state removed.
4.  **Test: Event - Enthusiasm (Does Not Trigger - Villager Eliminated)**
    *   **Setup:** `EnthusiasmEvent` activated. Player V (Villager) is alive.
    *   **Action:** Proceed through vote. Player V is eliminated.
    *   **Assertion:** After V's elimination resolved, game proceeds normally to Night phase. Enthusiasm event state removed.
5.  **Test: Event - Backfire (Transforms Villager)**
    *   **Setup:** `BackfireEvent` active for Night. WWs target Player V (Simple Villager).
    *   **Action:** Resolve Night actions.
    *   **Assertion:** Player V `Status` remains Alive. V `AssignedRole` becomes `SimpleWerewolfRole`. No death occurs from WW attack. Backfire event state removed.
6.  **Test: Event - Backfire (Deflects to Werewolf)**
    *   **Setup:** `BackfireEvent` active. WWs target Player S (Seer). Player WW_Left is first WW to S's left.
    *   **Action:** Resolve Night actions.
    *   **Assertion:** Player S `Status` remains Alive. Player WW_Left `Status` becomes Dead. Backfire event state removed.
7.  **Test: Event - Nightmare (Replaces Vote)**
    *   **Setup:** `NightmareEvent` drawn at start of Day phase. Player L is left of last eliminated.
    *   **Action:** Game enters `AccusationVoting` phase. Follow accusation process: L accuses P1, P1 accuses P2, etc. Player X receives most accusations.
    *   **Assertion:** Player X `Status` becomes Dead. Game proceeds to Night phase (skipping standard debate/vote). Nightmare event resolved.
8.  **Test: Event - Influences (Sequential Vote)**
    *   **Setup:** `InfluencesEvent` drawn. Player L is last eliminated. Player F is chosen by L (via Moderator input) to vote first.
    *   **Action:** Game enters sequential vote: F votes, person left votes, etc. Standard tie rules apply based on final tallies. Player X eliminated.
    *   **Assertion:** Player X `Status` becomes Dead. Game proceeds normally. Influences event resolved.
9.  **Test: Event - Executioner (Hides Role)**
    *   **Setup:** `ExecutionerEvent` active. Player E is Executioner. Player V voted out.
    *   **Action:** Resolve vote elimination for V.
    *   **Assertion:** V `Status` is Dead. `PendingModeratorInstruction` reveals V's role *only* to Player E (via private message/moderator knowledge). Public instruction does not reveal role.
10. **Test: Event - Double Agent (Win Condition)**
    *   **Setup:** `DoubleAgentEvent` active. Player DA is Double Agent (originally Villager). WWs achieve win condition (e.g., WW count >= non-WW count). DA is alive.
    *   **Action:** Check victory conditions.
    *   **Assertion:** `GamePhase` is `GameOver`. `PendingModeratorInstruction` declares Werewolves win. (DA wins with them).
11. **Test: Event - Great Distrust (Replaces Vote)**
    *   **Setup:** `GreatDistrustEvent` drawn.
    *   **Action:** Game enters `FriendVoting` phase. Moderator collects 'friend' votes (e.g., Player A names B, C, D). Player X receives 0 friend votes. Player Y also receives 0.
    *   **Assertion:** Both Player X and Player Y `Status` become Dead. Game proceeds to Night. Great Distrust event resolved.
12. **Test: Event - Spiritualism (Correct Answer)**
    *   **Setup:** `SpiritualismEvent` Card 3 drawn ("Is Player X a Werewolf?"). Player M is Medium. First WW victim was V1. Player X *is* a Werewolf.
    *   **Action:** Moderator input confirms M asks question about Player X.
    *   **Assertion:** `PendingModeratorInstruction` (publicly) is "The spirit answers: Yes". Spiritualism event resolved.
13. **Test: Event - Miracle (Saves Victim, Changes Role)**
    *   **Setup:** `MiracleEvent` activated (e.g., by being attached to a victim effect). Player H (Hunter) targeted by WW.
    *   **Action:** Resolve Night actions.
    *   **Assertion:** Player H `Status` remains Alive. H `AssignedRole` becomes `SimpleVillagerRole`. Miracle event resolved/consumed.
14. **Test: Event - The Little Rascal (Vote Modifier)**
    *   **Setup:** `TheLittleRascalEvent` occurred last turn. Player LR (youngest) returns. Player LR votes for Player X. Player A votes for X. Player B votes for Y. Sheriff is Player A.
    *   **Action:** Resolve vote.
    *   **Assertion:** Player X has 3 (LR) + 1 (A) + 1 (Sheriff) = 5 votes. Player Y has 1 vote. Player X is eliminated. LR vote modifier persists.
15. **Test: Event - Punishment (Vouching Saves)**
    *   **Setup:** `PunishmentEvent` drawn. Last eliminated player L targets Player T. Players V1, V2 are alive.
    *   **Action:** Generate instruction for vouching. Moderator input indicates V1 and V2 vouch for T.
    *   **Assertion:** Player T remains Alive. Punishment event resolved.
16. **Test: Event - Punishment (Not Enough Vouchers)**
    *   **Setup:** `PunishmentEvent` drawn. Player L targets Player T. Only Player V1 vouches.
    *   **Action:** Resolve vouching period.
    *   **Assertion:** Player T `Status` becomes Dead. Punishment event resolved.
17. **Test: Event - The Specter (Transforms Victim, Kills WW)**
    *   **Setup:** `TheSpecterEvent` active for Night. WWs target Player V (Villager). WW1 and WW2 are original Werewolves.
    *   **Action:** Night resolution modified: Moderator wakes V, shows WWs. Moderator input indicates V chooses WW1 to be eliminated.
    *   **Assertion:** V `Status` remains Alive. V `AssignedRole` becomes `SimpleWerewolfRole`. WW1 `Status` becomes Dead. Specter event resolved.
18. **Test: Event - Burial (Hides WW Victim Role)**
    *   **Setup:** `BurialEvent` is permanently active. Player V (Villager) killed by WW.
    *   **Action:** Enter Day_Event phase. Reveal deaths.
    *   **Assertion:** `PendingModeratorInstruction` states "Player V was killed during the night." It does *not* reveal V was a Villager.

**VII. Combined & Edge Cases**

1.  **Test: Witch Heals Protected Target**
    *   **Setup:** Player A is Defender, protects Player B. Player B targeted by WW. Player C is Witch, heals Player B.
    *   **Action:** Resolve night actions.
    *   **Assertion:** Player B remains Alive. Witch's healing potion is used. (Protection was redundant here).
2.  **Test: Witch Poisons Protected Target**
    *   **Setup:** Player A is Defender, protects Player B. Player C is Witch, poisons Player B.
    *   **Action:** Resolve night actions.
    *   **Assertion:** Player B `Status` is Dead. (Poison bypasses protection).
3.  **Test: Hunter Lover Dies by Vote, Kills Other Lover**
    *   **Setup:** Player H (Hunter) and Player L (Lover, non-Hunter) are Lovers.
    *   **Action:** Player H eliminated by vote. Trigger Hunter shot. Moderator input targets Player L.
    *   **Assertion:** H `Status` is Dead. L `Status` becomes Dead (from Hunter shot). *Then* check for Lover death cascade - L is already dead, so no further effect.
4.  **Test: Event Interaction - Enthusiasm + Nightmare**
    *   **Setup:** `EnthusiasmEvent` would be active for next vote. `NightmareEvent` is drawn.
    *   **Action:** Nightmare accusation process occurs. Player W (Werewolf) is eliminated.
    *   **Assertion:** Nightmare vote resolves. Game proceeds to Night. Enthusiasm's condition wasn't met via a *standard* vote, so it does not trigger a second vote. Enthusiasm event is likely discarded or expires without effect. (*Clarify exact rule interaction if needed, this is logical interpretation*).
5.  **Test: Event Interaction - Backfire + Miracle on Same Victim**
    *   **Setup:** `BackfireEvent` active. `MiracleEvent` active (affecting next WW victim). WWs target Player V (Simple Villager).
    *   **Action:** Resolve Night actions.
    *   **Assertion:** Backfire takes precedence (transforms V to Werewolf). Miracle likely has no effect as V wasn't "eliminated". V is Alive and Werewolf. (*Rule priority clarification might be needed, Backfire seems more specific*).
6.  **Test: Event Interaction - Specter + Miracle on Same Victim**
    *   **Setup:** `SpecterEvent` active. `MiracleEvent` active. WWs target Player V (Villager). V chooses WW1 to die.
    *   **Action:** Resolve Night actions.
    *   **Assertion:** Specter transforms V to WW, kills WW1. V remains Alive as WW. Miracle has no elimination to prevent. (*Specter effect seems primary*).