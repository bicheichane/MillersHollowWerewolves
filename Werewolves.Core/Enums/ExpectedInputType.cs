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
    VoteCounts,
    AccusationCounts,
    FriendVotes,
    Confirmation,
    VoucherSelection, // Likely maps to PlayerSelectionMultiple with specific context
    SuccessorSelection // Likely maps to PlayerSelectionSingle with specific context
} 