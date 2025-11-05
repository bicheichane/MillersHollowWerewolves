using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels.Core;

namespace Werewolves.GameLogic.Models.StateMachine;

/// <summary>
/// Interface for phase definitions that can handle different sub-phase enum types.
/// Allows the main PhaseDefinitions dictionary to hold phase handlers for different, unrelated sub-phase enums.
/// </summary>
internal interface IPhaseDefinition
{
    /// <summary>
    /// Processes input and updates the phase state according to the defined state machine rules.
    /// </summary>
    /// <param name="session">The current game session.</param>
    /// <param name="input">The moderator response to process.</param>
    /// <returns>A PhaseHandlerResult indicating the outcome of the processing.</returns>
    PhaseHandlerResult ProcessInputAndUpdatePhase(GameSession session, ModeratorResponse input);
}
