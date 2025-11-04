namespace Werewolves.StateModels.Enums;

/// <summary>
/// Represents the type of hook listener.
/// Used to distinguish between different categories of listeners that can respond to game hooks.
/// </summary>
public enum GameHookListenerType
{
    /// <summary>
    /// A role-based listener (e.g., Werewolf, Seer, Hunter).
    /// </summary>
    Role,
    
    /// <summary>
    /// An event card-based listener (e.g., Retribution, Backfire).
    /// </summary>
    Event
}
