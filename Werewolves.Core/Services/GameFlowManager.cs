using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Enums;
using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Models.StateMachine;
using Werewolves.Core.Resources;

namespace Werewolves.Core.Services;

/// <summary>
/// Holds the state machine configuration and provides access to phase definitions.
/// </summary>
public class GameFlowManager
{
    private readonly Dictionary<GamePhase, PhaseDefinition> _phaseDefinitions;

    // Dependency injection for role implementations
    private readonly Dictionary<RoleType, IRole> _roleImplementations;

    public GameFlowManager(Dictionary<RoleType, IRole> roleImplementations)
    {
        _roleImplementations = roleImplementations ?? throw new ArgumentNullException(nameof(roleImplementations));
        _phaseDefinitions = BuildPhaseDefinitions();
    }

    public PhaseDefinition GetPhaseDefinition(GamePhase phase)
    {
        if (_phaseDefinitions.TryGetValue(phase, out var definition))
        {
            return definition;
        }
        // TODO: Use GameStrings.PhaseDefinitionNotFound
        throw new KeyNotFoundException($"Phase definition not found for {phase}");
    }

    private Dictionary<GamePhase, PhaseDefinition> BuildPhaseDefinitions()
    {
		// Define transition reason constants
		const string ReasonSetupConfirmed = "SetupConfirmed";
        const string ReasonNightStartsConfirmed = "NightStartsConfirmed";
        const string ReasonIdentifiedAndProceedToWwAction = "IdentifiedAndProceedToWwAction";
        const string ReasonWwActionComplete = "WwActionComplete";
        const string ReasonNightResolutionConfirmedProceedToReveal = "NightResolutionConfirmedProceedToReveal";
        const string ReasonNightResolutionConfirmedNoVictims = "NightResolutionConfirmedNoVictims";
        const string ReasonRoleRevealedProceedToDebate = "RoleRevealedProceedToDebate";
        const string ReasonRoleRevealedProceedToNight = "RoleRevealedProceedToNight";
        const string ReasonDebateConfirmedProceedToVote = "DebateConfirmedProceedToVote";
        const string ReasonVoteOutcomeReported = "VoteOutcomeReported";
        const string ReasonVoteResolvedProceedToReveal = "VoteResolvedProceedToReveal";
        const string ReasonVoteResolvedTieProceedToNight = "VoteResolvedTieProceedToNight";
        const string ReasonVictoryConditionMet = "VictoryConditionMet"; // Implicit transition handled by GameService

		// Note: GameService reference needed for handlers is passed via the Func<> signature.

		return new Dictionary<GamePhase, PhaseDefinition>
        {
            [GamePhase.Setup] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleSetupPhase, // Static reference to the method
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Night, ReasonSetupConfirmed, ExpectedInputType.Confirmation)
                }
            ),

            [GamePhase.Night] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleNightPhase, // Static reference
                DefaultEntryInstruction: session => new ModeratorInstruction
                {
                    InstructionText = GameStrings.NightStartsPrompt,
                    ExpectedInputType = ExpectedInputType.Confirmation
                },
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Night, ReasonNightStartsConfirmed, ExpectedInputType.PlayerSelectionMultiple), // -> WW ID
                    new(GamePhase.Night, ReasonIdentifiedAndProceedToWwAction, ExpectedInputType.PlayerSelectionSingle), // -> WW Action
                    new(GamePhase.Day_ResolveNight, ReasonWwActionComplete, ExpectedInputType.Confirmation) // -> Resolve Night
                }
            ),

            [GamePhase.Day_ResolveNight] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayResolveNightPhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Event, ReasonNightResolutionConfirmedProceedToReveal, ExpectedInputType.AssignPlayerRoles),
                    new(GamePhase.Day_Debate, ReasonNightResolutionConfirmedNoVictims, ExpectedInputType.Confirmation)
                }
            ),

            [GamePhase.Day_Event] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayEventPhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Debate, ReasonRoleRevealedProceedToDebate, ExpectedInputType.Confirmation),
                    new(GamePhase.Night, ReasonRoleRevealedProceedToNight, ExpectedInputType.Confirmation)
                }
            ),

            [GamePhase.Day_Debate] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayDebatePhase, // Static reference
                DefaultEntryInstruction: session => new ModeratorInstruction
                {
                    // TODO: Use GameStrings.ProceedToVotePrompt
                    InstructionText = "Proceed to Vote?",
                    ExpectedInputType = ExpectedInputType.Confirmation
                },
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Vote, ReasonDebateConfirmedProceedToVote, ExpectedInputType.PlayerSelectionSingle)
                }
            ),

            [GamePhase.Day_Vote] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayVotePhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_ResolveVote, ReasonVoteOutcomeReported, ExpectedInputType.Confirmation)
                }
            ),

            [GamePhase.Day_ResolveVote] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayResolveVotePhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Event, ReasonVoteResolvedProceedToReveal, ExpectedInputType.AssignPlayerRoles),
                    new(GamePhase.Night, ReasonVoteResolvedTieProceedToNight, ExpectedInputType.Confirmation)
                }
            ),

            [GamePhase.GameOver] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleGameOverPhase // Static reference
                // No transitions out of GameOver
            )
        };
    }
} 