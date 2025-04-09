using System;
using Werewolves.Core.Enums;

namespace Werewolves.Core.Models.StateMachine;

/// <summary>
/// Documents and defines a potential transition out of a game phase,
/// including the input expected upon arrival at the target phase.
/// </summary>
/// <param name="TargetPhase">The phase transitioned to.</param>
/// <param name="ConditionOrReason">A unique key describing why this specific transition occurs (e.g., "Confirmed", "VoteTied", "PlayerEliminated"). Used for validation lookup.</param>
/// <param name="ExpectedInputOnArrival">The input type the TargetPhase should expect immediately following this specific transition.</param>
public record PhaseTransitionInfo(
    GamePhase TargetPhase,
    string? ConditionOrReason,
    ExpectedInputType ExpectedInputOnArrival
); 