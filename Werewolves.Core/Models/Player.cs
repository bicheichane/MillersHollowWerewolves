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
    public bool IsRoleRevealed => Role != null;
    public PlayerState State { get; } = new PlayerState();

    // Role property added in Phase 1
    public IRole? Role { get; internal set; } = null;

    /// <summary>
    /// Assigns a role to the player and marks it as revealed to the application.
    /// </summary>
    /// <param name="roleToAssign">The role instance to assign.</param>
    internal void AssignRole(IRole roleToAssign)
    {
        if (Role != null)
        {
            // Potentially log a warning or throw if trying to re-assign?
            // For now, allow overwrite but could be refined.
        }
        Role = roleToAssign;
        // IsRoleRevealed is computed based on Role != null
    }
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

    // Data-Carrying States
    public Guid? LoverId { get; internal set; } = null;
    public WitchPotionType PotionsUsed { get; internal set; } = WitchPotionType.None;

    // Other properties will be added in later phases as defined in Architecture doc
    // e.g.:
    // public int VoteMultiplier { get; internal set; } = 1;
    // ... and many more
} 