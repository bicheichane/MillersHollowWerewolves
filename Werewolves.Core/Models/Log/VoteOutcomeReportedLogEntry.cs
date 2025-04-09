using System;
using Werewolves.Core.Enums;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs the raw outcome of a vote as reported by the moderator.
/// </summary>
public record VoteOutcomeReportedLogEntry : GameLogEntryBase
{
    public required Guid ReportedOutcomePlayerId { get; init; }
    // Guid.Empty represents a reported tie.
    // A specific PlayerId represents the player reported as eliminated.
} 