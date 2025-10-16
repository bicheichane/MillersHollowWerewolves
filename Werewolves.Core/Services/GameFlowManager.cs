using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using Werewolves.Core.Enums;
using Werewolves.Core.Extensions;
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

    protected internal GameFlowManager(Dictionary<RoleType, IRole> roleImplementations)
    {
        _roleImplementations = roleImplementations ?? throw new ArgumentNullException(nameof(roleImplementations));
        _phaseDefinitions = BuildPhaseDefinitions();
    }

    private PhaseDefinition GetPhaseDefinition(GamePhase phase)
    {
        if (_phaseDefinitions.TryGetValue(phase, out var definition))
        {
            return definition;
        }
        // TODO: Use GameStrings.PhaseDefinitionNotFound
        throw new KeyNotFoundException($"Phase definition not found for {phase}");
    }
	#region Phase Navigation Setup
	private Dictionary<GamePhase, PhaseDefinition> BuildPhaseDefinitions()
    {
		// Note: GameService reference needed for handlers is passed via the Func<> signature.

		return new Dictionary<GamePhase, PhaseDefinition>
        {
            [GamePhase.Setup] = new(
                ProcessInputAndUpdatePhase: HandleSetupPhase, // Static reference to the method
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Night_Start, PhaseTransitionReason.SetupConfirmed, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

            [GamePhase.Night_Start] = new(
                ProcessInputAndUpdatePhase: HandleNewNightLogic, // Static reference to the method
                DefaultEntryInstruction: session => new ModeratorInstruction
                {
                    PublicText = GameStrings.NightStartsPrompt,
                    ExpectedInputType = ExpectedInputType.Confirmation
                },
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Night_RoleAction, PhaseTransitionReason.NightStarted, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

			[GamePhase.Night_RoleAction] = new(
                ProcessInputAndUpdatePhase: HandleNightPhase, // Static reference
                DefaultEntryInstruction: session => new ModeratorInstruction
                {
	                PublicText = String.Format(GameStrings.RoleWakesUp, session.CurrentNightRole),
	                ExpectedInputType = ExpectedInputType.Confirmation
                },
				PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    // These transitions are initiated *inside* HandleNightPhase based on internal logic.
                    // The HandlerResult provides the reason.
                    // We list possible outcomes here for documentation/validation.
                    // Need to revisit if this validation logic in ProcessModeratorInput needs adjustment
                    // based on how HandleNightPhase now returns its reasons.
                    new(GamePhase.Night_RoleAction, PhaseTransitionReason.IdentifiedRoleAndProceedToAction, ExpectedInputType.PlayerSelectionSingle), // -> Role Action (Post ID)
                    new(GamePhase.Night_RoleSleep, PhaseTransitionReason.RoleActionComplete, ExpectedInputType.Confirmation), // -> If there's more roles left for the current night
                    // Note: The exact ExpectedInputType depends on what GenerateNextNightInstruction returns.
                    // Confirmation is the final expected input when transitioning out of Night.
                }
            ),
            
            [GamePhase.Night_RoleSleep] = new(
                ProcessInputAndUpdatePhase: HandleNightRoleGoesToSleep, // Static reference
                DefaultEntryInstruction: session => new ModeratorInstruction
                {
                    PublicText = String.Format(GameStrings.RoleGoesToSleepSingle, session.CurrentNightRole),
                    ExpectedInputType = ExpectedInputType.Confirmation
                },
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Night_RoleAction, PhaseTransitionReason.RoleSleep, ExpectedInputType.Confirmation), // -> If this was not the last role to be called tonight
					new(GamePhase.Day_ResolveNight, PhaseTransitionReason.RoleSleep, ExpectedInputType.Confirmation), // -> If this was the last role to be called tonight
                    
                    // Confirmation is the final expected input when transitioning out of Night.
                }
            ),

			      [GamePhase.Day_ResolveNight] = new(
                ProcessInputAndUpdatePhase: HandleResolveNightPhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Event, PhaseTransitionReason.NightResolutionConfirmedProceedToReveal, ExpectedInputType.AssignPlayerRoles), // Use Enum
                    new(GamePhase.Day_Debate, PhaseTransitionReason.NightResolutionConfirmedNoVictims, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

            [GamePhase.Day_Event] = new(
                ProcessInputAndUpdatePhase: HandleDayEventPhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Debate, PhaseTransitionReason.RoleRevealedProceedToDebate, ExpectedInputType.Confirmation), // Use Enum
                    new(GamePhase.Night_RoleAction, PhaseTransitionReason.RoleRevealedProceedToNight, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

            [GamePhase.Day_Debate] = new(
                ProcessInputAndUpdatePhase: HandleDayDebatePhase, // Static reference
                DefaultEntryInstruction: session => new ModeratorInstruction
                {
                    // TODO: Use GameStrings.ProceedToVotePrompt
                    PublicText = "Proceed to Vote?",
                    ExpectedInputType = ExpectedInputType.Confirmation
                },
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Vote, PhaseTransitionReason.DebateConfirmedProceedToVote, ExpectedInputType.PlayerSelectionSingle) // Use Enum
                }
            ),

            [GamePhase.Day_Vote] = new(
                ProcessInputAndUpdatePhase: HandleDayVotePhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_ResolveVote, PhaseTransitionReason.VoteOutcomeReported, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

            [GamePhase.Day_ResolveVote] = new(
                ProcessInputAndUpdatePhase: HandleDayResolveVotePhase, // Static reference
                PossibleTransitions: new List<PhaseTransitionInfo>
                {
                    new(GamePhase.Day_Event, PhaseTransitionReason.VoteResolvedProceedToReveal, ExpectedInputType.AssignPlayerRoles), // Use Enum
                    new(GamePhase.Night_RoleAction, PhaseTransitionReason.VoteResolvedTieProceedToNight, ExpectedInputType.Confirmation) // Use Enum
                }
            ),

            [GamePhase.GameOver] = new(
                ProcessInputAndUpdatePhase: HandleGameOverPhase // Static reference
                // No transitions out of GameOver
            )
        };
    }

	#endregion

	#region State Machine

	protected internal ProcessResult HandleInput(GameService service, GameSession session, ModeratorInput input)
	{
		var phaseBeforeHandler = session.GamePhase;
		var inputTypeBeforeHandler = session.PendingModeratorInstruction?.ExpectedInputType;
		PhaseDefinition currentPhaseDef;
		try
		{
			currentPhaseDef = GetPhaseDefinition(phaseBeforeHandler);
		}
		catch (KeyNotFoundException ex)
		{
			// TODO: Use proper GameString
			return ProcessResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, $"Internal error: Phase definition missing for {phaseBeforeHandler}. {ex.Message}"));
		}

		// --- Execute Phase Handler ---
		PhaseHandlerResult handlerResult = currentPhaseDef.ProcessInputAndUpdatePhase(session, input, service);

		// --- Handle Handler Failure ---
		if (!handlerResult.IsSuccess)
		{
			// Return the failure, keeping the current pending instruction
			return ProcessResult.Failure(handlerResult.Error!);
		}

		// --- Process Handler Success ---
		var phaseAfterHandler = session.GamePhase; // Fetch potentially updated phase

		var inputTypeAfterHandler = handlerResult.NextInstruction?.ExpectedInputType;
		ModeratorInstruction nextInstructionToSend;

		try // Wrap state machine validation logic in try-catch for robustness
		{
			if (phaseAfterHandler != phaseBeforeHandler)
			{
				// --- Phase Changed --- Validate Transition and Determine Next Instruction ---
				if (handlerResult.TransitionReason == null)
				{
					// TODO: Use GameString
					throw new InvalidOperationException($"Internal State Machine Error: Phase transitioned from {phaseBeforeHandler} to {phaseAfterHandler} but handler did not provide a TransitionReason.");
				}

				PhaseDefinition previousPhaseDef = currentPhaseDef; // Alias for clarity
				PhaseDefinition targetPhaseDef = GetPhaseDefinition(phaseAfterHandler);

				var possibleTransitions = previousPhaseDef.PossibleTransitions ?? new List<PhaseTransitionInfo>();
				var transitionInfo = possibleTransitions.FirstOrDefault(t =>
					t.TargetPhase == phaseAfterHandler &&
					t.ExpectedInputOnArrival == inputTypeAfterHandler &&
					t.ConditionOrReason == handlerResult.TransitionReason); // Enum comparison

				if (transitionInfo == null)
				{
					// TODO: Use GameString
					throw new InvalidOperationException($"Internal State Machine Error: Undocumented transition from {phaseBeforeHandler} to {phaseAfterHandler} with input type '{inputTypeAfterHandler}' and reason '{handlerResult.TransitionReason}'. Add to PhaseDefinition.");
				}

				// Determine instruction
				if (handlerResult.UseDefaultInstructionForNextPhase)
				{
					if (targetPhaseDef.DefaultEntryInstruction == null)
					{
						// TODO: Use GameString
						throw new InvalidOperationException($"Internal State Machine Error: Transition to {phaseAfterHandler} (Reason: {handlerResult.TransitionReason}) requested default instruction, but none defined for target phase.");
					}
					nextInstructionToSend = targetPhaseDef.DefaultEntryInstruction(session);
				}
				else
				{
					if (handlerResult.NextInstruction == null)
					{
						// TODO: Use GameString
						throw new InvalidOperationException($"Internal State Machine Error: Transition to {phaseAfterHandler} (Reason: {handlerResult.TransitionReason}) did not request default instruction, but no specific NextInstruction provided.");
					}
					nextInstructionToSend = handlerResult.NextInstruction;
				}


			}
			else
			{
				// --- Phase Did Not Change --- Determine Next Instruction ---
				if (handlerResult.NextInstruction == null)
				{
					// TODO: Use GameString
					throw new InvalidOperationException($"Internal State Machine Error: Phase {phaseBeforeHandler} handler succeeded but did not transition and provided no NextInstruction.");
				}
				nextInstructionToSend = handlerResult.NextInstruction;
			}
		}
		catch (Exception ex) when (ex is InvalidOperationException || ex is KeyNotFoundException)
		{
			// Log the internal error and return a generic failure to the caller
			Console.Error.WriteLine($"STATE MACHINE ERROR: {ex.Message}");
			// TODO: Consider a specific internal error GameError for the public API
			return ProcessResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, $"Internal state machine error: {ex.Message}"));
		}

		// --- Update Pending Instruction ---
		session.PendingModeratorInstruction = nextInstructionToSend;

		// --- Post-Processing: Victory Check ---
		// Check victory ONLY after specific resolution phases
		if (phaseBeforeHandler == GamePhase.Day_ResolveNight || phaseBeforeHandler == GamePhase.Day_ResolveVote)
		{
			var victoryCheckResult = CheckVictoryConditions(session);
			if (victoryCheckResult != null && session.GamePhase != GamePhase.GameOver) // Check if victory wasn't already set
			{
				// Victory condition met!
				session.WinningTeam = victoryCheckResult.Value.WinningTeam;

				session.TransitionToPhaseDefaultInstruction(GamePhase.GameOver, PhaseTransitionReason.VictoryConditionMet);

				var victoryLog = new VictoryConditionMetLogEntry
				{
					WinningTeam = victoryCheckResult.Value.WinningTeam,
					ConditionDescription = victoryCheckResult.Value.Description,
					TurnNumber = session.TurnNumber,
					CurrentPhase = session.GamePhase
				};
				session.GameHistoryLog.Add(victoryLog);

				var finalInstruction = new ModeratorInstruction
				{
					PublicText = string.Format(GameStrings.GameOverMessage, victoryCheckResult.Value.Description),
					ExpectedInputType = ExpectedInputType.None
				};
				session.PendingModeratorInstruction = finalInstruction; // Override instruction
				return ProcessResult.Success(finalInstruction); // Return final victory result
			}
		}

		// If no victory override, return the determined success result
		return ProcessResult.Success(nextInstructionToSend);
	}

	private (Team WinningTeam, string Description)? CheckVictoryConditions(GameSession session)
	{
		// Phase 1: Basic checks using assigned/revealed roles
		int aliveWerewolves = session.Players.Values.Count(p => p.Health == PlayerHealth.Alive && p.Role?.RoleType == RoleType.SimpleWerewolf);
		int aliveNonWerewolves = session.Players.Values.Count(p => p.Health == PlayerHealth.Alive && p.Role?.RoleType != RoleType.SimpleWerewolf);

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

	private PhaseHandlerResult HandleSetupPhase(GameSession session, ModeratorInput input, GameService service)
	{
		if (service.ShouldReissueCommand(session, input, out var result))
			return result!;

		var nightStartInstruction = new ModeratorInstruction
		{
			PublicText = GameStrings.NightStartsPrompt,
			PrivateModeratorInfo = GameStrings.ConfirmNightStarted,
			ExpectedInputType = ExpectedInputType.Confirmation
		};
		// Transition happens, specific instruction provided.
		return session.TransitionToPhase(GamePhase.Night_Start, PhaseTransitionReason.SetupConfirmed, nightStartInstruction);
	}

	private PhaseHandlerResult HandleNewNightLogic(GameSession session, ModeratorInput input, GameService service)
	{
		if (service.ShouldReissueCommand(session, input, out var result))
		{
			return result!;
		}

		session.StartNewTurn();

		session.AdvanceToNextNightRole();

		return session.TransitionToPhaseDefaultInstruction(GamePhase.Night_RoleAction, PhaseTransitionReason.NightStarted);
	}

	private PhaseHandlerResult HandleNightPhase(GameSession session, ModeratorInput input, GameService service)
	{
		if (service.ShouldReissueCommand(session, input, out var result))
			return result!;

		var currentNightRole = session.CurrentNightRole;

		result = null;

		//check if it's the first night, the role requires ID, and the role hasn't been assigned yet
		if (session.TurnNumber == 1 &&
		    currentNightRole.RequiresNight1Identification() &&
		    session.Players.WithRole(currentNightRole).Any() == false)
		{
			result = HandleNightRoleIdentification(session, input, service);
		}
		else
		{
			result = HandleNightActionPhase(session, input, service);
		}

		if (result.ShouldTransitionPhase)
		{
			if (result.UseDefaultInstructionForNextPhase)
			{
				return session.TransitionToPhaseDefaultInstruction(GamePhase.Night_RoleSleep, result.TransitionReason!.Value);
			}
			else
			{
				return session.TransitionToPhase(GamePhase.Night_RoleSleep, result.TransitionReason!.Value,
					result.NextInstruction!);
			}
		}

		return result;
	}

	private PhaseHandlerResult HandleNightRoleIdentification(GameSession session, ModeratorInput input, GameService service)
	{
		var currentNightRole = session.CurrentNightRole;
		// ProcessIdentificationInput updates session state directly
		currentNightRole.GenerateIdentificationInstructions(session);
		var identificationResult = currentNightRole.ProcessIdentificationInput(session, input);

		if (identificationResult.IsSuccess)
		{
			// Log successful identification
			if (input.SelectedPlayerIds != null)
			{
				if (input.SelectedPlayerIds.Count > 1)
				{
					session.LogInitialAssignments(input.SelectedPlayerIds, currentNightRole.RoleType);
				}
				else
				{
					session.LogInitialAssignment(input.SelectedPlayerIds[0], currentNightRole.RoleType);
				}
				
			}
		}

		return identificationResult;
	}

	private PhaseHandlerResult HandleNightActionPhase(GameSession session, ModeratorInput input, GameService service)
	{
		var currentNightRole = session.CurrentNightRole;
		
		// Phase 1 simplification: Assume input IS for the current role's action
		// (More robust validation might be needed later)
		return currentNightRole.ProcessNightAction(session, input);
	}

	private PhaseHandlerResult HandleNightRoleGoesToSleep(GameSession session, ModeratorInput input, GameService service)
	{
		if (service.ShouldReissueCommand(session, input, out var result))
			return result!;

		var nextRole = session.AdvanceToNextNightRole();
		if (nextRole != null)
		{
			// More roles to call this night
			var nextInstruction = nextRole.GenerateNightInstructions(session);
			return session.TransitionToPhaseDefaultInstruction(GamePhase.Night_RoleAction, PhaseTransitionReason.RoleSleep);
			return PhaseHandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.RoleSleep);
		}
		else
		{
			// No more roles left, transition to Day_ResolveNight
			return session.TransitionToPhaseDefaultInstruction(GamePhase.Day_ResolveNight, PhaseTransitionReason.RoleSleep);
		}
	}

	private PhaseHandlerResult HandleResolveNightPhase(GameSession session, ModeratorInput input, GameService service)
	{
		if (service.ShouldReissueCommand(session, input, out var result))
			return result!;

		var wwVictimAction = session.GameHistoryLog
			.OfType<NightActionLogEntry>()
			.Where(log => log.TurnNumber == session.TurnNumber && log.ActionType == NightActionType.WerewolfVictimSelection)
			.OrderByDescending(log => log.Timestamp)
			.FirstOrDefault();

		List<Player> eliminatedPlayers = new List<Player>();
		Guid? victimId = null;

		if (wwVictimAction?.TargetId != null && session.Players.TryGetValue(wwVictimAction.TargetId.Value, out var victim))
		{
			if (victim.Health == PlayerHealth.Alive)
			{
				victim.Health = PlayerHealth.Dead;
				eliminatedPlayers.Add(victim);
				session.LogElimination(victim.Id, EliminationReason.WerewolfAttack);
			}
		}

		string announcement;
		if (eliminatedPlayers.Any())
		{
			announcement = string.Format(
			  GameStrings.PlayersEliminatedAnnouncement,
			  string.Join(", ", eliminatedPlayers.Select(p => p.Name)));
		}
		else
		{
			announcement = GameStrings.NoOneEliminatedAnnouncement;
		}

		var previousPhase = session.GamePhase;

		if (eliminatedPlayers.Any())
		{
			var playersWithoutRolesIdList = eliminatedPlayers.Where(p => p.Role == null).Select(p => p.Id).ToList();

			var nextInstruction = new ModeratorInstruction
			{
				PublicText = $"{announcement} {GameStrings.RevealRolePromptSpecify}",
				ExpectedInputType = ExpectedInputType.AssignPlayerRoles,
				AffectedPlayerIds = playersWithoutRolesIdList,
				SelectableRoles = Enum.GetValues<RoleType>().ToList()
			};

			return session.TransitionToPhase(GamePhase.Day_Event, PhaseTransitionReason.NightResolutionConfirmedProceedToReveal, nextInstruction);
		}
		else
		{
			// Let the state machine use the Default instruction defined for Day_Debate
			return session.TransitionToPhaseDefaultInstruction(GamePhase.Day_Debate, PhaseTransitionReason.NightResolutionConfirmedNoVictims);
		}

	}

	private PhaseHandlerResult HandleDayEventPhase(GameSession session, ModeratorInput input, GameService service)
	{
		if (session.PendingModeratorInstruction?.ExpectedInputType == ExpectedInputType.AssignPlayerRoles)
		{
			if (input.AssignedPlayerRoles == null || !input.AssignedPlayerRoles.Any())
			{
				return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_RequiredDataMissing, GameStrings.RoleNotSelectedError));
			}

			foreach (var assignedPlayerRole in input.AssignedPlayerRoles)
			{
				var assignmentResult = ProcessSingleRoleAssignment(session, assignedPlayerRole);
				if (assignmentResult.IsSuccess == false) // Check if the helper returned a failure
				{
					return assignmentResult; // Propagate the failure
				}
			}

			var previousPhase = session.PreviousPhase;
			if (previousPhase == GamePhase.Day_ResolveVote)
			{
				return session.TransitionToPhaseDefaultInstruction(GamePhase.Night_RoleAction, PhaseTransitionReason.RoleRevealedProceedToNight); ;
			}
			else if (previousPhase == GamePhase.Day_ResolveNight)
			{
				var nextInstruction = new ModeratorInstruction
				{
					PublicText = GameStrings.ProceedToDebatePrompt,
					ExpectedInputType = ExpectedInputType.Confirmation
				};
				return session.TransitionToPhase(GamePhase.Day_Debate, PhaseTransitionReason.RoleRevealedProceedToDebate, nextInstruction);
			}
		}

		// Fallback for Phase 1
		return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, string.Format(GameStrings.PhaseLogicNotImplemented, session.GamePhase)));
	}

	private PhaseHandlerResult HandleDayDebatePhase(GameSession session, ModeratorInput input, GameService service)
	{
		if (service.ShouldReissueCommand(session, input, out var result))
			return result!;

		var livingPlayers = session.Players.Values
			.Where(p => p.Health == PlayerHealth.Alive)
			// TODO: Add CanVote check later
			.Select(p => p.Id)
			.ToList();

		var nextInstruction = new ModeratorInstruction
		{
			PublicText = GameStrings.VotePhaseStartPrompt,
			ExpectedInputType = ExpectedInputType.PlayerSelectionSingle, // Moderator reports the *outcome*
			SelectablePlayerIds = livingPlayers // Provide context of who *could* be eliminated
												// Note: Allow empty selection for tie via validation in HandleDayVotePhase
		};
		return session.TransitionToPhase(GamePhase.Day_Vote, PhaseTransitionReason.DebateConfirmedProceedToVote, nextInstruction);
	}

	private PhaseHandlerResult HandleDayVotePhase(GameSession session, ModeratorInput input, GameService service)
	{
		// Validate input: Expecting PlayerSelectionSingle
		// Allow 0 or 1 player ID.
		if (input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count > 1)
		{
			return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, GameStrings.VoteOutcomeInvalidSelection));
		}

		Guid? eliminatedPlayerId = input.SelectedPlayerIds.FirstOrDefault();

		// Validate Player ID if one was provided
		if (eliminatedPlayerId != Guid.Empty)
		{
			if (!session.Players.TryGetValue(eliminatedPlayerId.Value, out var targetPlayer))
			{
				return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_PlayerIdNotFound, string.Format(GameStrings.PlayerIdNotFound, eliminatedPlayerId.Value)));
			}
			if (targetPlayer.Health == PlayerHealth.Dead)
			{
				return PhaseHandlerResult.Failure(new GameError(ErrorType.RuleViolation, GameErrorCode.RuleViolation_TargetIsDead, string.Format(GameStrings.TargetIsDeadError, targetPlayer.Name)));
			}
		}

		session.RecordDayVote(eliminatedPlayerId ?? Guid.Empty);

		var nextInstruction = new ModeratorInstruction
		{
			PublicText = GameStrings.ResolveVotePrompt,
			ExpectedInputType = ExpectedInputType.Confirmation
		};

		return session.TransitionToPhase(GamePhase.Day_ResolveVote, PhaseTransitionReason.VoteOutcomeReported, nextInstruction);
	}

	private PhaseHandlerResult HandleDayResolveVotePhase(GameSession session, ModeratorInput input, GameService service)
	{
		if (service.ShouldReissueCommand(session, input, out var result))
			return result!;

		if (!session.PendingVoteOutcome.HasValue)
		{
			return PhaseHandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, GameStrings.VoteOutcomeMissingError));
		}

		Guid outcome = session.PendingVoteOutcome.Value;
		session.ClearPendingVoteOutcome();

		session.LogVoteResolved((outcome == Guid.Empty) ? null : outcome, outcome == Guid.Empty);

		ModeratorInstruction nextInstruction;
		PhaseTransitionReason transitionReason;
		GamePhase nextGamePhase;

		if (outcome == Guid.Empty) // Tie
		{
			nextGamePhase = GamePhase.Night_RoleAction;
			transitionReason = PhaseTransitionReason.VoteResolvedTieProceedToNight;
			nextInstruction = new ModeratorInstruction
			{ PublicText = GameStrings.VoteResultTieProceedToNight, ExpectedInputType = ExpectedInputType.Confirmation };
		}
		else // Player eliminated
		{
			Guid eliminatedPlayerId = outcome;
			if (session.Players.TryGetValue(eliminatedPlayerId, out var eliminatedPlayer))
			{
				if (eliminatedPlayer.Health == PlayerHealth.Alive)
				{
					eliminatedPlayer.Health = PlayerHealth.Dead;
					session.LogElimination(eliminatedPlayerId, EliminationReason.DayVote);
				}

				nextGamePhase = GamePhase.Day_Event;
				transitionReason = PhaseTransitionReason.VoteResolvedProceedToReveal;
				nextInstruction = new ModeratorInstruction
				{
					// Placeholder for GameStrings.PlayerEliminatedByVoteRevealRole
					PublicText = string.Format(GameStrings.PlayerEliminatedByVoteRevealRole, eliminatedPlayer.Name),
					ExpectedInputType = ExpectedInputType.AssignPlayerRoles,
					AffectedPlayerIds = new List<Guid> { eliminatedPlayerId },
					SelectableRoles = Enum.GetValues<RoleType>().ToList()
				};
			}
			else
			{
				return PhaseHandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, string.Format(GameStrings.PlayerIdNotFound, eliminatedPlayerId)));
			}
		}

		return session.TransitionToPhase(nextGamePhase, transitionReason, nextInstruction);
	}

	private PhaseHandlerResult HandleGameOverPhase(GameSession session, ModeratorInput input, GameService service)
	{
		// No actions allowed in GameOver state, return error if any input is attempted
		var error = new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_GameIsOver, GameStrings.GameOverMessage);
		return PhaseHandlerResult.Failure(error);
	}

	#endregion

	#region Helpers
	/// <summary>
	/// Processes the assignment of a single role to a player during the Day Event phase (role reveal).
	/// Updates the player's role and logs the reveal.
	/// </summary>
	/// <returns>A HandlerResult.Failure if an error occurs, generic success message.</returns>
	private PhaseHandlerResult ProcessSingleRoleAssignment(GameSession session, KeyValuePair<Guid, RoleType> assignment)
	{
		Guid playerToRevealId = assignment.Key;
		if (!session.Players.TryGetValue(playerToRevealId, out var playerToReveal))
		{
			// Should not happen if instruction was generated correctly
			return PhaseHandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, GameStrings.RevealTargetNotFoundError));
		}

		// Instantiate the role based on input
		RoleType revealedRoleType = assignment.Value;

		// Player role should never be RoleType.Unknown to the moderator, and implementation should exist
		if (!_roleImplementations.TryGetValue(revealedRoleType, out var revealedRoleInstance) || revealedRoleInstance == null)
		{
			return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_RoleNameNotFound, string.Format(GameStrings.RoleImplementationNotFound, revealedRoleType)));
		}

		playerToReveal.Role = revealedRoleInstance;
		session.LogRoleReveal(playerToReveal.Id, revealedRoleType); // Use 'this.LogRoleReveal' if not static

		return PhaseHandlerResult.SuccessInternalGeneric();
	}
#endregion
}