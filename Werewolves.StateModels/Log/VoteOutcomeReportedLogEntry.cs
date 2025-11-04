using Werewolves.StateModels.Models;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Logs the raw outcome of a vote as reported by the moderator.
/// </summary>
public record VoteOutcomeReportedLogEntry : GameLogEntryBase
{
    // Guid.Empty represents a reported tie.
    // A specific PlayerId represents player reported as eliminated.
    public required Guid ReportedOutcomePlayerId { get; init; }

    // Consider adding VoteType (Standard, Nightmare, etc.) if needed later

    /// <summary>
    /// Applies the vote outcome to the game state.
    /// </summary>
    internal override void Apply(GameSession.IStateMutator mutator)
    {
        mutator.SetPendingVoteOutcome(ReportedOutcomePlayerId);
    }
}
