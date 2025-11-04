using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Models;

public interface IPlayerState
{
	public bool IsRoleRevealed => Role != null;
	public RoleType? Role { get; }
	public PlayerHealth Health { get; }
	public bool IsSheriff { get; }
	public bool IsInLove { get; }
	public bool IsInfected { get; }
}

/// <summary>
/// Wrapper record holding all dynamic state information for a Player.
/// Properties use public set where modification is controlled by GameService.
/// </summary>
internal class PlayerState : IPlayerState
{
	public bool IsRoleRevealed => Role != null;
	public RoleType? Role { get; set; } = null;
	public PlayerHealth Health { get; set; } = PlayerHealth.Alive;
	public bool IsSheriff { get; set; } = false;
    public bool IsInLove { get; set; } = false;
    public bool IsInfected { get; set; } = false;

    // Other properties will be added in later phases as defined in Architecture doc
    // e.g.:
    // public Guid? LoverId { get; public set; } = null;
    // public int VoteMultiplier { get; public set; } = 1;
    // ... and many more
} 