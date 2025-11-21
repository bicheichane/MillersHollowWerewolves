using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.InternalMessages;

/// <summary>
/// Abstract base record representing the outcome of a phase handler's execution.
/// </summary>
internal abstract record PhaseHandlerResult(ModeratorInstruction? ModeratorInstruction);

/// <summary>
/// This only exists to group MajorNavigationPhaseHandlerResults: MainPhaseHandlerResult and SubPhaseHandlerResult.
/// </summary>
/// <param name="ModeratorInstruction"></param>
internal abstract record MajorNavigationPhaseHandlerResult(ModeratorInstruction? ModeratorInstruction)
    : PhaseHandlerResult(ModeratorInstruction);

/// <summary>
/// For transitioning between main phases (e.g., Night -> Day_Dawn).
/// </summary>
internal sealed record MainPhaseHandlerResult(
    ModeratorInstruction? ModeratorInstruction, 
    GamePhase MainPhase) : MajorNavigationPhaseHandlerResult(ModeratorInstruction)
{
    /// <summary>
    /// Creates a main phase transition result.
    /// </summary>
    public static MainPhaseHandlerResult TransitionPhase(ModeratorInstruction nextInstruction, GamePhase mainPhase) =>
        new(nextInstruction, mainPhase);
}

/// <summary>
/// For transitioning between sub-phases (e.g., Night.Start -> Night.ActionLoop).
/// </summary>
internal sealed record SubPhaseHandlerResult(
    ModeratorInstruction? ModeratorInstruction, 
    Enum SubGamePhase) : MajorNavigationPhaseHandlerResult(ModeratorInstruction)
{
    /// <summary>
    /// Creates a sub-phase transition result.
    /// </summary>
    public static SubPhaseHandlerResult TransitionSubPhase(ModeratorInstruction nextInstruction, Enum subGamePhase) =>
        new(nextInstruction, subGamePhase);

    public static SubPhaseHandlerResult TransitionSubPhaseSilent(Enum subGamePhase) =>
        new(null, subGamePhase);
}

/// <summary>
/// For remaining in the current sub-phase (e.g., while awaiting hook listener input).
/// If ModeratorInstruction is null, no action is required by the moderator.
/// So next sub-phase stage execution will proceed automatically.
/// </summary>
internal sealed record StayInSubPhaseHandlerResult(
    ModeratorInstruction? ModeratorInstruction) : PhaseHandlerResult(ModeratorInstruction)
{
	/// <summary>
	/// Creates a stay-in-sub-phase result.
	/// </summary>
	/// <param name="nextInstruction"></param>
	/// <returns></returns>
	public static StayInSubPhaseHandlerResult StayInSubPhase(ModeratorInstruction? nextInstruction) =>
        new(nextInstruction);
}
