using Shouldly;
using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Resources;
using Werewolves.Core.Services;
using Xunit;
using Xunit.Abstractions;
using static Werewolves.Core.Tests.TestHelper;
using static Werewolves.Core.Tests.TestModeratorInput;

namespace Werewolves.Core.Tests;

public class DayPhaseTests
{
    private readonly GameService _gameService = new();

    private readonly ITestOutputHelper _output;

    public DayPhaseTests(ITestOutputHelper output)
    {
        _output = output;
    }


    [Fact]
    public void DayEvent_ProcessRoleReveal_ShouldUpdatePlayerAndProceedToDebate()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();

        var wolfId = pList[0];
        var victimId = pList[1];

        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true), // Confirm game start
            Confirm(GamePhase.Night_RoleAction, true), // Confirm night phase start (village sleeps)
            SelectPlayers(GamePhase.Night_RoleAction, wolfId), //Identify wolf
            SelectPlayer(GamePhase.Night_RoleAction, victimId), //wolf chooses victim
            Confirm(GamePhase.Day_ResolveNight, true), // Confirm night phase end (village wakes up)
            AssignPlayerRoles(GamePhase.Day_Event, new(){{victimId, RoleType.SimpleVillager}}), // choose role for victim
        };


        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        var revealedPlayer = session.Players[victimId];
        revealedPlayer.IsRoleRevealed.ShouldBeTrue();
        revealedPlayer.RoleType.ShouldNotBeNull();
        revealedPlayer.RoleType.RoleType.ShouldBe(RoleType.SimpleVillager);

        session.GameHistoryLog.OfType<RoleRevealedLogEntry>()
            .ShouldContain(rl => rl.PlayerId == victimId && rl.RevealedRole == RoleType.SimpleVillager);

        session.GamePhase.ShouldBe(GamePhase.Day_Debate);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
    }

    [Fact]
    public void DayDebate_ProcessConfirmation_ShouldProceedToVote()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var wolfId = pList[0];
        var victimId = pList[1]; // Assuming a victim to reach Day_Debate via Day_Event

        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),                 // -> Night
            Confirm(GamePhase.Night_RoleAction, true),                 // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, wolfId),         // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, victimId),        // -> Day_ResolveNight
            Confirm(GamePhase.Day_ResolveNight, true),      // -> Day_Event (Reveal Victim Role)
            AssignPlayerRoles(GamePhase.Day_Event, new(){{victimId, RoleType.SimpleVillager}}), // -> Day_Debate
            Confirm(GamePhase.Day_Debate, true),             // -> Day_Vote
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.GamePhase.ShouldBe(GamePhase.Day_Vote);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionSingle);
        session.PendingModeratorInstruction.SelectablePlayerIds.ShouldNotBeNull();
        session.PendingModeratorInstruction.SelectablePlayerIds.Count.ShouldBe(playerNames.Count - 1); //player killed by WW
    }

    [Fact]
    public void DayVote_ProcessPlayerEliminationOutcome_ShouldProceedToResolveVote()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var wolfId = pList[0];
        var victimId = pList[1];
        // We need the target player ID for the final step
        var targetPlayerId = pList[2];

        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),                 // -> Night
            Confirm(GamePhase.Night_RoleAction, true),                 // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, wolfId),         // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, victimId),        // -> Day_ResolveNight
            Confirm(GamePhase.Day_ResolveNight, true),      // -> Day_Event (Reveal Victim Role)
            AssignPlayerRoles(GamePhase.Day_Event, new(){{victimId, RoleType.SimpleVillager}}), // -> Day_Debate
            Confirm(GamePhase.Day_Debate, true),            // -> Day_Vote
            SelectPlayer(GamePhase.Day_Vote, targetPlayerId), // -> Day_ResolveVote
            //Confirm(GamePhase.Day_ResolveVote, true)        // -> Day_Event (Reveal Eliminated Role)
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.GamePhase.ShouldBe(GamePhase.Day_ResolveVote);
        session.PendingVoteOutcome.ShouldBe(targetPlayerId);
        session.GameHistoryLog.OfType<VoteOutcomeReportedLogEntry>()
            .ShouldContain(vol => vol.ReportedOutcomePlayerId == targetPlayerId);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
    }

    [Fact]
    public void DayVote_ProcessTieOutcome_ShouldProceedToResolveVote()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var wolfId = pList[0];
        var victimId = pList[1];

        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),                 // -> Night
            Confirm(GamePhase.Night_RoleAction, true),                 // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, wolfId),         // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, victimId),        // -> Day_ResolveNight
            Confirm(GamePhase.Day_ResolveNight, true),      // -> Day_Event (Reveal Victim Role)
            AssignPlayerRoles(GamePhase.Day_Event, new(){{victimId, RoleType.SimpleVillager}}), // -> Day_Debate
            Confirm(GamePhase.Day_Debate, true),            // -> Day_Vote
            SelectPlayer(GamePhase.Day_Vote, Guid.Empty),    // -> Day_ResolveVote (Tie)
            //Confirm(GamePhase.Day_ResolveVote, true)        // -> Night (Turn 2)
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.GamePhase.ShouldBe(GamePhase.Day_ResolveVote);
        session.PendingVoteOutcome.ShouldBe(Guid.Empty); // Tie represented by Guid.Empty
        session.GameHistoryLog.OfType<VoteOutcomeReportedLogEntry>()
            .ShouldContain(vol => vol.ReportedOutcomePlayerId == Guid.Empty);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
    }

    [Fact]
    public void DayVote_ProcessInvalidSelectionCount_ShouldFail()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var wolfId = pList[0];
        var victimId = pList[1];

        // Setup sequence to reach Day_Vote
        var setupInputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),                 // -> Night
            Confirm(GamePhase.Night_RoleAction, true),                 // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, wolfId),         // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, victimId),        // -> Day_ResolveNight
            Confirm(GamePhase.Day_ResolveNight, true),      // -> Day_Event (Reveal Victim Role)
            AssignPlayerRoles(GamePhase.Day_Event, new(){{victimId, RoleType.SimpleVillager}}), // -> Day_Debate
            Confirm(GamePhase.Day_Debate, true)             // -> Day_Vote
        };
        var setupResult = ProcessInputSequence(_gameService, gameId, setupInputs);
        setupResult.IsSuccess.ShouldBeTrue("Setup for the test failed");

        // The actual input to test
        var invalidInput = SelectPlayers(GamePhase.Day_Vote, new List<Guid>{ wolfId, victimId }); // Select two players

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, invalidInput);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InputTypeMismatch);
        
        // Re-fetch session to check state hasn't changed incorrectly
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Day_Vote); // Should remain in Vote phase
        session.PendingVoteOutcome.ShouldBeNull(); // Outcome should not be stored
    }

    [Fact]
    public void DayResolveVote_ProcessPlayerElimination_ShouldEliminateAndAskForRole()
    {
        // Arrange
        var playerNames = GetPlayerNames(6);
        var roles = GetRoles(4,2);
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var wolfId = pList[0];
        var victimId = pList[1];
        var wolf2Id = pList[2];


        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),                 // -> Night
            Confirm(GamePhase.Night_RoleAction, true),                 // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, [wolfId, wolf2Id]),         // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, victimId),        // -> Day_ResolveNight
            Confirm(GamePhase.Day_ResolveNight, true),      // -> Day_Event (Reveal Victim Role)
            AssignPlayerRoles(GamePhase.Day_Event, new(){{victimId, RoleType.SimpleVillager}}), // -> Day_Debate
            Confirm(GamePhase.Day_Debate, true),            // -> Day_Vote
            SelectPlayer(GamePhase.Day_Vote, wolfId), // -> Day_ResolveVote
            Confirm(GamePhase.Day_ResolveVote, true)        // -> Day_Event (Reveal Eliminated Role)
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.Players[wolfId].Status.ShouldBe(PlayerStatus.Dead);
        session.PendingVoteOutcome.ShouldBeNull(); // Should be cleared

        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == wolfId && pel.Reason == EliminationReason.DayVote);
        session.GameHistoryLog.OfType<VoteResolvedLogEntry>()
            .ShouldContain(vrl => vrl.EliminatedPlayerId == wolfId && !vrl.WasTie);

        session.GamePhase.ShouldBe(GamePhase.Day_Event); // Back to reveal role
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.AssignPlayerRoles);
        session.PendingModeratorInstruction.AffectedPlayerIds.ShouldBe(new[] { wolfId });
    }

    [Fact]
    public void DayResolveVote_ProcessTie_ShouldProceedToNight()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();
        var wolfId = pList[0];
        var victimId = pList[1];

        var inputs = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),                 // -> Night
            Confirm(GamePhase.Night_RoleAction, true),                 // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, wolfId),         // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, victimId),        // -> Day_ResolveNight
            Confirm(GamePhase.Day_ResolveNight, true),      // -> Day_Event (Reveal Victim Role)
            AssignPlayerRoles(GamePhase.Day_Event, new(){{victimId, RoleType.SimpleVillager}}), // -> Day_Debate
            Confirm(GamePhase.Day_Debate, true),            // -> Day_Vote
            SelectPlayer(GamePhase.Day_Vote, null),   // -> Day_ResolveVote (Tie)
            Confirm(GamePhase.Day_ResolveVote, true)        // -> Night (Turn 2)
        };

        // Act
        var result = ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.Players.Values.Where(p => p.Id != victimId).ShouldAllBe(p => p.Status == PlayerStatus.Alive);
        session.PendingVoteOutcome.ShouldBeNull(); // Should be cleared

        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>().Where(e => e.Reason == EliminationReason.DayVote).ShouldBeEmpty();
        session.GameHistoryLog.OfType<VoteResolvedLogEntry>()
            .ShouldContain(vrl => vrl.EliminatedPlayerId == null && vrl.WasTie);

        session.GamePhase.ShouldBe(GamePhase.Night_RoleAction); // Should transition to night
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
    }

    // --- Victory Condition Tests removed from here ---
} 