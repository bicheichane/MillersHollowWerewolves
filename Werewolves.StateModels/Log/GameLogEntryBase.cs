using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Abstract base class for all game log entries.
/// Provides common properties like timestamp, turn number, and game phase.
/// Based on Roadmap Phase 0 and Architecture doc log structure.
/// </summary>
public abstract record GameLogEntryBase
{
    public required DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required int TurnNumber { get; init; }
    public required GamePhase CurrentPhase { get; init; }

    /// <summary>
    /// Applies the state changes from this log entry to the game session.
    /// This method is internal to ensure state mutations only happen through the log-driven pattern.
    /// </summary>
    /// <param name="mutator">The state mutator interface for applying changes</param>
    internal abstract void Apply(GameSession.IStateMutator mutator);
}
