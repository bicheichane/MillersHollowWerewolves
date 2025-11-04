using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Logs when a player's role is revealed.
/// </summary>
public record RoleRevealedLogEntry : GameLogEntryBase
{
    public required Guid PlayerId { get; init; }
    public required RoleType RevealedRole { get; init; }

    /// <summary>
    /// Applies the role reveal to the game state.
    /// Note: This doesn't set the actual role instance as that's handled by the GameLogic layer.
    /// The role reveal is primarily for logging and UI purposes.
    /// </summary>
    internal override void Apply(GameSession.IStateMutator mutator)
    {
        // Role reveal doesn't change core game state, it's primarily for logging
        // The actual role assignment is handled by InitialRoleLogAssignment
        // This could potentially set a "IsRoleRevealed" flag if needed in the future
    }
}
