using Werewolves.Core.Enums;
using System;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs when a player is eliminated from the game.
/// </summary>
public record PlayerEliminatedLogEntry : GameLogEntryBase
{
    public required Guid PlayerId { get; init; }
    public required EliminationReason Reason { get; init; }
} 