using Werewolves.GameLogic.Models;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.InternalMessages;

/// <summary>
/// Represents the outcome of a phase handler's execution.
/// </summary>
public record PhaseHandlerResult(ModeratorInstruction ModeratorInstruction);

public record MainPhaseHandlerResult(ModeratorInstruction ModeratorInstruction, GamePhase MainPhase, PhaseTransitionReason TransitionReason) 
	: PhaseHandlerResult(ModeratorInstruction)
{
    // Static factory methods
    public static MainPhaseHandlerResult TransitionPhase(ModeratorInstruction nextInstruction, GamePhase mainPhase, PhaseTransitionReason transitionReason) =>
        new(nextInstruction, mainPhase, transitionReason);
}

public record SubPhaseHandlerResult(ModeratorInstruction ModeratorInstruction, Enum? SubGamePhase) 
	: PhaseHandlerResult(ModeratorInstruction)
{
	public static SubPhaseHandlerResult TransitionSubPhase(ModeratorInstruction nextInstruction, Enum subGamePhase) =>
		new(nextInstruction, subGamePhase);
	public static SubPhaseHandlerResult StayInSubPhase(ModeratorInstruction nextInstruction) =>
		new(nextInstruction, null);
}