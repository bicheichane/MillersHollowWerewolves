using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Extensions;
using Werewolves.StateModels.Resources;

namespace Werewolves.StateModels.Models.Instructions;

/// <summary>
/// Instruction that requires a simple yes/no confirmation from the moderator.
/// </summary>
public record ConfirmationInstruction : ModeratorInstruction
{
    /// <summary>
    /// Initializes a new instance of ConfirmationInstruction.
    /// </summary>
    /// <param name="publicAnnouncement">The text to be read aloud to players.</param>
    /// <param name="privateInstruction">Private guidance for the moderator.</param>
    /// <param name="affectedPlayerIds">Optional list of affected player IDs for context.</param>
    public ConfirmationInstruction(
        string? publicAnnouncement = null,
        string? privateInstruction = null,
        IReadOnlyList<Guid>? affectedPlayerIds = null)
        : base(publicAnnouncement, privateInstruction, affectedPlayerIds)
    {
    }

    /// <summary>
    /// Creates a ModeratorResponse with the provided confirmation value.
    /// Performs contractual validation to ensure the response is properly formed.
    /// </summary>
    /// <param name="confirmation">The moderator's confirmation response.</param>
    /// <returns>A validated ModeratorResponse.</returns>
    public virtual ModeratorResponse CreateResponse(bool confirmation)
    {
        return new ModeratorResponse
        {
            Type = ExpectedInputType.Confirmation,
            Confirmation = confirmation
        };
    }
}

public record StartGameConfirmationInstruction(Guid GameGuid) : ConfirmationInstruction(GameStrings.GameStartPrompt)
{
    public Guid GameGuid { get; } = GameGuid;
}

public record FinishedGameConfirmationInstruction(string VictoryDescription) : ConfirmationInstruction(GameStrings.GameOverMessage.Format(VictoryDescription))
{
}
