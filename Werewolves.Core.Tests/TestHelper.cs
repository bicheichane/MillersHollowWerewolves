using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Services;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Werewolves.Core.Tests;

public static class TestHelper
{
    /// <summary>
    /// Simulates a game from start until the beginning of the first Day_Debate phase.
    /// Assumes a simple first night where no players are eliminated.
    /// Handles the transition through Night -> Day_Event (if applicable) -> Day_Debate.
    /// </summary>
    public static Guid SimulateGameUntilDebatePhase(GameService gameService, List<string> playerNames, List<RoleType> roles)
    {
        var gameId = gameService.StartNewGame(playerNames, roles);
        var session = gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();

        // --- Simulate Night Phase ---
        // Assuming the first phase after start is Night.
        session.GamePhase.ShouldBe(GamePhase.Night);
        session.TurnNumber.ShouldBe(1);

        // TODO: Add logic here to simulate actual night actions based on roles if needed for specific tests.
        // For now, we'll assume the moderator confirms to proceed without specific actions causing eliminations.
        // This might involve multiple ProcessModeratorInput calls depending on the roles present.
        // Example: Confirming werewolf choice (even if null), confirming seer choice, etc.

        // Assuming a simple confirmation proceeds past Night for basic role setups.
        // If the first prompt is confirmation to proceed to day:
        if (session.PendingModeratorInstruction?.ExpectedInputType == ExpectedInputType.Confirmation)
        {
             var nightConfirmInput = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };
             var nightResult = gameService.ProcessModeratorInput(gameId, nightConfirmInput);
             nightResult.IsSuccess.ShouldBeTrue($"Night confirmation failed: {nightResult.Error?.Message}");
             session = gameService.GetGameStateView(gameId); // Refresh state
        }
        else
        {
            // Handle more complex night scenarios if necessary, potentially skipping them for Day tests
            // For now, we might throw or log if the state isn't as expected.
            // throw new NotImplementedException("Unhandled night phase start state in TestHelper.");
            // Or, attempt a generic confirmation if that's the pattern
             var nightConfirmInput = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };
             var nightResult = gameService.ProcessModeratorInput(gameId, nightConfirmInput);
             // We might not assert success here if we are unsure of the expected state
             session = gameService.GetGameStateView(gameId); // Refresh state
        }


        // --- Handle Potential Day_Event (Role Reveal from Night Eliminations) ---
        // If the night resulted in an elimination, the game would go to Day_Event first.
        if (session.GamePhase == GamePhase.Day_Event)
        {
            session.PendingModeratorInstruction.ShouldNotBeNull();
            session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.RoleSelection);
            var eliminatedPlayerId = session.PendingModeratorInstruction.AffectedPlayerIds.ShouldHaveSingleItem();
            var actualRole = session.Players[eliminatedPlayerId].Role?.RoleType ?? RoleType.Unassigned; // Get actual assigned role

            var revealInput = new ModeratorInput
            {
                InputTypeProvided = ExpectedInputType.RoleSelection,
                SelectedRole = actualRole // Moderator must provide the correct role
            };
            var revealResult = gameService.ProcessModeratorInput(gameId, revealInput);
            revealResult.IsSuccess.ShouldBeTrue($"Role reveal failed: {revealResult.Error?.Message}");
            session = gameService.GetGameStateView(gameId); // Refresh state
        }
        // If no elimination, game might go directly from Night confirmation to Day_Debate (or via another intermediate step).
        // Let's assume for now it proceeds to Debate.

        // --- Verify State is Day_Debate ---
        // This assertion might fail if the game logic requires more steps between Night and Debate
        session.GamePhase.ShouldBe(GamePhase.Day_Debate, $"Expected to reach Day_Debate phase, but was {session.GamePhase}");
        session.TurnNumber.ShouldBe(1); // Still the first day cycle
        session.PendingModeratorInstruction.ShouldNotBeNull();
        session.PendingModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation); // Expecting prompt to proceed to vote

        return gameId;
    }

    // We can add more simulation helpers here later, e.g., SimulateGameUntilVotePhase, SimulateNightElimination, etc.

    /// <summary>
    /// Processes a sequence of moderator inputs for a given game.
    /// Stops processing if an error occurs or all inputs are processed.
    /// </summary>
    /// <param name="gameService">The GameService instance.</param>
    /// <param name="gameId">The ID of the game.</param>
    /// <param name="inputs">The sequence of ModeratorInput objects to process.</param>
    /// <returns>The Result of the last processed input, or the first error encountered.</returns>
    public static ProcessResult ProcessInputSequence(GameService gameService, Guid gameId, IEnumerable<ModeratorInput> inputs)
    {
        ProcessResult lastResult = ProcessResult.Failure(
            new GameError(ErrorType.Unknown,
                            GameErrorCode.Unknown_InternalError,
                            "Failed to process input sequence"));

        foreach (var input in inputs)
        {
            lastResult = gameService.ProcessModeratorInput(gameId, input);
            if (!lastResult.IsSuccess)
            {
                // Stop processing on the first error
                break;
            }
        }

        return lastResult;
    }

	public static List<string> GetDefaultPlayerNames(int count = 4) =>
		Enumerable.Range(1, count).Select(i => $"Player {i}").ToList();

	public static List<RoleType> GetDefaultRoles4() =>
		new() { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager, RoleType.SimpleVillager };
} 