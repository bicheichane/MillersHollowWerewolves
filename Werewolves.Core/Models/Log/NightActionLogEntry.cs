using Werewolves.Core.Enums;
using System;
using Werewolves.Core.Resources;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Represents a generic night action taken by a player/role.
/// Used temporarily within GameSession.NightActionsLog.
/// </summary>
public record NightActionLogEntry : GameLogEntryBase
{
    public required Guid ActorId { get; init; } // ID of the player performing the action
    public Guid? TargetId { get; init; } // ID of the player targeted, if applicable
    /// Example: "WerewolfVictimSelection", "SeerCheck", "WitchSave", "WitchKill"
    public required NightActionType ActionType { get; init; } = NightActionType.Unknown; // Use enum

    // Add other relevant fields as needed, e.g., chosen role, potion type
} 