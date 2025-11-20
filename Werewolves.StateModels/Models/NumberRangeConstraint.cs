namespace Werewolves.StateModels.Models;

/// <summary>
/// Defines the constraints for number range constraints,
/// such as player selection counts in moderator instructions.
/// This flexible structure can express all required scenarios.
/// </summary>
public readonly record struct NumberRangeConstraint(int Minimum, int Maximum)
{
    /// <summary>
    /// Creates a number constraint for an exact count.
    /// </summary>
    public static NumberRangeConstraint Exact(int count) => new(count, count);
    
    /// <summary>
    /// Creates a number interval constraint.
    /// </summary>
    public static NumberRangeConstraint Range(int minimum, int maximum) => new(minimum, maximum);
    
    /// <summary>
    /// Creates a number constraint for either 0 or 1.
    /// Commonly used for vote outcomes with ties or optional actions.
    /// </summary>
    public static NumberRangeConstraint Optional => Range(0, 1);

	/// <summary>
	/// Creates a constraint for an exact count of 1.
	/// </summary>
	public static NumberRangeConstraint Single => Exact(1);
    public static NumberRangeConstraint Any => Range(0, int.MaxValue);

	public static void EnforceConstraint<T>(ICollection<T> value, NumberRangeConstraint countConstraint)
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

    public void Enforce<T>(ICollection<T> value)
    {
        EnforceConstraint(value, this);
    }
}
