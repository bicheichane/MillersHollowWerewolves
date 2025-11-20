using System;
using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.GameLogic.Services;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;
using static Werewolves.GameLogic.Models.InternalMessages.StayInSubPhaseHandlerResult;

namespace Werewolves.GameLogic.Models.StateMachine;

/// <summary>
/// A SubPhaseStage represents a single stage within a game's phase state machine,
/// that can either represent a GameHook or a custom handler function (that cannot fire game hooks internally).
///
/// EACH STAGE CAN ONLY BE EXECUTED ONCE PER SUB-PHASE ENTRY. They cannot be interrupted to ask for input.
/// In case of a workflow of input requirement and input processing, multiple stages should be defined in sequence.
/// One for requesting input, and another for processing that input.
///
/// This is to avoid idempotency being an issue.
/// </summary>
internal abstract class SubPhaseStage
{
    protected SubPhaseStage(string id)
    {
        Id = id;
    }

    protected SubPhaseStage(Enum idEnum)
        : this(idEnum.ToString())
    {
    }

    public string Id { get; }
    private Action<ModeratorResponse>? _validateInputRequirements = null;

	/// <summary>
	/// This tries to execute the sub-phase stage, returning true if it was executed, 
	/// with the corresponding PhaseHandlerResult. If it was not executed (because the stage was already executed
	/// for this sub-phase entry), it returns false and a null result.
	/// </summary>
	/// <param name="session"></param>
	/// <param name="input"></param>
	/// <param name="result"></param>
	/// <returns></returns>
	internal bool TryExecute(GameSession session, ModeratorResponse input, out PhaseHandlerResult? result)
    {
        if (session.TryEnterSubPhaseStage(Id))
        {
            result = Execute(session, input);
            return true;
        }

        result = null;
        return false;
    }

    protected PhaseHandlerResult Execute(GameSession session, ModeratorResponse input)
    {
        _validateInputRequirements?.Invoke(input);

        var result = InnerExecute(session, input);

		return result;
    }

	protected abstract PhaseHandlerResult InnerExecute(GameSession session, ModeratorResponse input);

    internal SubPhaseStage RequiresInputType(ExpectedInputType expectedInputType)
    {
        _validateInputRequirements = (moderatorResponse) =>
        {
            if (moderatorResponse.Type != expectedInputType)
            {
                throw new InvalidOperationException(
                    $"Sub-phase stage {Id} expected moderator input of type {expectedInputType}, " +
                    $"but the last instruction sent to the moderator expected input of type {moderatorResponse.Type}.");
            }
        };
        return this;
	}
}

/// <summary>
/// Models a sub-phase stage that represents a navigation point within the sub-phase.
/// THIS IS THE ONLY SUB-PHASE STAGE THAT CAN NAVIGATE OUT OF THE SUB-PHASE.
/// It may or may not exist at any point in the sub-phase's stage sequence, one or multiple times,
/// but it MUST exist at the end of the sub-phase's stage sequence to allow for proper navigation out of the sub-phase.
/// </summary>
internal abstract class NavigationSubPhaseStage : SubPhaseStage
{
    private readonly Func<GameSession, ModeratorResponse, PhaseHandlerResult> _handler;

    private protected NavigationSubPhaseStage(Enum idEnum, Func<GameSession, ModeratorResponse, PhaseHandlerResult> handler)
        : base(idEnum)
    {
        _handler = handler;
    }

    protected override PhaseHandlerResult InnerExecute(GameSession session, ModeratorResponse input)
        => _handler(session, input);
}

/// <summary>
/// Can be used anywhere in the sub-phase's stage sequence, except at the end.
/// It's allowed to fall-through and continue to the next stage in the sequence.
/// </summary>
internal sealed class MidNavigationSubPhaseStage : NavigationSubPhaseStage
{
    private MidNavigationSubPhaseStage(Enum idEnum, Func<GameSession, ModeratorResponse, PhaseHandlerResult> handler)
        : base(idEnum, handler) { }

    internal static SubPhaseStage MidNavigationStage<TEnum>(
        TEnum idEnum,
        Func<GameSession, ModeratorResponse, PhaseHandlerResult> handler) where TEnum : struct, Enum
        => new MidNavigationSubPhaseStage(idEnum, handler);
}

/// <summary>
/// MUST be used at the end of the sub-phase's stage sequence.
/// </summary>
internal class EndNavigationSubPhaseStage : NavigationSubPhaseStage
{
    private EndNavigationSubPhaseStage(Enum idEnum, Func<GameSession, ModeratorResponse, PhaseHandlerResult> handler)
        : base(idEnum, handler) { }

    internal static SubPhaseStage NavigationEndStage<TEnum>(
        TEnum idEnum, Func<GameSession, ModeratorResponse, MajorNavigationPhaseHandlerResult> handler) where TEnum : struct, Enum
        => new EndNavigationSubPhaseStage(idEnum, handler);

    internal static SubPhaseStage NavigationEndStageSilent<T>(T phaseEnum) where T : struct, Enum
        => new EndNavigationSubPhaseStage(phaseEnum,
            (_, _) => new SubPhaseHandlerResult(null, phaseEnum));

