using Werewolves.Core.Enums;
//using Werewolves.Core.Models.Enums; // Removed incorrect using

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Logs the Witch's attempt to use a specific potion on a target.
/// </summary>
public record WitchPotionUseAttemptLogEntry : GameLogEntryBase
{
    public required Guid ActorId { get; init; }
    public required WitchPotionType PotionType { get; init; }
    public required Guid TargetId { get; init; } // Target of the potion (could be WW victim for heal, or someone else for poison)
} 