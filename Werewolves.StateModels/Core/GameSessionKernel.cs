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
	internal sealed partial class GameSessionKernel
	{
		private class GameLogManager
		{
			private readonly List<GameLogEntryBase> _logEntries = new();
			internal void AddLogEntry(SessionMutator.IStateMutatorKey key, GameLogEntryBase entry)
			{
				_logEntries.Add(entry);
			}

			internal IReadOnlyList<GameLogEntryBase> GetAllLogEntries() => _logEntries.AsReadOnly();

			/// <summary>
			/// Searches the game history log for entries of a specific type, with optional filters.
			/// </summary>
			internal IEnumerable<TLogEntry> FindLogEntries<TLogEntry>(NumberRangeConstraint turnIntervalConstraint, GamePhase? phase = null,
				Func<TLogEntry, bool>? filter = null) where TLogEntry : GameLogEntryBase
			{
				IEnumerable<TLogEntry> query = _logEntries.OfType<TLogEntry>();

				var turnsAgo = turnIntervalConstraint;
				if (turnsAgo.Minimum < 0 || turnsAgo.Maximum < 0)
					throw new ArgumentOutOfRangeException(nameof(turnIntervalConstraint), "turnsAgo cannot be negative.");

				query = query.Where(log =>
					log.TurnNumber >= turnsAgo.Minimum &&
					log.TurnNumber <= turnsAgo.Maximum);

				if (phase.HasValue)
				{
					query = query.Where(log => log.CurrentPhase == phase.Value);
				}

				if (filter != null)
				{
					query = query.Where(filter);
				}

				return query;
			}
		}

		private readonly Dictionary<Guid, Player> _players = new();
		private readonly List<Guid> _playerSeatingOrder = new();
		private readonly List<MainRoleType> _rolesInPlay = new();

		// Private canonical state - the single source of truth
		private readonly GameLogManager _gameHistoryLog = new();
		// Transient execution state
		private GamePhaseStateCache _phaseStateCache = new();

		public IGamePhaseStateCache PhaseStateCache => _phaseStateCache;

		internal IEnumerable<TLogEntry> FindLogEntries<TLogEntry>
			(NumberRangeConstraint? turnIntervalConstraint = null, GamePhase? phase = null, Func<TLogEntry, bool>? filter = null) where TLogEntry : GameLogEntryBase 
			=> _gameHistoryLog.FindLogEntries(turnIntervalConstraint ?? NumberRangeConstraint.Any, phase, filter);
		public IReadOnlyList<Guid> GetPlayerSeatingOrder() => _playerSeatingOrder.AsReadOnly();
		public IReadOnlyList<MainRoleType> GetRolesInPlay() => _rolesInPlay.AsReadOnly();
		public IReadOnlyList<GameLogEntryBase> GetAllLogEntries() => _gameHistoryLog.GetAllLogEntries();

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

		private PlayerState GetMutablePlayerState(SessionMutator.IStateMutatorKey key, Guid playerId) => GetPlayerInternal(playerId).GetMutableState(key);
	}
}
