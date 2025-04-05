using Werewolves.Core.Enums;
using Werewolves.Core.Models.Log;
// using Werewolves.Core.Models.Events; // Still requires EventCard and ActiveEventState
using System.Collections.Generic; // Required for Dictionary

namespace Werewolves.Core.Models;

/// <summary>
/// Represents the tracked state of a single ongoing game.
/// </summary>
public class GameSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public required Dictionary<Guid, Player> Players { get; init; }
    public GamePhase GamePhase { get; set; }
    public int TurnNumber { get; set; } = 0;

    // Core collections defined in Phase 0 roadmap
    public List<Guid> PlayerSeatingOrder { get; set; } = new();
    public List<RoleType> RolesInPlay { get; set; } = new();
    public List<GameLogEntryBase> GameHistoryLog { get; } = new();

    // Placeholders for future phases based on Architecture Doc
    // Event types will be defined later
    // public List<EventCard> EventDeck { get; set; } = new();
    // public List<EventCard> DiscardPile { get; set; } = new();
    // public List<ActiveEventState> ActiveEvents { get; set; } = new();

    // Nullable state flags from Phase 0 roadmap / Architecture Doc
    public Guid? SheriffPlayerId { get; set; } = null;
    public Tuple<Guid, Guid>? Lovers { get; set; } = null;

    // Pending instruction for the moderator
    public ModeratorInstruction? PendingModeratorInstruction { get; set; }

    // Other state flags from Architecture doc will be added later
    // e.g.:
    // public HashSet<Guid> InfectedPlayerIds { get; } = new();
    // public Guid? ProtectedPlayerId { get; set; }
    // public Guid? LastProtectedPlayerId { get; set; }
    // public HashSet<Guid> CharmedPlayerIds { get; } = new();
    // public Dictionary<Guid, int>? VoteResultsCache { get; set; }
} 