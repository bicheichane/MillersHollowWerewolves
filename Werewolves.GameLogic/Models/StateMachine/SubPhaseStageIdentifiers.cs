namespace Werewolves.GameLogic.Models.StateMachine;

internal enum SetupSubPhaseStage
{
    ConfirmSetup
}

internal enum NightSubPhaseStage
{
    NightStart,
    NightEnd
}

internal enum DawnSubPhaseStage
{
    CheckForVictims,
    AnnounceVictimsAndRequestRoles,
    AssignVictimRoles
}

internal enum DaySubPhaseStage
{
    Debate,
    RequestVote,
    HandleVoteResponse,
    VerifyLynchingOcurred,
    VoteOutcomeNavigation
}
