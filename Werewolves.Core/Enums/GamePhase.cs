namespace Werewolves.Core.Enums;

/// <summary>
/// Represents the current stage of the game.
/// Initial values from Roadmap Phase 0.
/// Expanded based on Architecture doc Game Loop Outline.
/// </summary>
public enum GamePhase
{
    Setup,
    Night,
    Day_ResolveNight,
    Day_Event,
    Day_Debate,
    Day_Vote,
    Day_ResolveVote,
    AccusationVoting, // For Nightmare Event
    FriendVoting, // For Great Distrust Event
    GameOver
} 