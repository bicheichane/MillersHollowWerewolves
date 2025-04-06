using Werewolves.Core.Enums;
using System;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs when a player's role is revealed.
/// </summary>
public record RoleRevealedLogEntry : GameLogEntryBase
{
    public required Guid PlayerId { get; init; }
    public required RoleType RevealedRole { get; init; }
} 