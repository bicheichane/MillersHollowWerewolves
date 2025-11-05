using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.GameLogic.Services;
using Werewolves.StateModels;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.GameLogic.Models.StateMachine;

/// <summary>
/// Generic phase definition that manages a declarative map of sub-phase stages.
/// Implements IPhaseDefinition to allow the main PhaseDefinitions dictionary to hold phase handlers for different sub-phase enums.
/// </summary>
/// <typeparam name="TSubPhaseEnum">The enum type defining the sub-phases for this phase.</typeparam>
internal class PhaseDefinition<TSubPhaseEnum> : IPhaseDefinition where TSubPhaseEnum : struct, Enum
{
    private readonly Dictionary<TSubPhaseEnum, SubPhaseStage<TSubPhaseEnum>> _subPhaseStages;
    private readonly TSubPhaseEnum _entrySubPhase;

    /// <summary>
    /// Creates a new PhaseDefinition with the specified stages and entry point.
    /// </summary>
    /// <param name="stages">List of sub-phase stages that define the state machine.</param>
    /// <param name="entrySubPhase">The default entry sub-phase when no sub-phase state is cached.</param>
    public PhaseDefinition(List<SubPhaseStage<TSubPhaseEnum>> stages, TSubPhaseEnum entrySubPhase)
    {
        _entrySubPhase = entrySubPhase;
        
        // Validate that all stages are unique and build the lookup dictionary
        var duplicateStages = stages.GroupBy(s => s.StartSubPhase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
            
        if (duplicateStages.Any())
        {
            throw new ArgumentException($"Duplicate sub-phase stages found for: {string.Join(", ", duplicateStages)}");
        }
        
        _subPhaseStages = stages.ToDictionary(s => s.StartSubPhase);
    }

    /// <summary>
    /// Processes input and updates the phase state according to the defined state machine rules.
    /// </summary>
    /// <param name="session">The current game session.</param>
    /// <param name="input">The moderator response to process.</param>
    /// <returns>A PhaseHandlerResult indicating the outcome of the processing.</returns>
    public PhaseHandlerResult ProcessInputAndUpdatePhase(GameSession session, ModeratorResponse input)
    {
        // 1. Determine the current sub-phase state, defaulting to the defined entry point
        var subPhaseState = session.GetSubPhase<TSubPhaseEnum>() ?? _entrySubPhase;

        // 2. Find the corresponding stage definition
        if (!_subPhaseStages.TryGetValue(subPhaseState, out var stageToExecute))
        {
            throw new InvalidOperationException($"Internal State Machine Error: No sub-phase stage definition found for phase '{session.GetCurrentPhase()}' and sub-phase '{subPhaseState}'.");
        }

        // 3. Execute the specific handler for this stage
        var result = stageToExecute.Handler(session, input);

        // 4. Validate the resulting transition against the declarative rules of the stage
        AttemptTransition(session, subPhaseState, result, stageToExecute);

        return result;
    }
    
    /// <summary>
    /// Validates that the transition result is allowed by the stage's declarative rules.
    /// </summary>
    private void AttemptTransition(
        GameSession session,
        TSubPhaseEnum currentSubPhase, 
        PhaseHandlerResult result, 
        SubPhaseStage<TSubPhaseEnum> executedStage)
    {
        switch (result)
        {
            case SubPhaseHandlerResult subPhaseResult:
            {
                var nextSubPhase = (TSubPhaseEnum)subPhaseResult.SubGamePhase;
                var allowed = executedStage.PossibleNextSubPhases;
                if (allowed == null || !allowed.Contains(nextSubPhase))
                {
                    throw new InvalidOperationException(
                        $"Internal State Machine Error: Illegal sub-phase transition from '{currentSubPhase}' to '{nextSubPhase}'. " +
                        $"Valid next sub-phases are: {(allowed == null ? "None" : string.Join(", ", allowed))}.");
                }

                session.TransitionSubPhase(subPhaseResult.SubGamePhase);
				break;
            }
            case MainPhaseHandlerResult mainPhaseResult:
            {
                var allowed = executedStage.PossibleNextMainPhaseTransitions;
                var requested = new PhaseTransitionInfo(mainPhaseResult.MainPhase, mainPhaseResult.TransitionReason);
                if (allowed == null || !allowed.Contains(requested))
                {
                     throw new InvalidOperationException(
                        $"Internal State Machine Error: Illegal main-phase transition from '{currentSubPhase}' to '{requested.TargetPhase}' with reason '{requested.ConditionOrReason}'. " +
                        $"Valid main phase transitions are: {(allowed == null ? "None" : string.Join(", ", allowed))}.");
                }

                session.TransitionMainPhase(
                    mainPhaseResult.MainPhase,
                    mainPhaseResult.TransitionReason);
				break;
            }
            case StayInSubPhaseHandlerResult:
                // This result type explicitly signals the intent to not transition, so no validation is needed
                break;
        }
    }
}

/// <summary>
/// Legacy PhaseDefinition record for backward compatibility during migration.
/// This will be removed once all phases are migrated to the new declarative approach.
/// </summary>
/// <param name="ProcessInputAndUpdatePhase">Handler function for the phase.</param>
/// <param name="PossiblePhaseTransitions">List of valid exit transitions for documentation and validation.</param>
internal record PhaseDefinition(
    Func<GameSession, ModeratorResponse, PhaseHandlerResult> ProcessInputAndUpdatePhase,
    IReadOnlyList<PhaseTransitionInfo>? PossiblePhaseTransitions = null
) : IPhaseDefinition
{
    /// <summary>
    /// Processes input and updates the phase state using the legacy handler function.
    /// </summary>
    /// <param name="session">The current game session.</param>
    /// <param name="input">The moderator response to process.</param>
    /// <returns>A PhaseHandlerResult indicating the outcome of the processing.</returns>
    PhaseHandlerResult IPhaseDefinition.ProcessInputAndUpdatePhase(GameSession session, ModeratorResponse input)
    {
        return ProcessInputAndUpdatePhase(session, input);
    }
}
