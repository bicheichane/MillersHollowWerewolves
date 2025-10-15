namespace Werewolves.Core.Models;

/// <summary>
/// Wrapper class holding all dynamic state information for a Player.
/// Properties use internal set where modification is controlled by GameService.
/// </summary>
public class PlayerState
{
    // Initial boolean properties from Roadmap Phase 0
    public bool IsSheriff { get; internal set; } = false;
    public bool IsInLove { get; internal set; } = false;
    public bool IsInfected { get; internal set; } = false;

    // Other properties will be added in later phases as defined in Architecture doc
    // e.g.:
    // public Guid? LoverId { get; internal set; } = null;
    // public int VoteMultiplier { get; internal set; } = 1;
    // ... and many more
} 