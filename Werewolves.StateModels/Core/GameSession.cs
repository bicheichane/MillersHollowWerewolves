using System.Diagnostics.CodeAnalysis;
using Werewolves.GameLogic.Models;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Interfaces;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;

namespace Werewolves.StateModels.Core;

/// <summary>
/// Represents the tracked state of a single ongoing game.
/// This class encapsulates all game state and provides a controlled API for state mutations.
/// The GameHistoryLog is the single source of truth for all non-deterministic game events.
/// </summary>
internal partial class GameSession : IGameSession
{
    #region Private Fields

    // Core immutable properties
    private readonly Dictionary<Guid, Player> _players;
    private readonly List<Guid> _playerSeatingOrder;
    private readonly List<MainRoleType> _rolesInPlay;

    // Private canonical state - the single source of truth
    private readonly List<GameLogEntryBase> _gameHistoryLog = new();

    // Transient execution state
    private GamePhaseStateCache PhaseStateCache { get; }

	#endregion


	#region Public Game Cache read-access

	public GamePhase GetCurrentPhase() => PhaseStateCache.GetCurrentPhase();

	#endregion

	#region Internal Game Cache read-access
	internal T? GetSubPhase<T>() where T : struct, Enum => PhaseStateCache.GetSubPhase<T>();
    internal ListenerIdentifier? GetCurrentListener() => PhaseStateCache.GetCurrentListener();

    internal T? GetCurrentListenerState<T>(ListenerIdentifier listener) where T : struct, Enum =>
        PhaseStateCache.GetCurrentListenerState<T>(listener);
	#endregion

	#region Internal Game Cache write-access

    internal void TransitionSubPhase(Enum subPhase) =>
        PhaseStateCache.TransitionSubPhase(subPhase);

    internal bool TryGetActiveGameHook(out GameHook hook) =>
        Enum.TryParse<GameHook>(PhaseStateCache.GetActiveSubPhaseStage(), out hook);

	/// <summary>
	/// Checks if the specified sub-phase stage can be entered,
	/// and starts it if entering for the first time for the current sub-phase.
	/// </summary>
	/// <param name="subPhaseStageId"></param>
	/// <returns></returns>
	internal bool TryEnterSubPhaseStage(string subPhaseStageId)
    {
        var currentSubPhaseStage = PhaseStateCache.GetActiveSubPhaseStage();

		// If already in a different sub-phase stage, cannot enter
		if (currentSubPhaseStage != null && currentSubPhaseStage != subPhaseStageId)
        {
            return false;
        }
        else
        // If no sub-phase stage is active:
        if (currentSubPhaseStage == null)
        {
			// If this sub-phase stage has already been completed, cannot enter
			if (PhaseStateCache.HasSubPhaseStageCompleted(subPhaseStageId))
            {
                return false;
            }
			// Otherwise, enter the sub-phase stage
			else
			{
				PhaseStateCache.StartSubPhaseStage(subPhaseStageId);
			}
        }

		// Either already in this sub-phase stage, or just entered it successfully
		return true;
    }

    internal void CompleteSubPhaseStage() =>
        PhaseStateCache.CompleteSubPhaseStage();

	internal void TransitionListenerState<T>(ListenerIdentifier listener, T state) where T : struct, Enum =>
        PhaseStateCache.TransitionListenerAndState<T>(listener, state);

    #endregion


    public Guid Id { get; } = Guid.NewGuid();

    // Derived cached state (computed from log, mutated only by Apply methods)
    public int TurnNumber { get; private set; }
    public Team? WinningTeam { get; private set; }

    public Guid? PendingVoteOutcome { get; private set; }

    public ModeratorInstruction? PendingModeratorInstruction { get; internal set; } = null;

    internal GameSession(List<string> playerNamesInOrder, List<MainRoleType> rolesInPlay,
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
            players.Add(player.Id, player);
            seatingOrder.Add(player.Id);
        }

