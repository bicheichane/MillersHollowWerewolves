using Werewolves.GameLogic.Interfaces;
using Werewolves.GameLogic.Models;
using Werewolves.GameLogic.Models.GameHookListeners;
using Werewolves.GameLogic.Models.Instructions;
using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.GameLogic.Models.StateMachine;
using Werewolves.GameLogic.Roles;
using Werewolves.GameLogic.Roles.MainRoles;
using Werewolves.StateModels;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Extensions;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;
using static Werewolves.GameLogic.Models.InternalMessages.MainPhaseHandlerResult;
using static Werewolves.GameLogic.Models.InternalMessages.PhaseHandlerResult;
using static Werewolves.GameLogic.Models.InternalMessages.SubPhaseHandlerResult;
using static Werewolves.StateModels.Enums.MainRoleType;
using static Werewolves.StateModels.Enums.SecondaryRoleType;
using static Werewolves.StateModels.Models.ListenerIdentifier;

namespace Werewolves.GameLogic.Services;

/// <summary>
/// Holds the state machine configuration and provides access to phase definitions.
/// </summary>
internal static class GameFlowManager
{
    #region Static Flow Definitions
    private static readonly Dictionary<GameHook, List<ListenerIdentifier>> HookListeners = new()
    {
        // Define hook-to-listener mappings here.
        // ORDER MATTERS!!!!
        [GameHook.NightActionLoop] =
        [
            Listener(Thief),                //first night only
            Listener(Actor),
            Listener(LittleGirl),           //first night only
            Listener(Cupid),                //first night only
            Listener(Lovers),              //first night only
            Listener(Fox),
            Listener(StutteringJudge),      //first night only
            Listener(TwoSisters),
            Listener(ThreeBrothers),
            Listener(WildChild),            //first night only
            Listener(BearTamer),            //first night only
            Listener(Defender),
			Listener(SimpleWerewolf),
            Listener(AccursedWolfFather),
            Listener(BigBadWolf),
			Listener(Seer),
            Listener(Witch),
            Listener(Gypsy),
            Listener(Piper),
            Listener(Charmed)
		],

        [GameHook.DayBreakAfterVictims] =
        [
            Listener(BearTamer),
            Listener(Gypsy),
            Listener(TownCrier),
        ],

        [GameHook.OnFirstVoteConcluded] =
        [
            Listener(StutteringJudge),
		],

        [GameHook.OnPlayerEliminationFinalized] =
        [
            Listener(Hunter),
            Listener(Lovers)
		]
	};

    private static readonly Dictionary<ListenerIdentifier, IGameHookListener> ListenerImplementations = new()
    {
        // Define listener implementations here
        [Listener(SimpleWerewolf)] = new SimpleWerewolfRole(),
        [Listener(Seer)] = new SeerRole(),
        [Listener(SimpleVillager)] = new SimpleVillagerRole()
    };

