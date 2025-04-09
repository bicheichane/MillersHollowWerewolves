using Werewolves.Core.Enums;
using System;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs the initial assignment of a specific role to a player during setup (Night 1).
/// </summary>
public record InitialRoleAssignmentLogEntry : GameLogEntryBase
{
	/// <summary>
	/// The ID of the player assigned the role.
	/// </summary>
	public required Guid PlayerId { get; init; }

	/// <summary>
	/// The specific role type assigned.
	/// </summary>
	public required RoleType AssignedRole { get; init; }

	// Potential future additions:
	// public RoleType? DiscardedRole { get; init; } // For Thief
	// public List<Guid>? AssociatedPlayerIds { get; init; } // For Cupid's Lovers
	// public Guid? ModelPlayerId { get; init; } // For Wild Child
}
