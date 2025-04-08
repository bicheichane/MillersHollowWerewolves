using Werewolves.Core.Services;
using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Roles;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Resources;
using static Werewolves.Core.Tests.TestHelper;
using static Werewolves.Core.Models.ModeratorInput;

namespace Werewolves.Core.Tests;

public class DayPhaseTests
{
    private readonly GameService _gameService = new();


    [Fact]
    public void DayEvent_ProcessRoleReveal_ShouldUpdatePlayerAndProceedToDebate()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
        
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        var pList = session!.Players.Keys.ToList();

        var wolfId = pList[0];
        var victimId = pList[1];

        var inputs = new List<ModeratorInput>
        {
            Confirm(true), // Confirm game start
            Confirm(true), // Confirm night phase start (village sleeps)
            ModeratorInput.SelectPlayers(wolfId), //Identify wolf
            ModeratorInput.SelectPlayer(victimId), //wolf chooses victim
            Confirm(true), // Confirm night phase end (village wakes up)
            ModeratorInput.SelectRole(RoleType.SimpleVillager), // choose role for victim
            //Confirm(true), // Confirm debate phase ended
        };


        // Act
        var result = TestHelper.ProcessInputSequence(_gameService, gameId, inputs);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        var revealedPlayer = session.Players[victimId];
        revealedPlayer.IsRoleRevealed.ShouldBeTrue();
        revealedPlayer.Role.ShouldNotBeNull();
        revealedPlayer.Role.RoleType.ShouldBe(RoleType.SimpleVillager);

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
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
        var gameSession = SimulateGameUntilDayDebatePhase(_gameService, playerNames, roles);
        var input = ModeratorInput.Confirm(true);

