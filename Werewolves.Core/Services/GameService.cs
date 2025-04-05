using Werewolves.Core.Enums;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using System.Collections.Concurrent; // For thread-safe storage
using System.Linq; // Needed for Any()
using System;
using System.Collections.Generic;

namespace Werewolves.Core.Services;

/// <summary>
/// Orchestrates the game flow based on moderator input and tracked state.
/// </summary>
public class GameService
{
    // Simple in-memory storage for game sessions. Replaceable with DI.
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();

    // Placeholder for resource strings (errors, instructions)
    // In a real app, use IStringLocalizer<GameService> injected via DI
    private static class Texts
    {
        public static string GameNotFound => "Game session not found.";
        public static string InputTypeMismatch => "The provided input type does not match the expected input type.";
        public static string UnexpectedInput => "Received input when no instruction was pending.";
        public static string SetupCompletePrompt => "Setup complete. Proceed to Night 1?";
        public static string NightStartsPrompt => "Night 1 begins. Call the first role."; // Placeholder
        public static string ActionNotInPhase(GameErrorCode code, GamePhase phase) => $"Action ({code}) not valid in phase {phase}.";
        public static string InternalError => "An unexpected internal error occurred.";
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
            throw new ArgumentException("Player list cannot be empty.", nameof(playerNamesInOrder));
        }
        if (!rolesInPlay.Any())
        {
            throw new ArgumentException("Role list cannot be empty.", nameof(rolesInPlay));
        }

        var players = new Dictionary<Guid, Player>();
        var seatingOrder = new List<Guid>();
        var playerInfos = new List<PlayerInfo>();

        foreach (var name in playerNamesInOrder)
        {
            var player = new Player { Name = name };
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
            InstructionText = Texts.SetupCompletePrompt,
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
                                                    Texts.GameNotFound));
        }

        // Basic validation (more sophisticated validation needed later)
        if (session.PendingModeratorInstruction == null)
        {
            // Unexpected input when none is pending
            return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation,
                                                    GameErrorCode.InvalidOperation_UnexpectedInput,
                                                    Texts.UnexpectedInput));
        }

        if (input.InputTypeProvided != session.PendingModeratorInstruction.ExpectedInputType)
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                                                    GameErrorCode.InvalidInput_InputTypeMismatch,
                                                    Texts.InputTypeMismatch));
        }

        // --- Phase 0 Logic --- 
        // Handle the initial confirmation after setup
        if (session.GamePhase == GamePhase.Setup && input.InputTypeProvided == ExpectedInputType.Confirmation)
        {
            if (input.Confirmation == true)
            {
                session.GamePhase = GamePhase.Night;
                session.TurnNumber = 1; // Start Night 1
                // Generate placeholder instruction for Night 1 start
                var nextInstruction = new ModeratorInstruction
                {
                    InstructionText = Texts.NightStartsPrompt,
                    ExpectedInputType = ExpectedInputType.None // Placeholder - Night logic will set this properly
                };
                session.PendingModeratorInstruction = nextInstruction;
                return ProcessResult.Success(nextInstruction);
            }
            else
            {
                // Confirmation was No? What should happen? Stay in setup? Error?
                // For now, just stay in setup with the same instruction.
                // Ensure PendingModeratorInstruction is not null before returning
                if (session.PendingModeratorInstruction != null)
                {
                    return ProcessResult.Success(session.PendingModeratorInstruction);
                }
                else
                {
                    // This case should ideally not happen if validation passed
                    // but handle defensively
                    return ProcessResult.Failure(new GameError(ErrorType.Unknown, 
                                                            GameErrorCode.Unknown_InternalError,
                                                            Texts.InternalError));
                }
            }
        }

        // --- End Phase 0 Logic --- 

        // Placeholder for unhandled input in this phase
        return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation,
                                                GameErrorCode.InvalidOperation_ActionNotInCorrectPhase,
                                                Texts.ActionNotInPhase(GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, session.GamePhase)));
    }

    // Internal helper methods for validation, state changes, etc. will be added later
} 