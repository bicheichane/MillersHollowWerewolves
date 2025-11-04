using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Interfaces;

public interface IPlayerState
{
	public bool IsRoleRevealed => Role != null;
	public RoleType? Role { get; }
	public PlayerHealth Health { get; }
	public bool IsSheriff { get; }
	public bool IsInLove { get; }
	public bool IsInfected { get; }
}