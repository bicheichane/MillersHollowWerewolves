using Xunit;
using Shouldly;
using Werewolves.Core.Services;
using Werewolves.Core.Models;
using Werewolves.Core.Enums;
using Werewolves.Core.Models.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Resources;
using Werewolves.Core.Extensions;
using static Werewolves.Core.Tests.TestHelper;

namespace Werewolves.Core.Tests;

public class GameServiceSetupTests
{
    private readonly GameService _gameService;

    public GameServiceSetupTests()
    {
        _gameService = new GameService();
    }

    [Fact]
    public void StartNewGame_ShouldCreateSession_WithCorrectInitialState()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames(3);
        var roles = GetDefaultRoles4();

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
        instruction.InstructionText.ShouldBe(GameStrings.SetupCompletePrompt);

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
        var roles = GetDefaultRoles4();
        var gameId = _gameService.StartNewGame(playerNames, roles);

        // Act
        var instruction = _gameService.GetCurrentInstruction(gameId);

        // Assert
        instruction.ShouldNotBeNull();
        instruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
        instruction.InstructionText.ShouldBe(GameStrings.SetupCompletePrompt);
    }

    [Fact]
    public void ProcessModeratorInput_WithCorrectConfirmation_ShouldAdvancePhaseToNight()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
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
        result.ModeratorInstruction.InstructionText.ShouldBe(GameStrings.NightStartsPrompt);
        result.ModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);

        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Night);
        session.TurnNumber.ShouldBe(1);

        nextInstruction.ShouldNotBeNull();
        nextInstruction.InstructionText.ShouldBe(GameStrings.NightStartsPrompt);
        nextInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation);
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
        result.Error.Message.ShouldBe(GameStrings.GameNotFound);
    }

    [Fact]
    public void ProcessModeratorInput_WithWrongInputType_ShouldReturnInputTypeMismatchError()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4();
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
        result.Error.Message.ShouldBe(GameStrings.InputTypeMismatch);

        // Verify game state hasn't changed
        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Setup);
        session.TurnNumber.ShouldBe(0);
    }

    [Fact]
    public void ProcessNightStartsConfirmation_ShouldPromptForWerewolfIdentification()
    {
        // Arrange
        var playerNames = GetDefaultPlayerNames();
        var roles = GetDefaultRoles4(); // Includes SimpleWerewolf which needs N1 ID
        var gameId = _gameService.StartNewGame(playerNames, roles);
        

        // 1. Process Setup Confirmation
        var setupConfirmInput = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };
        var setupResult = _gameService.ProcessModeratorInput(gameId, setupConfirmInput);
        setupResult.IsSuccess.ShouldBeTrue();
        _gameService.GetCurrentInstruction(gameId)?.ExpectedInputType.ShouldBe(ExpectedInputType.Confirmation); // Should be NightStartsPrompt

        // 2. Process Night Starts Confirmation
        var nightStartsConfirmInput = new ModeratorInput { InputTypeProvided = ExpectedInputType.Confirmation, Confirmation = true };

        // Act
        var result = _gameService.ProcessModeratorInput(gameId, nightStartsConfirmInput);
        var session = _gameService.GetGameStateView(gameId);
        var nextInstruction = _gameService.GetCurrentInstruction(gameId);

        var werewolfCount = session.GetAliveRoleCount(RoleType.SimpleWerewolf);

		// Assert
		result.IsSuccess.ShouldBeTrue();
        result.ModeratorInstruction.ShouldNotBeNull();
        // Expect WW Identification Prompt
        result.ModeratorInstruction.InstructionText.ShouldBe(GameStrings.IdentifyWerewolvesPrompt.Format(werewolfCount));
        result.ModeratorInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionMultiple);

        session.ShouldNotBeNull();
        session.GamePhase.ShouldBe(GamePhase.Night); // Still Night phase
        session.TurnNumber.ShouldBe(1);
        session.PendingNight1IdentificationForRole.ShouldBe(RoleType.SimpleWerewolf); // Check pending state

        nextInstruction.ShouldNotBeNull();
        nextInstruction.InstructionText.ShouldBe(GameStrings.IdentifyWerewolvesPrompt.Format(werewolfCount));
        nextInstruction.ExpectedInputType.ShouldBe(ExpectedInputType.PlayerSelectionMultiple);
        nextInstruction.SelectablePlayerIds.ShouldNotBeNull();
        nextInstruction.SelectablePlayerIds.Count.ShouldBe(playerNames.Count); // All players selectable for identification
    }
} 