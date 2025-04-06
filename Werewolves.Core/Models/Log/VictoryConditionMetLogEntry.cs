using Werewolves.Core.Enums;
using Werewolves.Core.Resources;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs when a victory condition is met.
/// </summary>
public record VictoryConditionMetLogEntry : GameLogEntryBase
{
    public required Team WinningTeam { get; init; }
    public string ConditionDescription { get; init; } = GameStrings.DefaultLogValue;
} 