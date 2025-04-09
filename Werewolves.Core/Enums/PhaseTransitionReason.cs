namespace Werewolves.Core.Enums;

/// <summary>
/// Represents the reason for a state machine transition between game phases.
/// </summary>
public enum PhaseTransitionReason
{
    SetupConfirmed,
    NightStartsConfirmed,
    IdentifiedAndProceedToAction, // General ID success
    RoleActionComplete, // General Role action success
    AllNightActionsComplete, // All roles finished, move to resolution
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