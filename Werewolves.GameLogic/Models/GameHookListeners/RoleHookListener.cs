using Werewolves.GameLogic.Interfaces;
using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Extensions;

using Werewolves.StateModels.Models;
using static Werewolves.StateModels.Enums.PlayerHealth;

namespace Werewolves.GameLogic.Models.GameHookListeners;

/// <summary>
/// The universal base for all role listeners.
/// Implement this class for roles with stateless or atomic "fire-and-forget" logic that
/// completes in a single transaction without needing to pause for moderator input.
/// </summary>
internal abstract class RoleHookListener : IGameHookListener
{
	internal abstract string PublicName { get; }
	
	public abstract ListenerIdentifier Id { get; }

	public virtual HookListenerActionResult AdvanceStateMachine(GameSession session, ModeratorResponse input)
	{
		//if there are no alive players with this role, skip

		var rolePlayer = GetAliveRolePlayers(session)?.FirstOrDefault();

		if (rolePlayer == null)
		{
			return HookListenerActionResult.Complete(); // No alive players with this role, skip
		}

		// otherwise, advance the core state machine
		return AdvanceCoreStateMachine(session, input);
	}

	protected abstract HookListenerActionResult AdvanceCoreStateMachine(GameSession session,
		ModeratorResponse input);

	#region MainRole Helper functions

	protected List<IPlayer>? GetAliveRolePlayers(GameSession session) =>
		session.GetPlayers().WithRole(Id).WithHealth(Alive).ToList();

	protected List<Guid> GetPotentialTargets(GameSession session, bool canTargetSelf)
	{
		var potentialTargets = session.GetPlayers()
			.WithHealth(Alive);

		if (canTargetSelf == false)
			potentialTargets = potentialTargets.WithoutRole(Id);

		var list = potentialTargets.ToIdList();

		if (!list.Any())
		{
			throw new InvalidOperationException($"No valid targets available for {Id} to select.");
		}

		return list;
	}

	#endregion
}


/// <summary>
/// The base for roles requiring a stateful, interactive workflow.
/// The generic parameter T must be a state enum, enabling the listener to pause
/// for moderator input and correctly resume its multi-step state machine.
/// </summary>
/// <typeparam name="TRoleStateEnum"></typeparam>
internal abstract class RoleHookListener<TRoleStateEnum> : RoleHookListener where TRoleStateEnum : struct, Enum
{
	//for when the listener starts to respond to a hook and it has no current state
	private Dictionary<GameHook, RoleStateMachineStage> InitialStateMachineStages { get; set; } = new();
	private Dictionary<GameHook, Dictionary<TRoleStateEnum, RoleStateMachineStage>>? StateMachineStagesDictionary { get; set; }

	protected abstract List<RoleStateMachineStage> DefineStateMachineStages();

	protected sealed override HookListenerActionResult AdvanceCoreStateMachine(GameSession session,
		ModeratorResponse input)
	{
		if (StateMachineStagesDictionary == null)
			InitStateMachineStages();

		if (session.TryGetActiveGameHook(out var currentHook) == false)
		{
			throw new InvalidOperationException(
				$"{Id}: Tried to advance role state machine without an active game hook");
		}
		var currentState = GetCurrentListenerState(session);

		RoleStateMachineStage? stageToExecute = null;

		if (currentState != null)
		{
			//we have a current state, so look up the appropriate stage
			if (StateMachineStagesDictionary!.TryGetValue(currentHook, out var stagesForHook) &&
			    stagesForHook.TryGetValue((TRoleStateEnum)currentState, out var stage))
			{
				stageToExecute = stage;
			}
		}
		else
		{
			//no current state, so look for an initial stage for this hook
			if (InitialStateMachineStages.TryGetValue(currentHook, out var initialStage))
			{
				stageToExecute = initialStage;
			}
		}

		if (stageToExecute == null)
		{
			throw new InvalidOperationException(
				$"{Id}: machine state stuck, cannot advance from {currentHook}:{currentState}");
		}

		//execute the stage
		var result = ((IRoleStageRestrictedApi)stageToExecute).Execute(session, input, currentState);

		return result;
	}

	private void InitStateMachineStages()
	{
		StateMachineStagesDictionary = new();

		var stages = DefineStateMachineStages();

		foreach (var stage in stages)
		{
			if (stage.StartStage == null)
			{
				if (stage.ShouldOverwriteStartStage == false &&
				    InitialStateMachineStages.ContainsKey(stage.GameHook))
				{
					throw new InvalidOperationException(
						$"{Id}: Illegal overwrite of initial stage handler for {stage.GameHook} game hook");
				}

				InitialStateMachineStages[stage.GameHook] = stage;
			}
			else
			{
				if (StateMachineStagesDictionary.ContainsKey(stage.GameHook) == false)
				{
					StateMachineStagesDictionary[stage.GameHook] = new();
				}

				var startStage = (TRoleStateEnum)stage.StartStage;

				if (stage.ShouldOverwriteStartStage == false &&
				    StateMachineStagesDictionary[stage.GameHook].ContainsKey(startStage))
				{
					throw new InvalidOperationException(
						$"{Id}: Illegal overwrite of {startStage} stage handler");
				}
				

				StateMachineStagesDictionary[stage.GameHook][startStage] = stage;
			}

			
		}
	}

	protected TRoleStateEnum? GetCurrentListenerState(GameSession session)
	{
		return session.GetCurrentListenerState<TRoleStateEnum>(this.Id);
	}