	/// <summary>
	/// Use this when you just want to navigate to a specific main-phase without any custom logic,
	/// AND you don't need to send any instruction to the moderator.
	/// </summary>
	/// <param name="phaseEnum">Main-phase enum that will be navigated to</param>
	/// <returns></returns>
internal static SubPhaseStage NavigationEndStageSilent(GamePhase phaseEnum)
        => new EndNavigationSubPhaseStage(phaseEnum,
            (_, _) => new MainPhaseHandlerResult(null, phaseEnum));
}

/// <summary>
/// Models a sub-phase stage that executes custom logic via a provided handler function.
/// CANNOT BE USED TO NAVIGATE OUT OF THE SUB-PHASE.
/// </summary>
internal sealed class LogicSubPhaseStage : SubPhaseStage
{
    private readonly Func<GameSession, ModeratorResponse, PhaseHandlerResult> _handler;

    internal static SubPhaseStage LogicStage<TEnum>(TEnum idEnum, Func<GameSession, ModeratorResponse, ModeratorInstruction?> handler)
        where TEnum : struct, Enum
        => new LogicSubPhaseStage(idEnum, handler);

	/// <summary>
	/// Used for when the logic stage is meant to transition silently to the next stage.
	/// </summary>
	/// <typeparam name="TEnum"></typeparam>
	/// <param name="idEnum"></param>
	/// <param name="handler"></param>
	/// <returns></returns>
	internal static SubPhaseStage LogicStage<TEnum>(TEnum idEnum, Action<GameSession, ModeratorResponse> handler)
        where TEnum : struct, Enum
        => new LogicSubPhaseStage(idEnum, ((session, response) =>
        {
            handler(session, response);
            return null;
        })
    );

	private LogicSubPhaseStage(Enum idEnum, Func<GameSession, ModeratorResponse, ModeratorInstruction?> handler)
        : base(idEnum)
    {
        _handler = (gameSession, moderatorResponse) =>
        {
            var instruction = handler(gameSession, moderatorResponse);
            return StayInSubPhase(instruction);
        };
    }

    protected override PhaseHandlerResult InnerExecute(GameSession session, ModeratorResponse input) 
        => _handler(session, input);
}

/// <summary>
/// Represents a sub-phase stage that executes a game hook and processes registered listeners within the game flow.
///
/// CANNOT BE USED TO NAVIGATE OUT OF THE SUB-PHASE.
/// 
/// </summary>
/// <remarks>This class is used internally to manage the execution of a specific game hook during a sub-phase,
/// invoking all associated listeners in sequence. If no listeners are registered for the hook, or all listeners
/// complete successfully, the provided completion delegate is called to finalize the phase. If a listener requires
/// additional input, the phase remains active until input is provided. This type is intended for internal use within
/// the game flow management system and is not thread-safe.</remarks>
internal sealed class HookSubPhaseStage : SubPhaseStage
{
    private readonly GameHook _hook;
    private readonly Func<GameSession, ModeratorResponse, PhaseHandlerResult> _onComplete;

    internal static SubPhaseStage HookStage(GameHook gameHook) 
        => new HookSubPhaseStage(gameHook);

    private HookSubPhaseStage(GameHook hook) : base(hook)
    {
        _hook = hook;
        _onComplete = (gameSession, moderatorResponse) => StayInSubPhase(null);
    }

    protected override PhaseHandlerResult InnerExecute(GameSession session, ModeratorResponse input) =>
        FireHook(_hook, session, input, _onComplete);

    private static PhaseHandlerResult FireHook(
        GameHook hook,
        GameSession session,
        ModeratorResponse input,
        Func<GameSession, ModeratorResponse, PhaseHandlerResult> onComplete)
    {
		// Get registered listeners for this hook
        if (!GameFlowManager.HookListeners.TryGetValue(hook, out var listeners))
        {
			// No listeners registered for this hook, complete it
            return onComplete(session, input);
        }

		// Check if we have a currently paused listener
        var currentListener = session.GetCurrentListener();

		// Dispatch to each listener in sequence
        foreach (var listenerId in listeners)
        {
            if (!GameFlowManager.ListenerImplementations.TryGetValue(listenerId, out var listener))
            {
				//throw new InvalidOperationException($"Listener implementation not found for listener ID: {listenerId}");
				// TODO: Skip unimplemented listeners for now
                continue;
            }

            if (currentListener != null && currentListener != listenerId)
            {
				// Another listener is currently paused, skip until resumed
                continue;
            }

			// Call the listener's state machine
            var hookResult = listener.AdvanceStateMachine(session, input);

            switch (hookResult.Outcome)
            {
                case HookListenerOutcome.NeedInput:
					// Handler needs input, pause processing
                    return StayInSubPhase(hookResult.Instruction!);

                case HookListenerOutcome.Complete:
					// Listener completed successfully, continue to next
                    continue;

                default:
                    throw new InvalidOperationException($"Unknown HookListenerActionOutcome: {hookResult.Outcome}");
            }
        }

		// All listeners completed successfully
        return onComplete(session, input);
    }
}
