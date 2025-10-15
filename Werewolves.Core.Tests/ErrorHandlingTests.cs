using Werewolves.Core.Services;
using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using static Werewolves.Core.Tests.TestHelper;
using static Werewolves.Core.Tests.TestModeratorInput;

namespace Werewolves.Core.Tests;

public class ErrorHandlingTests
{
    private readonly GameService _gameService = new();

    [Fact]
    public void ProcessModeratorInput_NonExistentGame_ShouldReturnGameNotFoundError()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        var input = Confirm(GamePhase.Setup, true);

        // Act
        var result = _gameService.ProcessModeratorInput(nonExistentGameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ModeratorInstruction.ShouldBeNull();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.GameNotFound);
        result.Error.Code.ShouldBe(GameErrorCode.GameNotFound_SessionNotFound);
    }

    [Fact]
    public void ProcessModeratorInput_WrongInputType_ShouldReturnInputTypeMismatchError()
    {
        // Arrange
        var playerNames = GetPlayerNames();
        var roles = GetRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var input = SelectPlayer(GamePhase.Setup, Guid.NewGuid());

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InputTypeMismatch);

        var session = _gameService.GetGameStateView(gameId);
        session.GamePhase.ShouldBe(GamePhase.Setup);
        session.PendingModeratorInstruction?.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
    }

    [Fact]
    public void ProcessModeratorInput_WhenGameIsOver_ShouldReturnError()
    {
        // Arrange: Set up a 1v1 game and play until WW victory triggers GameOver
        var playerNames = new List<string> { "Wolf", "Villager" };
        var roles = new List<RoleType> { RoleType.SimpleWerewolf, RoleType.SimpleVillager };
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);
        
        var pList = session!.Players.Keys.ToList();
        var wolfId = pList[0];
        var villagerId = pList[1];

        var gameEndingSequence = new List<TestModeratorInput>
        {
            Confirm(GamePhase.Setup, true),             // -> Night
            Confirm(GamePhase.Night_RoleAction, true),             // -> Night (WW ID)
            SelectPlayers(GamePhase.Night_RoleAction, wolfId),     // -> Night (WW Action)
            SelectPlayer(GamePhase.Night_RoleAction, villagerId),  // -> Day_ResolveNight
            Confirm(GamePhase.Day_ResolveNight, true)   // -> GameOver (WW win condition met)
        };

        // Act: Play the sequence that should end the game
        var sequenceResult = ProcessInputSequence(_gameService, gameId, gameEndingSequence);

        // Assert: Verify the game ended as expected
        sequenceResult.IsSuccess.ShouldBeTrue("The game ending sequence failed.");
        session = _gameService.GetGameStateView(gameId);
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.GameOver);
        session.WinningTeam.ShouldBe(Team.Werewolves);

        // Arrange: Prepare an input to send *after* the game is over
        var inputAfterGameOver = Confirm(GamePhase.GameOver, true); // Input type doesn't matter

        // Act: Attempt to process input now that the game is over
        var result = _gameService.ProcessModeratorInput(gameId, inputAfterGameOver);

        // Assert: Verify the correct error is returned
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidOperation);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidOperation_GameIsOver);
    }
} 