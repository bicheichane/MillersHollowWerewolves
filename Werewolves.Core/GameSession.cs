using System;
using System.Collections.Generic;

namespace Werewolves.Core
{
    public class GameSession
    {
        public Guid Id { get; }
        public Dictionary<Guid, Player> Players { get; } = new Dictionary<Guid, Player>();
        public GamePhase GamePhase { get; internal set; } = GamePhase.Setup; // Modifiable by GameService
        public int TurnNumber { get; internal set; } = 0; // Modifiable by GameService
        public ModeratorInstruction PendingModeratorInstruction { get; internal set; } = ModeratorInstruction.None; // Modifiable by GameService

        // Add RolesInPlay, EventDeck, DiscardPile, ActiveEvents, State Flags (SheriffPlayerId, Lovers, etc.)
        // from the architecture document as needed by tests and features.

        public GameSession()
        {
            Id = Guid.NewGuid();
        }
    }
} 