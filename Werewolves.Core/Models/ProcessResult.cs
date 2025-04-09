using Werewolves.Core.Models.Log; // Log namespace might contain ModeratorInstruction

namespace Werewolves.Core.Models;

/// <summary>
/// Represents the final outcome of processing moderator input,
/// containing either the next instruction or an error.
/// </summary>
/// <param name="IsSuccess">Indicates if the overall processing was successful.</param>
/// <param name="ModeratorInstruction">The instruction for the moderator if successful.</param>
/// <param name="Error">Error details if IsSuccess is false.</param>
public record ProcessResult(
    bool IsSuccess,
    ModeratorInstruction? ModeratorInstruction,
    GameError? Error = null
)
{
    // Static factory methods for convenience
    public static ProcessResult Success(ModeratorInstruction instruction) =>
        new(true, instruction, null);

    public static ProcessResult Failure(GameError error) =>
        new(false, null, error);
} 