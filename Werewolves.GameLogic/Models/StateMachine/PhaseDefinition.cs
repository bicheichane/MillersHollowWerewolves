using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.GameLogic.Services;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.StateMachine;

/// <summary>
/// Represents the definition and behavior of a single GetCurrentPhase.
/// </summary>
/// <param name="ProcessInputAndUpdatePhase">Handler function for the phase.</param>
/// <param name="PossiblePhaseTransitions">List of valid exit transitions for documentation and validation.</param>
public record PhaseDefinition(
    Func<GameSession, ModeratorResponse, GameService, PhaseHandlerResult> ProcessInputAndUpdatePhase,
    IReadOnlyList<PhaseTransitionInfo>? PossiblePhaseTransitions = null
); 