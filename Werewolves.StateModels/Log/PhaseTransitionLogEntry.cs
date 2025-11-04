using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Logs a transition between game phases.
/// </summary>
public record PhaseTransitionLogEntry : GameLogEntryBase
{
    public required GamePhase PreviousPhase { get; init; }
    public required PhaseTransitionReason Reason { get; init; }

    /// <summary>
    /// Applies the phase transition to the game state.
    /// </summary>
    internal override void Apply(GameSession.IStateMutator mutator)
    {
        mutator.SetCurrentPhase(CurrentPhase);
	}
}
