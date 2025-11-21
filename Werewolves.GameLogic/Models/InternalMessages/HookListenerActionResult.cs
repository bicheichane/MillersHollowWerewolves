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
    

    protected HookListenerActionResult(HookListenerOutcome outcome, ModeratorInstruction? instruction = null)
    {
        Outcome = outcome;
        Instruction = instruction;
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
}

internal class HookListenerActionResult<T> : HookListenerActionResult where T : struct, Enum
{
	public T? NextListenerPhase { get; }

	private HookListenerActionResult(HookListenerOutcome outcome, ModeratorInstruction? instruction = null, T? nextListenerPhase = null) : base(outcome, instruction)
	{
		NextListenerPhase = nextListenerPhase;
	}

	public HookListenerActionResult(HookListenerActionResult baseResult, T nextListenerPhase) : 
		base(baseResult.Outcome, baseResult.Instruction)
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
	/// </summary>
	/// <param name="instruction">Optional instruction to show to the moderator.</param>
	/// <returns>A HookListenerActionResult indicating completion.</returns>
	public static HookListenerActionResult<T> Complete(T nextListenerPhase)
	{
		return new HookListenerActionResult<T>(HookListenerOutcome.Complete, nextListenerPhase: nextListenerPhase);
	}
}