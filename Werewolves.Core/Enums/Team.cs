namespace Werewolves.Core.Enums;

/// <summary>
/// Identifies the fundamental winning factions/conditions.
/// Derived from Architecture Doc.
/// </summary>
public enum Team
{
    Villagers,
    Werewolves,
    Lovers, // Opposing team lovers win condition
    Solo_WhiteWerewolf,
    Solo_Piper,
    Solo_Angel, // Early win condition
    Solo_PrejudicedManipulator
} 