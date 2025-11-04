namespace Werewolves.StateModels.Enums;

/// *****************************************************************
/// ********************* CRITICAL INFO *****************************
/// 
/// Each sub-phase must be idempotent, meaning that re-entering the same sub-phase
/// multiple times should not cause unintended side effects.
/// 
/// For example, do not have a sub-phase that starts by modifying some game state
/// and afterwards firing a hook to notify players. If the sub-phase is re-entered,
/// the game state would be modified again, leading to inconsistent behavior.
/// 
/// *****************************************************************

/// <summary>
/// Internal sub-phases for the consolidated Night phase.
/// Updated per Revision 2.1: Night hooks simplification.
/// </summary>
public enum NightSubPhases
{
    Start,            // Village goes to sleep, increment turn number
    ActionLoop        // Main sequence: iterate through night roles and fire hooks (includes first-night identification)
}

/// <summary>
/// Internal sub-phases for the consolidated Day_Dawn phase.
/// Updated per Revision 2: Game Phase Simplification.
/// </summary>
public enum DawnSubPhases
{
    CalculateVictims,   // Process night actions to determine final victims
    AnnounceVictims,    // Moderator announces all victims from the night
    ProcessRoleReveals, // Reveal roles for each eliminated player
    Finalize           // Complete dawn processing and transition to debate
}

/// <summary>
/// Internal sub-phases for the Day_Dusk phase (vote resolution).
/// Kept for potential future expansion, though currently simpler.
/// </summary>
public enum DayDuskSubPhases
{
    ResolveVote,        // Process the vote outcome and handle eliminations
    TransitionToNext    // Determine next phase (night or dawn for role reveals)
}
