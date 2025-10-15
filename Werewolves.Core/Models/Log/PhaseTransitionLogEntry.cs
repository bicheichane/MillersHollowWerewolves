using System;
using Werewolves.Core.Enums;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs a transition between game phases.
/// </summary>
public record PhaseTransitionLogEntry : GameLogEntryBase
{
    public required GamePhase PreviousPhase { get; init; }
    public required PhaseTransitionReason Reason { get; init; }
} 