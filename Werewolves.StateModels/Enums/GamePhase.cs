namespace Werewolves.StateModels.Enums;

/// <summary>
/// Represents the current stage of the game.
/// Updated per Revision 2: Game Phase Simplification.
/// Consolidated phases for improved architectural clarity.
/// </summary>
public enum GamePhase
{
    Setup,           // Initial game setup and role assignment
    Night,           // Consolidated night phase with internal sub-phases
    Day_Dawn,        // Consolidated dawn phase for night resolution and role reveals
    Day_Debate,      // Day discussion phase
    Day_Vote,        // Day voting phase
    Day_Dusk,        // Vote resolution phase (renamed from Day_ResolveVote)
    AccusationVoting, // For Nightmare Event
    FriendVoting,    // For Great Distrust Event
    GameOver
}
