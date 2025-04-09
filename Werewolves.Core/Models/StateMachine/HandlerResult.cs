using Werewolves.Core.Models.Log;
using System;

namespace Werewolves.Core.Models.StateMachine;

/// <summary>
/// Represents the outcome of a phase handler's execution.
/// </summary>
/// <param name="IsSuccess">Indicates if processing was successful.</param>
/// <param name="NextInstruction">The instruction for the moderator for the next step.</param>
/// <param name="TransitionReason">The ConditionOrReason key matching the PhaseTransitionInfo for the transition that occurred. Null if no phase transition happened.</param>
/// <param name="UseDefaultInstructionForNextPhase">If true, signals ProcessModeratorInput to use the target phase's DefaultEntryInstruction instead of NextInstruction.</param>
/// <param name="Error">Error details if IsSuccess is false.</param>
public record HandlerResult(
    bool IsSuccess,
    ModeratorInstruction? NextInstruction,
    string? TransitionReason,
    bool UseDefaultInstructionForNextPhase = false,
    GameError? Error = null
)
{
    // Static factory methods
    public static HandlerResult SuccessTransition(ModeratorInstruction nextInstruction, string transitionReason) =>
        new(true, nextInstruction, transitionReason, false, null);
    public static HandlerResult SuccessTransitionUseDefault(string transitionReason) =>
        new(true, null, transitionReason, true, null);
    public static HandlerResult SuccessStayInPhase(ModeratorInstruction nextInstruction) =>
        new(true, nextInstruction, null, false, null);
    public static HandlerResult Failure(GameError error) =>
        new(false, null, null, false, error);
} 