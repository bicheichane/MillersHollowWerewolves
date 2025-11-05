using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Interfaces;

public interface IPlayerState
{
	public bool IsMainRoleRevealed => MainRole != null;
	public MainRoleType? MainRole { get; }
	/// <summary>
	/// these can be stacked on top of main role types AND represent additional abilities that are linked to specific GameHooks.
	/// by contrast, the cursed one or the sheriff can be given to any main role type, but do not have specific game hooks associated with them, so are not added here
	/// </summary>
	public SecondaryRoleType? SecondaryRoles { get; }
	public PlayerHealth Health { get; }
	public bool IsInfected { get; }
	public bool IsSheriff { get; }

}