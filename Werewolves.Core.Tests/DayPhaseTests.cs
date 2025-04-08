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

        var wolfPlayerIndex = 0;
        var eliminatedPlayerIndex = 1;
        
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
        session.PendingModeratorInstruction.InstructionText.ShouldContain(Resources.GameStrings.ProceedToVotePrompt);
    }

    [Fact]
    public void DayDebate_ProcessConfirmation_ShouldProceedToVote()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" };
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        // Use the new simulation helper instead of direct state manipulation
        var gameId = TestHelper.SimulateGameUntilDebatePhase(_gameService, playerNames, roles);
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.GamePhase.ShouldBe(GamePhase.Day_Vote);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionSingle);
        session.PendingModeratorInstruction.InstructionText.ShouldContain(Resources.GameStrings.VoteOutcomeSelectionPrompt);
        session.PendingModeratorInstruction.SelectablePlayerIds.ShouldNotBeNull();
        // Assuming all players start alive and no one died during the first night simulation
        session.PendingModeratorInstruction.SelectablePlayerIds.Count.ShouldBe(playerNames.Count);
    }

    [Fact]
    public void DayVote_ProcessPlayerEliminationOutcome_ShouldProceedToResolveVote()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = _gameService.StartNewGame(playerNames, roles); // Uses refactored helper now
        var session = _gameService.GetGameStateView(gameId);
        var targetPlayerId = session.Players.Values.First(p => p.Name == "Alice").Id; // Target the WW
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { targetPlayerId } };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

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
        session.PendingModeratorInstruction.InstructionText.ShouldBe(Resources.GameStrings.ResolveVotePrompt);
    }

    [Fact]
    public void DayVote_ProcessTieOutcome_ShouldProceedToResolveVote()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" };
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = _gameService.StartNewGame(playerNames, roles); // Uses refactored helper now
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid>() }; // Empty list signifies Tie

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.GamePhase.ShouldBe(GamePhase.Day_ResolveVote);
        session.PendingVoteOutcome.ShouldBe(Guid.Empty); // Tie represented by Guid.Empty
        session.GameHistoryLog.OfType<VoteOutcomeReportedLogEntry>()
            .ShouldContain(vol => vol.ReportedOutcomePlayerId == Guid.Empty);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        session.PendingModeratorInstruction.InstructionText.ShouldBe(Resources.GameStrings.ResolveVotePrompt);
    }

    [Fact]
    public void DayVote_ProcessInvalidSelectionCount_ShouldFail()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var gameId = _gameService.StartNewGame(playerNames, roles); // Uses refactored helper now
        var session = _gameService.GetGameStateView(gameId);
        var p1Id = session.Players.Values.First(p => p.Name == "Alice").Id;
        var p2Id = session.Players.Values.First(p => p.Name == "Bob").Id;
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { p1Id, p2Id } }; // Select two players

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount);
        // Re-fetch session to check state hasn't changed incorrectly
        var postFailSession = _gameService.GetGameStateView(gameId);
        postFailSession.ShouldNotBeNull();
        postFailSession.GamePhase.ShouldBe(GamePhase.Day_Vote); // Should remain in Vote phase
        postFailSession.PendingVoteOutcome.ShouldBeNull(); // Outcome should not be stored
    }

    [Fact]
    public void DayResolveVote_ProcessPlayerElimination_ShouldEliminateAndAskForRole()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        // Need to refactor SetupGameForVoteResolution to use simulation
        var sessionSetup = _gameService.GetGameStateView(_gameService.StartNewGame(playerNames, roles));
        var targetPlayerId = sessionSetup.Players.Values.First(p => p.Name == "Alice").Id;
        var gameId = _gameService.StartNewGame(playerNames, roles); // Uses refactored helper indirectly
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.Players[targetPlayerId].Status.ShouldBe(PlayerStatus.Dead);
        session.PendingVoteOutcome.ShouldBeNull(); // Should be cleared

        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == targetPlayerId && pel.Reason == EliminationReason.DayVote);
        session.GameHistoryLog.OfType<VoteResolvedLogEntry>()
            .ShouldContain(vrl => vrl.EliminatedPlayerId == targetPlayerId && !vrl.WasTie);

        session.GamePhase.ShouldBe(GamePhase.Day_Event); // Back to reveal role
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.RoleSelection);
        session.PendingModeratorInstruction.InstructionText.ShouldContain(string.Format(GameStrings.VoteEliminatedAnnounce, session.Players[targetPlayerId].Name));
        session.PendingModeratorInstruction.InstructionText.ShouldContain(GameStrings.RevealRolePromptSpecify);
        session.PendingModeratorInstruction.AffectedPlayerIds.ShouldBe(new[] { targetPlayerId });
    }

    [Fact]
    public void DayResolveVote_ProcessTie_ShouldProceedToNight()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        // Need to refactor SetupGameForVoteResolution to use simulation
        var gameId = _gameService.StartNewGame(playerNames, roles); // Setup with a Tie outcome; uses refactored helper indirectly
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert: No one eliminated, logs updated, transition to next night prompt
        result.IsSuccess.ShouldBeTrue();
        var session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.Players.Values.ShouldAllBe(p => p.Status == PlayerStatus.Alive);
        session.PendingVoteOutcome.ShouldBeNull(); // Should be cleared

        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>().ShouldBeEmpty();
        session.GameHistoryLog.OfType<VoteResolvedLogEntry>()
            .ShouldContain(vrl => vrl.EliminatedPlayerId == null && vrl.WasTie);

        session.GamePhase.ShouldBe(GamePhase.Night); // Should transition to night
        session.TurnNumber.ShouldBe(2); // Turn should increment
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        // Use resource key for the prompt
        session.PendingModeratorInstruction.InstructionText.ShouldBe(GameStrings.ProceedToNightPrompt);
    }

    // --- Victory Condition Tests removed from here ---
} 