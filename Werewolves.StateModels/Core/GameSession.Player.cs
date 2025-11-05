using System.Numerics;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Extensions;
using Werewolves.StateModels.Interfaces;

namespace Werewolves.StateModels.Core;



internal partial class GameSession
{
	/// <summary>
	/// Represents a participant in the game and the tracked information about them.
	/// </summary>
	protected class Player : IPlayer
	{
		/// <summary>
		/// Represents a participant in the game and the tracked information about them.
		/// </summary>
		public Player(string name)
		{
			Name = name;
		}

		public Guid Id { get; } = Guid.NewGuid();
		public string Name { get; init; }
		public PlayerState State { get; set; } = new();
		IPlayerState IPlayer.State => State;

	}

	/// <summary>
	/// Wrapper record holding all dynamic state information for a Player.
	/// Properties use internal set, they are meant to only be managed through GameSession,
	/// as they represent a purely derived state cache of the game.
	/// </summary>
	protected class PlayerState : IPlayerState
	{
		public bool IsMainRoleRevealed => MainRole != null;
		public MainRoleType? MainRole { get; internal set; } = null;
		/// <summary>
		/// these can be stacked on top of main role types AND represent additional abilities that are linked to specific GameHooks.
		/// by contrast, the infected one or the sheriff can be given to any main role type, but do not have specific game hooks associated with them, so are not added here
		/// </summary>
		public SecondaryRoleType? SecondaryRoles { get; internal set; } = null;
		public PlayerHealth Health { get; internal set; } = PlayerHealth.Alive;

		public bool IsInfected { get; internal set; } = false;
		public bool IsSheriff { get; internal set; } = false;


		// Other properties will be added in later phases as defined in Architecture doc
		// e.g.:
		// public Guid? LoverId { get; public set; } = null;
		// public int VoteMultiplier { get; public set; } = 1;
		// ... and many more
	}

	
}