using System;
using Werewolves.Core.Enums;
using Werewolves.Core.Models.Log; // Assuming base class is here

namespace Werewolves.Core.Models.LogEntries;

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
} 