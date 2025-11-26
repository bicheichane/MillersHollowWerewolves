using FluentAssertions;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models.Instructions;
using Werewolves.StateModels.Log;
using Werewolves.Tests.Helpers;
using Xunit;

namespace Werewolves.Tests.Integration;

/// <summary>
/// Tests for night phase actions: werewolf attacks, seer investigations, action logging.
/// Test IDs: NA-001 through NA-022
/// </summary>
public class NightActionTests
{
    #region NA-001 to NA-003: Werewolf Actions

    /// <summary>
    /// NA-001: Werewolves wake and select victim, action is logged.
    /// On Night 1, werewolves need to be identified first, then select a victim.
    /// </summary>
    [Fact]
    public void Werewolves_WakeAndSelectVictim_LogsNightAction()
    {
        // Arrange - 4 players: 1 WW, 1 Seer, 2 Villagers
        var builder = GameTestBuilder.Create()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Confirm night starts
        ConfirmNightStart(builder);

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        // Night 1: First role to wake is Werewolf (based on hook order after any first-night-only roles)
        // The instruction should be to identify the werewolf player
        var wwInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction(),
            "Werewolf wake/identify instruction");

        // Identify player 0 as the werewolf
        var werewolfPlayer = players[0];
        var identifyResponse = wwInstruction.CreateResponse([werewolfPlayer.Id]);
        var afterIdentify = builder.Process(identifyResponse);

