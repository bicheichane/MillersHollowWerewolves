using FluentAssertions;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models.Instructions;
using Werewolves.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Werewolves.Tests.Integration;

/// <summary>
/// Tests for phase and sub-phase transitions and state cache behavior.
/// Test IDs: PT-001 through PT-020
/// </summary>
public class PhaseTransitionTests : DiagnosticTestBase
{
    public PhaseTransitionTests(ITestOutputHelper output) : base(output) { }
    #region PT-001 to PT-004: Valid Transitions

    /// <summary>
    /// PT-001: New game starts in Night phase.
    /// </summary>
    [Fact]
    public void NewGame_StartsInNightPhase()
    {
        // Arrange & Act
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();

        // Assert
        var gameState = builder.GetGameState();
        gameState!.GetCurrentPhase().Should().Be(GamePhase.Night);

        MarkTestCompleted();
    }

    /// <summary>
    /// PT-002: Night.Start to Dawn.CalculateVictims is a valid transition.
    /// This test verifies that completing all night actions leads to Dawn phase.
    /// Note: Requires completing the full night action sequence.
    /// </summary>
    [Fact(Skip = "Requires full night action flow implementation to test")]
    public void NightStart_ToDawnCalculateVictims_IsValidTransition()
    {
        // This test will be implemented once night action flow is complete
        // The flow: Night actions → Dawn.CalculateVictims
    }

    /// <summary>
    /// PT-003: Dawn.Finalize to Day.Debate is a valid transition.
    /// </summary>
    [Fact(Skip = "Requires dawn phase flow to test")]
    public void DawnFinalize_ToDayDebate_IsValidTransition()
    {
        // This test will be implemented once dawn phase flow is complete
    }

    /// <summary>
    /// PT-004: Day.Finalize to Night.Start is a valid transition.
    /// TurnNumber should increment when transitioning from Day to Night.
    /// </summary>
    [Fact(Skip = "Requires full day phase flow to test")]
    public void DayFinalize_ToNightStart_IsValidTransition()
    {
        // This test will be implemented once day phase flow is complete
    }

    #endregion

    #region PT-010 to PT-011: Phase Cache Behavior

    /// <summary>
    /// PT-010: Main phase transition clears sub-phase cache.
    /// </summary>
    [Fact(Skip = "Requires internal state access to verify cache clearing")]
    public void MainPhaseTransition_ClearsSubPhaseCache()
    {
        // This test requires access to internal phase state cache
        // Will be implemented when test infrastructure supports internal access
    }

    /// <summary>
    /// PT-011: Sub-phase transition clears stage data.
    /// </summary>
    [Fact(Skip = "Requires internal state access to verify cache clearing")]
    public void SubPhaseTransition_ClearsStageData()
    {
        // This test requires access to internal phase state cache
    }

    #endregion

    #region PT-020: Invalid Transitions

    /// <summary>
    /// PT-020: Invalid phase transition throws exception.
    /// The state machine should prevent skipping required phases.
    /// </summary>
    [Fact(Skip = "Requires mechanism to attempt invalid transitions")]
    public void InvalidPhaseTransition_ThrowsException()
    {
        // This test requires a way to attempt an invalid transition
        // Currently the state machine doesn't expose a way to force invalid transitions
    }

    #endregion

    #region Additional Phase Transition Tests

    /// <summary>
    /// Verify game starts in Night phase.
    /// </summary>
    [Fact]
    public void NewGame_HasNightAsInitialPhase()
    {
        // Arrange & Act
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();

        // Assert
        var gameState = builder.GetGameState();
        gameState!.GetCurrentPhase().Should().Be(GamePhase.Night);

        MarkTestCompleted();
    }

    /// <summary>
    /// Verify the initial instruction after game start is a confirmation.
    /// </summary>
    [Fact]
    public void NewGame_HasConfirmationInstruction()
    {
        // Arrange & Act
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();

        // Assert
        var instruction = builder.GetCurrentInstruction();
        instruction.Should().BeOfType<StartGameConfirmationInstruction>();

        MarkTestCompleted();
    }

    /// <summary>
    /// After transitioning to Night, pending instruction should not be null.
    /// </summary>
    [Fact]
    public void AfterNightTransition_HasPendingInstruction()
    {
        // Arrange
        var builder = CreateBuilder()
            .WithSimpleGame(playerCount: 4, werewolfCount: 1, includeSeer: true);
        builder.StartGame();

        // Act
        builder.ConfirmGameStart();

        // Assert
        var instruction = builder.GetCurrentInstruction();
        instruction.Should().NotBeNull("Night phase should have an active instruction");

        MarkTestCompleted();
    }

    #endregion
}
