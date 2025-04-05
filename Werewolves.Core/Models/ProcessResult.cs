namespace Werewolves.Core.Models;

/// <summary>
/// Standard return type for operations that can succeed (yielding the next step)
/// or fail (providing error details).
/// Based on Roadmap Phase 0 and Architecture doc.
/// </summary>
public class ProcessResult
{
    public bool IsSuccess { get; } // Use private set and factory methods
    public ModeratorInstruction? ModeratorInstruction { get; } // Valid if IsSuccess is true
    public GameError? Error { get; } // Valid if IsSuccess is false

    private ProcessResult(bool isSuccess, ModeratorInstruction? instruction, GameError? error)
    {
        IsSuccess = isSuccess;
        ModeratorInstruction = instruction;
        Error = error;
    }

    /// <summary>
    /// Creates a success result with the next moderator instruction.
    /// </summary>
    public static ProcessResult Success(ModeratorInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        return new ProcessResult(true, instruction, null);
    }

    /// <summary>
    /// Creates a failure result with error details.
    /// </summary>
    public static ProcessResult Failure(GameError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ProcessResult(false, null, error);
    }
} 