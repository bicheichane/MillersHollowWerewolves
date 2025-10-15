using Werewolves.Core.Enums;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Abstract base class for all game log entries.
/// Provides common properties like timestamp, turn number, and game phase.
/// Based on Roadmap Phase 0 and Architecture doc log structure.
/// </summary>
public abstract record GameLogEntryBase
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required int TurnNumber { get; init; }
    public required GamePhase CurrentPhase { get; init; }
} 