using Werewolves.Core.Enums;

namespace Werewolves.Core.Models;

/// <summary>
/// Represents an instruction or prompt directed towards the game moderator.
/// This structure defines what the moderator needs to do or what input is expected next.
/// </summary>
public record ModeratorInstruction
{
    /// <summary>
    /// The primary text message or question displayed to the moderator.
    /// This should clearly state the current game situation or the required action.
    /// Example: "Werewolves, wake up and choose your victim."
    /// </summary>
    public required string InstructionText { get; init; }

    /// <summary>
    /// Specifies the type of input the application expects the moderator to provide
    /// in response to this instruction. This guides the UI on what input controls
    /// to present and helps the GameService validate the incoming ModeratorInput.
    /// </summary>
    public required ExpectedInputType ExpectedInputType { get; init; }

    /// <summary>
    /// Optional: Contains private information intended only for the moderator's eyes,
    /// not necessarily part of the main public instruction or prompt.
    /// Useful for revealing sensitive information like a Seer's check result.
    /// Example: "Target PlayerX is on the Werewolf team (Thumbs Up)."
    /// </summary>
    public string? PrivateModeratorInfo { get; init; }

    /// <summary>
    /// Optional: A list of player IDs that this instruction primarily relates to.
    /// This provides context, for example, indicating which player needs their role revealed
    /// or who is the target of a specific action being prompted.
    /// </summary>
    public List<Guid>? AffectedPlayerIds { get; init; }

    /// <summary>
    /// Populated if the ExpectedInputType involves selecting one or more players
    /// (e.g., PlayerSelectionSingle, PlayerSelectionMultiple). This list provides the
    /// valid player IDs from which the moderator can choose.
    /// </summary>
    public List<Guid>? SelectablePlayerIds { get; init; }

    /// <summary>
    /// Populated if the ExpectedInputType is RoleAssignment. This list provides the
    /// RoleType values that the moderator can assign to players via the
    /// ModeratorInput.AssignedPlayerRoles dictionary.
    /// </summary>
    public List<RoleType>? SelectableRoles { get; init; }

    /// <summary>
    /// Populated if the ExpectedInputType is OptionSelection. This list provides the
    /// specific string options the moderator can choose from (e.g., event card names,
    /// alignment choices).
    /// </summary>
    public List<string>? SelectableOptions { get; init; }
}