// Added for IRole
// Added for specific roles
// For thread-safe storage
// Needed for Any()
// Add this line for resource access
// For Debug.Fail
using System.Collections.Concurrent;
using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels.Models.Instructions;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;
using static Werewolves.StateModels.Enums.ExpectedInputType;

namespace Werewolves.GameLogic.Services;

/// <summary>
/// Orchestrates the game flow based on moderator input and tracked state using a state machine.
/// </summary>
public class GameService
{
	// Simple in-memory storage for game sessions. Replaceable with DI.
	private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();

	public GameService()
    {
    }

    public StartGameConfirmationInstruction StartNewGame(
        List<string> playerNamesInOrder, 
        List<MainRoleType> rolesInPlay, 
        List<string>? eventCardIdsInDeck = null) => StartNewGameCore(
            playerNamesInOrder, 
            rolesInPlay, 
            eventCardIdsInDeck, 
            stateChangeObserver: null);

    // Overload to accept state change observer for test suite diagnostics
    internal StartGameConfirmationInstruction StartNewGameWithObserver(
        List<string> playerNamesInOrder, 
        List<MainRoleType> rolesInPlay, 
        List<string>? eventCardIdsInDeck = null,
        IStateChangeObserver? stateChangeObserver = null) => StartNewGameCore(
            playerNamesInOrder, 
            rolesInPlay, 
            eventCardIdsInDeck, 
            stateChangeObserver);            

    /// <summary>
    /// Starts a new game session.
    /// </summary>
    /// <param name="playerNamesInOrder">List of player names in clockwise seating order.</param>
    /// <param name="rolesInPlay">List of RoleTypes included in the game.</param>
    /// <param name="eventCardIdsInDeck">Optional list of event card IDs included.</param>
    /// <param name="stateChangeObserver">Optional observer for state change diagnostics.</param>
    /// <returns>The unique ID for the newly created game session.</returns>
    private StartGameConfirmationInstruction StartNewGameCore(
        List<string> playerNamesInOrder, 
        List<MainRoleType> rolesInPlay, 
        List<string>? eventCardIdsInDeck = null,
        IStateChangeObserver? stateChangeObserver = null)
    {
        // 1. Generate the game ID
        var gameId = Guid.NewGuid();
        
        // 2. Get the initial instruction from GameFlowManager (pure function)
        var initialInstruction = GameFlowManager.GetInitialInstruction(rolesInPlay, gameId);
        
        // 3. Create the session with both the ID and instruction
        var session = new GameSession(gameId, initialInstruction, playerNamesInOrder, rolesInPlay, eventCardIdsInDeck, stateChangeObserver);
        
        // 4. Store the session
        _sessions.TryAdd(session.Id, session);
        
        // 5. Return the same instruction that was passed to the session
        return initialInstruction;
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
    public IGameSession? GetGameStateView(Guid gameId)
    {
        _sessions.TryGetValue(gameId, out var session);
        return session; // Or throw GameNotFoundException, or return a dedicated DTO
    }

    /// <summary>
    /// Processes input provided by the moderator using the state machine.
    /// </summary>
    public ProcessResult ProcessInstruction(Guid gameId, ModeratorResponse input)
	{
		if (!_sessions.TryGetValue(gameId, out var session))
		{
			return ProcessResult.Failure(new ConfirmationInstruction(privateInstruction: "ERROR: Game not found"));
		}

		// --- Input Validation Against Last Instruction ---
		EnsureInputTypeIsExpected(session, input);
		
		var result = GameFlowManager.HandleInput(session, input);

		if (result.ModeratorInstruction is FinishedGameConfirmationInstruction)
		{
			_sessions.Remove(gameId, out _);
		}

		return result;
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
    private static void EnsureInputTypeIsExpected(GameSession session, ModeratorResponse input)
    {
	    var lastRequest = session.PendingModeratorInstruction;

		if (lastRequest == null)
        {
            // Should only happen if StartNewGame didn't set the initial instruction
            // TODO: Use GameString
            throw new InvalidOperationException("Internal error: No pending instruction available.");
        }

        // In the new architecture, validation is handled by each instruction's CreateResponse method
        // The instruction type itself determines what kind of response it expects
        // So we just need to ensure the response type matches the instruction type
        
        // For now, we'll do basic type checking - the detailed validation happens in CreateResponse
        if (DoesResponseTypeMatchInstruction(lastRequest, input) == false)
        {
            throw new InvalidOperationException("Confirmation instruction requires a boolean confirmation.");
        }
    }

    private static bool DoesResponseTypeMatchInstruction(ModeratorInstruction instruction, ModeratorResponse response)
    {
        return instruction switch
        {
            StartGameConfirmationInstruction => response.Type == Confirmation,
            FinishedGameConfirmationInstruction => response.Type == Confirmation,
            ConfirmationInstruction => response.Type == Confirmation,
            SelectPlayersInstruction => response.Type == PlayerSelection,
            AssignRolesInstruction => response.Type == AssignPlayerRoles,
            SelectOptionsInstruction => response.Type == OptionSelection,
            _ => false,
        };
    }

	#endregion
}
