using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Log;


/// <summary>
/// Logs the initial assignment of a specific role to a player during setup (Night 1).
/// </summary>
public record AssignRoleLogEntry : GameLogEntryBase
{
	/// <summary>
	/// The ID of the player assigned the role.
	/// </summary>
	public required List<Guid> PlayerIds { get; init; }

	/// <summary>
	/// The specific role type assigned.
	/// </summary>
	public required RoleType AssignedRole { get; init; }

	// Potential future additions:
	// public RoleType? DiscardedRole { get; init; } // For Thief
	// public List<Guid>? AssociatedPlayerIds { get; init; } // For Cupid's Lovers
	// public Guid? ModelPlayerId { get; init; } // For Wild Child

    
	/// <summary>
	/// Applies the initial role assignment to the game state.
	/// Note: This doesn't set the actual role instance as that's handled by the GameLogic layer.
	/// The log entry records the assignment for historical purposes.
	/// </summary>
	internal override void Apply(GameSession.IStateMutator mutator)
	{
		// Initial role assignment doesn't directly set the role instance here
		// The GameLogic layer handles the actual role instantiation
		// This could potentially set role assignment flags if needed in future
	}
}
