using System;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs the raw outcome of a vote as reported by the moderator.
/// </summary>
public record VoteOutcomeReportedLogEntry : GameLogEntryBase
{
    // Guid.Empty represents a reported tie.
    // A specific PlayerId represents the player reported as eliminated.
    public required Guid ReportedOutcomePlayerId { get; init; }

    // Consider adding VoteType (Standard, Nightmare, etc.) if needed later
} 