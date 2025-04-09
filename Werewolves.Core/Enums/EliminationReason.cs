namespace Werewolves.Core.Enums;

/// <summary>
/// Defines the reason a player was eliminated from the game.
/// </summary>
public enum EliminationReason
{
    Unknown, // Default or error state
    WerewolfAttack, // Standard Werewolf kill
    DayVote, // Voted out during the day
    // Phase 2
    WitchPoison,
    // Phase 3+
    LoversHeartbreak,
    // Add other reasons later: HunterShot, KnightCurse, Scapegoat, Events, etc.
} 