        // Now we should get victim selection instruction
        var victimInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterIdentify,
            "Werewolf victim selection");

        // Select a villager as victim (player 3 should be a villager)
        var victimPlayer = players[3];
        var victimResponse = victimInstruction.CreateResponse([victimPlayer.Id]);
        builder.Process(victimResponse);

        // Assert - Check that a NightActionLogEntry was created
        var updatedState = builder.GetGameState()!;
        var nightActions = updatedState.GameHistoryLog
            .OfType<NightActionLogEntry>()
            .Where(e => e.ActionType == NightActionType.WerewolfVictimSelection)
            .ToList();

        nightActions.Should().HaveCount(1);
        nightActions[0].TargetIds.Should().BeEquivalentTo([victimPlayer.Id]);
    }

    /// <summary>
    /// NA-002: Werewolves cannot select another werewolf as victim.
    /// </summary>
    [Fact]
    public void Werewolves_CannotSelectWerewolf_AsVictim()
    {
        // Arrange - 5 players: 2 WW, 1 Seer, 2 Villagers
        var builder = GameTestBuilder.Create()
            .WithSimpleGame(playerCount: 5, werewolfCount: 2, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Confirm night starts
        ConfirmNightStart(builder);

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        // Get werewolf identification instruction
        var wwInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction(),
            "Werewolf wake/identify instruction");

        // Identify players 0 and 1 as werewolves
        var werewolf1 = players[0];
        var werewolf2 = players[1];
        var identifyResponse = wwInstruction.CreateResponse([werewolf1.Id, werewolf2.Id]);
        var afterIdentify = builder.Process(identifyResponse);

        // Get victim selection instruction
        var victimInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterIdentify,
            "Werewolf victim selection");

        // Assert - Werewolves should not be in selectable targets
        victimInstruction.SelectablePlayerIds.Should().NotContain(werewolf1.Id);
        victimInstruction.SelectablePlayerIds.Should().NotContain(werewolf2.Id);
    }

    /// <summary>
    /// NA-003: Werewolves complete the full wake-select-sleep cycle.
    /// </summary>
    [Fact]
    public void Werewolves_CompleteFlow_WakeSleepCycle()
    {
        // Arrange
        var builder = GameTestBuilder.Create()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Confirm night starts
        ConfirmNightStart(builder);

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        // Step 1: Identify werewolf
        var wwInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction());
        var identifyResponse = wwInstruction.CreateResponse([players[0].Id]);
        var result1 = builder.Process(identifyResponse);

        // Step 2: Select victim
        var victimInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(result1);
        var victimResponse = victimInstruction.CreateResponse([players[3].Id]);
        var result2 = builder.Process(victimResponse);

        // Step 3: Confirm sleep
        var sleepInstruction = InstructionAssert.ExpectSuccessWithType<ConfirmationInstruction>(
            result2,
            "Werewolf sleep confirmation");
        var sleepResponse = sleepInstruction.CreateResponse(true);
        var result3 = builder.Process(sleepResponse);

        // Assert - Should have moved to next role (Seer) or completed night
        result3.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region NA-010 to NA-012: Seer Actions

    /// <summary>
    /// NA-010: Seer checks a werewolf and receives appropriate feedback.
    /// </summary>
    [Fact]
    public void Seer_ChecksWerewolf_ActionIsLogged()
    {
        // Arrange - Complete werewolf actions first to get to Seer
        var builder = GameTestBuilder.Create()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Confirm night starts
        ConfirmNightStart(builder);

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();
        var werewolfPlayer = players[0];
        var seerPlayer = players[1]; // Seer is second in role order
        var villagerPlayer = players[3];

        // Complete werewolf actions
        CompleteWerewolfNightAction(builder, [werewolfPlayer.Id], villagerPlayer.Id);

        // Now Seer should wake up - first instruction is to identify
        var seerIdentifyInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction(),
            "Seer identification");
        var seerIdentifyResponse = seerIdentifyInstruction.CreateResponse([seerPlayer.Id]);
        var afterSeerIdentify = builder.Process(seerIdentifyResponse);

        // Get Seer's target selection instruction
        var seerTargetInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterSeerIdentify,
            "Seer target selection");

        // Seer checks the werewolf
        var seerTargetResponse = seerTargetInstruction.CreateResponse([werewolfPlayer.Id]);
        builder.Process(seerTargetResponse);

        // Assert - Check that a SeerCheck action was logged
        var updatedState = builder.GetGameState()!;
        var seerActions = updatedState.GameHistoryLog
            .OfType<NightActionLogEntry>()
            .Where(e => e.ActionType == NightActionType.SeerCheck)
            .ToList();

        seerActions.Should().HaveCount(1);
        seerActions[0].TargetIds.Should().BeEquivalentTo([werewolfPlayer.Id]);
    }

    /// <summary>
    /// NA-011: Seer checks a villager and action is logged.
    /// </summary>
    [Fact]
    public void Seer_ChecksVillager_ActionIsLogged()
    {
        // Arrange
        var builder = GameTestBuilder.Create()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Confirm night starts
        ConfirmNightStart(builder);

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();
        var werewolfPlayer = players[0];
        var seerPlayer = players[1];
        var villagerToCheck = players[2]; // Check a villager
        var victimPlayer = players[3];

        // Complete werewolf actions
        CompleteWerewolfNightAction(builder, [werewolfPlayer.Id], victimPlayer.Id);

        // Identify seer
        var seerIdentifyInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction());
        var seerIdentifyResponse = seerIdentifyInstruction.CreateResponse([seerPlayer.Id]);
        var afterSeerIdentify = builder.Process(seerIdentifyResponse);

        // Seer checks a villager
        var seerTargetInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterSeerIdentify);
        var seerTargetResponse = seerTargetInstruction.CreateResponse([villagerToCheck.Id]);
        builder.Process(seerTargetResponse);

        // Assert
        var updatedState = builder.GetGameState()!;
        var seerActions = updatedState.GameHistoryLog
            .OfType<NightActionLogEntry>()
            .Where(e => e.ActionType == NightActionType.SeerCheck)
            .ToList();

        seerActions.Should().HaveCount(1);
        seerActions[0].TargetIds.Should().BeEquivalentTo([villagerToCheck.Id]);
    }

    /// <summary>
    /// NA-012: Seer action is logged with correct details.
    /// </summary>
    [Fact]
    public void Seer_ActionLogged_WithCorrectDetails()
    {
        // Arrange
        var builder = GameTestBuilder.Create()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Confirm night starts
        ConfirmNightStart(builder);

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();

        // Complete werewolf and seer actions
        CompleteWerewolfNightAction(builder, [players[0].Id], players[3].Id);
        CompleteSeerNightAction(builder, players[1].Id, players[2].Id);

        // Assert
        var updatedState = builder.GetGameState()!;
        var seerAction = updatedState.GameHistoryLog
            .OfType<NightActionLogEntry>()
            .First(e => e.ActionType == NightActionType.SeerCheck);

        seerAction.TurnNumber.Should().Be(1);
        seerAction.CurrentPhase.Should().Be(GamePhase.Night);
        seerAction.TargetIds.Should().BeEquivalentTo([players[2].Id]);
    }

    #endregion

    #region NA-020 to NA-022: Edge Cases

    /// <summary>
    /// NA-020: Seer targeted on Night 1 still acts before Dawn.
    /// Death only resolves at Dawn, so Seer performs their action.
    /// </summary>
    [Fact]
    public void Seer_TargetedNight1_StillActsBeforeDawn()
    {
        // Arrange
        var builder = GameTestBuilder.Create()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Confirm night starts
        ConfirmNightStart(builder);

        var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();
        var werewolfPlayer = players[0];
        var seerPlayer = players[1];

        // Werewolf targets the Seer
        CompleteWerewolfNightAction(builder, [werewolfPlayer.Id], seerPlayer.Id);

        // Seer should still wake up and act (death resolves at Dawn)
        var seerInstruction = builder.GetCurrentInstruction();
        seerInstruction.Should().BeOfType<SelectPlayersInstruction>(
            "Seer should still wake up even if targeted");

        // Complete Seer's action
        var seerIdentify = InstructionAssert.ExpectType<SelectPlayersInstruction>(seerInstruction);
        var identifyResponse = seerIdentify.CreateResponse([seerPlayer.Id]);
        var afterIdentify = builder.Process(identifyResponse);

        var seerTarget = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(afterIdentify);
        var targetResponse = seerTarget.CreateResponse([werewolfPlayer.Id]);
        builder.Process(targetResponse);

        // Assert - Seer's action should be logged
        var updatedState = builder.GetGameState()!;
        var seerActions = updatedState.GameHistoryLog
            .OfType<NightActionLogEntry>()
            .Where(e => e.ActionType == NightActionType.SeerCheck)
            .ToList();

        seerActions.Should().HaveCount(1, "Seer should have acted despite being targeted");
    }

    /// <summary>
    /// NA-021: Seer killed Night 1 cannot act on Night 2.
    /// </summary>
    [Fact(Skip = "Requires full Night→Dawn→Day→Night cycle to test")]
    public void Seer_KilledNight1_CannotActNight2()
    {
        // This test requires:
        // 1. Complete Night 1 where werewolf kills Seer
        // 2. Process Dawn (Seer eliminated)
        // 3. Complete Day (vote someone out or no vote)
        // 4. Start Night 2
        // 5. Verify Seer is skipped
    }

    /// <summary>
    /// NA-022: First night role identification records role correctly.
    /// </summary>
    [Fact]
    public void FirstNight_RoleIdentification_RecordsRoleCorrectly()
    {
        // Arrange
        var builder = GameTestBuilder.Create()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Confirm night starts
        ConfirmNightStart(builder);

		var gameState = builder.GetGameState()!;
        var players = gameState.GetPlayers().ToList();
        var werewolfPlayer = players[0];

        // Initially, player should have no role assigned
        werewolfPlayer.State.MainRole.Should().BeNull("Role not yet identified");

        // Identify werewolf
        var wwInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction());
        var identifyResponse = wwInstruction.CreateResponse([werewolfPlayer.Id]);
        builder.Process(identifyResponse);

        // Assert - Role should now be assigned
        var updatedState = builder.GetGameState()!;
        var updatedPlayer = updatedState.GetPlayer(werewolfPlayer.Id);
        updatedPlayer.State.MainRole.Should().Be(MainRoleType.SimpleWerewolf);

        // Also verify via log entry
        var roleAssignments = updatedState.GameHistoryLog
            .OfType<AssignRoleLogEntry>()
            .Where(e => e.PlayerIds.Contains(werewolfPlayer.Id))
            .ToList();

        roleAssignments.Should().HaveCount(1);
        roleAssignments[0].AssignedMainRole.Should().Be(MainRoleType.SimpleWerewolf);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Confirms the "night starts" instruction that precedes the hook loop.
    /// </summary>
    private static void ConfirmNightStart(GameTestBuilder builder)
    {
        var nightStartInstruction = InstructionAssert.ExpectType<ConfirmationInstruction>(
            builder.GetCurrentInstruction(),
            "Night start confirmation");
        var response = nightStartInstruction.CreateResponse(true);
        builder.Process(response);
    }

    /// <summary>
    /// Completes the werewolf night action sequence: identify (if Night 1) → select victim → confirm sleep.
    /// </summary>
    private static void CompleteWerewolfNightAction(GameTestBuilder builder, List<Guid> werewolfIds, Guid victimId)
    {
        // Identify werewolves
        var identifyInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction(),
            "Werewolf identification");
        var identifyResponse = identifyInstruction.CreateResponse(werewolfIds);
        var afterIdentify = builder.Process(identifyResponse);

        // Select victim
        var victimInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterIdentify,
            "Werewolf victim selection");
        var victimResponse = victimInstruction.CreateResponse([victimId]);
        var afterVictim = builder.Process(victimResponse);

        // Confirm sleep
        var sleepInstruction = InstructionAssert.ExpectSuccessWithType<ConfirmationInstruction>(
            afterVictim,
            "Werewolf sleep confirmation");
        var sleepResponse = sleepInstruction.CreateResponse(true);
        builder.Process(sleepResponse);
    }

    /// <summary>
    /// Completes the Seer night action sequence: identify (if Night 1) → select target → confirm sleep.
    /// </summary>
    private static void CompleteSeerNightAction(GameTestBuilder builder, Guid seerId, Guid targetId)
    {
        // Identify seer
        var identifyInstruction = InstructionAssert.ExpectType<SelectPlayersInstruction>(
            builder.GetCurrentInstruction(),
            "Seer identification");
        var identifyResponse = identifyInstruction.CreateResponse([seerId]);
        var afterIdentify = builder.Process(identifyResponse);

        // Select target
        var targetInstruction = InstructionAssert.ExpectSuccessWithType<SelectPlayersInstruction>(
            afterIdentify,
            "Seer target selection");
        var targetResponse = targetInstruction.CreateResponse([targetId]);
        var afterTarget = builder.Process(targetResponse);

        // Confirm sleep
        var sleepInstruction = InstructionAssert.ExpectSuccessWithType<ConfirmationInstruction>(
            afterTarget,
            "Seer sleep confirmation");
        var sleepResponse = sleepInstruction.CreateResponse(true);
        builder.Process(sleepResponse);
    }

    #endregion
}
