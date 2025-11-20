using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;

namespace Werewolves.StateModels.Core
{
	internal partial class GameSessionKernel
	{
		private readonly Dictionary<Guid, Player> _players = new();
		private readonly List<Guid> _playerSeatingOrder = new();
		private readonly List<MainRoleType> _rolesInPlay = new();

		// Private canonical state - the single source of truth
		private readonly List<GameLogEntryBase> _gameHistoryLog = new();
		// Transient execution state
		private GamePhaseStateCache _phaseStateCache = new();

		public IGamePhaseStateCache PhaseStateCache => _phaseStateCache;

		public IReadOnlyList<GameLogEntryBase> GetLogs() => _gameHistoryLog.AsReadOnly();
		public IReadOnlyList<Guid> GetPlayerSeatingOrder() => _playerSeatingOrder.AsReadOnly();
		public IReadOnlyList<MainRoleType> GetRolesInPlay() => _rolesInPlay.AsReadOnly();

		// Derived cached state (computed from log, mutated only by Apply methods)
		public int TurnNumber { get; private set; }

		private ModeratorInstruction? _pendingModeratorInstruction = null;
		public ModeratorInstruction? PendingModeratorInstruction => _pendingModeratorInstruction;
		public void SetPendingModeratorInstruction(ModeratorInstruction instruction) => _pendingModeratorInstruction = instruction;
		public GamePhase CurrentPhase => _phaseStateCache.GetCurrentPhase();

		internal GameSessionKernel(List<string> playerNamesInOrder, List<MainRoleType> rolesInPlay,
			List<string>? eventCardIdsInDeck = null)
		{
			ArgumentNullException.ThrowIfNull(playerNamesInOrder);
			ArgumentNullException.ThrowIfNull(rolesInPlay);
			if (!playerNamesInOrder.Any())
			{
				throw new ArgumentException(GameStrings.PlayerListCannotBeEmpty, nameof(playerNamesInOrder));
			}

			if (!rolesInPlay.Any())
			{
				throw new ArgumentException(GameStrings.RoleListCannotBeEmpty, nameof(rolesInPlay));
			}

			var players = new Dictionary<Guid, Player>();
			var seatingOrder = new List<Guid>();

			foreach (var name in playerNamesInOrder)
			{
				var player = new Player(name);
				_players.Add(player.Id, player);

				//TODO: add seating order input logic
				seatingOrder.Add(player.Id);
			}

			_playerSeatingOrder = seatingOrder;
			_rolesInPlay = rolesInPlay;
			TurnNumber = 0;
			_phaseStateCache = new GamePhaseStateCache(GamePhase.Setup);
		}

		internal void AddEntryAndUpdateState(GameLogEntryBase entry)
		{
			entry.Apply(new SessionMutator(this));

			//todo: can we make this go away?
			//hack to ensure that if TurnNumber changed during Apply, the log reflects the new value
			if (entry is PhaseTransitionLogEntry phaseEntry)
				entry = entry with { TurnNumber = this.TurnNumber };

			_gameHistoryLog.Add(entry);
		}

		internal void TransitionSubPhase(Enum subPhase) =>
			_phaseStateCache.TransitionSubPhase(subPhase);

		internal void StartSubPhaseStage(string subPhaseStage) =>
			_phaseStateCache.StartSubPhaseStage(subPhaseStage);

		internal void CompleteSubPhaseStage() => 
			_phaseStateCache.CompleteSubPhaseStage();

		internal void TransitionListenerAndState<T>(ListenerIdentifier listener, T state) where T : struct, Enum =>
			_phaseStateCache.TransitionListenerAndState<T>(listener, state);

		internal IPlayer GetPlayer(Guid playerId) => GetPlayerInternal(playerId);

		internal IEnumerable<IPlayer> GetPlayers() => _players.Values;

		private Player GetPlayerInternal(Guid playerId)
		{
			if (!_players.TryGetValue(playerId, out var player))
			{
				throw new KeyNotFoundException($"Player with ID {playerId} not found.");
			}

			return player;
		}

		private PlayerState GetPlayerStateInternal(Guid playerId) => GetPlayerInternal(playerId).State;
	}
}
