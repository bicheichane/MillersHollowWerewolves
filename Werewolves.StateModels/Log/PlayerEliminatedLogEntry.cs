using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Logs when a player is eliminated from the game.
/// </summary>
internal record PlayerEliminatedLogEntry : GameLogEntryBase
{
    public required Guid PlayerId { get; init; }
    public required EliminationReason Reason { get; init; }
	/// <summary>
	/// Applies the player elimination to the game state.
	/// </summary>
	protected override GameLogEntryBase InnerApply(ISessionMutator mutator)
    {
        mutator.SetPlayerHealth(PlayerId, PlayerHealth.Dead);
        return this;
    }
}
