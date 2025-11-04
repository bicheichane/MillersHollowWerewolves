using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using static Werewolves.GameLogic.Models.InternalMessages.HookListenerActionResult<Werewolves.GameLogic.Models.GameHookListeners.NightRoleIdOnlyState>;

namespace Werewolves.GameLogic.Models.GameHookListeners;

internal enum NightRoleIdOnlyState
{
	Awake,
	Asleep
}

internal abstract class NightRoleIdOnlyHookListener : NightRoleHookListener<NightRoleIdOnlyState>
{
	protected sealed override bool HasNightPowers => false;
	protected override NightRoleIdOnlyState WokenUpStateEnum => NightRoleIdOnlyState.Awake;
	protected override NightRoleIdOnlyState ReadyToSleepStateEnum => NightRoleIdOnlyState.Awake;
	protected override NightRoleIdOnlyState AsleepStateEnum => NightRoleIdOnlyState.Asleep;

	protected override List<RoleStateMachineStage> DefineStateMachineStages() =>
	[
		CreateStage(GameHook.NightActionLoop, null, WokenUpStateEnum, HandleRoleWakeupAndId),
		CreateStage(GameHook.NightActionLoop, WokenUpStateEnum, AsleepStateEnum, HandleNightPowerUse_AndId),
		CreateEndStage(GameHook.NightActionLoop, AsleepStateEnum, (_, _) => Complete(AsleepStateEnum))
	];

	protected override HookListenerActionResult<NightRoleIdOnlyState> HandleNightPowerUse(GameSession session,
		ModeratorResponse input) =>
		Complete(AsleepStateEnum);
}