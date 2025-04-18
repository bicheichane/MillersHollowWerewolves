namespace Werewolves.Core.Enums;

/// <summary>
/// Represents the specific character roles in the game.
/// Minimal initial set from Roadmap Phase 0.
/// (Will be expanded significantly in later phases based on Architecture doc)
/// </summary>
public enum RoleType
{
    Unassigned, // Player role not yet determined/revealed
    SimpleVillager,
    SimpleWerewolf,

    // Phase 2
    Seer,
    Defender,
    Witch,

    // Phase 3+
    Cupid
    // Add all other roles from Architecture doc RoleType list here in subsequent phases
} 