    private static readonly Dictionary<GamePhase, PhaseDefinition> PhaseDefinitions = new()
    {
        [GamePhase.Setup] = new(
            ProcessInputAndUpdatePhase: HandleSetupPhase,
            PossiblePhaseTransitions: [
                new(GamePhase.Night, PhaseTransitionReason.SetupConfirmed)]
        ),

        [GamePhase.Night] = new(
            ProcessInputAndUpdatePhase: HandleNightPhase,
            PossiblePhaseTransitions: [
                new(GamePhase.Day_Dawn, PhaseTransitionReason.NightActionLoopComplete)]
        ),

        [GamePhase.Day_Dawn] = new(
            ProcessInputAndUpdatePhase: HandleDayDawnPhase,
            PossiblePhaseTransitions: [
                new(GamePhase.Day_Debate, PhaseTransitionReason.DawnNoVictimsProceedToDebate), 
                new(GamePhase.Day_Debate, PhaseTransitionReason.DawnVictimsProceedToDebate)]
        ),

        [GamePhase.Day_Debate] = new(
            ProcessInputAndUpdatePhase: HandleDayDebatePhase,
            PossiblePhaseTransitions: [
                new(GamePhase.Day_Vote, PhaseTransitionReason.DebateConfirmedProceedToVote)]
        ),

        [GamePhase.Day_Vote] = new(
            ProcessInputAndUpdatePhase: HandleDayVotePhase,
            PossiblePhaseTransitions: [
                new(GamePhase.Day_Dusk, PhaseTransitionReason.VoteOutcomeReported)]
        ),

        [GamePhase.Day_Dusk] = new(
            ProcessInputAndUpdatePhase: HandleDayDuskPhase,
            PossiblePhaseTransitions: [
                new(GamePhase.Day_Dawn, PhaseTransitionReason.VoteResolvedProceedToReveal),
                new(GamePhase.Night, PhaseTransitionReason.VoteResolvedTieProceedToNight)]
        ),

        [GamePhase.AccusationVoting] = new(
            ProcessInputAndUpdatePhase: HandleAccusationVotingPhase,
            PossiblePhaseTransitions: []
        ),

        [GamePhase.FriendVoting] = new(
            ProcessInputAndUpdatePhase: HandleFriendVotingPhase,
            PossiblePhaseTransitions: []
        ),

        [GamePhase.GameOver] = new(
            ProcessInputAndUpdatePhase: HandleGameOverPhase
            // No transitions out of GameOver
        )
    };
    #endregion

    #region State Machine

    internal static ProcessResult HandleInput(GameService service, GameSession session, ModeratorResponse input)
    {
        
        ModeratorInstruction? nextInstructionToSend = null;
        
    
        var currentPhase = session.GetCurrentPhase();
        
        // --- Execute Phase Handler ---
        PhaseHandlerResult handlerResult = ProcessInputAndUpdatePhase(session, input);

        if (handlerResult is MainPhaseHandlerResult mainPhaseResult)
        {
            var previousPhaseDef = PhaseDefinitions[currentPhase];

            var possibleTransitions =
                previousPhaseDef.PossiblePhaseTransitions ?? (List<PhaseTransitionInfo>)[];

            var transitionInfo = possibleTransitions.FirstOrDefault(t =>
                t.TargetPhase == mainPhaseResult.MainPhase &&
                t.ConditionOrReason == mainPhaseResult.TransitionReason);

            if (transitionInfo == null)
            {
                throw new InvalidOperationException(
                    $"Internal State Machine Error: Undocumented transition from '{currentPhase}' to '{mainPhaseResult.MainPhase}' with reason '{mainPhaseResult.TransitionReason}'");
            }

			session.TransitionMainPhase(
                mainPhaseResult.MainPhase,
                mainPhaseResult.TransitionReason);
		}
        else 
        if (handlerResult is SubPhaseHandlerResult { SubGamePhase: not null } subPhaseResult)
        {
            session.TransitionSubPhase(subPhaseResult.SubGamePhase);
        }


		nextInstructionToSend = handlerResult.ModeratorInstruction;

		if(TryGetVictoryInstructions(session, currentPhase, out var victoryInstruction))
        {
            nextInstructionToSend = victoryInstruction;
		}

		// --- Update Pending Instruction ---
		session.PendingModeratorInstruction = nextInstructionToSend;

		// If no victory override, return the determined success result
		return ProcessResult.Success(nextInstructionToSend);
    }

    private static bool TryGetVictoryInstructions(GameSession session, GamePhase currentPhase,
        out ModeratorInstruction? nextInstructionToSend)
    {
        nextInstructionToSend = null;
        // --- Post-Processing: Victory Check ---
        // Check victory ONLY after specific resolution phases
        if (currentPhase == GamePhase.Day_Dawn || currentPhase == GamePhase.Day_Dusk)
        {
            var victoryCheckResult = CheckVictoryConditions(session);
            if (victoryCheckResult != null)
            {
                // Victory condition met!
                session.VictoryConditionMet(victoryCheckResult.Value.WinningTeam, victoryCheckResult.Value.Description);

                var finalInstruction = new ConfirmationInstruction(
                    String.Format(GameStrings.GameOverMessage, victoryCheckResult.Value.Description)
                );
                nextInstructionToSend = finalInstruction; // Override instruction
                return true;
            }
        }

		return false;
    }

