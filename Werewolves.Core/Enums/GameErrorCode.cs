namespace Werewolves.Core.Enums;

/// <summary>
/// Unified enum containing all specific error codes for game operations.
/// Prefixes indicate the high-level category (matching ErrorType).
/// </summary>
public enum GameErrorCode
{
    // --- GameNotFound ---
    GameNotFound_SessionNotFound,

    // --- InvalidInput ---
    InvalidInput_InputTypeMismatch,
    InvalidInput_RequiredDataMissing,
    InvalidInput_PlayerIdNotFound,
    InvalidInput_RoleNameNotFound,
    InvalidInput_OptionNotAvailable,
    InvalidInput_InvalidPlayerSelectionCount,

    // --- RuleViolation ---
    RuleViolation_TargetIsDead,
    RuleViolation_TargetIsInvalid, // Role-specific invalid targets
    RuleViolation_TargetIsSelf, // Targeted self when not allowed
    RuleViolation_TargetIsAlly, // e.g., Werewolf targeting Werewolf
    RuleViolation_DefenderRepeatTarget,
    RuleViolation_WitchPotionAlreadyUsed,
    RuleViolation_AccursedInfectionAlreadyUsed, // For AWF role
    RuleViolation_PowerLostOrUnavailable, // e.g., Fox, lost Elder powers, Judge used power
    RuleViolation_LoverVotingAgainstLover,
    RuleViolation_VoterIsInvalid, // e.g., Village Idiot, Muted player
    RuleViolation_EventRuleConflict, // Action conflicts with active event rules
    // Add more rule violations as needed...

    // --- InvalidOperation ---
    InvalidOperation_GameIsOver,
    InvalidOperation_ActionNotInCorrectPhase,
    InvalidOperation_UnexpectedInput, // Input received when none expected

    // --- Unknown (Should not happen) ---
    Unknown_InternalError
} 