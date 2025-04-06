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

namespace Werewolves.Core.Tests;

public class VictoryConditionTests
{
    private readonly GameService _gameService = new();

    // Helper to set up a game, manually assign roles, set statuses, and position for victory check
    private Guid SetupGameForVictoryCheck(List<string> playerNames, List<RoleType> roles, Dictionary<string, RoleType> manualRoleAssignments, Dictionary<string, PlayerStatus> playerStatuses, GamePhase phaseBeforeCheck, Action<GameSession> preCheckAction = null)
    {
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);

        // Manually assign roles for testing Phase 1 victory conditions
        foreach (var assignment in manualRoleAssignments)
        {
            var player = session.Players.Values.FirstOrDefault(p => p.Name == assignment.Key);
            if (player != null)
            {
                // Phase 1 Correction: Instantiate simple roles directly
                player.Role = assignment.Value switch
                {
                    RoleType.SimpleWerewolf => new SimpleWerewolfRole(),
                    RoleType.SimpleVillager => new SimpleVillagerRole(),
                    _ => null // Should not happen in Phase 1 tests
                };
                player.IsRoleRevealed = true; // Assume roles are known for victory check in Phase 1
            }
        }

        // Set player statuses
        foreach (var status in playerStatuses)
        {
            var player = session.Players.Values.FirstOrDefault(p => p.Name == status.Key);
            if (player != null)
            {
                player.Status = status.Value;
            }
        }

        // Allow custom actions like setting PendingVoteOutcome
        preCheckAction?.Invoke(session);

        // Set phase right before the check is typically performed
        session.GamePhase = phaseBeforeCheck;
        session.PendingModeratorInstruction = new ModeratorInstruction { ExpectedInputType = ExpectedInputType.Confirmation }; // Assume confirmation triggers the check

        return gameId;
    }


    [Fact]
    public void CheckVictory_WerewolfWin_WhenWWsEqualVillagers()
    {
        // Arrange: 1 WW (Alice), 1 V (Bob) left alive after a vote eliminated Charlie (V)
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" };
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var manualRoles = new Dictionary<string, RoleType> { { "Alice", RoleType.SimpleWerewolf }, { "Bob", RoleType.SimpleVillager }, { "Charlie", RoleType.SimpleVillager } };
        var statuses = new Dictionary<string, PlayerStatus> { { "Alice", PlayerStatus.Alive }, { "Bob", PlayerStatus.Alive }, { "Charlie", PlayerStatus.Dead } }; // Charlie just died

        // Setup state as if resolving the vote that killed Charlie
        var gameId = SetupGameForVictoryCheck(playerNames, roles, manualRoles, statuses, GamePhase.Day_ResolveVote, session =>
        {
            var charlieId = session.Players.Values.First(p => p.Name == "Charlie").Id;
            session.PendingVoteOutcome = charlieId; // Vote outcome that just happened
            // Manually set Charlie status here again to ensure consistency before check
             session.Players[charlieId].Status = PlayerStatus.Dead;
        });

        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true }; // Input to trigger resolution/victory check

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input); // This should trigger the victory check after processing elimination

        // Assert
        result.IsSuccess.ShouldBeTrue(); // Operation successful, leading to game over
        var session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.GamePhase.ShouldBe(GamePhase.GameOver);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.None); // No more input expected
        session.PendingModeratorInstruction.InstructionText.ShouldContain(Team.Werewolves.ToString()); // Check for WW win message

        session.GameHistoryLog.OfType<VictoryConditionMetLogEntry>()
            .ShouldContain(vcl => vcl.WinningTeam == Team.Werewolves);
        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == session.Players.Values.First(p => p.Name == "Charlie").Id); // Verify Charlie was eliminated
    }

    [Fact]
    public void CheckVictory_VillagerWin_WhenWWsAreEliminated()
    {
         // Arrange: 1 V (Bob), 1 V (Charlie) left alive after a vote eliminated Alice (WW)
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" };
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };
        var manualRoles = new Dictionary<string, RoleType> { { "Alice", RoleType.SimpleWerewolf }, { "Bob", RoleType.SimpleVillager }, { "Charlie", RoleType.SimpleVillager } };
        var statuses = new Dictionary<string, PlayerStatus> { { "Alice", PlayerStatus.Dead }, { "Bob", PlayerStatus.Alive }, { "Charlie", PlayerStatus.Alive } }; // Alice just died

        // Setup state as if resolving the vote that killed Alice
         var gameId = SetupGameForVictoryCheck(playerNames, roles, manualRoles, statuses, GamePhase.Day_ResolveVote, session =>
        {
            var aliceId = session.Players.Values.First(p => p.Name == "Alice").Id;
            session.PendingVoteOutcome = aliceId; // Vote outcome that just happened
            // Manually set Alice status here again to ensure consistency before check
            session.Players[aliceId].Status = PlayerStatus.Dead;
        });

        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true }; // Input to trigger resolution/victory check

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        session.GamePhase.ShouldBe(GamePhase.GameOver);
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.None);
        session.PendingModeratorInstruction.InstructionText.ShouldContain(Team.Villagers.ToString()); // Check for Villager win message

        session.GameHistoryLog.OfType<VictoryConditionMetLogEntry>()
            .ShouldContain(vcl => vcl.WinningTeam == Team.Villagers);
        session.GameHistoryLog.OfType<PlayerEliminatedLogEntry>()
            .ShouldContain(pel => pel.PlayerId == session.Players.Values.First(p => p.Name == "Alice").Id); // Verify Alice was eliminated
    }

    // Potential future test: Victory check after Night Resolution
}
