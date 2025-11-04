using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models;

// Assuming base class is here

namespace Werewolves.StateModels.LogEntries;

/// <summary>
/// Logs the action taken by the Seer during the night phase.
/// </summary>
/// <param name="Timestamp">When the event occured.</param>
/// <param name="TurnNumber">The turn number when the event happened.</param>
/// <param name="Phase">The game phase when the event happened.</param>
/// <param name="SeerPlayerId">The ID of the player who performed the Seer check.</param>
/// <param name="TargetPlayerId">The ID of the player who was targeted by the Seer.</param>
/// <param name="WasTargetAffiliatedWithWerewolves">Indicates the result provided to the moderator (True if the target wakes with werewolves, False otherwise).</param>
public record SeerViewAttemptLogEntry : GameLogEntryBase
{
    /// <summary>
    /// The ID of the player who performed the Seer check.
    /// </summary>
    public Guid SeerPlayerId { get; set; }

    /// <summary>
    /// The ID of the player who was targeted by the Seer.
    /// </summary>
    public Guid TargetPlayerId { get; set; }

    /// <summary>
    /// Indicates the result provided to the moderator (True if the target wakes with werewolves, False otherwise).
    /// </summary>
    public bool WasTargetAffiliatedWithWerewolves { get; set; }

    /// <summary>
    /// Applies the Seer view attempt to the game state.
    /// Note: This is primarily for logging purposes as the Seer action doesn't change game state.
    /// </summary>
    internal override void Apply(GameSession.IStateMutator mutator)
    {
        // Seer view attempt doesn't change core game state, it's primarily for logging
        // The result is provided to moderator but doesn't affect player states
    }
}