    private static PhaseHandlerResult ProcessInputAndUpdatePhase(GameSession session, ModeratorResponse input)
    {
        var currentPhase = session.GetCurrentPhase();
        
        if (!PhaseDefinitions.TryGetValue(currentPhase, out var phaseDef))
        {
            throw new InvalidOperationException($"No phase definition found for phase: {currentPhase}");
        }

        // Get service reference - this would need to be passed in or stored
        // For now, we'll need to refactor this method signature or find another approach
        // TODO: Fix service reference issue
        return phaseDef.ProcessInputAndUpdatePhase(session, input, null!); // Temporary null for service
    }

    private static (Team WinningTeam, string Description)? CheckVictoryConditions(GameSession session)
    {
        // Phase 1: Basic checks using assigned/revealed roles
        var aliveWerewolves = session.GetPlayers().WithHealth(PlayerHealth.Alive).WithRole(SimpleWerewolf).Count();
        int aliveNonWerewolves = session.GetPlayers().WithHealth(PlayerHealth.Alive).WithoutRole(SimpleWerewolf).Count();

		// Villager win
		if (aliveWerewolves == 0 && aliveNonWerewolves > 0)
        {
            return (Team.Villagers, GameStrings.VictoryConditionAllWerewolvesEliminated);
        }

        // Werewolf win
        if (aliveWerewolves >= aliveNonWerewolves && aliveWerewolves > 0)
        {
            return (Team.Werewolves, GameStrings.VictoryConditionWerewolvesOutnumber);
        }

        return null;
    }

    #endregion

    #region Phase Handlers

	private static PhaseHandlerResult HandleSetupPhase(GameSession session, ModeratorResponse input, GameService service)
    {
        var nightStartInstruction = new ConfirmationInstruction(
            publicAnnouncement: GameStrings.NightStartsPrompt,
            privateInstruction: GameStrings.ConfirmNightStarted
        );
        // Transition happens, specific instruction provided.
        
        return TransitionPhase(nightStartInstruction, GamePhase.Night, PhaseTransitionReason.SetupConfirmed);
    }

    /// <summary>
    /// Consolidated Night phase handler with internal sub-phase management.
    /// Handles village sleep, role identification (first night only), and action loop.
    /// </summary>
    private static PhaseHandlerResult HandleNightPhase(GameSession session, ModeratorResponse input, GameService service)
    {
        // Get current sub-phase from cache, default to Start
        var subPhase = session.GetSubPhase<NightSubPhases>() ?? NightSubPhases.Start;

        switch (subPhase)
        {
            case NightSubPhases.Start:
                return HandleNightStart(session, input, service);
            
            case NightSubPhases.ActionLoop:
                return HandleNightActionLoop(session, input, service);
            
            default:
                throw new InvalidOperationException($"Unknown night sub-phase: {subPhase}");
        }
    }

    /// <summary>
    /// Consolidated Day_Dawn phase handler with internal sub-phase management.
    /// Handles victim calculation, announcements, and role reveals.
    /// </summary>
    private static PhaseHandlerResult HandleDayDawnPhase(GameSession session, ModeratorResponse input, GameService service)
    {
        // Get current sub-phase from cache, default to CalculateVictims
        var subPhase = session.GetSubPhase<DawnSubPhases>() ?? DawnSubPhases.CalculateVictims;

        switch (subPhase)
        {
            case DawnSubPhases.CalculateVictims:
                return HandleDawnCalculateVictims(session, input, service);
            
            case DawnSubPhases.AnnounceVictims:
                return HandleDawnAnnounceVictims(session, input, service);
            
            case DawnSubPhases.ProcessRoleReveals:
                return HandleDawnProcessRoleReveals(session, input, service);
            
            case DawnSubPhases.Finalize:
                return HandleDawnFinalize(session, input, service);
            
            default:
                throw new InvalidOperationException(
                    $"Unknown dawn sub-phase: {subPhase}");
        }
    }

