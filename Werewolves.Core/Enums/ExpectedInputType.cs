namespace Werewolves.Core.Enums;

/// <summary>
/// Specifies the type of input expected from the moderator.
/// Based on Architecture doc list.
/// </summary>
public enum ExpectedInputType
{
    None,
    PlayerSelectionSingle,
    PlayerSelectionMultiple,
    RoleSelection,
    OptionSelection,
    Confirmation
} 