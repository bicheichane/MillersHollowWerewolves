namespace Werewolves.GameLogic.Models.StateMachine;

internal enum SetupSubPhaseStage
{
    ConfirmSetup
}

internal enum NightSubPhaseStage
{
    NightStart
}

internal enum DawnSubPhaseStage
{
    CheckForVictims,
    AnnounceVictims,
    DawnRoleReveals
}

internal enum DaySubPhaseStage
{
    Debate,
    StartNormalVote,
    ProcessVote,
    VoteRoleRevealRequest,
    VoteRoleRevealResponse
}
