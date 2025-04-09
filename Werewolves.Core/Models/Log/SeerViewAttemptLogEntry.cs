using Werewolves.Core.Enums;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs the Seer's action of attempting to view another player's role.
/// </summary>
public record SeerViewAttemptLogEntry : GameLogEntryBase
{
    public required Guid ActorId { get; init; }
    public required Guid TargetId { get; init; }
} 