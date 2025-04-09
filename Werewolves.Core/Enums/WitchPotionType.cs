namespace Werewolves.Core.Enums;

/// <summary>
/// Represents the types of potions the Witch possesses.
/// Using Flags allows tracking multiple used potions (though typically one of each).
/// </summary>
[Flags]
public enum WitchPotionType
{
    None = 0,
    Healing = 1 << 0, // 1
    Poison = 1 << 1,  // 2
    // Potential future: All = Healing | Poison
} 