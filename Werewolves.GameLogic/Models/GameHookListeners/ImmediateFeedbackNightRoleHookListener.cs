using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.GameHookListeners;

internal abstract class ImmediateFeedbackNightRoleHookListener : NightRoleHookListener<ImmediateFeedbackNightRoleState>
{
	protected override ImmediateFeedbackNightRoleState WokenUpStateEnum => ImmediateFeedbackNightRoleState.AwaitingAwakeConfirmation;
	protected ImmediateFeedbackNightRoleState AwaitingTargetSelectionEnum => ImmediateFeedbackNightRoleState.AwaitingTargetSelection;
	protected ImmediateFeedbackNightRoleState AwaitingModeratorFeedbackEnum => ImmediateFeedbackNightRoleState.AwaitingModeratorFeedback;
	protected override ImmediateFeedbackNightRoleState ReadyToSleepStateEnum => ImmediateFeedbackNightRoleState.AwaitingSleepConfirmation;
	protected override ImmediateFeedbackNightRoleState AsleepStateEnum => ImmediateFeedbackNightRoleState.Asleep;
	protected override bool HasNightPowers => true;

	protected override List<RoleStateMachineStage> DefineStateMachineStages() =>
	[
		CreateStage(GameHook.NightMainActionLoop, null, WokenUpStateEnum, HandleRoleWakeupAndId),
		CreateStage(GameHook.NightMainActionLoop, WokenUpStateEnum, AwaitingTargetSelectionEnum, HandleNightPowerUse_AndId),
		CreateStage(GameHook.NightMainActionLoop, AwaitingTargetSelectionEnum, AsleepStateEnum, HandleParseNightPowerConsequences),
		CreateStage(GameHook.NightMainActionLoop, ReadyToSleepStateEnum, AsleepStateEnum, HandleAsleepConfirmation),
		CreateEndStage(GameHook.NightMainActionLoop, AsleepStateEnum, (_, _) => HookListenerActionResult.Complete(AsleepStateEnum)),
	];

	protected abstract ModeratorInstruction ProcessTargetSelection(GameSession session, ModeratorResponse input);

	private HookListenerActionResult HandleParseNightPowerConsequences(GameSession session, ModeratorResponse input)
	{
		var instruction = ProcessTargetSelection(session, input);
		return HookListenerActionResult.NeedInput(instruction, ReadyToSleepStateEnum);
	}
}