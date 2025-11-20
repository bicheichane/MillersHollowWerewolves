using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Logs the final calculated result of a voting phase.
/// </summary>
public record VoteResolvedLogEntry : GameLogEntryBase
{
    public Guid? EliminatedPlayerId { get; init; } // Null if tie or no elimination
    public bool WasTie { get; init; }

    // Consider adding VoteType (Standard, Nightmare, etc.) if needed later

    /// <summary>
    /// Applies the final vote resolution to the game state.
    /// </summary>
    internal override void Apply(ISessionMutator mutator)
    {
        if (EliminatedPlayerId.HasValue && !WasTie)
        {
            mutator.SetPlayerHealth(EliminatedPlayerId.Value, PlayerHealth.Dead);
        }
    }
}
