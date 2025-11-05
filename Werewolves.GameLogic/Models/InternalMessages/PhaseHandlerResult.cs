using Werewolves.GameLogic.Models;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.InternalMessages;

/// <summary>
/// Abstract base record representing the outcome of a phase handler's execution.
/// </summary>
internal abstract record PhaseHandlerResult(ModeratorInstruction? ModeratorInstruction);

/// <summary>
/// For transitioning between main phases (e.g., Night -> Day_Dawn).
/// </summary>
internal sealed record MainPhaseHandlerResult(
    ModeratorInstruction? ModeratorInstruction, 
    GamePhase MainPhase, 
    PhaseTransitionReason TransitionReason) : PhaseHandlerResult(ModeratorInstruction)
{
    /// <summary>
    /// Creates a main phase transition result.
    /// </summary>
    public static MainPhaseHandlerResult TransitionPhase(ModeratorInstruction nextInstruction, GamePhase mainPhase, PhaseTransitionReason transitionReason) =>
        new(nextInstruction, mainPhase, transitionReason);
}

/// <summary>
/// For transitioning between sub-phases (e.g., Night.Start -> Night.ActionLoop).
/// </summary>
internal sealed record SubPhaseHandlerResult(
    ModeratorInstruction? ModeratorInstruction, 
    Enum SubGamePhase) : PhaseHandlerResult(ModeratorInstruction)
{
    /// <summary>
    /// Creates a sub-phase transition result.
    /// </summary>
    public static SubPhaseHandlerResult TransitionSubPhase(ModeratorInstruction nextInstruction, Enum subGamePhase) =>
        new(nextInstruction, subGamePhase);
}

/// <summary>
/// For remaining in the current sub-phase (e.g., while awaiting hook listener input).
/// </summary>
internal sealed record StayInSubPhaseHandlerResult(
    ModeratorInstruction? ModeratorInstruction) : PhaseHandlerResult(ModeratorInstruction)
{
    /// <summary>
    /// Creates a stay-in-sub-phase result.
    /// </summary>
    public static StayInSubPhaseHandlerResult StayInSubPhase(ModeratorInstruction nextInstruction) =>
        new(nextInstruction);
}
