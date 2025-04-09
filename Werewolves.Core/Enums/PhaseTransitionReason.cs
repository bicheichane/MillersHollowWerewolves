namespace Werewolves.Core.Enums;

/// <summary>
/// Represents the reason for a state machine transition between game phases.
/// </summary>
public enum PhaseTransitionReason
{
    SetupConfirmed,
    NightStartsConfirmed,
    IdentifiedAndProceedToWwAction, // May need renaming if WW action isn't always next
    WwActionComplete,
    NightResolutionConfirmedProceedToReveal,
    NightResolutionConfirmedNoVictims,
    RoleRevealedProceedToDebate,
    RoleRevealedProceedToNight,
    DebateConfirmedProceedToVote,
    VoteOutcomeReported,
    VoteResolvedProceedToReveal,
    VoteResolvedTieProceedToNight,
    VictoryConditionMet, // Although handled implicitly in GameService, might be useful to log
    RepeatInstruction // If needed as a distinct reason (from GameService constants)
} 