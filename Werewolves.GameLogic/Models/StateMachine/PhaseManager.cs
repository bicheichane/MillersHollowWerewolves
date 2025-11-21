using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels.Core;

namespace Werewolves.GameLogic.Models.StateMachine;


/// <summary>
/// Generic phase definition that manages a declarative map of sub-phase stages.
/// Implements IPhaseDefinition to allow the main PhaseDefinitions dictionary to hold phase handlers for different sub-phase enums.
/// </summary>
/// <typeparam name="TSubPhaseEnum">The enum type defining the sub-phases for this phase.</typeparam>
internal class PhaseManager<TSubPhaseEnum> : IPhaseDefinition where TSubPhaseEnum : struct, Enum
{
    private readonly Dictionary<TSubPhaseEnum, SubPhaseManager<TSubPhaseEnum>> _subPhaseDictionary;
    private readonly TSubPhaseEnum _entrySubPhase;

    /// <summary>
    /// Creates a new PhaseManager with the specified subPhaseList and entry point.
    /// </summary>
    /// <param name="subPhaseList">List of sub-phase subPhaseList that define the state machine.</param>
    /// <param name="entrySubPhase">The default entry sub-phase when no sub-phase state is cached.</param>
    public PhaseManager(TSubPhaseEnum entrySubPhase, List<SubPhaseManager<TSubPhaseEnum>> subPhaseList)
    {
        _entrySubPhase = entrySubPhase;
        
        // Validate that all subPhaseList are unique and build the lookup dictionary
        var duplicateStages = subPhaseList.GroupBy(s => s.StartSubPhase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
            
        if (duplicateStages.Count > 0)
        {
            throw new ArgumentException($"Duplicate sub-phase subPhaseList found for: {string.Join(", ", duplicateStages)}");
        }
        
        _subPhaseDictionary = subPhaseList.ToDictionary(s => s.StartSubPhase);
    }

	/// <summary>
	/// Processes input and updates the phase state according to the defined state machine rules.
	/// Loops internally until a ModeratorInstruction is produced.
	/// </summary>
	/// <param name="session">The current game session.</param>
	/// <param name="input">The moderator response to process.</param>
	/// <returns>A PhaseHandlerResult indicating the outcome of the processing.</returns>
	public PhaseHandlerResult ProcessInputAndUpdatePhase(GameSession session, ModeratorResponse input)
    {
        PhaseHandlerResult result;
        do
        {
            // 1. Determine the current sub-phase state, defaulting to the defined entry point
            var subPhaseState = session.GetSubPhase<TSubPhaseEnum>() ?? _entrySubPhase;

            // 2. Find the corresponding stage definition
            if (!_subPhaseDictionary.TryGetValue(subPhaseState, out var subPhase))
            {
                throw new InvalidOperationException(
                    $"Internal State Machine Error: No sub-phase stage definition found for phase '{session.GetCurrentPhase()}' and sub-phase '{subPhaseState}'.");
            }

            // 3. Execute the specific handler for this stage
            result = subPhase.Execute(session, input);

            // 4. Validate the resulting transition against the declarative rules of the stage
            AttemptTransition(session, subPhaseState, result, subPhase);
        } while (result.ModeratorInstruction == null);
        

        return result;
    }
    
    /// <summary>
    /// Validates that the transition result is allowed by the stage's declarative rules.
    /// </summary>
    private void AttemptTransition(
        GameSession session,
        TSubPhaseEnum currentSubPhase, 
        PhaseHandlerResult result, 
        SubPhaseManager<TSubPhaseEnum> subPhaseManager)
    {
        switch (result)
        {
            case SubPhaseHandlerResult subPhaseResult:
            {
                var nextSubPhase = (TSubPhaseEnum)subPhaseResult.SubGamePhase;
                var allowed = subPhaseManager.PossibleNextSubPhases;
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
                var allowed = subPhaseManager.PossibleNextMainPhaseTransitions;
                var requested = new PhaseTransitionInfo(mainPhaseResult.MainPhase);
                if (allowed == null || !allowed.Contains(requested))
                {
                     throw new InvalidOperationException(
                        $"Internal State Machine Error: Illegal main-phase transition from '{currentSubPhase}' to '{requested.TargetPhase}'. " +
                        $"Valid main phase transitions are: {(allowed == null ? "None" : string.Join(", ", allowed))}.");
                }

                session.TransitionMainPhase(
                    mainPhaseResult.MainPhase);
				break;
            }
            case StayInSubPhaseHandlerResult stayInSubPhase:
                session.CompleteSubPhaseStage();
				break;
        }
    }
}
