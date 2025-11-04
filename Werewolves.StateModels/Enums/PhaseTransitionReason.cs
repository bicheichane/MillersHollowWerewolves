namespace Werewolves.StateModels.Enums;

/// <summary>
/// Represents the reason for a state machine transition between game phases.
/// Updated per Revision 2: Game Phase Simplification.
/// </summary>
public enum PhaseTransitionReason
{
    // Setup transitions
    SetupConfirmed,
    
    // Night phase transitions (internal)
    NightStarted,
    NightActionLoopComplete,
    
    // Dawn phase transitions
    DawnVictimsCalculated,
    DawnVictimsAnnounced,
    DawnRoleRevealsComplete,
    DawnFinalized,
    DawnNoVictimsProceedToDebate,
    DawnVictimsProceedToDebate,
    
    // Day phase transitions
    DebateConfirmedProceedToVote,
    VoteOutcomeReported,
    VoteResolvedProceedToReveal,
    VoteResolvedTieProceedToNight,
    
    // Victory and special cases
    VictoryConditionMet,
    RepeatInstruction
}
