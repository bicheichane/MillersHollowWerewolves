using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Roles; // Required for PlaceholderRole
using Werewolves.Core.Resources;

namespace Werewolves.Core
{
    public class GameService
    {
        // Using a static dictionary for simplicity. Replace with proper session management if needed.
        private static readonly Dictionary<Guid, GameSession> _sessions = new Dictionary<Guid, GameSession>();

        /// <summary>
        /// Creates a new game session and initializes players.
        /// </summary>
        /// <param name="playerNames">List of names for the players.</param>
        /// <param name="selectedRoleTypes">List of role types that are in play.</param>
        /// <param name="selectedEventCardIds">Optional list of event card IDs to include.</param>
        /// <returns>The unique ID of the newly created game session.</returns>
        /// <exception cref="ArgumentException">Thrown if inputs are invalid.</exception>
        public Guid StartNewGame(List<string> playerNames, List<RoleType> selectedRoleTypes, List<string>? selectedEventCardIds = null)
        {
            if (playerNames == null || !playerNames.Any() || playerNames.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException(GameStrings.Exception_PlayerNamesInvalid, nameof(playerNames));
            if (selectedRoleTypes == null || selectedRoleTypes.Count < playerNames.Count)
                throw new ArgumentException(GameStrings.Exception_NotEnoughRoles, nameof(selectedRoleTypes));

            var session = new GameSession();

            // --- Player Creation ---
            foreach (var name in playerNames)
            {
                var player = new Player(name);
                session.Players.Add(player.Id, player);
            }

            // --- Initial Game State ---
            session.TurnNumber = 1;
            session.GamePhase = GamePhase.Night;

            // --- Initial Moderator Instruction ---
            // First night: Thief (if present) > Cupid (if present) > Seer (if present)
            var firstNightRoles = new List<RoleType> { RoleType.Thief, RoleType.Cupid, RoleType.Seer };
            var rolesInPlay = selectedRoleTypes.Intersect(firstNightRoles).ToList();

            if (rolesInPlay.Any())
            {
                // First role to be revealed
                var firstRole = rolesInPlay[0];
                session.PendingModeratorInstruction = new ModeratorInstruction(
                    $"Night 1 begins. The {firstRole} is in play. Please select which player has this role.",
                    ExpectedInputType.PlayerSelection
                )
                {
                    SelectablePlayerIds = session.Players.Keys.ToList()
                };
            }
            else
            {
                // No special roles to reveal on Night 1
                session.PendingModeratorInstruction = new ModeratorInstruction(
                    "Night 1 begins. No special roles to reveal.",
                    ExpectedInputType.Confirmation
                );
            }

            // --- Event Deck Setup ---
            // TODO TDD: Implement event card loading and shuffling if selectedEventCardIds is provided.

            _sessions.Add(session.Id, session);
            return session.Id;
        }

        /// <summary>
        /// Retrieves the current game state for a given session ID.
        /// Used primarily for testing and potentially for UI layers.
        /// </summary>
        public GameSession GetGameSession(Guid gameId)
        {
            if (_sessions.TryGetValue(gameId, out var session))
            {
                return session;
            }
            throw new KeyNotFoundException(string.Format(GameStrings.Exception_GameSessionNotFound, gameId));
        }

        /// <summary>
        /// Processes input from the moderator, advances game state, and returns the next instruction.
        /// </summary>
        /// <param name="gameId">The ID of the game session.</param>
        /// <param name="input">The input provided by the moderator (type will vary).</param>
        /// <returns>The next instruction for the moderator.</returns>
        public ModeratorInstruction ProcessModeratorInput(Guid gameId, object input)
        {
            var session = GetGameSession(gameId);

            // TODO TDD: Implement the core game loop logic here.
            // This will involve:
            // 1. Validating the input against session.PendingModeratorInstruction.ExpectedInputType
            // 2. If input is a player selection for role revelation:
            //    - Update the player's KnownRole
            //    - Check if there are more Night 1 roles to reveal
            //    - If yes, prompt for next role
            //    - If no, proceed to next phase
            // 3. If input is a death reveal:
            //    - Update the player's KnownRole
            //    - Handle any death effects (Hunter, etc.)
            // 4. Update GameSession state (GamePhase, TurnNumber, etc.)
            // 5. Check for victory conditions
            // 6. Generate the next ModeratorInstruction

            // Placeholder response:
            session.PendingModeratorInstruction = new ModeratorInstruction(GameStrings.Debug_InputReceivedPlaceholder);
            return session.PendingModeratorInstruction;
        }

        /// <summary>
        /// Gets the currently pending instruction for the moderator.
        /// </summary>
        public ModeratorInstruction GetCurrentInstruction(Guid gameId)
        {
            return GetGameSession(gameId).PendingModeratorInstruction;
        }
    }
} 