using Werewolves.Core.Enums;

namespace Werewolves.Core.Models;

/// <summary>
/// Data structure for communication TO the moderator.
/// Based on Roadmap Phase 0 and Architecture doc.
/// </summary>
public class ModeratorInstruction
{
    public required string InstructionText { get; init; }
    public ExpectedInputType ExpectedInputType { get; init; } = ExpectedInputType.None;

    // Optional fields based on Architecture doc, useful for UI/clients
    public List<Guid>? AffectedPlayerIds { get; init; } = null;
    public List<Guid>? SelectablePlayerIds { get; init; } = null;
    public List<string>? SelectableRoleNames { get; init; } = null;
    public List<string>? SelectableOptions { get; init; } = null;
    public bool RequiresConfirmation { get => ExpectedInputType == ExpectedInputType.Confirmation; }
} 