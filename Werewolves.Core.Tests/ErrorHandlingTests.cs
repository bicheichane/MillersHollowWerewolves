using Werewolves.Core.Services;
using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;

namespace Werewolves.Core.Tests;

public class ErrorHandlingTests
{
    private readonly GameService _gameService = new();

    [Fact]
    public void ProcessModeratorInput_NonExistentGame_ShouldReturnGameNotFoundError()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };

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
        var playerNames = new List<string> { "Alice" };
        var roles = new List<RoleType> { RoleType.SimpleVillager };
        var gameId = _gameService.StartNewGame(playerNames, roles);
        // Game expects Confirmation, but we send PlayerSelection
        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, SelectedPlayerIds = new List<Guid> { Guid.NewGuid() } };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InputTypeMismatch);
    }

    [Fact]
    public void ProcessModeratorInput_WhenGameIsOver_ShouldReturnError()
    {
        // Arrange: Get game to GameOver state (simulate manual state change for simplicity)
        var playerNames = new List<string> { "Alice" };
        var roles = new List<RoleType> { RoleType.SimpleVillager };
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);
        session.GamePhase = GamePhase.GameOver;
        session.PendingModeratorInstruction = null; // Simulate no pending instruction

        var input = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };

        // Act:
        var result = _gameService.ProcessModeratorInput(gameId, input);

        // Assert:
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidOperation);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidOperation_GameIsOver);
    }
} 