	#region RoleStateMachineStage Wrappers
	internal RoleStateMachineStage CreateStage(
		GameHook gameHook,
		TRoleStateEnum? startStage,
		TRoleStateEnum endStage,
		Func<GameSession, ModeratorResponse, HookListenerActionResult<TRoleStateEnum>> actionToPerform,
		bool shouldOverwriteStartStage = false
		)
		=> new(
			Id,
			gameHook,
			startStage,
			actionToPerform,
			[endStage],
			false,
			shouldOverwriteStartStage
		);

	internal RoleStateMachineStage CreateStage(
		GameHook gameHook,
		TRoleStateEnum? startStage,
		HashSet<TRoleStateEnum> possibleEndStages,
		Func<GameSession, ModeratorResponse, HookListenerActionResult<TRoleStateEnum>> actionToPerform,
		bool shouldOverwriteStartStage = false
		)
		=> new(
			Id,
			gameHook,
			startStage,
			actionToPerform,
			possibleEndStages,
			false,
			shouldOverwriteStartStage
		);

	/// <summary>
	/// Used for stages that are defined at abstract parents, and navigation is left to the devices of the child class.
	/// </summary>
	/// <param name="gameHook"></param>
	/// <param name="startStage"></param>
	/// <param name="actionToPerform"></param>
	/// <param name="shouldOverwriteStartStage"></param>
	/// <returns></returns>
	internal RoleStateMachineStage CreateOpenEndedStage(
		GameHook gameHook,
		TRoleStateEnum? startStage,
		Func<GameSession, ModeratorResponse, HookListenerActionResult<TRoleStateEnum>> actionToPerform,
		bool shouldOverwriteStartStage = false
		)
		=> new(
			Id,
			gameHook,
			startStage,
			actionToPerform,
			null,
			ShouldAdvanceState: true,
			shouldOverwriteStartStage
		);

	/// <summary>
	/// Same as open ended but explicitly is not meant to advance state.
	/// </summary>
	/// <param name="gameHook"></param>
	/// <param name="startStage"></param>
	/// <param name="actionToPerform"></param>
	/// <param name="shouldOverwriteStartStage"></param>
	/// <returns></returns>
	internal RoleStateMachineStage CreateEndStage(
		GameHook gameHook,
		TRoleStateEnum startStage,
		Func<GameSession, ModeratorResponse, HookListenerActionResult<TRoleStateEnum>> actionToPerform,
		bool shouldOverwriteStartStage = false
	)
		=> new(
			Id,
			gameHook,
			startStage,
			actionToPerform,
			[startStage],
			ShouldAdvanceState: false,
			shouldOverwriteStartStage
		);

	#endregion

	#region RoleStateMachineStage Definition
	private interface IRoleStageRestrictedApi
	{
		HookListenerActionResult<TRoleStateEnum> Execute(GameSession session, ModeratorResponse input, TRoleStateEnum? currentState);
	}



	/// <summary>
	/// 
	/// </summary>
	/// <param name="MainRoleType"></param>
	/// <param name="GameHook"></param>
	/// <param name="StartStage"></param>
	/// <param name="ActionToPerform">
	/// Whatever happens inside of this function, it must result in the state being set to one of the PossibleEndStages.
	/// </param>
	/// <param name="PossibleEndStages"></param>
	/// <param name="ShouldAdvanceState">
	/// Used for open ended stages, to ensure they at least advanced state.
	/// If false, the stage is allowed to leave the state unchanged.
	/// Otherwise, throw an error if the state is unchanged after execution.
	/// </param>
	/// <param name="ShouldOverwriteStartStage">While false, will throw an exception if it attempts to replace a handler for the same start stage.
	/// Otherwise, will proceed with replacing it</param>
	internal record RoleStateMachineStage(
		MainRoleType MainRoleType,
		GameHook GameHook,
		TRoleStateEnum? StartStage,
		Func<GameSession, ModeratorResponse, HookListenerActionResult<TRoleStateEnum>> ActionToPerform,
		HashSet<TRoleStateEnum>? PossibleEndStages,
		bool ShouldAdvanceState,
		bool ShouldOverwriteStartStage = false)
		: IRoleStageRestrictedApi
	{

		// Ensure this is only visible to the class RoleHookListener<TRoleStateEnum>
		HookListenerActionResult<TRoleStateEnum> IRoleStageRestrictedApi.Execute(GameSession session, ModeratorResponse input, TRoleStateEnum? currentState)
		{
			if (currentState.Equals(StartStage) == false)
			{
				throw new InvalidOperationException(
					$"State Machine Error: MainRole '{MainRoleType}' attempted to execute stage for '{currentState} but " +
					$"executed {StartStage} instead'.");
			}

			var output = ActionToPerform(session, input);
			var newState = output.NextListenerPhase;

			if (newState == null)
			{
				throw new InvalidOperationException(
					$"State Machine Error: MainRole '{MainRoleType}' attempted to transition to null state");
			}

			//if we are in a stage that should advance state, but we didn't, throw
			else if (ShouldAdvanceState &&
			         newState.Equals(currentState))
			{
				throw new InvalidOperationException(
					$"State Machine Error: MainRole '{MainRoleType}' attempted to remain in state '{newState}' " +
					$"but this stage requires state advancement.");
			}

			//if we didn't error out and we ended up in a state not in PossibleEndStages, throw
			else if (PossibleEndStages != null &&
			         PossibleEndStages.Contains((TRoleStateEnum)newState) == false)
			{
				var joinedPossibleStateList = string.Join(", ", PossibleEndStages);
				throw new InvalidOperationException(
					$"State Machine Error: MainRole '{MainRoleType}' attempted to transition to invalid state '{newState}'. " +
					$"Valid end states from this stage are: {joinedPossibleStateList}."
				);
			}

			//set the new state
			session.TransitionListenerState(MainRoleType, (TRoleStateEnum)newState);


			return output;
		}
	}
	#endregion
}
