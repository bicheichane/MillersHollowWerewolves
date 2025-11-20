using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Core;

//todo: move this to its own file once this is finalized.
internal interface ISessionMutator
{
	void SetPlayerHealth(Guid playerId, PlayerHealth health);
	void SetPlayerRole(Guid playerId, MainRoleType role);
	void SetCurrentPhase(GamePhase newPhase);
	void SetElderExtraLife(Guid playerId, bool hasExtraLife);
	void SetPlayerInfected(Guid playerId, bool isInfected);
}

internal partial class GameSessionKernel
{
	private class SessionMutator : ISessionMutator
	{
		private readonly GameSessionKernel _kernel;

		public SessionMutator(GameSessionKernel kernel)
		{
			_kernel = kernel;
		}

		public void SetPlayerHealth(Guid playerId, PlayerHealth health) =>
			_kernel.GetPlayerStateInternal(playerId).Health = health;

		public void SetPlayerRole(Guid playerId, MainRoleType role)
		{
			var mainRole = _kernel.GetPlayerStateInternal(playerId).MainRole = role;

			if (mainRole == MainRoleType.Elder)
			{
				_kernel.GetPlayerStateInternal(playerId).HasElderExtraLife = true;
			}
		}

		public void SetCurrentPhase(GamePhase newPhase)
		{
			_kernel._phaseStateCache.TransitionMainPhase(newPhase);

			if (newPhase == GamePhase.Night)
			{
				_kernel.TurnNumber += 1;
			}
		}

		public void SetElderExtraLife(Guid playerId, bool hasExtraLife)
			=> _kernel.GetPlayerStateInternal(playerId).HasElderExtraLife = hasExtraLife;

		public void SetPlayerInfected(Guid playerId, bool isInfected)
			=> _kernel.GetPlayerStateInternal(playerId).IsInfected = isInfected;
	}
}