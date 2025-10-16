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
            PublicText = GameStrings.SetupCompletePrompt,
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

		// --- Input Validation Against Last Instruction ---
		var validationResult = ValidateExpectedInput(session, input);
		if (validationResult != null)
		{
			return validationResult; // Return failure if validation fails
		}

		return _gameFlowManager.HandleInput(this, session, input);
	}

	// --- Phase-Specific Handlers (Refactored Signatures) ---
	#region Phase Specific Handlers
	// Note: These methods are now static as they are referenced directly in GameFlowManager
	// They receive the 'this' instance (GameService) as the third parameter.


	#endregion
	// --- Helper Methods ---
	#region Helpers
	/// <summary>
	/// Generates the next instruction during the Night phase.
	/// Handles role ordering, identification, action prompts, and phase transition.
	/// Updates session state (CurrentNightActingRoleIndex, PendingNight1IdentificationForRole, GamePhase).
	/// </summary>


	

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
	/// Handles phases expecting a confirmation. If not confirmed, re-issues the current instruction.
	/// Otherwise, does nothing and allows the caller to proceed.
	/// </summary>
	/// <param name="session">The game session.</param>
	/// <param name="input">The moderator input.</param>
	/// <param name="handlerResult">The handlerResult to return if input.Confirmation is true.</param>
	/// <returns> In case of non-confirmation, a PhaseHandlerResult with the previous instruction.
	/// Otherwise, a null phase handler result that should be ignored</returns>
	protected internal bool ShouldReissueCommand(GameSession session, ModeratorInput input, out PhaseHandlerResult? handlerResult)
	{
		handlerResult = null;
		if (input.Confirmation != true)
		{
            // Re-issue the current instruction
            if (session.PendingModeratorInstruction == null)
            {
	            throw new Exception("cannot re-issue null instruction");
            }
            else
            {
				handlerResult = PhaseHandlerResult.SuccessStayInPhase(session.PendingModeratorInstruction);
			}

            return true;
        }

        return false;
    }

	#endregion
}