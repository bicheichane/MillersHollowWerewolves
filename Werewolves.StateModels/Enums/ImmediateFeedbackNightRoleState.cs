namespace Werewolves.GameLogic.Models.GameHookListeners;

public enum ImmediateFeedbackNightRoleState
{
	AwaitingAwakeConfirmation,
	AwaitingTargetSelection,
	AwaitingModeratorFeedback,
	AwaitingSleepConfirmation,
	Asleep
}