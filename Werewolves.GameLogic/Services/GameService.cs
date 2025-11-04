// Added for IRole
// Added for specific roles
// For thread-safe storage
// Needed for Any()
// Add this line for resource access
// For Debug.Fail
using System.Collections.Concurrent;
using Werewolves.GameLogic.Models;
using Werewolves.GameLogic.Models.Instructions;
using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;
using static Werewolves.StateModels.Enums.ExpectedInputType;

namespace Werewolves.GameLogic.Services;

/// <summary>
/// Orchestrates the game flow based on moderator input and tracked state using a state machine.
/// </summary>
public class GameService
{
	// Simple in-memory storage for game sessions. Replaceable with DI.
	private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();

	private readonly GameFlowManager _gameFlowManager;

	public GameService()
    {
        _gameFlowManager = new GameFlowManager();
    }

    /// <summary>
    /// Starts a new game session.
    /// </summary>
    /// <param name="playerNamesInOrder">List of player names in clockwise seating order.</param>
    /// <param name="rolesInPlay">List of RoleTypes included in the game.</param>
    /// <param name="eventCardIdsInDeck">Optional list of event card IDs included.</param>
    /// <returns>The unique ID for the newly created game session.</returns>
    public StartGameConfirmationInstruction StartNewGame(List<string> playerNamesInOrder, List<RoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)
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

        foreach (var name in playerNamesInOrder)
        {
            var player = new Player { Name = name };
            players.Add(player.Id, player);
            seatingOrder.Add(player.Id);
        }

        var session = new GameSession(
            players: players,
            playerSeatingOrder: seatingOrder,
            rolesInPlay: new List<RoleType>(rolesInPlay) // Copy list
            // EventDeck setup would happen here if events were implemented
        );

        _sessions.TryAdd(session.Id, session);

        return new(session.Id);
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
    public ProcessResult RespondToInstruction(Guid gameId, ModeratorResponse input)
	{
		if (!_sessions.TryGetValue(gameId, out var session))
		{
			return ProcessResult.Failure(new GameError(ErrorType.GameNotFound,
													GameErrorCode.GameNotFound_SessionNotFound,
													GameStrings.GameNotFound));
		}

		// --- Input Validation Against Last Instruction ---
		var validationResult = ValidateExpectedInput(session, input);
		if (validationResult?.IsSuccess == false)
		{
			return validationResult; // Return failure if validation fails
		}

		return _gameFlowManager.HandleInput(this, session, input);
	}

	// --- Helper Methods ---
	#region Helpers
	/// <summary>
	/// Generates the next instruction during the Night phase.
	/// Handles role ordering, identification, action prompts, and phase transition.
	/// Updates session state (CurrentNightActingRoleIndex, PendingNight1IdentificationForRole, GetCurrentPhase).
	/// </summary>


	
    /// <summary>
    /// Validates moderator input against the expected instruction type.
    /// In the new architecture, each instruction type handles its own validation via CreateResponse methods.
    /// </summary>
    /// <returns>A ProcessResult.Failure if validation fails, otherwise null.</returns>
    private static ProcessResult? ValidateExpectedInput(GameSession session, ModeratorResponse input)
    {
	    var lastRequest = session.PendingModeratorInstruction;

		if (lastRequest == null)
        {
            // Should only happen if StartNewGame didn't set the initial instruction
            // TODO: Use GameString
            return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation,
                                                    GameErrorCode.InvalidOperation_UnexpectedInput,
                                                    "Internal error: No pending instruction available."));
        }

        // In the new architecture, validation is handled by each instruction's CreateResponse method
        // The instruction type itself determines what kind of response it expects
        // So we just need to ensure the response type matches the instruction type
        
        // For now, we'll do basic type checking - the detailed validation happens in CreateResponse
        if (DoesResponseTypeMatchInstruction(lastRequest, input) == false)
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                                                    GameErrorCode.InvalidInput_InputTypeMismatch,
                                                    "Confirmation instruction requires a boolean confirmation."));
        }

        return null; // Basic validation passed
    }

    private static bool DoesResponseTypeMatchInstruction(ModeratorInstruction instruction, ModeratorResponse response)
    {
        return instruction switch
        {
            ConfirmationInstruction => response.Type == Confirmation,
            SelectPlayersInstruction => response.Type == PlayerSelection,
            AssignRolesInstruction => response.Type == AssignPlayerRoles,
            SelectOptionsInstruction => response.Type == OptionSelection,
            _ => false,
        };
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
	protected internal bool ShouldReissueCommand(GameSession session, ModeratorResponse input, out PhaseHandlerResult? handlerResult)
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
