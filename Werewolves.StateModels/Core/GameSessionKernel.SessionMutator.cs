using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;

namespace Werewolves.StateModels.Core;

//todo: move this to its own file once this is finalized.
internal interface ISessionMutator
{
	int CurrentTurnNumber { get; }
	void SetPlayerHealth(Guid playerId, PlayerHealth health);
	void SetPlayerRole(Guid playerId, MainRoleType role);
	void SetCurrentPhase(GamePhase newPhase);
	void SetElderExtraLife(Guid playerId, bool hasExtraLife);
	void SetPlayerInfected(Guid playerId, bool isInfected);
	void AddLogEntry<T>(T entry) where T : GameLogEntryBase;
}

internal partial class GameSessionKernel
{
	private class SessionMutator(GameSessionKernel kernel) : ISessionMutator
	{
		/// <summary>
		/// Represents a key used to allow access to mutate persistent state, player's, game state (i.e. main phase) or game logs.
		/// </summary>
		internal interface IStateMutatorKey{}
		/// <summary>
		/// Private implementation of the state mutator key to restrict access.
		/// </summary>
		private class StateMutatorKey : IStateMutatorKey{}
		private static readonly StateMutatorKey Key = new();

		private PlayerState GetMutablePlayerState(Guid playerId) =>
			kernel.GetMutablePlayerState(Key, playerId);

		public int CurrentTurnNumber => kernel.TurnNumber;

		public void SetPlayerHealth(Guid playerId, PlayerHealth health) =>
			GetMutablePlayerState(playerId).Health = health;

		public void SetPlayerRole(Guid playerId, MainRoleType role)
		{
			var mainRole = GetMutablePlayerState(playerId).MainRole = role;

			if (mainRole == MainRoleType.Elder)
			{
				GetMutablePlayerState(playerId).HasElderExtraLife = true;
			}
		}

		public void SetCurrentPhase(GamePhase newPhase)
		{
			kernel._phaseStateCache.TransitionMainPhase(Key, newPhase);

			if (newPhase == GamePhase.Night)
			{
				kernel.TurnNumber += 1;
			}
		}

		public void SetElderExtraLife(Guid playerId, bool hasExtraLife)
			=> GetMutablePlayerState(playerId).HasElderExtraLife = hasExtraLife;

		public void SetPlayerInfected(Guid playerId, bool isInfected)
			=> GetMutablePlayerState(playerId).IsInfected = isInfected;

		public void AddLogEntry<T>(T entry) where T : GameLogEntryBase 
			=> kernel._gameHistoryLog.AddLogEntry(Key, entry);
	}
}