namespace Werewolves.StateModels.Enums;

/// <summary>
/// Represents the outcome of a listener's state machine advancement.
/// This communicates the result back to the GameFlowManager dispatcher.
/// </summary>
public enum HookListenerOutcome
{
    /// <summary>
    /// The listener is active and requires further input from the moderator.
    /// The GameFlowManager should halt all processing and await the next moderator input.
    /// </summary>
    NeedInput,
    
    /// <summary>
    /// The listener has successfully completed all its actions for this hook invocation.
    /// The GameFlowManager should proceed to the next listener in the hook sequence.
    /// </summary>
    Complete
}