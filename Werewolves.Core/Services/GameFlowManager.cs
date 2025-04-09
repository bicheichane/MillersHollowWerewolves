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
		// REMOVED: Define transition reason constants
		// const string ReasonSetupConfirmed = "SetupConfirmed";
        // const string ReasonNightStartsConfirmed = "NightStartsConfirmed";
        // const string ReasonIdentifiedAndProceedToWwAction = "IdentifiedAndProceedToWwAction";
        // const string ReasonWwActionComplete = "WwActionComplete";
        // const string ReasonNightResolutionConfirmedProceedToReveal = "NightResolutionConfirmedProceedToReveal";
        // const string ReasonNightResolutionConfirmedNoVictims = "NightResolutionConfirmedNoVictims";
        // const string ReasonRoleRevealedProceedToDebate = "RoleRevealedProceedToDebate";
        // const string ReasonRoleRevealedProceedToNight = "RoleRevealedProceedToNight";
        // const string ReasonDebateConfirmedProceedToVote = "DebateConfirmedProceedToVote";
        // const string ReasonVoteOutcomeReported = "VoteOutcomeReported";
        // const string ReasonVoteResolvedProceedToReveal = "VoteResolvedProceedToReveal";
        // const string ReasonVoteResolvedTieProceedToNight = "VoteResolvedTieProceedToNight";
        // const string ReasonVictoryConditionMet = "VictoryConditionMet"; // Implicit transition handled by GameService

		// Note: GameService reference needed for handlers is passed via the Func<> signature.

		return new Dictionary<GamePhase, PhaseDefinition>
        {
            [GamePhase.Setup] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleSetupPhase, // Static reference to the method
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Night, PhaseTransitionReason.SetupConfirmed, ExpectedInputType.Confirmation) // Use Enum
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
                    // These transitions are initiated *inside* HandleNightPhase based on internal logic.
                    // The HandlerResult provides the reason.
                    // We list possible outcomes here for documentation/validation.
                    // Need to revisit if this validation logic in GameService.ProcessModeratorInput needs adjustment
                    // based on how HandleNightPhase now returns its reasons.
                    new(GamePhase.Night, PhaseTransitionReason.NightStartsConfirmed, ExpectedInputType.PlayerSelectionMultiple), // -> WW ID (If N1 ID needed)
                    new(GamePhase.Night, PhaseTransitionReason.IdentifiedAndProceedToWwAction, ExpectedInputType.PlayerSelectionSingle), // -> WW Action (Post ID)
                    new(GamePhase.Day_ResolveNight, PhaseTransitionReason.WwActionComplete, ExpectedInputType.Confirmation) // -> Resolve Night
                    // Note: The exact ExpectedInputOnArrival might depend on what GenerateNextNightInstruction returns.
                    // Confirmation is the final expected input when transitioning out of Night.
                }
            ),

            [GamePhase.Day_ResolveNight] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayResolveNightPhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Event, PhaseTransitionReason.NightResolutionConfirmedProceedToReveal, ExpectedInputType.AssignPlayerRoles), // Use Enum
                    new(GamePhase.Day_Debate, PhaseTransitionReason.NightResolutionConfirmedNoVictims, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

            [GamePhase.Day_Event] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayEventPhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Debate, PhaseTransitionReason.RoleRevealedProceedToDebate, ExpectedInputType.Confirmation), // Use Enum
                    new(GamePhase.Night, PhaseTransitionReason.RoleRevealedProceedToNight, ExpectedInputType.Confirmation) // Use Enum
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
                    new(GamePhase.Day_Vote, PhaseTransitionReason.DebateConfirmedProceedToVote, ExpectedInputType.PlayerSelectionSingle) // Use Enum
                }
            ),

            [GamePhase.Day_Vote] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayVotePhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_ResolveVote, PhaseTransitionReason.VoteOutcomeReported, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

            [GamePhase.Day_ResolveVote] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleDayResolveVotePhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Event, PhaseTransitionReason.VoteResolvedProceedToReveal, ExpectedInputType.AssignPlayerRoles), // Use Enum
                    new(GamePhase.Night, PhaseTransitionReason.VoteResolvedTieProceedToNight, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

            [GamePhase.GameOver] = new PhaseDefinition(
                ProcessInputAndUpdatePhase: GameService.HandleGameOverPhase // Static reference
                // No transitions out of GameOver
            )
        };
    }
} 