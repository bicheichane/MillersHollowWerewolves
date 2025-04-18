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
using System.Diagnostics; // For Debug.Fail
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
    private static readonly Dictionary<RoleType, IRole> _roleImplementations = new()
    {
        { RoleType.SimpleVillager, new SimpleVillagerRole() },
        { RoleType.SimpleWerewolf, new SimpleWerewolfRole() },
        // Phase 2 Roles
        { RoleType.Seer, new SeerRole() },
        { RoleType.Defender, new DefenderRole() },
        { RoleType.Witch, new WitchRole() }
        // Add other roles here in later phases
    };

    private readonly GameFlowManager _gameFlowManager;

    // REMOVED: Constants for Transition Reasons are now in PhaseTransitionReason enum
	// private const string ReasonSetupConfirmed = "SetupConfirmed";
    // private const string ReasonNightStartsConfirmed = "NightStartsConfirmed";
    // private const string ReasonIdentifiedAndProceedToWwAction = "IdentifiedAndProceedToWwAction";
    // private const string ReasonWwActionComplete = "WwActionComplete";
    // private const string ReasonNightResolutionConfirmedProceedToReveal = "NightResolutionConfirmedProceedToReveal";
    // private const string ReasonNightResolutionConfirmedNoVictims = "NightResolutionConfirmedNoVictims";
    // private const string ReasonRoleRevealedProceedToDebate = "RoleRevealedProceedToDebate";
	// private const string ReasonRoleRevealedProceedToNight = "RoleRevealedProceedToNight";
	// private const string ReasonDebateConfirmedProceedToVote = "DebateConfirmedProceedToVote";
    // private const string ReasonVoteOutcomeReported = "VoteOutcomeReported";
    // private const string ReasonVoteResolvedProceedToReveal = "VoteResolvedProceedToReveal";
    // private const string ReasonVoteResolvedTieProceedToNight = "VoteResolvedTieProceedToNight";
    // private const string ReasonRepeatInstruction = "RepeatInstruction"; // For re-issuing instructions

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

        var session = new GameSession
        {
            Players = players,
            PlayerSeatingOrder = seatingOrder,
            RolesInPlay = new List<RoleType>(rolesInPlay), // Copy list
            GamePhase = GamePhase.Setup,
            TurnNumber = 0
            // EventDeck setup would happen here if events were implemented
        };

        // Initial log entry
        var logEntry = new GameStartedLogEntry
        {
            InitialRoles = session.RolesInPlay.AsReadOnly(),
            InitialPlayers = playerInfos.AsReadOnly(),
            InitialEvents = eventCardIdsInDeck?.AsReadOnly(),
            TurnNumber = session.TurnNumber,
            Phase = session.GamePhase
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
                var oldPhaseForLog = session.GamePhase;
                session.GamePhase = GamePhase.GameOver;
                session.WinningTeam = victoryCheckResult.Value.WinningTeam;

				LogPhaseTransition(session, oldPhaseForLog, GamePhase.GameOver, PhaseTransitionReason.VictoryConditionMet);

                var victoryLog = new VictoryConditionMetLogEntry
                {
                    WinningTeam = victoryCheckResult.Value.WinningTeam,
                    ConditionDescription = victoryCheckResult.Value.Description,
                    TurnNumber = session.TurnNumber,
                    Phase = session.GamePhase
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
        return service.HandleConfirmationOrReissue(session, input, () =>
        {
            var oldPhase = session.GamePhase;
            session.GamePhase = GamePhase.Night;

            service.LogPhaseTransition(session, oldPhase, session.GamePhase, PhaseTransitionReason.SetupConfirmed);

            var nightStartInstruction = new ModeratorInstruction
            {
                InstructionText = GameStrings.NightStartsPrompt,
                ExpectedInputType = ExpectedInputType.Confirmation
            };
            // Transition happens, specific instruction provided.
            return HandlerResult.SuccessTransition(nightStartInstruction, PhaseTransitionReason.SetupConfirmed);
        });
    }

    public static HandlerResult HandleNightPhase(GameSession session, ModeratorInput input, GameService service)
    {
        // If this is the very start of the Night phase (first instruction)
        if (session.PendingModeratorInstruction?.InstructionText == GameStrings.NightStartsPrompt)
        {
            return service.HandleConfirmationOrReissue(session, input, () =>
            {
                // Confirmed night start
                session.CurrentNightActingRoleIndex = 0;
                session.PendingNight1IdentificationForRole = null;
                var nextInstruction = service.GenerateNextNightInstruction(session);
                // Stay in Night phase, issue the first real night instruction (ID or Action)
                return HandlerResult.SuccessStayInPhase(nextInstruction);
            });
        }

        // --- Handle Night 1 Identification Input ---
        if (session.PendingNight1IdentificationForRole.HasValue)
        {
            RoleType roleToIdentify = session.PendingNight1IdentificationForRole.Value;
            // Use static dictionary directly
            if (!_roleImplementations.TryGetValue(roleToIdentify, out var roleInstance))
            {
                 return HandlerResult.Failure(GameError.InternalError(GameErrorCode.Unknown_InternalError, $"Role implementation not found for pending identification: {roleToIdentify}"));
            }
            
            // Ensure input type matches expected ID type (usually PlayerSelectionSingle or Multiple)
            // Note: GenerateIdentificationInstructions should set the correct expected type.
            // Basic check:
             if(input.InputTypeProvided != session.PendingModeratorInstruction?.ExpectedInputType)
             {
                 return HandlerResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_TypeMismatch, "Input type doesn't match expected for role identification."));
             }

            var identificationResult = roleInstance.ProcessIdentificationInput(session, input);

            if (!identificationResult.IsSuccess)
            {
                // ID Failed - Re-issue the identification prompt
                var reissueInstruction = roleInstance.GenerateIdentificationInstructions(session);
                if (reissueInstruction == null) 
                     return HandlerResult.Failure(GameError.InternalError(GameErrorCode.Unknown_InternalError, $"Failed to regenerate ID instruction for {roleToIdentify} after input failure."));
                
                return HandlerResult.Failure(identificationResult.Error!, reissueInstruction); // Return error *and* the re-issued instruction
            }
            else
            {
                // ID Succeeded!
                session.PendingNight1IdentificationForRole = null; // Clear pending state
                
                // Immediately generate the ACTION instruction for the SAME role
                var actionInstruction = roleInstance.GenerateNightInstructions(session);
                if (actionInstruction == null)
                {
                    // Role identified, but has no immediate action? Move to next role.
                    session.CurrentNightActingRoleIndex++;
                    actionInstruction = service.GenerateNextNightInstruction(session);
                }
                // Stay in Night phase, issue the action instruction
                return HandlerResult.SuccessStayInPhase(actionInstruction);
            }
        }

        // --- Handle Night Action Input ---
        var nightWakeUpOrder = service.GetNightWakeUpOrder(session);
        if (session.CurrentNightActingRoleIndex >= nightWakeUpOrder.Count)
        {
            // Should have been caught by GenerateNextNightInstruction returning the resolution prompt?
            // Or maybe GenerateNextNightInstruction returned the final action, which succeeded.
            // If we reach here after processing an action, it means the night ends.
            service.LogPhaseTransition(session, GamePhase.Night, GamePhase.Day_ResolveNight, PhaseTransitionReason.AllNightActionsComplete); // New reason
             // TODO: Use GameString for resolution prompt
            var resolutionInstruction = new ModeratorInstruction { InstructionText = "Night actions complete. Prepare for day resolution.", ExpectedInputType = ExpectedInputType.Confirmation };
            return HandlerResult.SuccessTransition(resolutionInstruction, PhaseTransitionReason.AllNightActionsComplete);
        }

        var currentRole = nightWakeUpOrder[session.CurrentNightActingRoleIndex];
        
         // Validate input type against expected type from the generated action instruction
        if (input.InputTypeProvided != session.PendingModeratorInstruction?.ExpectedInputType)
        {
             return HandlerResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_TypeMismatch, "Input type doesn't match expected for role action."));
        }

        var actionResult = currentRole.ProcessNightAction(session, input);

        if (!actionResult.IsSuccess)
        {
            // Action failed - Re-issue the action prompt
            var reissueInstruction = currentRole.GenerateNightInstructions(session);
             if (reissueInstruction == null) 
                 return HandlerResult.Failure(GameError.InternalError(GameErrorCode.Unknown_InternalError, $"Failed to regenerate action instruction for {currentRole.Name} after input failure."));

            return HandlerResult.Failure(actionResult.Error!, reissueInstruction); // Return error and re-issued instruction
        }
        else
        {
            // Action Succeeded!
            // If the action result provided a specific confirmation, use it briefly?
            // For now, assume ProcessNightAction returns Success() or Success(confirmation)
            // and we immediately move to the next required step.
            
            // Move to the next role
            session.CurrentNightActingRoleIndex++;
            var nextInstruction = service.GenerateNextNightInstruction(session);

             // Check if GenerateNextNightInstruction determined the night is over
             if (session.CurrentNightActingRoleIndex >= nightWakeUpOrder.Count) 
             { 
                 // Night is over, transition needed
                 service.LogPhaseTransition(session, GamePhase.Night, GamePhase.Day_ResolveNight, PhaseTransitionReason.AllNightActionsComplete);
                  // The instruction returned by GenerateNextNightInstruction in this case is the resolution confirmation
                  return HandlerResult.SuccessTransition(nextInstruction, PhaseTransitionReason.AllNightActionsComplete);
             } 
             else 
             { 
                 // Stay in Night phase, issue next instruction (could be for the *next* role)
                 return HandlerResult.SuccessStayInPhase(nextInstruction);
             } 
        }
    }

    public static HandlerResult HandleDayResolveNightPhase(GameSession session, ModeratorInput input, GameService service)
    {
        // Expecting confirmation to proceed with resolution
        return service.HandleConfirmationOrReissue(session, input, () =>
        {
            var currentTurn = session.TurnNumber;
            var log = session.GameHistoryLog;
            var players = session.Players;
            var eliminations = new Dictionary<Guid, EliminationReason>(); // Store final eliminations

            // 1. Find WW victim choice for this night (TurnNumber matches, Phase=Night)
            var wwChoiceLog = log.OfType<WerewolfVictimChoiceLogEntry>()
                                 .LastOrDefault(l => l.TurnNumber == currentTurn && l.Phase == GamePhase.Night);
            Guid? wwVictimId = wwChoiceLog?.VictimId;

            // 2. Check Defender Protection
            var defenseLog = log.OfType<DefenderProtectionChoiceLogEntry>()
                                .LastOrDefault(l => l.TurnNumber == currentTurn && l.Phase == GamePhase.Night);
            bool wwKillNegatedByDefender = wwVictimId.HasValue && wwVictimId == defenseLog?.TargetId;

            // 3. Check Witch Actions
            var witchLogs = log.OfType<WitchPotionUseAttemptLogEntry>()
                               .Where(l => l.TurnNumber == currentTurn && l.Phase == GamePhase.Night)
                               .ToList();

            bool wwKillNegatedByWitchHeal = false;
            Guid? witchPoisonTargetId = null;

            foreach (var witchLog in witchLogs)
            {
                if (witchLog.PotionType == WitchPotionType.Healing && witchLog.TargetId == wwVictimId)
                {
                    wwKillNegatedByWitchHeal = true;
                }
                if (witchLog.PotionType == WitchPotionType.Poison)
                {
                    // Ensure target is still valid (alive)
                    if (players.TryGetValue(witchLog.TargetId, out var poisonTarget) && poisonTarget.Status == PlayerStatus.Alive)
                    {
                        witchPoisonTargetId = witchLog.TargetId; 
                    }
                }
            }

            // 4. Determine final WW elimination
            if (wwVictimId.HasValue && !wwKillNegatedByDefender && !wwKillNegatedByWitchHeal)
            {
                if (players.TryGetValue(wwVictimId.Value, out var victim) && victim.Status == PlayerStatus.Alive)
                {
                    // TODO: Add Elder check here in Phase 3 (if target is Elder, increment count, survive first time)
                    // TODO: Add Little Girl check here in Phase 3 (if LG caught, override victim)
                     eliminations[wwVictimId.Value] = EliminationReason.WerewolfAttack;
                }
            }
            
            // 5. Add Witch Poison elimination
            if(witchPoisonTargetId.HasValue && !eliminations.ContainsKey(witchPoisonTargetId.Value)) // Don't double-eliminate
            {
                 if (players.TryGetValue(witchPoisonTargetId.Value, out var victim) && victim.Status == PlayerStatus.Alive)
                {
                    eliminations[witchPoisonTargetId.Value] = EliminationReason.WitchPoison;
                }
            }

            // --- Process Eliminations & Generate Next Instruction --- 
            
            var eliminatedPlayerNames = new List<string>();
             foreach (var kvp in eliminations)
            {
                var playerId = kvp.Key;
                var reason = kvp.Value;
                if(players.TryGetValue(playerId, out var playerToEliminate))
                {
                    playerToEliminate.Status = PlayerStatus.Dead;
                    service.LogElimination(session, playerId, reason);
                    eliminatedPlayerNames.Add(playerToEliminate.Name); 
                     // TODO: Trigger death effects here (Hunter, Lovers, Sheriff etc. in later phases)
                }
            }

            // Update Defender state AFTER all effects resolved
            session.LastProtectedPlayerId = defenseLog?.TargetId; // Store who was protected last night
            session.ProtectedPlayerId = null; // Clear current protection

            ModeratorInstruction nextInstruction;
            PhaseTransitionReason transitionReason;

            if (eliminatedPlayerNames.Any())
            {
                // Eliminations occurred -> Proceed to Reveal step (Day_Event)
                // TODO: Handle multiple eliminations text
                string eliminationText = string.Join(", ", eliminatedPlayerNames);
                // TODO: Use GameString for reveal prompt
                nextInstruction = new ModeratorInstruction
                {
                    InstructionText = $"{eliminationText} {(eliminatedPlayerNames.Count > 1 ? "were" : "was")} eliminated. Reveal roles?",
                    ExpectedInputType = ExpectedInputType.AssignPlayerRoles, // Corrected Enum
                    AffectedPlayerIds = eliminations.Keys.ToList(),
                    SelectableRoles = Enum.GetValues<RoleType>().Where(rt => rt > RoleType.Unassigned).ToList() // Provide roles list
                };
                service.LogPhaseTransition(session, GamePhase.Day_ResolveNight, GamePhase.Day_Event, PhaseTransitionReason.NightResolutionConfirmedProceedToReveal);
                transitionReason = PhaseTransitionReason.NightResolutionConfirmedProceedToReveal;

            }
            else
            {
                // No eliminations -> Proceed directly to Debate
                 // TODO: Use GameString
                nextInstruction = new ModeratorInstruction
                {
                    InstructionText = "No one was eliminated during the night. Proceed to day debate?",
                    ExpectedInputType = ExpectedInputType.Confirmation
                };
                service.LogPhaseTransition(session, GamePhase.Day_ResolveNight, GamePhase.Day_Debate, PhaseTransitionReason.NightResolutionConfirmedNoVictims);
                 transitionReason = PhaseTransitionReason.NightResolutionConfirmedNoVictims;
                // Use default instruction for Debate phase?
                // For now, return specific instruction. Can refine with UseDefaultInstructionForNextPhase later.
            }

             // Check victory AFTER updating state
            var victory = service.CheckVictoryConditions(session);
            if (victory.HasValue)
            {
                 // TODO: Use GameString
                 return HandlerResult.SuccessTransition(
                     new ModeratorInstruction { InstructionText = $"Game Over! {victory.Value.WinningTeam} wins! Reason: {victory.Value.Description}", ExpectedInputType = ExpectedInputType.None },
                     PhaseTransitionReason.VictoryConditionMet
                 );
            }

            return HandlerResult.SuccessTransition(nextInstruction, transitionReason);
        });
    }

    public static HandlerResult HandleDayEventPhase(GameSession session, ModeratorInput input, GameService service)
    {
        // Handle Role Assignment input (for revealing roles after death)
        if (input.InputTypeProvided == ExpectedInputType.AssignPlayerRoles)
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
                session.GamePhase = GamePhase.Night;
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

            // If successful, check for death triggers (Hunter, Lovers - Phase 3+)
            var revealProcessedResult = service.ProcessSingleRoleAssignment(session, input.AssignedPlayerRoles.First());
            if (revealProcessedResult == null || !revealProcessedResult.IsSuccess)
            {
                // Handle potential error during role assignment/logging
                return revealProcessedResult ?? HandlerResult.Failure(GameError.InternalError(GameErrorCode.Unknown_InternalError, "Failed to process role reveal assignment."));
            }

            // TODO: Check death triggers here in Phase 3+

            // Transition to Debate
            service.LogPhaseTransition(session, GamePhase.Day_Event, GamePhase.Day_Debate, PhaseTransitionReason.RoleRevealedProceedToDebate);
            // Use default instruction for debate phase
            return HandlerResult.SuccessTransitionUseDefault(PhaseTransitionReason.RoleRevealedProceedToDebate);
        }
        else
        {
            // If input wasn't RoleAssignment, treat as unexpected
            return HandlerResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_TypeMismatch, "Expected role assignment input during Day Event phase."));
        }
    }

    public static HandlerResult HandleDayDebatePhase(GameSession session, ModeratorInput input, GameService service)
    {
        return service.HandleConfirmationOrReissue(session, input, () =>
        {
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
        });
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
        return service.HandleConfirmationOrReissue(session, input, () =>
        {
            if (!session.PendingVoteOutcome.HasValue)
            { 
                return HandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, GameStrings.VoteOutcomeMissingError)); 
            }

            Guid outcome = session.PendingVoteOutcome.Value;
            session.PendingVoteOutcome = null; // Clear state

            service.LogVoteResolved(session, (outcome == Guid.Empty) ? null : outcome, outcome == Guid.Empty);

            var previousPhase = session.GamePhase;
            ModeratorInstruction nextInstruction;
            PhaseTransitionReason transitionReason;

            if (outcome == Guid.Empty) // Tie
            {
                session.GamePhase = GamePhase.Night;
                transitionReason = PhaseTransitionReason.VoteResolvedTieProceedToNight;
                nextInstruction = new ModeratorInstruction
                { InstructionText = GameStrings.VoteResultTieProceedToNight, ExpectedInputType = ExpectedInputType.Confirmation };
            }
            else // Player eliminated
            {
                Guid eliminatedPlayerId = outcome;
                if (session.Players.TryGetValue(eliminatedPlayerId, out var eliminatedPlayer))
                {
                     if(eliminatedPlayer.Status == PlayerStatus.Alive)
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
                        SelectableRoles = Enum.GetValues<RoleType>().Where(rt => rt > RoleType.Unassigned).ToList()
                    };
                }
                else
                {
                    return HandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, string.Format(GameStrings.PlayerIdNotFound, eliminatedPlayerId)));
                }
            }

            service.LogPhaseTransition(session, previousPhase, session.GamePhase, transitionReason);
            return HandlerResult.SuccessTransition(nextInstruction, transitionReason);
        });
    }

    public static HandlerResult HandleGameOverPhase(GameSession session, ModeratorInput input, GameService service)
    { 
        // No actions allowed in GameOver state, return error if any input is attempted
        var error = new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_GameIsOver, GameStrings.GameOverMessage);
        return HandlerResult.Failure(error);
    }

    // --- Helper Methods ---

    private void LogPhaseTransition(GameSession session, GamePhase previousPhase, GamePhase currentPhase, PhaseTransitionReason reason)
    {
        session.GameHistoryLog.Add(new PhaseTransitionLogEntry
        {
            Timestamp = DateTime.UtcNow,
            TurnNumber = session.TurnNumber,
            Phase = currentPhase, // Log the phase *entered*
            PreviousPhase = previousPhase,
            CurrentPhase = currentPhase,
            Reason = reason
        });
    }

    private void LogInitialAssignment(GameSession session, Guid playerId, RoleType roleType)
    {
        session.GameHistoryLog.Add(new InitialRoleLogAssignment
        {
             PlayerId = playerId, // Assuming ActorId is the correct property name
             AssignedRole = roleType,
             Timestamp = DateTimeOffset.UtcNow,
             TurnNumber = session.TurnNumber,
             Phase = session.GamePhase
		});
    }

     private void LogRoleReveal(GameSession session, Guid playerId, RoleType roleType)
     {
         session.GameHistoryLog.Add(new RoleRevealedLogEntry
         {
            PlayerId = playerId, 
            RevealedRole = roleType, 
            Timestamp = DateTimeOffset.UtcNow, 
            TurnNumber = session.TurnNumber, 
            Phase = session.GamePhase 
         }); // Removed the extra property assignments here as they are set above
     }

     private void LogElimination(GameSession session, Guid playerId, EliminationReason reason)
     {
         session.GameHistoryLog.Add(new PlayerEliminatedLogEntry
         {
             PlayerId = playerId, 
             Reason = reason, 
             Timestamp = DateTimeOffset.UtcNow, 
             TurnNumber = session.TurnNumber, 
             Phase = session.GamePhase
        }); // Removed the extra property assignments here
     }

    private void LogVoteOutcomeReported(GameSession session, Guid? reportedOutcomePlayerId)
    {
         session.GameHistoryLog.Add(new VoteOutcomeReportedLogEntry
         {
            ReportedOutcomePlayerId = reportedOutcomePlayerId ?? Guid.Empty,
            Timestamp = DateTimeOffset.UtcNow, 
            TurnNumber = session.TurnNumber, 
            Phase = session.GamePhase
         }); // Removed the extra property assignments here
    }

     private void LogVoteResolved(GameSession session, Guid? eliminatedPlayerId, bool wasTie)
     {
          session.GameHistoryLog.Add(new VoteResolvedLogEntry
          {
             EliminatedPlayerId = eliminatedPlayerId, 
             WasTie = wasTie,
             Timestamp = DateTimeOffset.UtcNow, 
             TurnNumber = session.TurnNumber, 
             Phase = session.GamePhase
          }); // Removed the extra property assignments here
     }

    /// <summary>
    /// Generates the next instruction during the Night phase.
    /// Handles role ordering, identification, action prompts, and phase transition.
    /// Updates session state (CurrentNightActingRoleIndex, PendingNight1IdentificationForRole, GamePhase).
    /// </summary>
    private ModeratorInstruction GenerateNextNightInstruction(GameSession session)
    {
        var nightWakeUpOrder = GetNightWakeUpOrder(session); // Now returns List<IRole>

        if (session.CurrentNightActingRoleIndex >= nightWakeUpOrder.Count)
        {
            // All roles have acted, night should end.
            // GameService HandleNightPhase should catch this and transition.
            // Return a placeholder/confirmation instruction for resolution start.
            // TODO: Use GameString
            return new ModeratorInstruction { InstructionText = "Night actions complete. Prepare for day resolution.", ExpectedInputType = ExpectedInputType.Confirmation }; 
        }

        var currentRole = nightWakeUpOrder[session.CurrentNightActingRoleIndex];
        ModeratorInstruction? nextInstruction = null;

        // --- Night 1 Identification Handling ---
        if (session.TurnNumber == 1 && currentRole.RequiresNight1Identification())
        {
            // Check if this role has ALREADY been identified (e.g., if input failed and we are re-prompting)
            var isIdentified = session.Players.Values.Any(p => p.Role?.RoleType == currentRole.RoleType);
            
            if (!isIdentified && !session.PendingNight1IdentificationForRole.HasValue)
            {
                // Need to identify this role first
                session.PendingNight1IdentificationForRole = currentRole.RoleType; 
                nextInstruction = currentRole.GenerateIdentificationInstructions(session);
                if (nextInstruction != null) return nextInstruction; // Return ID prompt
                else 
                {
                    // Role requires ID but couldn't generate prompt? Log error/fallback.
                    Debug.Fail($"Role {currentRole.RoleType} requires Night 1 ID but GenerateIdentificationInstructions returned null.");
                    // TODO: Use GameString, return error instruction?
                     nextInstruction = new ModeratorInstruction { InstructionText = $"Internal Error: Could not generate identification prompt for {currentRole.Name}. Skipping.", ExpectedInputType = ExpectedInputType.Confirmation };
                     session.PendingNight1IdentificationForRole = null; // Clear pending state
                     session.CurrentNightActingRoleIndex++; // Move to next role
                     // Recursive call might be dangerous, let HandleNightPhase re-trigger
                      return nextInstruction; 
                }
            }
            // Else: Role already identified OR another role is pending ID (will be handled by HandleNightPhase), so proceed to this role's action.
            // Clear the pending state as we are moving to action (or skipping if no action prompt).
            session.PendingNight1IdentificationForRole = null; 
        }
        
        // --- Standard Night Action --- 
        // Try to generate the action instruction for the current role
        nextInstruction = currentRole.GenerateNightInstructions(session);

        if (nextInstruction == null)
        {
            // Role has no action this turn (or couldn't generate prompt), move to the next role.
            session.CurrentNightActingRoleIndex++;
            // Recursive call to find the *next* instruction
            return GenerateNextNightInstruction(session); 
        }
        else
        {   
            // Found the instruction for the current role's action.
            return nextInstruction; 
        }
    }

    private List<IRole> GetNightWakeUpOrder(GameSession session)
    {
        // Get role instances for all roles currently in play that might act at night
        var roles = session.RolesInPlay
            .Where(rt => _roleImplementations.ContainsKey(rt)) // Ensure we have an implementation
            .Select(rt => _roleImplementations[rt])
            .Where(role => role.GetNightWakeUpOrder() < int.MaxValue) // Filter out roles with no night action
            .OrderBy(role => role.GetNightWakeUpOrder()) // Sort by priority
            .ToList();
        return roles;
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
    /// <param name="onConfirmedAction">The action to execute if input.Confirmation is true.</param>
    /// <returns>The result of the onConfirmedAction or SuccessStayInPhase.</returns>
    private HandlerResult HandleConfirmationOrReissue(GameSession session, ModeratorInput input, Func<HandlerResult> onConfirmedAction)
    {
        if (input.Confirmation == true)
        {
            return onConfirmedAction();
        }
        else
        {
            // Re-issue the current instruction
            if (session.PendingModeratorInstruction == null)
            {
                // This case should ideally not happen if called correctly, but handle defensively
                return HandlerResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, "Internal error: Cannot reissue null instruction.")); // TODO: GameString
            }
            return HandlerResult.SuccessStayInPhase(session.PendingModeratorInstruction);
        }
    }
}