        // Act
        var result = _gameService.ProcessModeratorInput(gameSession.Id, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        
        gameSession.ShouldNotBeNull();

        gameSession.GamePhase.ShouldBe(GamePhase.Day_Vote);
        gameSession.PendingModeratorInstruction.ShouldNotBeNull();
        gameSession.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionSingle);
        gameSession.PendingModeratorInstruction.InstructionText.ShouldContain(Resources.GameStrings.VoteOutcomeSelectionPrompt);
        gameSession.PendingModeratorInstruction.SelectablePlayerIds.ShouldNotBeNull();
        gameSession.PendingModeratorInstruction.SelectablePlayerIds.Count.ShouldBe(playerNames.Count);
    }

    [Fact]
    public void DayVote_ProcessPlayerEliminationOutcome_ShouldProceedToResolveVote()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
        var gameSession = SimulateGameUntilVotePhase(_gameService, playerNames, roles);
        var targetPlayerId = gameSession.Players.Values.First(p => p.Role?.RoleType == RoleType.SimpleWerewolf).Id;
        var input = ModeratorInput.SelectPlayer(targetPlayerId);

        // Act
        var result = _gameService.ProcessModeratorInput(gameSession.Id, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        gameSession.ShouldNotBeNull();

        gameSession.GamePhase.ShouldBe(GamePhase.Day_ResolveVote);
        gameSession.PendingVoteOutcome.ShouldBe(targetPlayerId);
        gameSession.GameHistoryLog.OfType<VoteOutcomeReportedLogEntry>()
            .ShouldContain(vol => vol.ReportedOutcomePlayerId == targetPlayerId);
        gameSession.PendingModeratorInstruction.ShouldNotBeNull();
        gameSession.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        gameSession.PendingModeratorInstruction.InstructionText.ShouldBe(Resources.GameStrings.ResolveVotePrompt);
    }

    [Fact]
    public void DayVote_ProcessTieOutcome_ShouldProceedToResolveVote()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
        var gameSession = SimulateGameUntilVotePhase(_gameService, playerNames, roles);
        var input = ModeratorInput.SelectPlayer(Guid.Empty); // Empty selection signifies Tie

        // Act
        var result = _gameService.ProcessModeratorInput(gameSession.Id, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        gameSession.ShouldNotBeNull();

        gameSession.GamePhase.ShouldBe(GamePhase.Day_ResolveVote);
        gameSession.PendingVoteOutcome.ShouldBe(Guid.Empty); // Tie represented by Guid.Empty
        gameSession.GameHistoryLog.OfType<VoteOutcomeReportedLogEntry>()
            .ShouldContain(vol => vol.ReportedOutcomePlayerId == Guid.Empty);
        gameSession.PendingModeratorInstruction.ShouldNotBeNull();
        gameSession.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        gameSession.PendingModeratorInstruction.InstructionText.ShouldBe(Resources.GameStrings.ResolveVotePrompt);
    }

    [Fact]
    public void DayVote_ProcessInvalidSelectionCount_ShouldFail()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
        var gameSession = SimulateGameUntilVotePhase(_gameService, playerNames, roles);
        var p1Id = gameSession.Players.Values.First(p => p.Role?.RoleType == RoleType.SimpleWerewolf).Id;
        var p2Id = gameSession.Players.Values.First(p => p.Role?.RoleType == RoleType.SimpleVillager).Id;
        var input = ModeratorInput.SelectPlayers(new List<Guid> { p1Id, p2Id }); // Select two players

        // Act
        var result = _gameService.ProcessModeratorInput(gameSession.Id, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount);
        // Re-fetch session to check state hasn't changed incorrectly
        gameSession.ShouldNotBeNull();
        gameSession.GamePhase.ShouldBe(GamePhase.Day_Vote); // Should remain in Vote phase
        gameSession.PendingVoteOutcome.ShouldBeNull(); // Outcome should not be stored
    }

    [Fact]
    public void DayResolveVote_ProcessPlayerElimination_ShouldEliminateAndAskForRole()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
        
        var targetPlayerId = gameSession.Players.Values.First(p => p.Role?.RoleType == RoleType.SimpleWerewolf).Id;
        

        // Act
        var gameSession = SimulateGameUntilResolveVotePhase(_gameService, playerNames, roles, targetPlayerId);



        // Assert
        result.IsSuccess.ShouldBeTrue();
        gameSession.ShouldNotBeNull();

        gameSession.Players[targetPlayerId].Status.ShouldBe(PlayerStatus.Dead);
        gameSession.PendingVoteOutcome.ShouldBeNull(); // Should be cleared

        gameSession.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == targetPlayerId && pel.Reason == EliminationReason.DayVote);
        gameSession.GameHistoryLog.OfType<VoteResolvedLogEntry>()
            .ShouldContain(vrl => vrl.EliminatedPlayerId == targetPlayerId && !vrl.WasTie);

        gameSession.GamePhase.ShouldBe(GamePhase.Day_Event); // Back to reveal role
        gameSession.PendingModeratorInstruction.ShouldNotBeNull();
        gameSession.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.RoleSelection);
        gameSession.PendingModeratorInstruction.InstructionText.ShouldContain(string.Format(Resources.GameStrings.VoteEliminatedAnnounce, gameSession.Players[targetPlayerId].Name));
        gameSession.PendingModeratorInstruction.InstructionText.ShouldContain(Resources.GameStrings.RevealRolePromptSpecify);
        gameSession.PendingModeratorInstruction.AffectedPlayerIds.ShouldBe(new[] { targetPlayerId });
    }

    [Fact]
    public void DayResolveVote_ProcessTie_ShouldProceedToNight()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
        var gameSession = SimulateGameUntilResolveVotePhase(_gameService, playerNames, roles, Guid.Empty); // Empty for tie
        var input = ModeratorInput.Confirm(true);

        // Act
        var result = _gameService.ProcessModeratorInput(gameSession.Id, input);



        // Assert
        result.IsSuccess.ShouldBeTrue();
        gameSession.ShouldNotBeNull();

        gameSession.Players.Values.ShouldAllBe(p => p.Status == PlayerStatus.Alive);
        gameSession.PendingVoteOutcome.ShouldBeNull(); // Should be cleared

        gameSession.GameHistoryLog.OfType<PlayerEliminatedLogEntry>().ShouldBeEmpty();
        gameSession.GameHistoryLog.OfType<VoteResolvedLogEntry>()
            .ShouldContain(vrl => vrl.EliminatedPlayerId == null && vrl.WasTie);

        gameSession.GamePhase.ShouldBe(GamePhase.Night); // Should transition to night
        gameSession.TurnNumber.ShouldBe(2); // Turn should increment
        gameSession.PendingModeratorInstruction.ShouldNotBeNull();
        gameSession.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        gameSession.PendingModeratorInstruction.InstructionText.ShouldBe(Resources.GameStrings.ProceedToNightPrompt);
    }

    // --- Victory Condition Tests removed from here ---
} 