using Werewolves.GameLogic.Models;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.InternalMessages;

/// <summary>
/// Represents the result of a listener's state machine advancement.
/// This communicates a precise, unambiguous outcome to the GFM dispatcher.
/// </summary>
internal class HookListenerActionResult
{
    /// <summary>
    /// The outcome type (NeedInput, Complete, Error).
    /// </summary>
    public HookListenerOutcome Outcome { get; }
    
    /// <summary>
    /// Optional instruction for the moderator when Outcome is NeedInput or Complete.
    /// </summary>
    public ModeratorInstruction? Instruction { get; }
    
    /// <summary>
    /// Error details when Outcome is Error.
    /// </summary>
    public GameError? ErrorMessage { get; }

    protected HookListenerActionResult(HookListenerOutcome outcome, ModeratorInstruction? instruction = null, GameError? error = null)
    {
        Outcome = outcome;
        Instruction = instruction;
        ErrorMessage = error;
    }

    /// <summary>
    /// Creates a NeedInput result with the provided instruction.
    /// </summary>
    /// <param name="instruction">The instruction to show to the moderator.</param>
    /// <returns>A HookListenerActionResult indicating input is needed.</returns>
    public static HookListenerActionResult NeedInput(ModeratorInstruction instruction)
    {
        return new HookListenerActionResult(HookListenerOutcome.NeedInput, instruction: instruction);
    }

    /// <summary>
    /// Creates a Complete result with an optional instruction.
    /// </summary>
    /// <param name="instruction">Optional instruction to show to the moderator.</param>
    /// <returns>A HookListenerActionResult indicating completion.</returns>
    public static HookListenerActionResult Complete()
    {
        return new HookListenerActionResult(HookListenerOutcome.Complete);
    }

    /// <summary>
    /// Creates an Error result with the provided error details.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    /// <returns>A HookListenerActionResult indicating an error occurred.</returns>
    public static HookListenerActionResult Error(GameError error)
    {
        return new HookListenerActionResult(HookListenerOutcome.Error, error: error);
    }
}

internal class HookListenerActionResult<T> : HookListenerActionResult where T : struct, Enum
{
	public T? NextListenerPhase { get; }

	private HookListenerActionResult(HookListenerOutcome outcome, ModeratorInstruction? instruction = null, T? nextListenerPhase = null, GameError? error = null) : base(outcome, instruction, error)
	{
		NextListenerPhase = nextListenerPhase;
	}

	public HookListenerActionResult(HookListenerActionResult baseResult, T nextListenerPhase) : 
		base(baseResult.Outcome, baseResult.Instruction, baseResult.ErrorMessage)
	{
		NextListenerPhase = nextListenerPhase;
	}

	/// <summary>
	/// Creates a NeedInput result with the provided instruction.
	/// </summary>
	/// <param name="instruction">The instruction to show to the moderator.</param>
	/// <returns>A HookListenerActionResult indicating input is needed.</returns>
	public static HookListenerActionResult<T> NeedInput(ModeratorInstruction instruction, T nextListenerPhase)
	{
		return new HookListenerActionResult<T>(HookListenerOutcome.NeedInput, instruction: instruction, nextListenerPhase: nextListenerPhase);
	}

	/// <summary>
	/// Creates a Complete result with an optional instruction.
	/// </summary>
	/// <param name="instruction">Optional instruction to show to the moderator.</param>
	/// <returns>A HookListenerActionResult indicating completion.</returns>
	public static HookListenerActionResult<T> Complete(T nextListenerPhase)
	{
		return new HookListenerActionResult<T>(HookListenerOutcome.Complete, nextListenerPhase: nextListenerPhase);
	}

	/// <summary>
	/// Creates an Error result with the provided error details.
	/// </summary>
	/// <param name="error">The error that occurred.</param>
	/// <returns>A HookListenerActionResult indicating an error occurred.</returns>
	public new static HookListenerActionResult<T> Error(GameError error)
	{
		return new HookListenerActionResult<T>(HookListenerOutcome.Error, error: error);
	}
}


public class HookHandlerResult
{
    public HookHandlerOutcome Outcome { get; }
    public ModeratorInstruction? Instruction { get; }
    public GameError? ErrorMessage { get; }
    private HookHandlerResult(HookHandlerOutcome outcome, ModeratorInstruction? instruction = null, GameError? error = null)
    {
        Outcome = outcome;
        Instruction = instruction;
        ErrorMessage = error;
    }

    public static HookHandlerResult NeedInput(ModeratorInstruction? instruction = null)
    {
        return new HookHandlerResult(HookHandlerOutcome.NeedInput, instruction: instruction);
    }
    public static HookHandlerResult Complete()
    {
        return new HookHandlerResult(HookHandlerOutcome.Complete);
    }
    public static HookHandlerResult Error(GameError error)
    {
        return new HookHandlerResult(HookHandlerOutcome.Error, error: error);
    }
}