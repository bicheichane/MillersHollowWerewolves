using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Logs a transition between game phases.
/// </summary>
public record PhaseTransitionLogEntry : GameLogEntryBase
{
    public required GamePhase PreviousPhase { get; init; }
    /// <summary>
    /// Applies the phase transition to the game state.
    /// </summary>
    internal override void Apply(ISessionMutator mutator)
    {
        mutator.SetCurrentPhase(CurrentPhase);
	}
}
