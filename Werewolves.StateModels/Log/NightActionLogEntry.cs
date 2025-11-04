using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Represents a generic night action taken by a player/role.
/// </summary>
public record NightActionLogEntry : GameLogEntryBase
{
    public List<Guid>? TargetIds { get; init; } // ID of the player targeted, if applicable

    /// Example: "WerewolfVictimSelection", "SeerCheck", "WitchSave", "WitchKill"
    public required NightActionType ActionType { get; init; } = NightActionType.Unknown; // Use enum

	// Add other relevant fields as needed, e.g., chosen role, potion type

	/// <summary>
	/// metadata or outcome of the action, if its result is absolute when the action is taken.
	/// i.e. the result of a Seer check.
	/// Not applicable to actions whose result is only realized at a later time, like a Werewolf attack.
	/// </summary>
	public object? ImmediateActionOutcome { get; init; }

    public T? ReadActionOutcome<T>() => (T?)ImmediateActionOutcome;

    /// <summary>
    /// Applies the night action to the game state.
    /// Note: This is primarily for logging purposes as individual night actions
    /// don't directly change game state - their effects are processed during dawn resolution.
    /// </summary>
    internal override void Apply(GameSession.IStateMutator mutator)
    {
        switch (ActionType)
        {
            default:
                break;
        }
    }
}
