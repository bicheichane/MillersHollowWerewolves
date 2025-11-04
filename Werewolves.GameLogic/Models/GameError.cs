using System.Diagnostics.CodeAnalysis;
using Werewolves.StateModels.Enums;

namespace Werewolves.GameLogic.Models;

/// <summary>
/// Provides structured information about a specific error that occurred during game logic processing.
/// Based on Roadmap Phase 0 and Architecture doc.
/// Relies on object initializers for required properties.
/// </summary>
public class GameError
{
    /// <summary>
    /// High-level category of the error.
    /// </summary>
    public required ErrorType Type { get; init; }

    /// <summary>
    /// Specific code identifying the error using the unified enum.
    /// </summary>
    public required GameErrorCode Code { get; init; }

    /// <summary>
    /// Human-readable description of the error for the moderator.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional dictionary containing context-specific data about the error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    // Constructor used for simplified creation internally
    [SetsRequiredMembers]
    internal GameError(ErrorType type, GameErrorCode code, string message, IReadOnlyDictionary<string, object>? context = null)
    {
        Type = type;
        Code = code;
        Message = message;
        Context = context;
    }
} 