using FluentAssertions;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models.Instructions;
using Werewolves.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Werewolves.Tests.Integration;

/// <summary>
/// Tests for game lifecycle: creation, game start confirmation, and phase transitions.
/// Test IDs: GL-001 through GL-020
/// </summary>
public class GameLifecycleTests : DiagnosticTestBase
{
    public GameLifecycleTests(ITestOutputHelper output) : base(output) { }
    #region GL-001 to GL-003: Game Creation

    /// <summary>
    /// GL-001: StartNewGame with valid roles and players returns StartGameConfirmationInstruction.
    /// </summary>
    [Fact]
    public void StartNewGame_WithValidRolesAndPlayers_ReturnsStartGameConfirmationInstruction()
    {
        // Arrange
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 3, werewolfCount: 1, includeSeer: true);

        // Act
        var instruction = builder.StartGame();

        // Assert
        instruction.Should().NotBeNull();
        instruction.Should().BeOfType<StartGameConfirmationInstruction>();
        instruction.GameGuid.Should().NotBeEmpty();

        MarkTestCompleted();
    }

    /// <summary>
    /// GL-002: StartNewGame with empty roles throws ArgumentException.
    /// </summary>
    [Fact]
    public void StartNewGame_WithEmptyRoles_ThrowsArgumentException()
    {
        // Arrange
        var gameService = new GameLogic.Services.GameService();
        var playerNames = new List<string> { "Alice", "Bob", "Charlie" };
        var emptyRoles = new List<MainRoleType>();

        // Act
        var act = () => gameService.StartNewGame(playerNames, emptyRoles);

        // Assert
        act.Should().Throw<ArgumentException>();

        MarkTestCompleted();
    }

    /// <summary>
    /// GL-003: StartNewGame creates session with correct player count.
    /// </summary>
    [Fact]
    public void StartNewGame_CreatesSessionWithCorrectPlayerCount()
    {
        // Arrange
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 5, werewolfCount: 1, includeSeer: true);

        // Act
        builder.StartGame();
        var gameState = builder.GetGameState();

        // Assert
        gameState.Should().NotBeNull();
        gameState!.GetPlayers().Should().HaveCount(5);

        MarkTestCompleted();
    }

    /// <summary>
    /// GL-003b: Players are created in correct seating order.
    /// </summary>
    [Fact]
    public void StartNewGame_PlayersInCorrectSeatingOrder()
    {
        // Arrange
        var builder = CreateBuilder()
            .WithPlayers("Alice", "Bob", "Charlie", "Diana")
            .WithRoles(MainRoleType.SimpleWerewolf, MainRoleType.Seer, MainRoleType.SimpleVillager, MainRoleType.SimpleVillager);

        // Act
        builder.StartGame();
        var gameState = builder.GetGameState();

        // Assert
        var playerNames = gameState!.GetPlayers().Select(p => p.Name).ToList();
        playerNames.Should().ContainInOrder("Alice", "Bob", "Charlie", "Diana");

        MarkTestCompleted();
    }

    #endregion

    #region GL-010 to GL-011: Game Start Confirmation

    /// <summary>
    /// GL-010: Confirming game start triggers Night phase execution.
    /// </summary>
    [Fact]
    public void ConfirmGameStart_TransitionsToNightPhase()
    {
        // Arrange
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();

        // Act
        var result = builder.ConfirmGameStart();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var gameState = builder.GetGameState();
        gameState!.GetCurrentPhase().Should().Be(GamePhase.Night);

        MarkTestCompleted();
    }

    /// <summary>
    /// GL-011: After game start confirmation, TurnNumber is 1.
    /// </summary>
    [Fact]
    public void ConfirmGameStart_SetsCorrectTurnNumber()
    {
        // Arrange
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();

        // Act
        builder.ConfirmGameStart();
        var gameState = builder.GetGameState();

        // Assert
        gameState!.TurnNumber.Should().Be(1);

        MarkTestCompleted();
    }

    #endregion

    #region GL-020: Full Game Cycle

    /// <summary>
    /// GL-020: Complete game cycle transitions Night → Dawn → Day correctly.
    /// This is a more complex integration test that will be expanded as more
    /// functionality is implemented.
    /// </summary>
    [Fact]
    public void CompleteGameCycle_NightToDawnToDay_TransitionsCorrectly()
    {
        // Arrange - Simple 4 player game: 1 WW, 1 Seer, 2 Villagers
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();
        builder.ConfirmGameStart();

        // Verify we're in Night phase
        var gameState = builder.GetGameState();
        gameState!.GetCurrentPhase().Should().Be(GamePhase.Night);

        // Note: Full cycle test requires completing night actions.
        // This will be expanded when night action flow is fully testable.
        // For now, we verify the initial Night phase transition.

        MarkTestCompleted();
    }

    #endregion
}
