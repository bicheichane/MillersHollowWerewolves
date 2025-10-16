using System;
using System.Collections.Generic;
using Werewolves.Core.Enums;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Services;

namespace Werewolves.Core.Models.StateMachine;

/// <summary>
/// Represents the definition and behavior of a single GamePhase.
/// </summary>
/// <param name="ProcessInputAndUpdatePhase">Handler function for the phase.</param>
/// <param name="DefaultEntryInstruction">Optional function for standard entry prompt.</param>
/// <param name="PossibleTransitions">List of valid exit transitions for documentation and validation.</param>
public record PhaseDefinition(
    Func<GameSession, ModeratorInput, GameService, PhaseHandlerResult> ProcessInputAndUpdatePhase,
    Func<GameSession, ModeratorInstruction>? DefaultEntryInstruction = null,
    IReadOnlyList<PhaseTransitionInfo>? PossibleTransitions = null
); 