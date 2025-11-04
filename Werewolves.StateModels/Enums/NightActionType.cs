namespace Werewolves.StateModels.Enums;

/// <summary>
/// Defines the specific types of actions that can occur during the night phase.
/// Used primarily for logging and potential internal logic.
/// </summary>
public enum NightActionType
{
    Unknown = 0, // Default/unspecified
    WerewolfVictimSelection, // Standard werewolf choice
    BigBadWolfVictimSelection, // BBW additional choice
    WhiteWerewolfVictimSelection, // WWWWW choice
    AccursedWolfFatherInfection, // AWF infection choice
    SeerCheck, // Seer looking at a player
    FoxCheck, // Fox checking neighbors
    WitchSave, // Witch using healing potion
    WitchKill, // Witch using poison potion
    DefenderProtect, // Defender choosing target
    PiperCharm, // Piper choosing targets
    // Add other night actions as roles are implemented (e.g., CupidLink, WildChildModel, ActorEmulate, etc.)
} 