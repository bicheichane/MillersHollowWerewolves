using Werewolves.StateModels.Core;

namespace Werewolves.StateModels.Log;

/// <summary>
/// Logs the specific choice made by the Werewolf group for their victim.
/// This is distinct from the NightActionLogEntry as it represents the final group decision.
/// </summary>
public record WerewolfVictimChoiceLogEntry : GameLogEntryBase
{
    // Consider adding ActorIds (List<Guid>) if tracking individual WW contributions is needed
    public required Guid VictimId { get; init; }

    /// <summary>
    /// Applies the werewolf victim choice to the game state.
    /// Note: This is primarily for logging purposes as the actual elimination
    /// is processed during dawn resolution phase.
    /// </summary>
    internal override void Apply(GameSession.IStateMutator mutator)
    {
        // Werewolf victim choice doesn't directly eliminate the player here
        // The actual elimination is processed during dawn resolution phase
        // This entry is primarily for historical tracking
    }
}
