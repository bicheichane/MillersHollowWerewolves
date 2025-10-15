using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Interfaces; // Added for IRole
using Werewolves.Core.Roles; // Added for specific roles
using System.Collections.Concurrent; // For thread-safe storage
using System.Linq; // Needed for Any()
using System;
using System.Collections.Generic;
using Werewolves.Core.Resources; // Add this line for resource access
using System.Diagnostics;
using System.Text.Json; // For Debug.Fail
using Werewolves.Core.Models.StateMachine;

namespace Werewolves.Core.Services;

/// <summary>
/// Orchestrates the game flow based on moderator input and tracked state using a state machine.
/// </summary>
public class GameService
{
    // Simple in-memory storage for game sessions. Replaceable with DI.
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();

	// TODO: Replace direct role instantiation with a factory or registry later
	public static readonly Dictionary<RoleType, IRole> _roleImplementations = new()
	{
		{ RoleType.SimpleVillager, new SimpleVillagerRole() },
		{ RoleType.SimpleWerewolf, new SimpleWerewolfRole() },
		{ RoleType.Seer, new SeerRole() }
	};

	private readonly GameFlowManager _gameFlowManager;

	public GameService()
    {
        _gameFlowManager = new GameFlowManager(_roleImplementations);
    }

    /// <summary>
    /// Starts a new game session.
    /// </summary>
    /// <param name="playerNamesInOrder">List of player names in clockwise seating order.</param>
    /// <param name="rolesInPlay">List of RoleTypes included in the game.</param>
    /// <param name="eventCardIdsInDeck">Optional list of event card IDs included.</param>
    /// <returns>The unique ID for the newly created game session.</returns>
    public Guid StartNewGame(List<string> playerNamesInOrder, List<RoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)
    {
        ArgumentNullException.ThrowIfNull(playerNamesInOrder);
        ArgumentNullException.ThrowIfNull(rolesInPlay);
        if (!playerNamesInOrder.Any())
        {
            throw new ArgumentException(GameStrings.PlayerListCannotBeEmpty, nameof(playerNamesInOrder));
        }
        if (!rolesInPlay.Any())
        {
            throw new ArgumentException(GameStrings.RoleListCannotBeEmpty, nameof(rolesInPlay));
        }

        var players = new Dictionary<Guid, Player>();
        var seatingOrder = new List<Guid>();
        var playerInfos = new List<PlayerInfo>();

        foreach (var name in playerNamesInOrder)
        {
            var player = new Player { Name = name, Role = null };
            players.Add(player.Id, player);
            seatingOrder.Add(player.Id);
            playerInfos.Add(new PlayerInfo(player.Id, player.Name));
        }

        var session = new GameSession(
            players: players,
            playerSeatingOrder: seatingOrder,
            rolesInPlay: new List<RoleType>(rolesInPlay), // Copy list
            gamePhase: GamePhase.Setup, 
            turnNumber: 0 
            // EventDeck setup would happen here if events were implemented
        );

        // Initial log entry
        var logEntry = new GameStartedLogEntry
        {
            InitialRoles = session.RolesInPlay.AsReadOnly(),
            InitialPlayers = playerInfos.AsReadOnly(),
            InitialEvents = eventCardIdsInDeck?.AsReadOnly(),
            TurnNumber = session.TurnNumber,
            CurrentPhase = session.GamePhase
        };
        session.GameHistoryLog.Add(logEntry);

        // Initial instruction
        session.PendingModeratorInstruction = new ModeratorInstruction
        {
            InstructionText = GameStrings.SetupCompletePrompt,
            ExpectedInputType = ExpectedInputType.Confirmation
        };

        _sessions.TryAdd(session.Id, session);

        return session.Id;
    }

    /// <summary>
    /// Retrieves the currently pending instruction for the moderator.
    /// </summary>
    /// <param name="gameId">The ID of the game session.</param>
    /// <returns>The pending instruction, or null if game not found or no instruction pending.</returns>
    public ModeratorInstruction? GetCurrentInstruction(Guid gameId)
    {
        if (_sessions.TryGetValue(gameId, out var session))
        {
            return session.PendingModeratorInstruction;
        }
        return null; // Or throw GameNotFoundException
    }

