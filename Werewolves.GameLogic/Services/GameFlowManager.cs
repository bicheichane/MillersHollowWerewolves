using System.Collections.Immutable;
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
using static Werewolves.GameLogic.Models.InternalMessages.StayInSubPhaseHandlerResult;
using static Werewolves.GameLogic.Models.InternalMessages.SubPhaseHandlerResult;
using static Werewolves.GameLogic.Models.StateMachine.EndNavigationSubPhaseStage;
using static Werewolves.GameLogic.Models.StateMachine.HookSubPhaseStage;
using static Werewolves.GameLogic.Models.StateMachine.LogicSubPhaseStage;
using static Werewolves.GameLogic.Models.StateMachine.MidNavigationSubPhaseStage;
using static Werewolves.GameLogic.Models.StateMachine.SubPhaseStage;
using static Werewolves.StateModels.Enums.GameHook;
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
    internal static readonly Dictionary<GameHook, List<ListenerIdentifier>> HookListeners = new()
    {
        // Define hook-to-listener mappings here.
        // ORDER MATTERS!!!!
        [NightActionLoop] = 
        [
            Listener(Thief),                //first night only
            Listener(Actor),
            Listener(LittleGirl),           //first night only
            Listener(Cupid),                //first night only
            Listener(Lovers),              //first night only
            Listener(Fox),
            Listener(StutteringJudge),      //first night only
            Listener(Elder),                //first night only, required to enable disregarding wolf infection
            Listener(TwoSisters),
            Listener(ThreeBrothers),
            Listener(WildChild),            //first night only
            Listener(BearTamer),            //first night only
            Listener(Defender),
            Listener(WolfHound),            //first night only
			Listener(SimpleWerewolf),
            Listener(AccursedWolfFather),
            Listener(BigBadWolf),
            Listener(WhiteWerewolf),
			Listener(Seer),
            Listener(Witch),
            Listener(Gypsy),
            Listener(Piper),
            Listener(Charmed)
		],

        [DayBreakBeforeVictims] =
        [
            Listener(Elder),

        ],

        [DayBreakAfterVictimsAnnounced] =
        [
            Listener(BearTamer),
            Listener(Gypsy),
            Listener(TownCrier),
        ],

        [OnFirstVoteConcluded] =
        [
            Listener(StutteringJudge),
		],

        [OnPlayerEliminationFinalized] =
        [
            Listener(Hunter),
            Listener(Lovers)
		]
	};

    internal static readonly Dictionary<ListenerIdentifier, IGameHookListener> ListenerImplementations = new()
    {
        // Define listener implementations here
        [Listener(SimpleWerewolf)] = new SimpleWerewolfRole(),
        [Listener(Seer)] = new SeerRole(),
        [Listener(SimpleVillager)] = new SimpleVillagerRole()
    };

    internal static readonly Dictionary<GamePhase, IPhaseDefinition> PhaseDefinitions = new()
    {
        [GamePhase.Setup] = new PhaseManager<SetupSubPhases>(
        [
            new(subPhase: SetupSubPhases.Confirm, //Handler = HandleSetupConfirmation,
                subPhaseStages: 
                    [NavigationEndStage(SetupSubPhaseStage.ConfirmSetup, HandleSetupPhase)], 
                possibleNextMainPhaseTransitions: 
                    [new(GamePhase.Night)])
        ], entrySubPhase: SetupSubPhases.Confirm),

        [GamePhase.Night] = new PhaseManager<NightSubPhases>(
        [
            new(
                subPhase: NightSubPhases.Start,
                subPhaseStages: [ 
                    LogicStage(NightSubPhaseStage.NightStart, HandleNightStart),
                    HookStage(NightActionLoop),
                    NavigationEndStage(NightSubPhaseStage.NightEnd, HandleNightActionLoopFinish)
                ],
                possibleNextMainPhaseTransitions:
                [ new (GamePhase.Dawn) ]
			)
        ], entrySubPhase: NightSubPhases.Start),

        
        [GamePhase.Dawn] = new PhaseManager<DawnSubPhases>(
        [
            new(
                subPhase : DawnSubPhases.CalculateVictims,
                subPhaseStages: [
                    NavigationEndStage(DawnSubPhaseStage.CheckForVictims, HandleDawnCalculateVictims)
                ],
                possibleNextSubPhases:[DawnSubPhases.AnnounceVictims, DawnSubPhases.Finalize]
			),
            new(
                subPhase : DawnSubPhases.AnnounceVictims,
                subPhaseStages: [
                    LogicStage(DawnSubPhaseStage.AnnounceVictims, HandleVictimsAnnounceAndRoleRequest),
                    LogicStage(DawnSubPhaseStage.DawnRoleReveals, HandleVictimsAnnounceAndRoleResponse)
                        .RequiresInputType(ExpectedInputType.AssignPlayerRoles),
                    NavigationEndStageSilent(DawnSubPhases.Finalize)
					],
                possibleNextSubPhases: [DawnSubPhases.Finalize]
			),
            new(
                subPhase: DawnSubPhases.Finalize,
                subPhaseStages: [
                    NavigationEndStageSilent(GamePhase.Day)
                ],
                possibleNextMainPhaseTransitions: [
                    new(GamePhase.Day),
                ]
			)
        ], entrySubPhase: DawnSubPhases.CalculateVictims),

        [GamePhase.Day] = new PhaseManager<DaySubPhases>(
        [
            new(
                subPhase: DaySubPhases.Debate,
                subPhaseStages: 
                    [ NavigationEndStage(DaySubPhaseStage.Debate, HandleDebate)],
                possibleNextSubPhases:
                    [ DaySubPhases.NormalVoting,
                        //Additional voting sub-phases to be added here
                    ]
			),
            new(
                subPhase: DaySubPhases.NormalVoting,
                subPhaseStages: [ 
                    LogicStage(DaySubPhaseStage.StartNormalVote, HandleDayNormalVoteOutcomeRequest),
                    NavigationEndStage(DaySubPhaseStage.ProcessVote, HandleDayNormalVoteOutcomeResponse)
                        .RequiresInputType(ExpectedInputType.PlayerSelection)],
                possibleNextSubPhases:
                    [ DaySubPhases.ProcessVoteRoleReveal, DaySubPhases.Finalize ]
			),
            // Additional sub-phases (AccusationVoting, FriendVoting) can be added here
            new(
                subPhase: DaySubPhases.ProcessVoteRoleReveal,
                subPhaseStages: [ 
                    LogicStage(DaySubPhaseStage.VoteRoleRevealRequest, HandleDayVoteRoleRevealRequest),
                    LogicStage(DaySubPhaseStage.VoteRoleRevealResponse, HandleDayVoteRoleRevealResponse)
                        .RequiresInputType(ExpectedInputType.AssignPlayerRoles),
                    NavigationEndStageSilent(DaySubPhases.Finalize)
				],
                possibleNextSubPhases:
                    [ DaySubPhases.Finalize ]
			),
            new(
                subPhase: DaySubPhases.Finalize,
                subPhaseStages:
                    [ NavigationEndStageSilent(GamePhase.Night) ],
                possibleNextMainPhaseTransitions:
                    [ new(GamePhase.Night) ]
            )
        ], entrySubPhase: DaySubPhases.Debate),

    };
    #endregion

    #region State Machine

    internal static ProcessResult HandleInput(GameSession session, ModeratorResponse input)
    {
        var currentPhase = session.GetCurrentPhase();
        
        // --- Execute Phase Handler ---
        PhaseHandlerResult handlerResult = RouteInputToPhaseHandler(session, input);

		var nextInstructionToSend = handlerResult.ModeratorInstruction;

		if(TryGetVictoryInstructions(session, currentPhase, out var victoryInstruction))
        {
            nextInstructionToSend = victoryInstruction;
		}

        if (nextInstructionToSend == null)
        {
            throw new InvalidOperationException("HandleInput: null nextInstructionToSend");
        }

        // --- Update Pending Instruction ---
		session.SetPendingModeratorInstruction(nextInstructionToSend);

		return ProcessResult.Success(nextInstructionToSend);
    }

    private static bool TryGetVictoryInstructions(GameSession session, GamePhase currentPhase,
        out ModeratorInstruction? nextInstructionToSend)
    {
        nextInstructionToSend = null;
        // --- Post-Processing: Victory Check ---
        // Check victory ONLY after specific resolution phases
        if (currentPhase == GamePhase.Dawn || 
            (currentPhase == GamePhase.Day && session.GetSubPhase<DaySubPhases>() == DaySubPhases.Finalize))
        {
            var victoryCheckResult = CheckVictoryConditions(session);
            if (victoryCheckResult != null)
            {
                // Victory condition met!
                session.VictoryConditionMet(victoryCheckResult.Value.WinningTeam, victoryCheckResult.Value.Description);

                var finalInstruction = new FinishedGameConfirmationInstruction(victoryCheckResult.Value.Description);
                nextInstructionToSend = finalInstruction; // Override instruction
                return true;
            }
        }

		return false;
    }

    private static PhaseHandlerResult RouteInputToPhaseHandler(GameSession session, ModeratorResponse input)
    {
        var currentPhase = session.GetCurrentPhase();
        
        if (!PhaseDefinitions.TryGetValue(currentPhase, out var phaseDef))
        {
            throw new InvalidOperationException($"No phase definition found for phase: {currentPhase}");
        }

        return phaseDef.ProcessInputAndUpdatePhase(session, input);
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

	#region Setup Phase Handlers

	private static MajorNavigationPhaseHandlerResult HandleSetupPhase(GameSession session, ModeratorResponse input)
    {
        var nightStartInstruction = new ConfirmationInstruction(
            publicAnnouncement: GameStrings.NightStartsPrompt,
            privateInstruction: GameStrings.ConfirmNightStarted
        );
        // Transition happens, specific instruction provided.

        return TransitionPhase(nightStartInstruction, GamePhase.Night);
    }

	#endregion

	#region Night Phase Handler Methods

	/// <summary>
	/// Handles the Night.Start sub-phase: village goes to sleep, increment turn number.
	/// </summary>
	private static ModeratorInstruction HandleNightStart(GameSession session, ModeratorResponse input)
    {
        var instruction = new ConfirmationInstruction(
            publicAnnouncement: "Village goes to sleep. Night actions begin."
        );

        return instruction;
    }

    private static MainPhaseHandlerResult HandleNightActionLoopFinish(GameSession session, ModeratorResponse input)
    {
        var instruction = new ConfirmationInstruction(
            publicAnnouncement: "Night actions complete. Village wakes up."
        );

        return TransitionPhase(instruction, GamePhase.Dawn);
	}

    #endregion

    #region Dawn Phase Handler Methods

    /// <summary>
    /// Handles the Dawn.CalculateVictims sub-phase: process night actions to determine final victims.
    /// </summary>
    private static MajorNavigationPhaseHandlerResult HandleDawnCalculateVictims(GameSession session, ModeratorResponse input)
    {
		// - Calculate final list of eliminated players
		// - Transition to AnnounceVictims if there are any victims, otherwise transition to Finalize

        var victimsExist = false;

		// Offensive actions
		Dictionary<NightActionType, IPlayer> offensiveSelections = new();

        AddAttackIfNotNull(NightActionType.WerewolfVictimSelection);
        AddAttackIfNotNull(NightActionType.AccursedWolfFatherInfection);
        AddAttackIfNotNull(NightActionType.BigBadWolfVictimSelection);
        AddAttackIfNotNull(NightActionType.WhiteWerewolfVictimSelection);
        AddAttackIfNotNull(NightActionType.WitchKill);
        AddAttackIfNotNull(NightActionType.RustySword);

		// Defensive actions
        Dictionary<NightActionType, IPlayer> defensiveSelections = new();

        AddDefenseIfNotNull(NightActionType.DefenderProtect);
        AddDefenseIfNotNull(NightActionType.WitchSave);

		foreach (var offensiveSelection in offensiveSelections)
        {
            var attackedPlayer = offensiveSelection.Value;
            var attackType = offensiveSelection.Key;
            switch (attackType)
            {
                //already handled by the werewolf victim selection handler
                case NightActionType.AccursedWolfFatherInfection:
					break;

                case NightActionType.WerewolfVictimSelection:
					// if the accursed wolf father infected the werewolf victim, skip the kill
                    if (offensiveSelections.ContainsKey(NightActionType.AccursedWolfFatherInfection))
                    {
                        // if the attacked player is the elder with extra life, they are immune
                        //otherwise, infect them
                        if (attackedPlayer.State is not { MainRole: Elder, HasElderExtraLife: true })
                        {
                            session.ApplyStatusEffect(StatusEffectTypes.LycanthropyInfection, attackedPlayer.Id);
                        }
						//skip the kill
						continue;
                    }
					// otherwise carry on with a normal werewolf attack
					else
					{
                        goto case NightActionType.BigBadWolfVictimSelection;
                    }


                case NightActionType.BigBadWolfVictimSelection:
					//if the attacked player is protected by defender, skip
					if (defensiveSelections.TryGetValue(NightActionType.DefenderProtect, out var protectedPlayer) &&
                        protectedPlayer.Equals(attackedPlayer) &&
                        protectedPlayer.State.MainRole != LittleGirl) //unless it's the little girl
                    {
                        //skip the kill
                        continue;
                    }

                    // if the attacked player is the elder with extra life, wound instead of kill
                    // and then skip
                    if (attackedPlayer.State is { MainRole: Elder, HasElderExtraLife: true })
                    {
                        session.ApplyStatusEffect(StatusEffectTypes.ElderProtectionLost, attackedPlayer.Id);

                        //skip the kill
						continue;
                    }

					// if the attacked player is healed by witch, skip
					// the witch's potion can only prevent actual deaths
					// so it doesn't prevent the elder from being wounded
					if (defensiveSelections.TryGetValue(NightActionType.WitchSave, out var healedPlayer) && healedPlayer.Equals(attackedPlayer))
                    {
                        //skip the kill
						continue;
                    }

					//otherwise, eliminate the player
                    session.EliminatePlayer(attackedPlayer.Id, EliminationReason.WerewolfAttack);
                    break;

				case NightActionType.WhiteWerewolfVictimSelection:
                    session.EliminatePlayer(attackedPlayer.Id, EliminationReason.WerewolfAttack);
                    break;

				case NightActionType.WitchKill:
                    session.EliminatePlayer(attackedPlayer.Id, EliminationReason.WitchKill);
					break;
                case NightActionType.RustySword:
                    session.EliminatePlayer(attackedPlayer.Id, EliminationReason.RustySword);
					break;

                default:
                    throw new InvalidOperationException($"Unhandled offensive action type: {attackType}");
			}

            victimsExist = true;
        }

        var nextSubPhase = victimsExist ? DawnSubPhases.AnnounceVictims : DawnSubPhases.Finalize;

		return TransitionSubPhaseSilent(nextSubPhase);

        void AddAttackIfNotNull(NightActionType actionType)
        {
            var player = session
                .GetPlayersTargetedLastNight(actionType,
                    NumberRangeConstraint.Optional).SingleOrDefault();
            if (player != null)
            {
                offensiveSelections.Add(actionType, player);
            }
        }

        void AddDefenseIfNotNull(NightActionType actionType)
        {
            var player = session
                .GetPlayersTargetedLastNight(actionType,
                    NumberRangeConstraint.Optional).SingleOrDefault();
            if (player != null)
            {
                defensiveSelections.Add(actionType, player);
            }
        }
	}

    private static ModeratorInstruction HandleVictimsAnnounceAndRoleRequest(GameSession session, ModeratorResponse input)
    {
        var victimList = session.GetPlayersEliminatedLastDawn().ToImmutableHashSet();

        var victimNameList = string.Join(Environment.NewLine, victimList.Select(p => p.Name));

        var unassignedRoles = session.GetUnassignedRoles();

        return new AssignRolesInstruction(
            publicAnnouncement: GameStrings.MultipleVictimEliminatedAnnounce.Format(victimNameList),
            privateInstruction: GameStrings.RevealRolePromptSpecify,
            playersForAssignment: victimList.Select(p => p.Id).ToImmutableHashSet(),
            rolesForAssignment: unassignedRoles
		);
    }

    /// <summary>
    /// Handles the Dawn.ProcessRoleReveals sub-phase: reveal roles for each eliminated player.
    /// </summary>
    private static void HandleVictimsAnnounceAndRoleResponse(GameSession session, ModeratorResponse input)
    {
        foreach (var entry in input.AssignedPlayerRoles!)
        {
            session.AssignRole(entry.Key, entry.Value);
        }
        
        //proceed to the next sub-phase state silently
    }

	#endregion

	#region Day Phase Handler Methods

    /// <summary>
    /// Handles the DayDebate.Confirm sub-phase: moderator confirms debate is complete.
    /// </summary>
    private static SubPhaseHandlerResult HandleDebate(GameSession session, ModeratorResponse input)
    {
        var voteInstruction = new ConfirmationInstruction(
            publicAnnouncement: GameStrings.DebateStartsPrompt,
            privateInstruction: GameStrings.DebateModeratorInstructions
        );

        //TODO: add conditional logic to check which voting style should be performed
        
        return TransitionSubPhase(voteInstruction, DaySubPhaseStage.StartNormalVote);
    }

    private static ModeratorInstruction HandleDayNormalVoteOutcomeRequest(GameSession session, ModeratorResponse input)
    {
        var alivePlayers = session.GetPlayers().WithHealth(PlayerHealth.Alive);

        var selectPlayerInstruction = new SelectPlayersInstruction(
            alivePlayers.ToIdList(),
            NumberRangeConstraint.Optional,
            publicAnnouncement: GameStrings.VoteStartsPublicInstruction,
            privateInstruction: GameStrings.VoteStartsModeratorInstruction);

        return selectPlayerInstruction;
    }

    /// <summary>
    /// Handles the DayVote.ProcessOutcome sub-phase: process vote outcome reported by moderator.
    /// </summary>
    private static MajorNavigationPhaseHandlerResult HandleDayNormalVoteOutcomeResponse(GameSession session, ModeratorResponse input)
    {
        var selectedPlayer = input.SelectedPlayerIds!;

		if (selectedPlayer.Count == 0) //tie
        {
            session.PerformDayVote(null);
            return TransitionSubPhaseSilent(DaySubPhases.Finalize);
		}
        else
        {
            var playerId = selectedPlayer[0];
            session.PerformDayVote(playerId);
            session.EliminatePlayer(playerId, EliminationReason.DayVote);
			return TransitionSubPhaseSilent(DaySubPhases.ProcessVoteRoleReveal);
        }
    }

    private static ModeratorInstruction HandleDayVoteRoleRevealRequest(GameSession gameSession, ModeratorResponse input)
    {
        var votedPlayer = gameSession.GetPlayerEliminatedLastVote();
        var availableRoles = gameSession.GetUnassignedRoles();

        return new AssignRolesInstruction(
            [votedPlayer],
            availableRoles,
            privateInstruction: GameStrings.RevealRolePromptSpecify
        );
    }

    private static void HandleDayVoteRoleRevealResponse(GameSession session, ModeratorResponse input)
    {
        var entry = input.AssignedPlayerRoles!.Single();
        session.AssignRole(entry.Key, entry.Value);
	}

    #endregion
}
