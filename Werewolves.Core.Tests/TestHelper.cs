using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Services;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Werewolves.Core.Tests;

public class TestModeratorInput : ModeratorInput{
  /// <summary>
  /// The expected game phase before the input is processed.
  /// </summary>
  public GamePhase ExpectedGamePhase {get; set;}

	/// <summary>
	/// An alternative to SelectedPlayerIds for when the input type is PlayerSelectionMultiple/Single for testing purposes.
	/// If this is set, SelectedPlayerIds will be ignored.
	/// </summary>
	public List<string>? SelectedPlayerNames { get; set; }

  public static TestModeratorInput Confirm(GamePhase expectedGamePhase, bool confirmation) => new()
	{
		InputTypeProvided = ExpectedInputType.Confirmation,
		Confirmation = confirmation,
		ExpectedGamePhase = expectedGamePhase
	};

    public static TestModeratorInput AssignPlayerRoles(GamePhase expectedGamePhase, Dictionary<Guid, RoleType> role) => new()
	{
		InputTypeProvided = ExpectedInputType.AssignPlayerRoles,
		AssignedPlayerRoles = role,
		ExpectedGamePhase = expectedGamePhase
	};

    public static TestModeratorInput SelectPlayer(GamePhase expectedGamePhase, Guid playerId) => new()
	{
		InputTypeProvided = ExpectedInputType.PlayerSelectionSingle,
		SelectedPlayerIds = new List<Guid> { playerId },
		ExpectedGamePhase = expectedGamePhase
	};

	public static TestModeratorInput SelectPlayers(GamePhase expectedGamePhase, Guid playerId) => new()
	{
		InputTypeProvided = ExpectedInputType.PlayerSelectionMultiple,
		SelectedPlayerIds = new() { playerId },
		ExpectedGamePhase = expectedGamePhase
	};

	public static TestModeratorInput SelectPlayers(GamePhase expectedGamePhase, List<Guid> playerIds) => new()
	{
		InputTypeProvided = ExpectedInputType.PlayerSelectionMultiple,
		SelectedPlayerIds = playerIds,
		ExpectedGamePhase = expectedGamePhase
	};

    public static TestModeratorInput SelectOption(GamePhase expectedGamePhase, string option) => new()
	{
		InputTypeProvided = ExpectedInputType.OptionSelection,
		SelectedOption = option,
		ExpectedGamePhase = expectedGamePhase
	};

	public void SetPlayerIds(List<Guid> playerIds)
	{
		SelectedPlayerIds = playerIds;
	}
}

public static class TestHelper
{

    /// <summary>
    /// Processes a sequence of moderator inputs for a given game.
    /// Stops processing if an error occurs or all inputs are processed.
    /// </summary>
    /// <param name="gameService">The GameService instance.</param>
    /// <param name="gameId">The ID of the game.</param>
    /// <param name="inputs">The sequence of ModeratorInput objects to process.</param>
    /// <returns>The Result of the last processed input, or the first error encountered.</returns>
    public static ProcessResult ProcessInputSequence(GameService gameService, Guid gameId, IEnumerable<TestModeratorInput> inputs)
    {
        ProcessResult lastResult = ProcessResult.Failure(
            new GameError(ErrorType.Unknown,
                            GameErrorCode.Unknown_InternalError,
                            "Failed to process input sequence"));

        foreach (var input in inputs)
        {
			// Check if the input's expected game phase matches the current game phase
			var gameSession = gameService.GetGameStateView(gameId);
			
			gameSession.ShouldNotBeNull();
			gameSession.GamePhase.ShouldBe(input.ExpectedGamePhase);

			if (input.SelectedPlayerNames?.Count > 0)
			{
				var playerIds = new List<Guid>();

				foreach (var selectedPlayerName in input.SelectedPlayerNames)
				{
					var playerId = gameSession.Players.Values
						.FirstOrDefault(p => p.Name == selectedPlayerName)?.Id;

					playerId.ShouldNotBeNull();
					playerIds.Add(playerId.Value);
				}

				input.SetPlayerIds(playerIds);
			}

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
		Enumerable.Range(1, count).Select(i => $"P {i}").ToList();

	public static List<RoleType> GetDefaultRoles4() => new() { 
        RoleType.SimpleWerewolf, 
        
        RoleType.SimpleVillager, 
        RoleType.SimpleVillager, 
        RoleType.SimpleVillager 
    };
} 