        _players = players;
        _playerSeatingOrder = seatingOrder;
        _rolesInPlay = rolesInPlay;
        TurnNumber = 0;
        PhaseStateCache = new GamePhaseStateCache(GamePhase.Setup);
    }


    // Public API for state queries

    #region Public Query API

    public IPlayer GetPlayer(Guid playerId)
    {
        return GetPlayerInternal(playerId);
    }

    public IPlayerState GetPlayerState(Guid playerId)
    {
        return GetPlayer(playerId).State;
    }

    public IEnumerable<IPlayer> GetPlayers()
    {
        var playerList = _players.Values.Select(p => (IPlayer)p);

        return playerList;
    }

    public int RoleInPlayCount(MainRoleType type) => _rolesInPlay.Count(r => r == type);

	#endregion

	#region Internal Command API

	internal void PerformNightActionNoTarget(NightActionType type, object? actionOutcome = null) 
        => PerformNightActionCore(type, null, actionOutcome);

    internal void PerformNightAction(NightActionType type, Guid targetId, object? actionOutcome = null) 
        => PerformNightActionCore(type, [targetId], actionOutcome);

    internal void PerformNightAction(NightActionType type, List<Guid> targetIds, object? actionOutcome = null)
        => PerformNightActionCore(type, targetIds, actionOutcome);

    internal IEnumerable<IPlayer> GetPlayersTargetedByNightAction(NightActionType actionType, SelectionCountConstraint countConstraint, int turnsAgo = 0)
    {
        var logEntries = FindLogEntries<NightActionLogEntry>(0, GamePhase.Night, log => log.ActionType == actionType);

        var playerList = logEntries.SelectMany(log => log.TargetIds ?? new()).ToList();

        SelectionCountConstraint.EnforceConstraint(playerList, countConstraint);

        return playerList.Select(GetPlayer);
    }

    internal IEnumerable<IPlayer> GetPlayersEliminatedLastDawn()
    {
        var logEntries = FindLogEntries<PlayerEliminatedLogEntry>(phase: GamePhase.Dawn);

		var playerList = logEntries.Select(log => log.PlayerId).ToList();

        return playerList.Select(GetPlayer);
    }

    internal Guid GetPlayerEliminatedLastVote()
    {
        var logEntries = FindLogEntries<PlayerEliminatedLogEntry>(phase: GamePhase.Day,
            filter: log => log.Reason == EliminationReason.DayVote);

        var playerId = logEntries.Select(log => log.PlayerId).Single();

        return playerId;
    }

    internal List<MainRoleType> GetUnassignedRoles()
    {
        var assignedRoles = _players.Values
            .Select(p => p.State.MainRole)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();
        var unassignedRoles = new List<MainRoleType>(_rolesInPlay);
        foreach (var role in assignedRoles)
        {
            unassignedRoles.Remove(role);
        }
        return unassignedRoles;
	}

	internal void EliminatePlayer(Guid playerId, EliminationReason reason)
    {
        var entry = new PlayerEliminatedLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = PhaseStateCache.GetCurrentPhase(),
            PlayerId = playerId,
            Reason = reason,
        };

        _gameHistoryLog.Add(entry);
        entry.Apply(new StateMutator(this));
    }

    internal void AssignRole(Guid playerId, MainRoleType mainRoleType) =>
        AssignRole([playerId], mainRoleType);


    internal void AssignRole(List<Guid> playerIds, MainRoleType mainRoleType)
    {
        var entry = new AssignRoleLogEntry()
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = PhaseStateCache.GetCurrentPhase(),
            PlayerIds = playerIds,
            AssignedMainRole = mainRoleType
        };

        _gameHistoryLog.Add(entry);
        entry.Apply(new StateMutator(this));
    }

    internal void PerformDayVote(Guid reportedOutcomePlayerId)
    {
        var entry = new VoteOutcomeReportedLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = PhaseStateCache.GetCurrentPhase(),
            ReportedOutcomePlayerId = reportedOutcomePlayerId
        };

        _gameHistoryLog.Add(entry);
        entry.Apply(new StateMutator(this));
    }

    internal void TransitionMainPhase(GamePhase newPhase)
    {
        var oldPhase = GetCurrentPhase();

        var entry = new PhaseTransitionLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            PreviousPhase = oldPhase,
            CurrentPhase = newPhase,
        };

        entry.Apply(new StateMutator(this));

        //todo: can we make this go away?
        //hack to ensure that if TurnNumber changed during Apply, the log reflects the new value
        entry = entry with { TurnNumber = TurnNumber };

        _gameHistoryLog.Add(entry);

    }

    internal void VictoryConditionMet(Team winningTeam, string description)
    {
        var entry = new VictoryConditionMetLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = PhaseStateCache.GetCurrentPhase(),
            WinningTeam = winningTeam,
            ConditionDescription = description
        };

        _gameHistoryLog.Add(entry);
        entry.Apply(new StateMutator(this));
    }

    #endregion

    #region Private helpers

    private Player GetPlayerInternal(Guid playerId)
    {
        if (!_players.TryGetValue(playerId, out var player))
        {
            throw new KeyNotFoundException($"Player with ID {playerId} not found.");
        }

        return player;
    }

    private void PerformNightActionCore(NightActionType type, List<Guid>? targetIds, object? immediateActionOutcome)
    {
        var entry = new NightActionLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = PhaseStateCache.GetCurrentPhase(),
            ActionType = type,
            TargetIds = targetIds,
            ImmediateActionOutcome = immediateActionOutcome
        };

        _gameHistoryLog.Add(entry);
        entry.Apply(new StateMutator(this));
    }

    private IEnumerable<TLogEntry> FindLogEntries<TLogEntry>(int? turnsAgo = null, GamePhase? phase = null,
        Func<TLogEntry, bool>? filter = null) where TLogEntry : GameLogEntryBase
    {
        IEnumerable<TLogEntry> query = _gameHistoryLog.OfType<TLogEntry>();

        if (turnsAgo.HasValue)
        {
            if (turnsAgo < 0)
                throw new ArgumentOutOfRangeException(nameof(turnsAgo), "turnsAgo cannot be negative.");

            int targetTurnNumber = this.TurnNumber - turnsAgo.Value;
            query = query.Where(log => log.TurnNumber == targetTurnNumber);
        }

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

    #endregion

    /// <summary>
    /// Searches the game history log for entries of a specific type, with optional filters.
    /// </summary>


    // State Mutator Pattern Implementation
    internal interface IStateMutator
    {
        void SetPlayerHealth(Guid playerId, PlayerHealth health);
        void SetPlayerRole(Guid playerId, MainRoleType role);
        void SetWinningTeam(Team? winningTeam);
        void SetPendingVoteOutcome(Guid? pendingVoteOutcome);
        void SetCurrentPhase(GamePhase newPhase);
    }

    protected class StateMutator : IStateMutator
    {
        private readonly GameSession _session;

        internal StateMutator(GameSession session)
        {
            _session = session;
        }

        public void SetPlayerHealth(Guid playerId, PlayerHealth health)
        {
            var player = _session.GetPlayerInternal(playerId);
            player.State.Health = health;

        }

        public void SetPlayerRole(Guid playerId, MainRoleType role)
        {
            var player = _session.GetPlayerInternal(playerId);
            player.State.MainRole = role;
        }

        public void SetWinningTeam(Team? winningTeam)
        {
            _session.WinningTeam = winningTeam;
        }

        public void SetPendingVoteOutcome(Guid? pendingVoteOutcome)
        {
            _session.PendingVoteOutcome = pendingVoteOutcome;
        }

        public void SetCurrentPhase(GamePhase newPhase)
        {
            _session.PhaseStateCache.TransitionMainPhase(newPhase);

            if (newPhase == GamePhase.Night)
            {
                _session.TurnNumber += 1;
            }
        }
    }
}
