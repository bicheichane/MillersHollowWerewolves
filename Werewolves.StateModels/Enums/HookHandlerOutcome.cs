namespace Werewolves.StateModels.Enums;

/// <summary>
/// Represents the outcome of a hook handler's state machine advancement.
/// </summary>
public enum HookHandlerOutcome
{
	/// <summary>
	/// The handler is active and requires further input from the moderator.
	/// The GameFlowManager should halt all processing and await the next moderator input.
	/// </summary>
	NeedInput,

	/// <summary>
	/// The handler has successfully looped through all registered listeners for this hook invocation.
	/// The GameFlowManager should determine where to navigate next.
	/// </summary>
	Complete,

	/// <summary>
	/// An error occurred during processing.
	/// The GameFlowManager should halt and report the error.
	/// </summary>
	Error
}