    /// <summary>
    /// Gets a view of the current game state.
    /// Basic implementation returns the session object itself (consider a DTO later).
    /// </summary>
    /// <param name="gameId">The ID of the game session.</param>
    /// <returns>The game session object, or null if not found.</returns>
    public GameSession? GetGameStateView(Guid gameId)
    {
        _sessions.TryGetValue(gameId, out var session);
        return session; // Or throw GameNotFoundException, or return a dedicated DTO
    }

    /// <summary>
    /// Processes input provided by the moderator using the state machine.
    /// </summary>
    public ProcessResult ProcessModeratorInput(Guid gameId, ModeratorInput input)
    {
        if (!_sessions.TryGetValue(gameId, out var session))
        {
            return ProcessResult.Failure(new GameError(ErrorType.GameNotFound,
                                                    GameErrorCode.GameNotFound_SessionNotFound,
                                                    GameStrings.GameNotFound));
        }

        var phaseBeforeHandler = session.GamePhase;
        var inputTypeBeforeHandler = session.PendingModeratorInstruction?.ExpectedInputType;
        PhaseDefinition currentPhaseDef;
        try
        {
             currentPhaseDef = _gameFlowManager.GetPhaseDefinition(phaseBeforeHandler);
        }
        catch (KeyNotFoundException ex)
        {
            // TODO: Use proper GameString
            return ProcessResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, $"Internal error: Phase definition missing for {phaseBeforeHandler}. {ex.Message}"));
        }

        // --- Input Validation Against Last Instruction ---
        var validationResult = ValidateExpectedInput(session, input);
        if (validationResult != null)
        {
            return validationResult; // Return failure if validation fails
        }

        // --- Execute Phase Handler ---
        HandlerResult handlerResult = currentPhaseDef.ProcessInputAndUpdatePhase(session, input, this);

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
                PhaseDefinition targetPhaseDef = _gameFlowManager.GetPhaseDefinition(phaseAfterHandler);

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
        // Check victory ONLY after specific resolution phases or if the phase is now GameOver
        if (phaseBeforeHandler == GamePhase.Day_ResolveNight || phaseBeforeHandler == GamePhase.Day_ResolveVote)
        {
            var victoryCheckResult = CheckVictoryConditions(session);
            if (victoryCheckResult != null && session.GamePhase != GamePhase.GameOver) // Check if victory wasn't already set
            {
                // Victory condition met!
                session.WinningTeam = victoryCheckResult.Value.WinningTeam;

				session.TransitionToPhase(GamePhase.GameOver, PhaseTransitionReason.VictoryConditionMet);

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
                    InstructionText = string.Format(GameStrings.GameOverMessage, victoryCheckResult.Value.Description),
                    ExpectedInputType = ExpectedInputType.None
                };
                session.PendingModeratorInstruction = finalInstruction; // Override instruction
                return ProcessResult.Success(finalInstruction); // Return final victory result
            }
        }

        // If no victory override, return the determined success result
        return ProcessResult.Success(nextInstructionToSend);
    }

    // --- Phase-Specific Handlers (Refactored Signatures) ---

    // Note: These methods are now static as they are referenced directly in GameFlowManager
    // They receive the 'this' instance (GameService) as the third parameter.
    public static HandlerResult HandleSetupPhase(GameSession session, ModeratorInput input, GameService service)
    {
        if (service.ShouldReissueCommand(session, input, out var result))
            return result!;

        session.TransitionToPhase(GamePhase.Night_Start, PhaseTransitionReason.SetupConfirmed);

        var nightStartInstruction = new ModeratorInstruction
        {
            InstructionText = GameStrings.NightStartsPrompt,
            ExpectedInputType = ExpectedInputType.Confirmation
        };
        // Transition happens, specific instruction provided.
        return HandlerResult.SuccessTransition(nightStartInstruction, PhaseTransitionReason.SetupConfirmed);
	}

    public static HandlerResult HandleNightLogic(GameSession session, ModeratorInput input, GameService service)
    {
	    if (service.ShouldReissueCommand(session, input, out var result))
	    {
		    return result!;
	    }

        session.StartNewTurn();
        
	    
	    // Stay in Night phase, but provide the first role instruction.
	    return HandlerResult.SuccessTransition(firstRoleInstruction);
	}

    public static HandlerResult HandleNightRoleIdentification(GameSession session, ModeratorInput input, GameService service)
    {
	    var currentNightRole = session.CurrentNightRole;
	    // ProcessIdentificationInput updates session state directly
	    var identificationResult = currentNightRole.ProcessIdentificationInput(session, input);

	    if (identificationResult.IsSuccess)
	    {
		    // Log successful identification
		    if (input.SelectedPlayerIds != null)
		    {
			    session.LogInitialAssignments(input.SelectedPlayerIds, currentNightRole.RoleType);
		    }

		    // Immediately generate ACTION instruction for the identified role
		    var actionInstruction = currentNightRole.GenerateNightInstructions(session);
		    if (actionInstruction == null)
		    {
			    // Role might not have an immediate action. Advance index & get next.
			    var nextInstruction = service.GenerateNextNightInstruction(session);
			    // Check if GenerateNext moved phase
			    if (session.GamePhase != GamePhase.Night_RoleAction)
			    {
				    return HandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.RoleActionComplete);
			    }
			    else
			    {
				    return HandlerResult.SuccessStayInPhase(nextInstruction);
			    }
		    }
		    else
		    {
			    // Return action instruction for the identified role. Stay in Night phase.
			    return HandlerResult.SuccessStayInPhase(actionInstruction);
		    }
	    }
	    else
	    {
		    // Identification failed validation. Return failure, stay in phase with same instruction.
		    return HandlerResult.Failure(identificationResult.Error!); // Use error from ProcessIdentificationInput
	    }
	}

    public static HandlerResult HandleNightActionPhase(GameSession session, ModeratorInput input, GameService service)
    {
        var currentNightRole = session.CurrentNightRole;

        

        // --- 2. Determine Current Role & Process Action (if no pending ID) ---
        // If index is invalid or out of bounds, try to generate the next instruction
        if (session.CurrentNightActingRoleIndex < 0 || session.CurrentNightActingRoleIndex >= actingRolesOrdered.Count)
        {
             /*var nextInstruction = service.GenerateNextNightInstruction(session);
             // Check if GenerateNext moved phase
             if(session.GamePhase == GamePhase.Day_ResolveNight)
             {
                return HandlerResult.SuccessTransition(nextInstruction, ReasonWwActionComplete);
             }
             else
             { 
                return HandlerResult.SuccessStayInPhase(nextInstruction);
             }*/
             return HandlerResult.Failure(new GameError(ErrorType.Unknown,
                                                    GameErrorCode.Unknown_InternalError,
                                                    $"Error: night acting role index out of bounds: {session.CurrentNightActingRoleIndex} for {actingRolesOrdered.Count} roles."));
        }

        var currentRoleType = actingRolesOrdered[session.CurrentNightActingRoleIndex];
        if (!_roleImplementations.TryGetValue(currentRoleType, out var currentRoleInstance))
        {
             var nextInstruction = service.GenerateNextNightInstruction(session);
             if (session.GamePhase == GamePhase.Day_ResolveNight)
             {
                 return HandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.RoleActionComplete);
             }
             else
             {
                 return HandlerResult.SuccessStayInPhase(nextInstruction);
             }
        }

        // Phase 1 simplification: Assume input IS for the current role's action
        // (More robust validation might be needed later)
        var actionResult = currentRoleInstance.ProcessNightAction(session, input);

        if (actionResult.IsSuccess)
        {
            if(actionResult.ModeratorInstruction != null)
            {
                return HandlerResult.SuccessStayInPhase(actionResult.ModeratorInstruction);
            }

            // Action successful. Generate instruction for the next step.
            var nextInstruction = service.GenerateNextNightInstruction(session);
            // GenerateNextNightInstruction handles incrementing index and phase transition
            if (session.GamePhase != GamePhase.Night_RoleAction)
            {
                
                // Phase 1 simplification: Assume transition description is ReasonWwActionComplete
                // Will need to add more logic to handle other transition reasons for when we have more night roles.
                return HandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.RoleActionComplete);
            }
            else
            {
                // Still in Night phase, moving to next role/action.
                return HandlerResult.SuccessStayInPhase(nextInstruction);
            }
        }
        else
        {
            // Action failed. Return failure, stay in phase with same instruction.
            return HandlerResult.Failure(actionResult.Error!); // Use error from ProcessNightAction
        }
    }

    public static HandlerResult HandleDayResolveNightPhase(GameSession session, ModeratorInput input, GameService service)
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
			victimId = victim.Id;
			if (victim.Status == PlayerStatus.Alive)
			{
				victim.Status = PlayerStatus.Dead;
				eliminatedPlayers.Add(victim);
				service.LogElimination(session, victim.Id, EliminationReason.WerewolfAttack);
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

			session.GamePhase = GamePhase.Day_Event;

			var nextInstruction = new ModeratorInstruction
			{
				InstructionText = $"{announcement} {GameStrings.RevealRolePromptSpecify}",
				ExpectedInputType = ExpectedInputType.AssignPlayerRoles,
				AffectedPlayerIds = playersWithoutRolesIdList,
				SelectableRoles = Enum.GetValues<RoleType>().ToList()
			};
			service.LogPhaseTransition(session, previousPhase, session.GamePhase, PhaseTransitionReason.NightResolutionConfirmedProceedToReveal);
			return HandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.NightResolutionConfirmedProceedToReveal);
		}
		else
		{
			session.GamePhase = GamePhase.Day_Debate;
			var nextInstruction = new ModeratorInstruction
			{
				InstructionText = $"{announcement} {GameStrings.ProceedToDebatePrompt}",
				ExpectedInputType = ExpectedInputType.Confirmation
			};
			service.LogPhaseTransition(session, previousPhase, session.GamePhase, PhaseTransitionReason.NightResolutionConfirmedNoVictims);
			// Let the state machine use the Default instruction defined for Day_Debate
			return HandlerResult.SuccessTransitionUseDefault(PhaseTransitionReason.NightResolutionConfirmedNoVictims);
		}

	}

    public static HandlerResult HandleDayEventPhase(GameSession session, ModeratorInput input, GameService service)
    {
        if (session.PendingModeratorInstruction?.ExpectedInputType == ExpectedInputType.AssignPlayerRoles)
        {
            if (input.AssignedPlayerRoles == null || !input.AssignedPlayerRoles.Any())
            {
                return HandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_RequiredDataMissing, GameStrings.RoleNotSelectedError));
            }

            foreach (var assignedPlayerRole in input.AssignedPlayerRoles)
            {
                var assignmentResult = service.ProcessSingleRoleAssignment(session, assignedPlayerRole);
                if (assignmentResult != null) // Check if the helper returned a failure
                {
                    return assignmentResult; // Propagate the failure
                }
            }

            var previousPhase = session.PreviousPhase;
            if(previousPhase == GamePhase.Day_ResolveVote)
            {
                session.GamePhase = GamePhase.Night_RoleAction;
                service.LogPhaseTransition(session, previousPhase, session.GamePhase, PhaseTransitionReason.RoleRevealedProceedToNight);
                return HandlerResult.SuccessTransitionUseDefault(PhaseTransitionReason.RoleRevealedProceedToNight);
			}
            else if(previousPhase == GamePhase.Day_ResolveNight)
            {
                session.GamePhase = GamePhase.Day_Debate;
                service.LogPhaseTransition(session, previousPhase, session.GamePhase, PhaseTransitionReason.RoleRevealedProceedToDebate);
				var nextInstruction = new ModeratorInstruction
				{
					InstructionText = GameStrings.ProceedToDebatePrompt,
					ExpectedInputType = ExpectedInputType.Confirmation
				};
				return HandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.RoleRevealedProceedToDebate);
			}
        }

        // Fallback for Phase 1
        return HandlerResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, string.Format(GameStrings.PhaseLogicNotImplemented, session.GamePhase)));
    }

    public static HandlerResult HandleDayDebatePhase(GameSession session, ModeratorInput input, GameService service)
    {
        if (service.ShouldReissueCommand(session, input, out var result))
            return result!;

        var previousPhase = session.GamePhase;
        session.GamePhase = GamePhase.Day_Vote;
        service.LogPhaseTransition(session, previousPhase, session.GamePhase, PhaseTransitionReason.DebateConfirmedProceedToVote);

        var livingPlayers = session.Players.Values
            .Where(p => p.Status == PlayerStatus.Alive)
            // TODO: Add CanVote check later
            .Select(p => p.Id)
            .ToList();

        var nextInstruction = new ModeratorInstruction
        {
            InstructionText = GameStrings.VotePhaseStartPrompt,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle, // Moderator reports the *outcome*
            SelectablePlayerIds = livingPlayers // Provide context of who *could* be eliminated
            // Note: Allow empty selection for tie via validation in HandleDayVotePhase
        };
        return HandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.DebateConfirmedProceedToVote);
	}

    public static HandlerResult HandleDayVotePhase(GameSession session, ModeratorInput input, GameService service)
    {
        // Validate input: Expecting PlayerSelectionSingle
        // Allow 0 or 1 player ID.
        if (input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count > 1)
        {
             return HandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, GameStrings.VoteOutcomeInvalidSelection));
        }

        Guid? eliminatedPlayerId = input.SelectedPlayerIds.FirstOrDefault();

        // Validate Player ID if one was provided
        if (eliminatedPlayerId != Guid.Empty)
        {             
             if (!session.Players.TryGetValue(eliminatedPlayerId.Value, out var targetPlayer))
             {
                 return HandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_PlayerIdNotFound, string.Format(GameStrings.PlayerIdNotFound, eliminatedPlayerId.Value)));
             }
             if (targetPlayer.Status == PlayerStatus.Dead)
             {
                 return HandlerResult.Failure(new GameError(ErrorType.RuleViolation, GameErrorCode.RuleViolation_TargetIsDead, string.Format(GameStrings.TargetIsDeadError, targetPlayer.Name)));
             }
             session.PendingVoteOutcome = eliminatedPlayerId.Value;
        }
        else
        {
            // Empty list means a tie was reported
            session.PendingVoteOutcome = Guid.Empty; 
        }

        service.LogVoteOutcomeReported(session, session.PendingVoteOutcome.Value);

        var previousPhase = session.GamePhase;
        session.GamePhase = GamePhase.Day_ResolveVote;
        service.LogPhaseTransition(session, previousPhase, session.GamePhase, PhaseTransitionReason.VoteOutcomeReported);

        var nextInstruction = new ModeratorInstruction
        {
             InstructionText = GameStrings.ResolveVotePrompt,
             ExpectedInputType = ExpectedInputType.Confirmation
        };
        return HandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.VoteOutcomeReported);
    }

    public static HandlerResult HandleDayResolveVotePhase(GameSession session, ModeratorInput input, GameService service)
    {
        if (service.ShouldReissueCommand(session, input, out var result))
            return result!;

		if (!session.PendingVoteOutcome.HasValue)
		{
			return HandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, GameStrings.VoteOutcomeMissingError));
		}

		Guid outcome = session.PendingVoteOutcome.Value;
		session.PendingVoteOutcome = null; // Clear state

		session.LogVoteResolved((outcome == Guid.Empty) ? null : outcome, outcome == Guid.Empty);

		var previousPhase = session.GamePhase;
		ModeratorInstruction nextInstruction;
		PhaseTransitionReason transitionReason;

		if (outcome == Guid.Empty) // Tie
		{
			session.TransitionToPhase(GamePhase.Night_RoleAction, PhaseTransitionReason.VoteResolvedTieProceedToNight);
			nextInstruction = new ModeratorInstruction
			{ InstructionText = GameStrings.VoteResultTieProceedToNight, ExpectedInputType = ExpectedInputType.Confirmation };
		}
		else // Player eliminated
		{
			Guid eliminatedPlayerId = outcome;
			if (session.Players.TryGetValue(eliminatedPlayerId, out var eliminatedPlayer))
			{
				if (eliminatedPlayer.Status == PlayerStatus.Alive)
				{
					eliminatedPlayer.Status = PlayerStatus.Dead;
					service.LogElimination(session, eliminatedPlayerId, EliminationReason.DayVote);
				}

				session.GamePhase = GamePhase.Day_Event;
				transitionReason = PhaseTransitionReason.VoteResolvedProceedToReveal;
				nextInstruction = new ModeratorInstruction
				{
					// Placeholder for GameStrings.PlayerEliminatedByVoteRevealRole
					InstructionText = string.Format(GameStrings.PlayerEliminatedByVoteRevealRole, eliminatedPlayer.Name),
					ExpectedInputType = ExpectedInputType.AssignPlayerRoles,
					AffectedPlayerIds = new List<Guid> { eliminatedPlayerId },
					SelectableRoles = Enum.GetValues<RoleType>().ToList()
				};
			}
			else
			{
				return HandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, string.Format(GameStrings.PlayerIdNotFound, eliminatedPlayerId)));
			}
		}

		return HandlerResult.SuccessTransition(nextInstruction, transitionReason);
	}

    public static HandlerResult HandleGameOverPhase(GameSession session, ModeratorInput input, GameService service)
    { 
        // No actions allowed in GameOver state, return error if any input is attempted
        var error = new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_GameIsOver, GameStrings.GameOverMessage);
        return HandlerResult.Failure(error);
    }

    // --- Helper Methods ---

    /// <summary>
    /// Generates the next instruction during the Night phase.
    /// Handles role ordering, identification, action prompts, and phase transition.
    /// Updates session state (CurrentNightActingRoleIndex, PendingNight1IdentificationForRole, GamePhase).
    /// </summary>
    private ModeratorInstruction GenerateNextNightInstruction(GameSession session)
    {
        while (session.IsCurrentNightComplete == false)
        {
	        var currentRole = session.CurrentNightRole;

            var livingPlayers = session.Players.Values.Where(p => p.Status == PlayerStatus.Alive).ToList();
            var assignedActors = livingPlayers.Where(p => p.Role == currentRole).ToList();

            // N1 Identification Trigger
            bool needsN1Identification = session.TurnNumber == 1 &&
                                         currentRole.RequiresNight1Identification() &&
                                         !assignedActors.Any();

            if (needsN1Identification)
            {
                session.TransitionToPhase(GamePhase.Night_RoleIdentification, PhaseTransitionReason.IdentifiedRoleAndProceedToAction);
                return currentRole.GenerateIdentificationInstructions(session)!; // Assume not null if Requires=true
            }

            // Action Instruction Generation
            if (!assignedActors.Any()) // Skip if no one has the role (and N1 ID wasn't needed)
            {
                session.CurrentNightActingRoleIndex++;
                continue;
            }

            var actionInstruction = currentRoleInstance.GenerateNightInstructions(session);
            if (actionInstruction != null)
            {
                return actionInstruction; // Found the next action instruction
            }
            else
            {
                // No action needed/possible for this role now, move to the next.
                session.CurrentNightActingRoleIndex++;
                continue;
            }
        }

        // No More Roles to Act - Transition to Day_ResolveNight
        // The *calling* handler (HandleNightPhase) detects this transition is needed
        // by seeing the index go out of bounds AFTER this returns.
        // This method signals the end by returning the confirmation prompt for the *next* phase.
        var previousPhase = session.GamePhase;
        session.GamePhase = GamePhase.Day_ResolveNight; // Update phase *before* returning instruction for it
        LogPhaseTransition(session, previousPhase, session.GamePhase, PhaseTransitionReason.RoleActionComplete);

        return new ModeratorInstruction { InstructionText = GameStrings.ResolveNightPrompt, ExpectedInputType = ExpectedInputType.Confirmation };
    }

    private (Team WinningTeam, string Description)? CheckVictoryConditions(GameSession session)
    {
        // Phase 1: Basic checks using assigned/revealed roles
        int aliveWerewolves = session.Players.Values.Count(p => p.Status == PlayerStatus.Alive && p.Role?.RoleType == RoleType.SimpleWerewolf);
        int aliveNonWerewolves = session.Players.Values.Count(p => p.Status == PlayerStatus.Alive && p.Role?.RoleType != RoleType.SimpleWerewolf);

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

    /// <summary>
    /// Validates the moderator input against the expected input type in the pending instruction.
    /// </summary>
    /// <returns>A ProcessResult.Failure if validation fails, otherwise null.</returns>
    private static ProcessResult? ValidateExpectedInput(GameSession session, ModeratorInput input)
    {
        if (session.PendingModeratorInstruction == null)
        {
            // Should only happen if StartNewGame didn't set the initial instruction
            // TODO: Use GameString
            return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation,
                                                    GameErrorCode.InvalidOperation_UnexpectedInput,
                                                    "Internal error: No pending instruction available."));
        }

        // Allow input when None is expected (e.g., error recovery or unsolicited input)
        if (session.PendingModeratorInstruction.ExpectedInputType == ExpectedInputType.None)
        {
            return null; // No validation needed if None is expected
        }

        if (input.InputTypeProvided != session.PendingModeratorInstruction.ExpectedInputType)
        {
            // Re-issue the last instruction on mismatch by returning a specific failure type
            // TODO: Consider a more specific error code/message?
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                                                    GameErrorCode.InvalidInput_InputTypeMismatch,
                                                    $"Expected input {session.PendingModeratorInstruction.ExpectedInputType} but got {input.InputTypeProvided}"));
        }

        return null; // Input type matches expected
    }

    /// <summary>
    /// Processes the assignment of a single role to a player during the Day Event phase (role reveal).
    /// Updates the player's role and logs the reveal.
    /// </summary>
    /// <returns>A HandlerResult.Failure if an error occurs, otherwise null.</returns>
    private HandlerResult? ProcessSingleRoleAssignment(GameSession session, KeyValuePair<Guid, RoleType> assignment)
    {
        Guid playerToRevealId = assignment.Key;
        if (!session.Players.TryGetValue(playerToRevealId, out var playerToReveal))
        {
            // Should not happen if instruction was generated correctly
            return HandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, GameStrings.RevealTargetNotFoundError));
        }

        // Instantiate the role based on input
        RoleType revealedRoleType = assignment.Value;

        // Player role should never be RoleType.Unknown to the moderator, and implementation should exist
        if (!_roleImplementations.TryGetValue(revealedRoleType, out var revealedRoleInstance) || revealedRoleInstance == null)
        {
            return HandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_RoleNameNotFound, string.Format(GameStrings.RoleImplementationNotFound, revealedRoleType)));
        }

        playerToReveal.Role = revealedRoleInstance;
        LogRoleReveal(session, playerToReveal.Id, revealedRoleType); // Use 'this.LogRoleReveal' if not static

        return null; // Indicate success
    }

	/// <summary>
	/// Handles phases expecting a confirmation. If confirmed, executes the provided action.
	/// If not confirmed, re-issues the current instruction.
	/// </summary>
	/// <param name="session">The game session.</param>
	/// <param name="input">The moderator input.</param>
	/// <param name="handlerResult">The handlerResult to return if input.Confirmation is true.</param>
	/// <returns>The result of the onConfirmedAction or SuccessStayInPhase.</returns>
	private bool ShouldReissueCommand(GameSession session, ModeratorInput input, out HandlerResult? handlerResult)
    {
        handlerResult = null;
		if (input.Confirmation != true)
		{
            // Re-issue the current instruction
            if (session.PendingModeratorInstruction == null)
            {
                // This case should ideally not happen if called correctly, but handle defensively
                handlerResult = HandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, "Internal error: Cannot reissue null instruction.")); // TODO: GameString
            }
            else
            {
				handlerResult = HandlerResult.SuccessStayInPhase(session.PendingModeratorInstruction);
			}

            return true;
        }

        return false;
    }
}