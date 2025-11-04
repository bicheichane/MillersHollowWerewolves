namespace Werewolves.GameLogic.Models;

/// <summary>
/// Defines the constraints for player selection counts in moderator instructions.
/// This flexible structure can express all required selection scenarios.
/// </summary>
public readonly record struct SelectionConstraint(int Minimum, int Maximum)
{
    /// <summary>
    /// Creates a constraint for exact selection of N players.
    /// </summary>
    public static SelectionConstraint Exact(int count) => new(count, count);
    
    /// <summary>
    /// Creates a constraint for selecting between minimum and maximum players.
    /// </summary>
    public static SelectionConstraint Range(int minimum, int maximum) => new(minimum, maximum);
    
    /// <summary>
    /// Creates a constraint for optional selection (0 or 1 players).
    /// Commonly used for vote outcomes with ties or optional actions.
    /// </summary>
    public static SelectionConstraint Optional => new(0, 1);
    
    /// <summary>
    /// Creates a constraint for single player selection.
    /// </summary>
    public static SelectionConstraint Single => new(1, 1);
}
