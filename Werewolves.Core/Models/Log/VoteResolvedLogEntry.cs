using System;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs the final calculated result of a voting phase.
/// </summary>
public record VoteResolvedLogEntry : GameLogEntryBase
{
    public Guid? EliminatedPlayerId { get; init; } // Null if tie or no elimination
    public bool WasTie { get; init; }

    // Consider adding VoteType (Standard, Nightmare, etc.) if needed later
} 