namespace Werewolves.StateModels.Models;

/// <summary>
/// Defines the constraints for player selection counts in moderator instructions.
/// This flexible structure can express all required selection scenarios.
/// </summary>
public readonly record struct SelectionCountConstraint(int Minimum, int Maximum)
{
    /// <summary>
    /// Creates a constraint for exact selection of N players.
    /// </summary>
    public static SelectionCountConstraint Exact(int count) => new(count, count);
    
    /// <summary>
    /// Creates a constraint for selecting between minimum and maximum players.
    /// </summary>
    public static SelectionCountConstraint Range(int minimum, int maximum) => new(minimum, maximum);
    
    /// <summary>
    /// Creates a constraint for optional selection (0 or 1 players).
    /// Commonly used for vote outcomes with ties or optional actions.
    /// </summary>
    public static SelectionCountConstraint Optional => new(0, 1);
    
    /// <summary>
    /// Creates a constraint for single player selection.
    /// </summary>
    public static SelectionCountConstraint Single => new(1, 1);

    public static void EnforceConstraint<T>(ICollection<T> value, SelectionCountConstraint countConstraint)
    {
        if (value.Count < countConstraint.Minimum)
        {
            throw new InvalidOperationException($"Selection constraint violation: Minimum of {countConstraint.Minimum} required, but only {value.Count} provided.");
        }

        if (value.Count > countConstraint.Maximum)
        {
            throw new InvalidOperationException($"Selection constraint violation: Maximum of {countConstraint.Maximum} allowed, but {value.Count} provided.");
        }
    }
}
