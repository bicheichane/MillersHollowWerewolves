using Xunit;
using Shouldly;
using Werewolves.Core.Services;
using Werewolves.Core.Models;
using Werewolves.Core.Enums;
using Werewolves.Core.Models.Log;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Werewolves.Core.Tests;

public class GameServicePhase0Tests
{
    private readonly GameService _gameService;

    public GameServicePhase0Tests()
    {
        _gameService = new GameService();
    }

    private List<string> GetDefaultPlayerNames(int count = 3) =>
        Enumerable.Range(1, count).Select(i => $"Player {i}").ToList();

    private List<RoleType> GetDefaultRoles() =>
        new() { RoleType.SimpleWerewolf, RoleType.SimpleVillager, RoleType.SimpleVillager };

    [Fact]
    public void StartNewGame_ShouldCreateSession_WithCorrectInitialState()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames(3);
        var roles = GetDefaultRoles();

        // Act
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var session = _gameService.GetGameStateView(gameId);
        var instruction = _gameService.GetCurrentInstruction(gameId);

        // Assert
        gameId.ShouldNotBe(Guid.Empty);
        session.ShouldNotBeNull();
        session.Id.ShouldBe(gameId);
        session.Players.Count.ShouldBe(playerNames.Count);
        session.Players.Values.Select(p => p.Name).ShouldBe(playerNames, ignoreOrder: true);
        session.Players.Values.ShouldAllBe(p => p.Status == PlayerStatus.Alive);
        session.Players.Values.ShouldAllBe(p => p.IsRoleRevealed == false);
        session.Players.Values.ShouldAllBe(p => p.State != null);
        session.Players.Values.ShouldAllBe(p => p.State.IsSheriff == false);
        session.Players.Values.ShouldAllBe(p => p.State.IsInLove == false);

        session.PlayerSeatingOrder.Count.ShouldBe(playerNames.Count);
        // Verify seating order maps back to player names correctly
        var seatedNames = session.PlayerSeatingOrder.Select(id => session.Players[id].Name).ToList();
        seatedNames.ShouldBe(playerNames);

        session.RolesInPlay.ShouldBe(roles);
        session.GamePhase.ShouldBe(GamePhase.Setup);
        session.TurnNumber.ShouldBe(0);
        session.SheriffPlayerId.ShouldBeNull();
        session.Lovers.ShouldBeNull();

        // Check initial instruction
        instruction.ShouldNotBeNull();
        instruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        instruction.InstructionText.ShouldContain("Setup complete");

        // Check Game Started Log Entry
        session.GameHistoryLog.Count.ShouldBe(1);
        var logEntry = session.GameHistoryLog.First().ShouldBeOfType<GameStartedLogEntry>();
        logEntry.Phase.ShouldBe(GamePhase.Setup);
        logEntry.TurnNumber.ShouldBe(0);
        logEntry.InitialRoles.ShouldBe(roles);
        logEntry.InitialPlayers.Count.ShouldBe(playerNames.Count);
        logEntry.InitialPlayers.Select(p => p.Name).ShouldBe(playerNames);
        logEntry.InitialEvents.ShouldBeNull();
    }

    [Fact]
    public void GetCurrentInstruction_ShouldRetrieveInitialInstruction()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);

        // Act
        var instruction = _gameService.GetCurrentInstruction(gameId);

        // Assert
        instruction.ShouldNotBeNull();
        instruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        instruction.InstructionText.ShouldContain("Setup complete");
    }

    [Fact]
    public void ProcessModeratorInput_WithCorrectConfirmation_ShouldAdvancePhaseToNight()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var confirmationInput = new ModeratorInput
        {
            InputTypeProvided = ExpectedInputType.Confirmation,
            Confirmation = true
        };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, confirmationInput);
        var session = _gameService.GetGameStateView(gameId);
        var nextInstruction = _gameService.GetCurrentInstruction(gameId);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBeNull();
        result.ModeratorInstruction.ShouldNotBeNull();
        result.ModeratorInstruction.InstructionText.ShouldContain("Night 1 begins");

        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Night);
        session.TurnNumber.ShouldBe(1);

        nextInstruction.ShouldNotBeNull();
        nextInstruction.InstructionText.ShouldContain("Night 1 begins");
        // ExpectedInputType for Night 1 start is None initially, night logic will set the actual first prompt
        nextInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.None);
    }

    [Fact]
    public void ProcessModeratorInput_ForNonExistentGame_ShouldReturnSessionNotFoundError()
    {
        // Arrange
        var nonExistentGameId = Guid.NewGuid();
        var confirmationInput = new ModeratorInput
        {
            InputTypeProvided = ExpectedInputType.Confirmation,
            Confirmation = true
        };

        // Act
        var result = _gameService.ProcessModeratorInput(nonExistentGameId, confirmationInput);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ModeratorInstruction.ShouldBeNull();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.GameNotFound);
        result.Error.Code.ShouldBe(GameErrorCode.GameNotFound_SessionNotFound);
        result.Error.Message.ShouldContain("Game session not found");
    }

    [Fact]
    public void ProcessModeratorInput_WithWrongInputType_ShouldReturnInputTypeMismatchError()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles();
        var gameId = _gameService.StartNewGame(playerNames, roles);
        var wrongInput = new ModeratorInput
        {
            InputTypeProvided = ExpectedInputType.PlayerSelectionSingle, // Expected Confirmation
            SelectedPlayerIds = new List<Guid> { Guid.NewGuid() }
        };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, wrongInput);
        var session = _gameService.GetGameStateView(gameId);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.ModeratorInstruction.ShouldBeNull();
        result.Error.ShouldNotBeNull();
        result.Error.Type.ShouldBe(ErrorType.InvalidInput);
        result.Error.Code.ShouldBe(GameErrorCode.InvalidInput_InputTypeMismatch);
        result.Error.Message.ShouldContain("input type does not match");

        // Verify game state hasn't changed
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Setup);
        session.TurnNumber.ShouldBe(0);
    }
} 