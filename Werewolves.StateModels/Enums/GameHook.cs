namespace Werewolves.StateModels.Enums;

/// <summary>
/// Represents specific moments in the game flow where listeners can be triggered.
/// Updated per Revision 2.1: Night hooks simplification.
/// </summary>
public enum GameHook
{
    // Core phase hooks
    NightSequenceStart,        // Fired when night actions begin (unified loop includes first-night identification)
    NightActionLoop,          // Main hook for iterating through night role actions
    
    // Player lifecycle hooks
    OnPlayerEliminationFinalized, // Fired when a player elimination is confirmed
    OnRoleRevealed,          // Fired when a player's role is revealed
    
    // Day phase hooks
    DayVoteStarted,           // Fired when voting phase begins
    NightResolutionStarted,    // Fired when night resolution begins
}
