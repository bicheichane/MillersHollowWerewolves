using Werewolves.Core.Enums;

namespace Werewolves.Core.Models;

/// <summary>
/// Data structure for communication TO the moderator.
/// Based on Roadmap Phase 0 and Architecture doc.
/// </summary>
public class ModeratorInstruction
{
    // The core message/question for the moderator
    public string InstructionText { get; init; }

    // Specifies the kind of input expected, and implies which Selectable* list might be populated
    public ExpectedInputType ExpectedInputType { get; init; }

    // Optional: Player(s) this instruction primarily relates to (for context)
    public List<Guid>? AffectedPlayerIds { get; init; }

    // --- Options presented to the moderator (only one list relevant based on ExpectedInputType) ---

    // Populated if ExpectedInputType involves selecting players (e.g., PlayerSelectionSingle, PlayerSelectionMultiple)
    public List<Guid>? SelectablePlayerIds { get; init; }

    // Populated if ExpectedInputType is RoleSelection
    public List<RoleType>? SelectableRoles { get; init; }

    // Populated if ExpectedInputType is OptionSelection
    public List<string>? SelectableOptions { get; init; }
}