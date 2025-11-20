using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Represents a generic power used by a player/role that's not constrained to be used at night.
/// </summary>
public record DayActionLogEntry : GameLogEntryBase
{
	public List<Guid>? TargetIds { get; init; } // ID of the player targeted, if applicable

	/// Example: "WerewolfVictimSelection", "SeerCheck", "WitchSave", "WitchKill"
	public required DayPowerType ActionType { get; init; }

	/// <summary>
	/// Applies the night action to the game state.
	/// Note: This is primarily for logging purposes as individual night actions
	/// don't directly change game state - their effects are processed during dawn resolution.
	/// </summary>
	internal override void Apply(ISessionMutator mutator)
	{
		//no state change, just logging
	}
}