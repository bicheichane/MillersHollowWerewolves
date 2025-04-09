namespace Werewolves.Core.Enums;

/// <summary>
/// Defines the reason a player was eliminated from the game.
/// </summary>
public enum EliminationReason
{
    Unknown, // Default or error state
    WerewolfAttack, // Standard Werewolf kill
    DayVote, // Voted out during the day
    // Add other reasons later: WitchPoison, HunterShot, LoversHeartbreak, KnightCurse, Scapegoat, Events, etc.
} 