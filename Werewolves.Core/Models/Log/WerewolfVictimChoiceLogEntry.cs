using System;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs the specific choice made by the Werewolf group for their victim.
/// This is distinct from the NightActionLogEntry as it represents the final group decision.
/// </summary>
public record WerewolfVictimChoiceLogEntry : GameLogEntryBase
{
    // Consider adding ActorIds (List<Guid>) if tracking individual WW contributions is needed
    public required Guid VictimId { get; init; }
} 