using Werewolves.Core.Enums;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs the Defender's choice of which player to protect for the night.
/// </summary>
public record DefenderProtectionChoiceLogEntry : GameLogEntryBase
{
    public required Guid ActorId { get; init; }
    public required Guid TargetId { get; init; }
} 