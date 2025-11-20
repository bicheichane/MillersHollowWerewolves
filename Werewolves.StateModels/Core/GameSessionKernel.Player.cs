using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Core;

public interface IPlayer : IEquatable<IPlayer>
{
	public Guid Id { get; }
	public string Name { get; init; }
	public IPlayerState State { get; }
	bool IEquatable<IPlayer>.Equals(IPlayer? other)
	{
		return other is not null && Id == other.Id;
	}
}

public interface IPlayerState
{
	public MainRoleType? MainRole { get; }
	/// <summary>
	/// these can be stacked on top of main role types AND represent additional abilities that are linked to specific GameHooks.
	/// by contrast, the cursed one or the sheriff can be given to any main role type, but do not have specific game hooks associated with them, so are not added here
	/// </summary>
	public SecondaryRoleType SecondaryRoles { get; }
	public PlayerHealth Health { get; }
	public bool IsInfected { get; }
	public bool IsSheriff { get; }
	public bool HasElderExtraLife { get; }

	public Team Team
	{
		get
		{
			switch (MainRole)
			{
				case MainRoleType.SimpleWerewolf:
					return Team.Werewolves;
				default:
					return Team.Villagers;
			}
		}
	}
}

internal partial class GameSessionKernel
{
	/// <summary>
	/// Represents a participant in the game and the tracked information about them.
	/// </summary>
	private class Player : IPlayer
	{
		public Player(string name)
		{
			Name = name;
		}

		public Guid Id { get; } = Guid.NewGuid();
		public string Name { get; init; }
		internal PlayerState State { get; } = new();
		IPlayerState IPlayer.State => State;
	}

	/// <summary>
	/// Wrapper record holding all dynamic state information for a Player.
	/// Properties use internal set, they are meant to only be managed through GameSession,
	/// as they represent a purely derived state cache of the game.
	/// </summary>
	private class PlayerState : IPlayerState
	{
		public MainRoleType? MainRole { get; internal set; } = null;

		/// <summary>
		/// these can be stacked on top of main role types AND represent additional abilities that are linked to specific GameHooks.
		/// by contrast, the infected one or the sheriff can be given to any main role type, but do not have specific game hooks associated with them, so are not added here
		/// </summary>
		public SecondaryRoleType SecondaryRoles { get; internal set; } = SecondaryRoleType.None;

		public PlayerHealth Health { get; internal set; } = PlayerHealth.Alive;

		public bool IsInfected { get; internal set; } = false;
		public bool IsSheriff { get; internal set; } = false;
		public bool HasElderExtraLife { get; internal set; } = false;
	}
}