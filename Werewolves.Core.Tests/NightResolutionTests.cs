using Werewolves.Core.Services;
using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Resources; // Add using statement

namespace Werewolves.Core.Tests;

public class NightResolutionTests
{
    private readonly GameService _gameService = new();

    // Helper to advance game to Day_ResolveNight after WW action
    private Guid SetupGameAndAdvanceToResolveNight(List<string> playerNames, List<RoleType> roles, Guid victimId)
    {
        var gameId = _gameService.StartNewGame(playerNames, roles);
        // Confirm Setup -> Night
        _gameService.ProcessModeratorInput(gameId, new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true });
        // Process WW Action
        var wwInput = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { victimId } };
        var wwResult = _gameService.ProcessModeratorInput(gameId, wwInput);
        wwResult.IsSuccess.ShouldBeTrue();
        _gameService.GetGameStateView(gameId)?.GamePhase.ShouldBe(GamePhase.Day_ResolveNight);
        return gameId;
    }

    [Fact]
    public void DayResolveNight_ProcessWerewolfKill_ShouldEliminateVictim()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" }; // WW, V, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var victimName = "Bob";

        // Get victimId first to pass to helper
        var tempGameId = _gameService.StartNewGame(playerNames, roles);
        var tempSession = _gameService.GetGameStateView(tempGameId);
        var victimId = tempSession.Players.Values.First(p => p.Name == victimName).Id;
        // Discard temp game, start fresh with helper

        var gameId = SetupGameAndAdvanceToResolveNight(playerNames, roles, victimId);
        var session = _gameService.GetGameStateView(gameId); // Session is now in Day_ResolveNight

        // The helper already processed the WW action, logging it to GameHistoryLog
        // No need to manually add log entries
        session.GameHistoryLog.OfType<NightActionLogEntry>().ShouldContain(nal => nal.TargetId == victimId);

        session.PendingModeratorInstruction = new ModeratorInstruction
        {
            InstructionText = $"Announce {victimName} eliminated...", // Simulate the announcement instruction
            ExpectedInputType = ExpectedInputType.Confirmation
        };

        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        var victimPlayer = session.Players[victimId];
        victimPlayer.Status.ShouldBe(PlayerStatus.Dead);

        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == victimId && pel.Reason == EliminationReason.WerewolfAttack);

        // Removed checks for NightActionsLog and WerewolfVictimChoiceLogEntry

        session.GamePhase.ShouldBe(GamePhase.Day_Event); // Should move to reveal role
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.AssignPlayerRoles);
        // Use resource key for the prompt format if available, otherwise check content
        session.PendingModeratorInstruction.InstructionText.ShouldContain(victimName);
        session.PendingModeratorInstruction.InstructionText.ShouldContain(GameStrings.RevealRolePromptSpecify);
        session.PendingModeratorInstruction.AffectedPlayerIds.ShouldBe(new[] { victimId });
    }

    [Fact]
    public void DayResolveNight_NoVictim_ShouldProceedToDebate()
    {
        // Arrange
        var playerNames = new List<string> { "Alice", "Bob" }; // WW, V
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager };

        // Simulate advancing to Day_ResolveNight without any WW action being logged
        var gameId = _gameService.StartNewGame(playerNames, roles);
        _gameService.ProcessModeratorInput(gameId, new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true }); // Setup -> Night
        // Intentionally SKIP WW action input processing
        var session = _gameService.GetGameStateView(gameId);
        session.GamePhase = GamePhase.Day_ResolveNight; // Manually set phase for test
        session.PendingModeratorInstruction = new ModeratorInstruction
        {
            InstructionText = "Announce no eliminations...", // Simulate the announcement instruction
            ExpectedInputType = ExpectedInputType.Confirmation
        };

        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.Players.Values.ShouldAllBe(p => p.Status == PlayerStatus.Alive);
        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>().ShouldBeEmpty(); // Check specifically for elimination logs
        // Removed check for NightActionsLog

        session.GamePhase.ShouldBe(GamePhase.Day_Debate);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        // Corrected expected instruction
        session.PendingModeratorInstruction.InstructionText.ShouldBe(GameStrings.ProceedToVotePrompt);
    }
} 