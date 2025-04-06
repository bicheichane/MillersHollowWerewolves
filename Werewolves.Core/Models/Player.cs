using Werewolves.Core.Enums;
using Werewolves.Core.Interfaces;

namespace Werewolves.Core.Models;

/// <summary>
/// Represents a participant in the game and the tracked information about them.
/// </summary>
public class Player
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; set; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Alive;
    public bool IsRoleRevealed { get; set; } = false;
    public PlayerState State { get; } = new PlayerState();

    // Role property added in Phase 1
    public IRole? Role { get; internal set; } = null;
}

/// <summary>
/// Wrapper class holding all dynamic state information for a Player.
/// Properties use internal set where modification is controlled by GameService.
/// </summary>
public class PlayerState
{
    // Initial boolean properties from Roadmap Phase 0
    public bool IsSheriff { get; internal set; } = false;
    public bool IsInLove { get; internal set; } = false;

    // Other properties will be added in later phases as defined in Architecture doc
    // e.g.:
    // public Guid? LoverId { get; internal set; } = null;
    // public int VoteMultiplier { get; internal set; } = 1;
    // ... and many more
} 