    private static PhaseHandlerResult HandleDayDebatePhase(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement debate phase logic
        // For now, transition to vote on confirmation

        var voteInstruction = new ConfirmationInstruction(
            publicAnnouncement: GameStrings.DebateStartsPrompt
        );
        
        return TransitionPhase(voteInstruction, GamePhase.Day_Vote, PhaseTransitionReason.DebateConfirmedProceedToVote);
    }

    private static PhaseHandlerResult HandleDayVotePhase(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement vote phase logic
        // For now, transition to dusk on any input
        var duskInstruction = new ConfirmationInstruction(
            publicAnnouncement: GameStrings.VoteStartsPrompt
        );
        
        return TransitionPhase(duskInstruction, GamePhase.Day_Dusk, PhaseTransitionReason.VoteOutcomeReported);
    }

    private static PhaseHandlerResult HandleDayDuskPhase(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement vote resolution logic
        // For now, transition to night (tie) or dawn (elimination)
        // This would normally depend on the vote outcome
        var nightInstruction = new ConfirmationInstruction(
            publicAnnouncement: GameStrings.NightStartsPrompt
        );
        
        return TransitionPhase(nightInstruction, GamePhase.Night, PhaseTransitionReason.VoteResolvedTieProceedToNight);
    }

    private static PhaseHandlerResult HandleAccusationVotingPhase(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement Nightmare event accusation voting
        throw new NotImplementedException();
    }

    private static PhaseHandlerResult HandleFriendVotingPhase(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement Great Distrust event friend voting
        throw new NotImplementedException();
    }

    private static PhaseHandlerResult HandleGameOverPhase(GameSession session, ModeratorResponse input, GameService service)
    {
        return StayInSubPhase(new ConfirmationInstruction("you won, shut up now"));
    }

    #endregion

    #region Night Phase Helper Methods

    /// <summary>
    /// Handles the Night.Start sub-phase: village goes to sleep, increment turn number.
    /// </summary>
    private static PhaseHandlerResult HandleNightStart(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement village sleep logic
        // - Increment turn number if transitioning from day phase
        // - Issue "Village goes to sleep" instruction
        // - Transition to ActionLoop (unified night actions including first-night identification)

        var instruction = new ConfirmationInstruction(
            publicAnnouncement: "Village goes to sleep. Night actions begin."
        );

        return TransitionSubPhase(instruction, NightSubPhases.ActionLoop);
    }

    /// <summary>
    /// Handles the Night.ActionLoop sub-phase: main role action sequence.
    /// </summary>
    private static PhaseHandlerResult HandleNightActionLoop(GameSession session, ModeratorResponse input, GameService service)
    {
        // Fire the unified night action loop hook
        var hookResult = FireHook(GameHook.NightActionLoop, session, input);

        return HandleHookResult(hookResult, onComplete: () =>
        {
            var instruction = new ConfirmationInstruction(
                publicAnnouncement: "Night actions complete. Village wakes up."
            );

            return TransitionPhase(instruction, GamePhase.Day_Dawn, PhaseTransitionReason.NightActionLoopComplete);
        });
    }

    #endregion

    #region Dawn Phase Helper Methods

    /// <summary>
    /// Handles the Dawn.CalculateVictims sub-phase: process night actions to determine final victims.
    /// </summary>
    private static PhaseHandlerResult HandleDawnCalculateVictims(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement victim calculation logic
        // - Query GameHistoryLog for all night actions
        // - Calculate final list of eliminated players
        // - Process queue of cascading effects (Hunter, Lovers, etc.)
        // - Handle moderator input for targets if needed
        // - Transition to AnnounceVictims when complete

        var instruction = new ConfirmationInstruction(
            publicAnnouncement: "Night victims calculated. Ready to announce."
        );

        return TransitionSubPhase(instruction, DawnSubPhases.AnnounceVictims);
    }

    /// <summary>
    /// Handles the Dawn.AnnounceVictims sub-phase: moderator announces all victims from the night.
    /// </summary>
    private static PhaseHandlerResult HandleDawnAnnounceVictims(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement victim announcement logic
        // - Issue single instruction to announce all victims
        // - Await moderator confirmation to proceed
        // - Transition to ProcessRoleReveals

        var instruction = new ConfirmationInstruction(
            publicAnnouncement: "Victims announced. Ready to process role reveals."
        );

        return TransitionSubPhase(instruction, DawnSubPhases.ProcessRoleReveals);
    }

