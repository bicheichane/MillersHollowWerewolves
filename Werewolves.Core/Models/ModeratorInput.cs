using Werewolves.Core.Enums;

namespace Werewolves.Core.Models;

/// <summary>
/// Data structure for communication FROM the moderator.
/// Based on Roadmap Phase 0 and Architecture doc.
/// </summary>
public class ModeratorInput
{
    public ExpectedInputType InputTypeProvided { get; init; }

    // Optional fields, presence depends on InputTypeProvided
    public List<Guid>? SelectedPlayerIds { get; init; }
    public string? SelectedRoleName { get; init; }
    public string? SelectedOption { get; init; }
    public Dictionary<Guid, int>? VoteResults { get; init; }
    public Dictionary<Guid, int>? AccusationResults { get; init; } // For Nightmare Event
    public Dictionary<Guid, List<Guid>>? FriendVoteResults { get; init; } // For Great Distrust Event
    public bool? Confirmation { get; init; }
    public List<Guid>? VouchedPlayerIds { get; init; } // For Punishment Event
} 