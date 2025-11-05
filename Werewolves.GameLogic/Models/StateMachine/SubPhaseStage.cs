using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels.Core;

namespace Werewolves.GameLogic.Models.StateMachine;

/// <summary>
/// Defines a single, validated stage within a main game phase's state machine.
/// </summary>
/// <typeparam name="TSubPhaseEnum">The enum type defining the sub-phases for the parent phase.</typeparam>
internal record SubPhaseStage<TSubPhaseEnum> where TSubPhaseEnum : struct, Enum
{
    /// <summary>
    /// The specific sub-phase that triggers this stage.
    /// </summary>
    public TSubPhaseEnum StartSubPhase { get; init; }
    
    /// <summary>
    /// The handler function that executes the logic for this stage.
    /// </summary>
    public required Func<GameSession, ModeratorResponse, PhaseHandlerResult> Handler { get; init; }

    /// <summary>
    /// A declarative set of all valid sub-phases that this stage is allowed to transition to.
    /// If null, any sub-phase transition is considered an error.
    /// </summary>
    public HashSet<TSubPhaseEnum>? PossibleNextSubPhases { get; init; }

    /// <summary>
    /// A declarative set of all valid main phase transitions that this stage is allowed to initiate.
    /// If null, any main phase transition is considered an error.
    /// </summary>
    public HashSet<PhaseTransitionInfo>? PossibleNextMainPhaseTransitions { get; init; }
}