    /// <summary>
    /// Handles the Dawn.ProcessRoleReveals sub-phase: reveal roles for each eliminated player.
    /// </summary>
    private static PhaseHandlerResult HandleDawnProcessRoleReveals(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement role reveal processing logic
        // - Iterate through eliminated players
        // - For each player, issue instruction to provide revealed role
        // - Pause and resume as input is received for each reveal
        // - Transition to Finalize when complete
        
        var instruction = new ConfirmationInstruction(
            publicAnnouncement: "MainRole reveals processed. Finalizing dawn phase."
        );
        
        return TransitionSubPhase(instruction, DawnSubPhases.Finalize);
    }

    /// <summary>
    /// Handles the Dawn.Finalize sub-phase: complete dawn processing and transition to debate.
    /// </summary>
    private static PhaseHandlerResult HandleDawnFinalize(GameSession session, ModeratorResponse input, GameService service)
    {
        // TODO: Implement dawn finalization logic
        // - Determine if there were victims or not
        // - Transition to Day_Debate with appropriate reason

        var instruction = new ConfirmationInstruction(
            publicAnnouncement: "Dawn phase complete. Beginning debate."
        );

        return TransitionPhase(instruction, GamePhase.Day_Debate, PhaseTransitionReason.DawnNoVictimsProceedToDebate);
    }

    #endregion

    #region Hook System Infrastructure

    /// <summary>
    /// Fires a game hook and dispatches to registered listeners.
    /// Implements the hook dispatch logic for the unified night action loop.
    /// </summary>
    /// <param name="hook">The hook to fire.</param>
    /// <param name="session">The current game session.</param>
    /// <param name="input">The moderator response.</param>
    /// <returns>A HookHandlerResult indicating the outcome of the hook firing.</returns>
    private static HookHandlerResult FireHook(GameHook hook, GameSession session, ModeratorResponse input)
    {
        // Set the active hook in the cache
        session.TransitionActiveHook(hook);
        
        // Get registered listeners for this hook
        if (!HookListeners.TryGetValue(hook, out var listeners))
        {
            // No listeners registered for this hook, complete it
            return HookHandlerResult.Complete();
        }
        
        // Check if we have a currently paused listener
        var currentListener = session.GetCurrentListener();

        // Dispatch to each listener in sequence
        foreach (var listenerId in listeners)
        {
            if (!ListenerImplementations.TryGetValue(listenerId, out var listener))
            {
				//throw new InvalidOperationException($"Listener implementation not found for listener ID: {listenerId}");
				// TODO: Skip unimplemented listeners for now
                continue;
            }

			if (currentListener != null && currentListener != listenerId)
            {
                // Another listener is currently paused, skip until resumed
                continue;
            }
            
            // Call the listener's state machine
            var result = listener.AdvanceStateMachine(session, input);
            
            switch(result.Outcome)
            {
                case HookListenerOutcome.NeedInput:
                    // Handler needs input, pause processing
                    return HookHandlerResult.NeedInput(result.Instruction);

                case HookListenerOutcome.Complete:
                    // Listener completed successfully, continue to next
                    continue;
                
                default:
                    throw new InvalidOperationException($"Unknown HookListenerActionOutcome: {result.Outcome}");
            }
        }
        
        // All listeners completed successfully
        return HookHandlerResult.Complete();
    }


    private static PhaseHandlerResult HandleHookResult(HookHandlerResult hookResult, Func<PhaseHandlerResult> onComplete)
    {
        switch (hookResult.Outcome)
        {
            case HookHandlerOutcome.NeedInput:
                return StayInSubPhase(hookResult.Instruction!);

            case HookHandlerOutcome.Complete:
                return onComplete();

            default:
                throw new InvalidOperationException($"Unknown HookListenerOutcome: {hookResult.Outcome}");
        }
    }



	#endregion
}
