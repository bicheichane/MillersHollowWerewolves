namespace Werewolves.StateModels.Enums;

/// <summary>
/// Defines the specific types of actions that can occur during the night phase.
/// Used primarily for logging and potential internal logic.
/// </summary>
public enum NightActionType
{
    Unknown = 0,                    // Default/unspecified
    WerewolfVictimSelection,        // Standard werewolf choice
    BigBadWolfVictimSelection,      // BBW additional choice
    WhiteWerewolfVictimSelection,   // WWWWW choice
    AccursedWolfFatherInfection,    // AWF infection choice
    SeerCheck,                      // Seer looking at a player
    FoxCheck,                       // Fox checking neighbors
    WitchSave,                      // Witch using healing potion
    WitchKill,                      // Witch using poison potion
    DefenderProtect,                // Defender choosing target
    PiperCharm,                     // Piper choosing targets
    RustySword,                     // Knight's Rusty Sword delayed action
    ThiefSwap,                      // Thief swapping roles
    ActorEmulate,                   // Actor emulating another role
    WildChildModel,                 // Wild Child choosing new role
    CupidLink,                      // Cupid linking lovers
    WolfHoundChoice,                // Wolf Hound team choice
					 // Add other night actions as roles are implemented (e.g., CupidLink, WildChildModel, ActorEmulate, etc.)
}

/// <summary>
/// Defines the types of role powers available in the game that can be used during the day phase.
/// These are non-deterministic events that players can choose to activate.
/// </summary>
public enum DayPowerType
{
	Unknown = 0,            // Default/unspecified
    JudgeReVote,            // Judge's ability to force a re-vote
    DevotedServantSwap,     // Devoted Servant's ability to swap roles with another player
    TownCrierCardReveal,    // Town Crier's ability to reveal an event card
}

/// <summary>
/// Specifies the types of status effects that can be applied to a player or entity.
/// These are deterministic in nature (can be rebuilt from logs),
/// and represent persistent conditions affecting gameplay.
/// </summary>
/// <remarks>Use this enumeration to identify and handle different status effects within the game logic. Each
/// value represents a distinct effect that may alter gameplay or player state.</remarks>
public enum StatusEffectTypes
{
    None = 0,
    ElderProtectionLost, // Elder's extra life has been used
    LycanthropyInfection, // Player has been infected by the wolf father
    WildChildChanged, // Wild Child has changed their role
}