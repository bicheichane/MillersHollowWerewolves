using Werewolves.Core.Enums;
using Werewolves.Core.Models.Log;
using System; // Required for Tuple
using System.Collections.Generic;
using Werewolves.Core.Extensions; // Required for Dictionary, List, HashSet

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

    // --- Phase 1 Additions ---
    /// <summary>
    /// Stores the reported outcome of the current day vote phase temporarily.
    /// Null if no vote outcome reported yet, Guid.Empty for a reported tie, PlayerId otherwise.
    /// </summary>
    public Guid? PendingVoteOutcome { get; set; } = null; // Using null initially, Guid.Empty for tie

    /// <summary>
    /// Stores the RoleType currently awaiting identification during the *first night*.
    /// Null if no Night 1 identification is pending.
    /// </summary>
    public RoleType? PendingNight1IdentificationForRole { get; set; } = null;

    /// <summary>
    /// Tracks the index of the role currently acting within the night wake-up order.
    /// Reset at the beginning of each Night phase.
    /// </summary>
    public int CurrentNightActingRoleIndex { get; set; } = -1;

    // --- End Phase 1 Additions ---

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
    // public Dictionary<Guid, int>? VoteResultsCache { get; set; } // Replaced by PendingVoteOutcome for Phase 1

    public int GetRoleCount(RoleType roleType) => RolesInPlay.Where(x => x == roleType).Count();
    public int GetAliveRoleCount(RoleType roleType)
    {
        var totalRoleCount = GetRoleCount(roleType);

        // App should always know the role of dead players.
        var killedRoleCount = Players.Values.WhereRole(roleType).WhereStatus(PlayerStatus.Dead).Count;

        return totalRoleCount - killedRoleCount;
    }

    

}