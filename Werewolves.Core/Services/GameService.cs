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

namespace Werewolves.Core.Services;

/// <summary>
/// Orchestrates the game flow based on moderator input and tracked state.
/// </summary>
public class GameService
{
    // Simple in-memory storage for game sessions. Replaceable with DI.
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();

    // TODO: Replace direct role instantiation with a factory or registry later
    private readonly Dictionary<RoleType, IRole> _roleImplementations = new()
    {
        { RoleType.SimpleVillager, new SimpleVillagerRole() },
        { RoleType.SimpleWerewolf, new SimpleWerewolfRole() }
    };

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
    /// Processes input provided by the moderator.
    /// </summary>
    /// <param name="gameId">The ID of the game session.</param>
    /// <param name="input">The moderator's input.</param>
    /// <returns>A ProcessResult indicating success or failure, with the next instruction or error details.</returns>
    public ProcessResult ProcessModeratorInput(Guid gameId, ModeratorInput input)
    {
        if (!_sessions.TryGetValue(gameId, out var session))
        {
            return ProcessResult.Failure(new GameError(ErrorType.GameNotFound,
                                                    GameErrorCode.GameNotFound_SessionNotFound,
                                                    GameStrings.GameNotFound));
        }

        // --- Basic Input Validation ---
        if (session.PendingModeratorInstruction == null)
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation,
                                                    GameErrorCode.InvalidOperation_UnexpectedInput,
                                                    GameStrings.UnexpectedInput));
        }
        if (input.InputTypeProvided != session.PendingModeratorInstruction.ExpectedInputType)
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                                                    GameErrorCode.InvalidInput_InputTypeMismatch,
                                                    GameStrings.InputTypeMismatch));
        }
        if (session.GamePhase == GamePhase.GameOver)
        {
             return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_GameIsOver, string.Format(GameStrings.GameOverMessage, "No further actions possible.")));
        }

        // --- Game Phase Logic ---
        ProcessResult result;
        var phaseBeingHandled = session.GamePhase; // Store phase before switch
        switch (session.GamePhase)
        {
            case GamePhase.Setup:
                result = HandleSetupPhase(session, input);
                break;
            case GamePhase.Night:
                result = HandleNightPhase(session, input);
                break;
            case GamePhase.Day_ResolveNight:
                result = HandleDayResolveNightPhase(session, input);
                break;
            case GamePhase.Day_Event: // Role Reveal happens here in Phase 1
                result = HandleDayEventPhase(session, input);
                break;
            case GamePhase.Day_Debate:
                result = HandleDayDebatePhase(session, input);
                break;
            case GamePhase.Day_Vote:
                result = HandleDayVotePhase(session, input);
                break;
            case GamePhase.Day_ResolveVote:
                result = HandleDayResolveVotePhase(session, input);
                break;
             case GamePhase.GameOver:
                 result = ProcessResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_GameIsOver, GameStrings.GameOverMessage));
                 break;
            default:
                result = ProcessResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, string.Format(GameStrings.ActionNotInPhase, $"Input processing for {session.GamePhase}", session.GamePhase)));
                break;
        }

        // --- Post-Processing (Victory Check & Instruction Update) ---
        if (result.IsSuccess)
        {
            // Check victory ONLY after specific resolution phases
            if (phaseBeingHandled == GamePhase.Day_ResolveNight || phaseBeingHandled == GamePhase.Day_ResolveVote)
            {
                var victoryCheckResult = CheckVictoryConditions(session);
                if (victoryCheckResult != null) // Victory condition met
                {
                    session.GamePhase = GamePhase.GameOver;
                    var victoryLog = new VictoryConditionMetLogEntry { WinningTeam = victoryCheckResult.Value.WinningTeam, ConditionDescription = victoryCheckResult.Value.Description, TurnNumber = session.TurnNumber, Phase = session.GamePhase };
                    session.GameHistoryLog.Add(victoryLog);

                    var finalInstruction = new ModeratorInstruction
                    {
                        InstructionText = string.Format(GameStrings.GameOverMessage, victoryCheckResult.Value.Description),
                        ExpectedInputType = ExpectedInputType.None
                    };
                    session.PendingModeratorInstruction = finalInstruction;
                    return ProcessResult.Success(finalInstruction); // Return the final game over instruction
                }
                // No victory condition met after the check, fall through to normal instruction update
            }

            // Update the pending instruction if the handler returned a new one (and no victory was triggered)
            if (result.ModeratorInstruction != null)
            {
                 session.PendingModeratorInstruction = result.ModeratorInstruction;
            }
            // If the handler didn't return a specific instruction, the state transition itself dictates the next step
            // (e.g., after WW action, the service knows to move to resolution)
            // The transition logic within the handlers should set the next logical PendingInstruction
            else if (session.PendingModeratorInstruction?.ExpectedInputType == ExpectedInputType.None)
            {
                 // If the last action resulted in no expected input, we need to generate the next logical step's instruction.
                 // This might involve calling a method like GenerateNextInstruction(session);
                 // For Phase 1, the handlers explicitly set the next instruction.
                 // This else if might be needed for more complex scenarios later.
            }
        }

        return result; // Return the original result (success or failure)
    }

    // --- Phase-Specific Handlers ---

    private ProcessResult HandleSetupPhase(GameSession session, ModeratorInput input)
    {
        if (input.Confirmation == true)
        {
            // Assign default role (e.g., SimpleVillager) to any unassigned players
            // Assuming RoleType.SimpleVillager exists and is in _roleImplementations

            // Transition to Night 1
            session.GamePhase = GamePhase.Night;
            session.TurnNumber = 1;
            session.CurrentNightActingRoleIndex = -1; // Reset index for the start of the night

            // Generate the first instruction for Night 1 using the helper
            // var firstNightInstruction = GenerateNextNightInstruction(session); // OLD
            // Initial night instruction is just confirmation
            var nightStartInstruction = new ModeratorInstruction
            {
                InstructionText = GameStrings.NightStartsPrompt,
                ExpectedInputType = ExpectedInputType.Confirmation
            };

            session.PendingModeratorInstruction = nightStartInstruction; // Set initial night prompt
            return ProcessResult.Success(nightStartInstruction);
        }
        else // Confirmation == false
        {
            // Stay in Setup phase, return the same instruction to prompt again
            return ProcessResult.Success(session.PendingModeratorInstruction);
        }
    }

    /// <summary>
    /// Generates the next appropriate instruction during the Night phase.
    /// Advances the CurrentNightActingRoleIndex and finds the next role that needs
    /// identification (Night 1 only) or action.
    /// Returns the instruction to transition to Day if no more roles need to act.
    /// </summary>
    private ModeratorInstruction GenerateNextNightInstruction(GameSession session)
    {
        var actingRolesOrdered = GetNightWakeUpOrder(session);

        // Start checking from the role *after* the last one processed
        session.CurrentNightActingRoleIndex++;
        
        while (session.CurrentNightActingRoleIndex < actingRolesOrdered.Count)
        {
            var currentRoleType = actingRolesOrdered[session.CurrentNightActingRoleIndex];
            if (!_roleImplementations.TryGetValue(currentRoleType, out var currentRoleInstance))
            {
                 // Log or handle error - Role implementation missing
                 Console.Error.WriteLine($"Error: Role implementation for {currentRoleType} not found during night processing.");
                 session.CurrentNightActingRoleIndex++; // Move to the next index
                 continue; // Skip to the next iteration
            }

            // Find living players who *could* have this role
            var livingPlayers = session.Players.Values.Where(p => p.Status == PlayerStatus.Alive).ToList();
            var assignedActors = livingPlayers.Where(p => p.Role?.RoleType == currentRoleType).ToList();

            // --- Night 1 Identification Trigger ---
            // Check if identification is required *for this role* before checking for actions.
            bool needsN1Identification = session.TurnNumber == 1 &&
                                         currentRoleInstance.RequiresNight1Identification() &&
                                         !assignedActors.Any(); // No one is assigned this role yet.

            if (needsN1Identification)
            {
                // Found the next required step: Identification for this role.
                session.PendingNight1IdentificationForRole = currentRoleType;
                return currentRoleInstance.GenerateIdentificationInstructions(session);
            }

            // --- Action Instruction Generation ---
            // Check if any living player actually *has* this role assigned.
            // If not, and N1 ID wasn't needed, skip this role.
            if (!assignedActors.Any())
            {
                session.CurrentNightActingRoleIndex++; // Move to the next index
                continue; // Skip to the next role
            }

            // Generate the action instruction for this role.
            // Determine actor: null for group actions, first assigned player otherwise (simplification).
            // TODO: Refine actor determination based on role needs.
            Player? actor = assignedActors.FirstOrDefault();
            var actionInstruction = currentRoleInstance.GenerateNightInstructions(session);

            if (actionInstruction != null)
            {
                // Found the next required step: Action for this role.
                return actionInstruction;
            }
            else
            {
                // Role instance exists, players are assigned, but no instruction generated.
                // This might mean the role's action is conditional and not met,
                // or the action was already completed implicitly.
                // Continue to the next role in the order.
                session.CurrentNightActingRoleIndex++;
                continue;
            }
        }

        // --- No More Roles to Act ---
        // If the loop completes, all roles have had their turn (or were skipped)
        session.GamePhase = GamePhase.Day_ResolveNight; // Transition state
        // Consider logging phase transition here if desired, perhaps with a generic log entry type.

        return new ModeratorInstruction { InstructionText = GameStrings.ResolveNightPrompt, ExpectedInputType = ExpectedInputType.Confirmation };
    }

    private ProcessResult HandleNightPhase(GameSession session, ModeratorInput input)
    {
        var actingRolesOrdered = GetNightWakeUpOrder(session);

        // --- 0. Handle Initial Night Start Confirmation ---
        // Check if the current instruction is the initial "Night Starts" prompt.
        if (session.PendingModeratorInstruction?.ExpectedInputType == ExpectedInputType.Confirmation &&
            session.PendingModeratorInstruction.InstructionText == GameStrings.NightStartsPrompt)
        {
            if (input.Confirmation == true)
            {
                // Moderator confirmed night start. Generate the *first* actual night instruction.
                // GenerateNextNightInstruction handles finding the first role needing ID or action.
                session.CurrentNightActingRoleIndex = -1; // Ensure we start from the beginning
                var firstRoleInstruction = GenerateNextNightInstruction(session);
                session.PendingModeratorInstruction = firstRoleInstruction;
                return ProcessResult.Success(firstRoleInstruction);
            }
            else
            {
                // Moderator did not confirm. Reissue the prompt.
                return ProcessResult.Success(session.PendingModeratorInstruction);
            }
        }

        // --- 1. Handle Pending Night 1 Identification Input ---
        if (session.PendingNight1IdentificationForRole.HasValue)
        {
            var roleTypeToIdentify = session.PendingNight1IdentificationForRole.Value;
            if (!_roleImplementations.TryGetValue(roleTypeToIdentify, out var roleInstance))
            {
                session.PendingNight1IdentificationForRole = null;
                return ProcessResult.Failure(new GameError(ErrorType.Unknown,
                                                    GameErrorCode.Unknown_InternalError,
                                                    $"Role implementation for {roleTypeToIdentify} not found."));
            }

            // Process the identification input using the role's logic
            // This call updates player roles in the session if successful.
            var identificationResult = roleInstance.ProcessIdentificationInput(session, input);

            if (identificationResult.IsSuccess)
            {
                // Log successful identification using the validated player IDs from the input
                if (input.SelectedPlayerIds != null)
                {
                    foreach (var playerId in input.SelectedPlayerIds)
                    {
                        // Fetch the updated player from the session
                        if (session.Players.TryGetValue(playerId, out var player) && player.Role != null)
                        {
                            session.GameHistoryLog.Add(new InitialRoleAssignmentLogEntry
                            {
                                PlayerId = player.Id,
                                AssignedRole = player.Role.RoleType,
                                TurnNumber = session.TurnNumber,
                                Phase = session.GamePhase
                            });
                        }
                        else
                        {
                             // Log error: Player not found or role not assigned after successful identification?
                             Console.Error.WriteLine($"Error logging identification: Player {playerId} not found or role not assigned.");
                        }
                    }
                }

                // Store the type of role just identified before clearing pending state
                var identifiedRoleTypeLocal = session.PendingNight1IdentificationForRole.Value;
                // Clear pending state AFTER successful processing and logging
                session.PendingNight1IdentificationForRole = null;

                // --- Immediately generate ACTION instruction for the identified role ---
                // Need to find the role instance again (could cache it)
                if (_roleImplementations.TryGetValue(identifiedRoleTypeLocal, out var identifiedRoleInstance))
                {
                    // Find an appropriate actor (could be null for group actions, or first identified player)
                    // Use the validated input IDs to find the first player instance.
                    Player? actorForInstruction = null;
                    if (input.SelectedPlayerIds != null && input.SelectedPlayerIds.Any())
                    {
                        session.Players.TryGetValue(input.SelectedPlayerIds.First(), out actorForInstruction);
                    }

                    var actionInstruction = identifiedRoleInstance.GenerateNightInstructions(session);

                    if (actionInstruction == null)
                    {
                        // Role might not have an immediate action after ID.
                        // Advance the index and generate the instruction for the *next* role.
                        var nextInstruction = GenerateNextNightInstruction(session);
                        session.PendingModeratorInstruction = nextInstruction;
                        return ProcessResult.Success(nextInstruction);
                    }
                    else
                    {
                        // Return the action instruction for the *identified* role.
                        // DO NOT advance CurrentNightActingRoleIndex yet.
                        session.PendingModeratorInstruction = actionInstruction;
                        return ProcessResult.Success(actionInstruction);
                    }
                }
                else
                {
                     // Should not happen if initial check passed, but handle defensively
                     Console.Error.WriteLine($"Error: Role implementation for {identifiedRoleTypeLocal} disappeared after identification.");
                     // Fallback: Move to next instruction
                     var nextInstruction = GenerateNextNightInstruction(session);
                     session.PendingModeratorInstruction = nextInstruction;
                     return ProcessResult.Success(nextInstruction);
                }
            }
            else
            {
                // Identification input failed validation. Return failure.
                // Pending state remains set for retry with the same instruction.
                return identificationResult; // Propagate the specific failure
            }
        }

        // --- 2. Determine Current Role to Process ---
        // If index is invalid, means we came from identification OR we need to start the night.
        if (session.CurrentNightActingRoleIndex < 0 || session.CurrentNightActingRoleIndex >= actingRolesOrdered.Count)
        {
             // If index is invalid, but we weren't handling identification, something is wrong OR the night just ended.
             // Let GenerateNextNightInstruction handle finding the first/next step or transitioning.
             var nextInstruction = GenerateNextNightInstruction(session);
             session.PendingModeratorInstruction = nextInstruction; // Update pending instruction
             return ProcessResult.Success(nextInstruction);

        }

        var currentRoleType = actingRolesOrdered[session.CurrentNightActingRoleIndex];
        if (!_roleImplementations.TryGetValue(currentRoleType, out var currentRoleInstance))
        {
             // Skip role if implementation missing, move to next
             return ProcessResult.Success(GenerateNextNightInstruction(session));
        }

         // Find living players assigned this role
         var assignedActors = session.Players.Values
                                     .Where(p => p.Status == PlayerStatus.Alive && p.Role?.RoleType == currentRoleType)
                                     .ToList();


        // --- 3. Trigger Night 1 Identification if Needed ---
        // This check happens *before* processing action input for the current index.
        bool needsN1Identification = session.TurnNumber == 1 &&
                                    currentRoleInstance.RequiresNight1Identification() &&
                                    !assignedActors.Any(); // No one is assigned this role yet

        if (needsN1Identification)
        {
            session.PendingNight1IdentificationForRole = currentRoleType;
            var idInstruction = currentRoleInstance.GenerateIdentificationInstructions(session);
            session.PendingModeratorInstruction = idInstruction; // Update pending instruction
            return ProcessResult.Success(idInstruction);
        }


        // --- 4. Process Action Input for the Current Role ---
        // Check if the received input matches the expected action for the current role.
        // This requires comparing input.InputTypeProvided and potentially target players
        // against session.PendingModeratorInstruction.

        // Determine the actor (can be null for group actions like Werewolves)
        // TODO: Refine actor determination logic. Needs to handle group vs single actor roles.
        Player? actor = assignedActors.FirstOrDefault(); // Simple approach for now

        // We assume the input received IS for the role at CurrentNightActingRoleIndex
        // because GenerateNextNightInstruction already generated the prompt for it in the previous turn,
        // OR we just processed identification and generated the action prompt above.

        // Validate that the input *type* matches what the role expects for its night action
        var expectedActionInstruction = currentRoleInstance.GenerateNightInstructions(session); // Regenerate to check type/details
        if (expectedActionInstruction == null)
        {
             // Role has no action now (perhaps already acted, or conditional). Move to next role.
             return ProcessResult.Success(GenerateNextNightInstruction(session));
        }

        // Check if the input provided matches the expected instruction details
        // (e.g., InputType, potentially player count if applicable)
        bool inputMatchesExpectation = input.InputTypeProvided == expectedActionInstruction.ExpectedInputType;
         // TODO: Add more sophisticated validation if needed (e.g., check PlayerSelection counts match)

        if (inputMatchesExpectation)
        {
             // Process the action using the role's logic
            var actionResult = currentRoleInstance.ProcessNightAction(session, input); // Pass determined actor

            if (actionResult.IsSuccess)
            {
                // Action successful, log it (ProcessNightAction should have added details to NightActionsLog)

                // --- Action Succeeded: Move to the next role ---
                // GenerateNextNightInstruction handles incrementing the index and finding the next step.
                var nextInstruction = GenerateNextNightInstruction(session);
                 session.PendingModeratorInstruction = nextInstruction; // Update pending instruction
                return ProcessResult.Success(nextInstruction);
            }
            else
            {
                // Action failed (invalid input for the action). Return failure to allow retry.
                // The PendingModeratorInstruction should remain the same (the action prompt).
                return actionResult; // Propagate the failure
            }
        }
        else
        {
            // Input received does not match the expected action for the current role index.
            // This indicates a state mismatch or unexpected input.
            // Option 1: Return error.
            // Option 2: Assume the input is stale/wrong and reissue the expected instruction.
            // Let's go with Option 2 for resilience. Reissue the correct prompt.
             session.PendingModeratorInstruction = expectedActionInstruction; // Re-set the expected instruction
             return ProcessResult.Success(expectedActionInstruction);

            // --- Alternative: Return Error ---
            // return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
            //                                         GameErrorCode.InvalidInput_UnexpectedActionInput,
            //                                         $"Received input {input.InputTypeProvided} but expected input for {currentRoleType} action ({expectedActionInstruction.ExpectedInputType})."));
        }


        // Note: The old logic for "Generate Action Instruction" (if input doesn't match) is implicitly
        // handled above by reissuing the instruction if inputMatchesExpectation is false.
    }

    // Placeholder implementation for GetNightWakeUpOrder
    // TODO: Implement the actual role ordering logic based on game rules.
    private List<RoleType> GetNightWakeUpOrder(GameSession session)
    {
        // Phase 1: Simple fixed order
        var order = new List<RoleType>();
        if (session.RolesInPlay.Contains(RoleType.SimpleWerewolf)) // Example role
        {
             order.Add(RoleType.SimpleWerewolf);
        }
        // Add other roles here in their correct wake-up order
        // e.g., Seer, Bodyguard, etc.

        return order;
    }

    // --- Victory Conditions ---
    // Corrected signature using Team enum
    private (Team WinningTeam, string Description)? CheckVictoryConditions(GameSession session)
    {
        // Ensure players have roles (HandleSetupPhase assigns default)
        // ... (existing logic to check conditions) ...

        // Example Check: Villager win
        bool werewolvesEliminated = !session.Players.Values.Any(p => p.Status == PlayerStatus.Alive && p.Role?.RoleType == RoleType.SimpleWerewolf); // Check RoleType
        if (werewolvesEliminated && session.Players.Values.Any(p => p.Status == PlayerStatus.Alive && p.Role?.RoleType != RoleType.SimpleWerewolf))
        {
            return (Team.Villagers, GameStrings.VictoryConditionAllWerewolvesEliminated); // Use Team enum
        }

        // Example Check: Werewolf win
        int aliveWerewolves = session.Players.Values.Count(p => p.Status == PlayerStatus.Alive && p.Role?.RoleType == RoleType.SimpleWerewolf); // Check RoleType
        int aliveNonWerewolves = session.Players.Values.Count(p => p.Status == PlayerStatus.Alive && p.Role?.RoleType != RoleType.SimpleWerewolf); // Check RoleType
        if (aliveWerewolves >= aliveNonWerewolves && aliveWerewolves > 0)
        {
             return (Team.Werewolves, GameStrings.VictoryConditionWerewolvesOutnumber); // Use Team enum
        }

        // If no victory condition met
        return null;
    }


    private ProcessResult HandleDayResolveNightPhase(GameSession session, ModeratorInput input)
    {
        // Expecting Confirmation input to proceed with resolution announcement
        if (input.Confirmation != true)
        {
            // Re-issue the prompt if not confirmed
            return ProcessResult.Success(session.PendingModeratorInstruction);
        }

        // Phase 1: Simple WW kill resolution
        // Find the WW victim choice from the *current* night's log entries.
        var wwVictimAction = session.GameHistoryLog
            .OfType<NightActionLogEntry>() // Filter to night actions
            .Where(log => log.TurnNumber == session.TurnNumber && log.ActionType == NightActionType.WerewolfVictimSelection)
            .OrderByDescending(log => log.Timestamp) // Get the latest if multiple (shouldn't happen for WW in P1)
            .FirstOrDefault();

        List<Player> eliminatedPlayers = new List<Player>();

        if (wwVictimAction?.TargetId != null && session.Players.TryGetValue(wwVictimAction.TargetId.Value, out var victim))
        {
            if (victim.Status == PlayerStatus.Alive)
            {
                victim.Status = PlayerStatus.Dead;
                eliminatedPlayers.Add(victim);

                // Log the elimination
                session.GameHistoryLog.Add(new PlayerEliminatedLogEntry
                {
                    PlayerId = victim.Id,
                    Reason = EliminationReason.WerewolfAttack, // Define this enum
                    TurnNumber = session.TurnNumber,
                    Phase = session.GamePhase
                });
            }
        }

        // Prepare announcement
        string announcement;
        if (eliminatedPlayers.Any())
        {
            // Placeholder for GameStrings.PlayersEliminatedAnnouncement
            announcement = string.Format(GameStrings.PlayersEliminatedAnnouncement, string.Join(", ", eliminatedPlayers.Select(p => p.Name)));
        }
        else
        {
            // Placeholder for GameStrings.NoOneEliminatedAnnouncement
            announcement = GameStrings.NoOneEliminatedAnnouncement;
        }

        // Transition to Day_Event for role reveals/death triggers
        session.GamePhase = GamePhase.Day_Event;

        // Prepare next instruction (Role Reveal or Proceed)
        // Phase 1: Simple reveal prompt for the first eliminated player
        ModeratorInstruction nextInstruction;
        if (eliminatedPlayers.Any())
        {
            // TODO: Need a way to track *who* needs revealing if multiple players eliminated.
            // For Phase 1, assume only one elimination possible per night resolution.
            var playerToReveal = eliminatedPlayers.First();
            nextInstruction = new ModeratorInstruction
            {
                // Placeholder for GameStrings.RevealRolePrompt incorporated
                InstructionText = $"{announcement} {string.Format(GameStrings.RevealRolePrompt, playerToReveal.Name)}",
                ExpectedInputType = ExpectedInputType.RoleSelection, // Or Confirmation if unknown/refused
                AffectedPlayerIds = new List<Guid> { playerToReveal.Id },
                SelectableRoles = Enum.GetValues<RoleType>().Where(rt => rt > RoleType.Unknown).ToList() // Provide all roles except system ones
            };
        }
        else
        {
            // No one died, proceed to debate
            session.GamePhase = GamePhase.Day_Debate; // Skip Day_Event if no deaths
            nextInstruction = new ModeratorInstruction
            {
                // Placeholder for GameStrings.ProceedToDebatePrompt incorporated
                InstructionText = $"{announcement} {GameStrings.ProceedToDebatePrompt}",
                ExpectedInputType = ExpectedInputType.Confirmation
            };
        }

        session.PendingModeratorInstruction = nextInstruction;
        return ProcessResult.Success(nextInstruction);
    }

    private ProcessResult HandleDayEventPhase(GameSession session, ModeratorInput input)
    {
        // --- Phase 1: Handle Role Reveal Input --- 
        if (session.PendingModeratorInstruction?.ExpectedInputType == ExpectedInputType.RoleSelection)
        {
            // Validate input: Expecting a role to be selected
            if (input.SelectedRole == null || input.SelectedRole == RoleType.Unassigned)
            {
                // Placeholder for GameStrings.RoleNotSelectedError
                return ProcessResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_RequiredDataMissing, GameStrings.RoleNotSelectedError));
            }

            // Find the player whose role needs revealing (using AffectedPlayerIds from instruction)
            Guid? playerToRevealId = session.PendingModeratorInstruction?.AffectedPlayerIds?.FirstOrDefault();
            if (playerToRevealId == null || !session.Players.TryGetValue(playerToRevealId.Value, out var playerToReveal))
            {                 
                // Should not happen if instruction was generated correctly
                // Placeholder for GameStrings.RevealTargetNotFoundError
                return ProcessResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, GameStrings.RevealTargetNotFoundError));
            }

            // Instantiate the role based on input
            RoleType revealedRoleType = input.SelectedRole.Value;
            IRole? revealedRoleInstance = null;
            if (revealedRoleType != RoleType.Unknown)
            {                
                 if (!_roleImplementations.TryGetValue(revealedRoleType, out revealedRoleInstance))
                 {
                    // Handle case where selected role has no implementation (shouldn't happen with SelectableRoles)
                    // Placeholder for GameStrings.RoleImplementationNotFound
                    return ProcessResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_RoleNameNotFound, string.Format(GameStrings.RoleImplementationNotFound, revealedRoleType)));
                 }
            }
            // If RoleType.Unknown, instance remains null

            // Update player state
            playerToReveal.Role = revealedRoleInstance; 
            playerToReveal.IsRoleRevealed = true; // Mark as revealed

            // Log the reveal
            session.GameHistoryLog.Add(new RoleRevealedLogEntry
            {
                 PlayerId = playerToReveal.Id,
                 RevealedRole = revealedRoleType, 
                 TurnNumber = session.TurnNumber,
                 Phase = session.GamePhase
            });

            // Proceed to Debate
            session.GamePhase = GamePhase.Day_Debate;
            var nextInstruction = new ModeratorInstruction
            {
                // Placeholder for GameStrings.RoleRevealedProceedToDebate
                InstructionText = string.Format(GameStrings.RoleRevealedProceedToDebate, playerToReveal.Name, revealedRoleType),
                ExpectedInputType = ExpectedInputType.Confirmation
            };
            session.PendingModeratorInstruction = nextInstruction;
            return ProcessResult.Success(nextInstruction);
        }

        // --- Placeholder for other Day_Event logic (death triggers, event cards) ---
        
        // Fallback if unexpected input or state
        return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, string.Format(GameStrings.PhaseLogicNotImplemented, session.GamePhase)));
    }

    private ProcessResult HandleDayDebatePhase(GameSession session, ModeratorInput input)
    {
        // Expecting Confirmation input to proceed to vote
        if (input.Confirmation == true)
        {
            session.GamePhase = GamePhase.Day_Vote;
            var livingPlayers = session.Players.Values
                .Where(p => p.Status == PlayerStatus.Alive)
                // TODO: Add CanVote check later
                .Select(p => p.Id)
                .ToList();

            var nextInstruction = new ModeratorInstruction
            {
                // Placeholder for GameStrings.VotePhaseStartPrompt
                InstructionText = GameStrings.VotePhaseStartPrompt,
                ExpectedInputType = ExpectedInputType.PlayerSelectionSingle, // Moderator reports the *outcome*
                SelectablePlayerIds = livingPlayers // Provide context of who *could* be eliminated
                // Note: Allow empty selection for tie via validation in HandleDayVotePhase
            };
            session.PendingModeratorInstruction = nextInstruction;
            return ProcessResult.Success(nextInstruction);
        }
        else
        {
            // Re-issue the prompt
            return ProcessResult.Success(session.PendingModeratorInstruction);
        }
    }

    private ProcessResult HandleDayVotePhase(GameSession session, ModeratorInput input)
    {
        // Validate input: Expecting PlayerSelectionSingle
        // Allow 0 or 1 player ID.
        if (input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count > 1)
        {
             // Placeholder for GameStrings.VoteOutcomeInvalidSelection
             return ProcessResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, GameStrings.VoteOutcomeInvalidSelection));
        }

        Guid? eliminatedPlayerId = input.SelectedPlayerIds.FirstOrDefault(); // Null if list is empty

        // Validate Player ID if one was provided
        if (eliminatedPlayerId != null)
        {             
             if (!session.Players.TryGetValue(eliminatedPlayerId.Value, out var targetPlayer))
             {
                 return ProcessResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_PlayerIdNotFound, string.Format(GameStrings.PlayerIdNotFound, eliminatedPlayerId.Value)));
             }
             if (targetPlayer.Status == PlayerStatus.Dead)
             {
                 // Can't vote to eliminate someone already dead
                 return ProcessResult.Failure(new GameError(ErrorType.RuleViolation, GameErrorCode.RuleViolation_TargetIsDead, string.Format(GameStrings.TargetIsDeadError, targetPlayer.Name)));
             }
             // Store the non-empty Guid
             session.PendingVoteOutcome = eliminatedPlayerId.Value;
        }
        else
        {
            // Empty list means a tie was reported
            session.PendingVoteOutcome = Guid.Empty; 
        }

        // Log the reported outcome
        session.GameHistoryLog.Add(new VoteOutcomeReportedLogEntry
        {
            ReportedOutcomePlayerId = session.PendingVoteOutcome.Value, // Store the ID or Guid.Empty // Corrected Property Name
            TurnNumber = session.TurnNumber,
            Phase = session.GamePhase
        });

        // Transition to resolution phase
        session.GamePhase = GamePhase.Day_ResolveVote;
        var nextInstruction = new ModeratorInstruction
        {
             InstructionText = GameStrings.ResolveVotePrompt,
             ExpectedInputType = ExpectedInputType.Confirmation 
        };
        session.PendingModeratorInstruction = nextInstruction;
        return ProcessResult.Success(nextInstruction);
    }

     private ProcessResult HandleDayResolveVotePhase(GameSession session, ModeratorInput input)
    {
        // Expecting Confirmation input to proceed
        if (input.Confirmation != true)
        {
            // Re-issue the prompt
            return ProcessResult.Success(session.PendingModeratorInstruction);
        }

        // Check the stored vote outcome
        if (!session.PendingVoteOutcome.HasValue)
        {
             // Should not happen if HandleDayVotePhase ran correctly
             // Placeholder for GameStrings.VoteOutcomeMissingError
             return ProcessResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, GameStrings.VoteOutcomeMissingError));
        }

        Guid outcome = session.PendingVoteOutcome.Value;
        session.PendingVoteOutcome = null; // Clear the pending state
        ModeratorInstruction nextInstruction;

        // Log the resolution
        session.GameHistoryLog.Add(new VoteResolvedLogEntry
        {
            EliminatedPlayerId = (outcome == Guid.Empty) ? null : outcome,
            WasTie = (outcome == Guid.Empty),
            TurnNumber = session.TurnNumber,
            Phase = session.GamePhase
        });

        if (outcome == Guid.Empty) // Tie
        {
            // Transition to Night
            session.GamePhase = GamePhase.Night;
            session.TurnNumber++;
            session.CurrentNightActingRoleIndex = -1; // Reset for new night

            // Generate initial night prompt
            nextInstruction = new ModeratorInstruction
            {
                 // Placeholder for GameStrings.VoteResultTieProceedToNight
                 InstructionText = GameStrings.VoteResultTieProceedToNight,
                 ExpectedInputType = ExpectedInputType.Confirmation
            };
        }
        else // Player eliminated
        {
            Guid eliminatedPlayerId = outcome;
            if (session.Players.TryGetValue(eliminatedPlayerId, out var eliminatedPlayer))
            {
                 if(eliminatedPlayer.Status == PlayerStatus.Alive)
                 {
                    eliminatedPlayer.Status = PlayerStatus.Dead;
                    // Log elimination
                    session.GameHistoryLog.Add(new PlayerEliminatedLogEntry
                    {
                        PlayerId = eliminatedPlayerId,
                        Reason = EliminationReason.DayVote, // Ensure enum exists
                        TurnNumber = session.TurnNumber,
                        Phase = session.GamePhase
                    });
                 }
                 else {
                    // Player reported eliminated was already dead? Log/handle.
                    // For now, proceed as if elimination happened.
                 }

                // Transition back to Day_Event for reveal/triggers
                session.GamePhase = GamePhase.Day_Event; 
                nextInstruction = new ModeratorInstruction
                {
                    // Placeholder for GameStrings.PlayerEliminatedByVoteRevealRole
                    InstructionText = string.Format(GameStrings.PlayerEliminatedByVoteRevealRole, eliminatedPlayer.Name),
                    ExpectedInputType = ExpectedInputType.RoleSelection,
                    AffectedPlayerIds = new List<Guid> { eliminatedPlayerId },
                    SelectableRoles = Enum.GetValues<RoleType>().Where(rt => rt > RoleType.Unknown).ToList()
                };
            }
            else
            {
                // Eliminated player ID not found - internal error
                return ProcessResult.Failure(new GameError(ErrorType.Unknown, GameErrorCode.Unknown_InternalError, string.Format(GameStrings.PlayerIdNotFound, eliminatedPlayerId)));
            }
        }

        session.PendingModeratorInstruction = nextInstruction;
        return ProcessResult.Success(nextInstruction);
    }
}