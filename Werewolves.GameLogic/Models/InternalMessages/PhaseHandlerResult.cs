using Werewolves.GameLogic.Models;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.InternalMessages;

/// <summary>
/// Represents the outcome of a phase handler's execution.
/// </summary>
/// <param name="IsSuccess">Indicates if processing was successful.</param>
/// <param name="ModeratorInstruction">The instruction for the moderator for the next step.</param>
/// <param name="TransitionReason">The ConditionOrReason key matching the PhaseTransitionInfo for the transition that occurred. Null if no phase transition happened.</param>
/// <param name="Error">Error details if IsSuccess is false.</param>
public record PhaseHandlerResult(
    bool IsSuccess,
    ModeratorInstruction? ModeratorInstruction,
    PhaseTransitionReason? TransitionReason,
    GameError? Error = null
)
{
    // Static factory methods
    public static PhaseHandlerResult SuccessTransition(ModeratorInstruction nextInstruction, PhaseTransitionReason transitionReason) =>
        new(true, nextInstruction, transitionReason,  null);
    public static PhaseHandlerResult SuccessStayInPhase(ModeratorInstruction nextInstruction) =>
        new(true, nextInstruction, null, null);

    public static PhaseHandlerResult SuccessInternalGeneric() => new(true, null, null);
    public static PhaseHandlerResult Failure(GameError error) =>
        new(false, null, null, error);

    public bool ShouldTransitionPhase => IsSuccess && TransitionReason != null;
} 