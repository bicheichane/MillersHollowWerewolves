using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Logs a transition between game phases.
/// </summary>
internal record PhaseTransitionLogEntry : GameLogEntryBase
{
    public required GamePhase PreviousPhase { get; init; }
    /// <summary>
    /// Applies the phase transition to the game state.
    /// </summary>
    protected override GameLogEntryBase InnerApply(ISessionMutator mutator)
    {
	    mutator.SetCurrentPhase(CurrentPhase);
		//current turn number may have changed if we transitioned from Day to Night
		return this with {TurnNumber = mutator.CurrentTurnNumber};
    }
}
