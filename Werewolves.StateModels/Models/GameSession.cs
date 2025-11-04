using System.Diagnostics.CodeAnalysis;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;
// Required for Tuple


namespace Werewolves.StateModels.Models;

/// <summary>
/// Represents the tracked state of a single ongoing game.
/// This class encapsulates all game state and provides a controlled API for state mutations.
/// The GameHistoryLog is the single source of truth for all non-deterministic game events.
/// </summary>
public partial class GameSession
{
    // Core immutable properties
    public Guid Id { get; } = Guid.NewGuid();
    private readonly Dictionary<Guid, Player> _players;
    private readonly List<Guid> _playerSeatingOrder;
    private readonly List<RoleType> _rolesInPlay;

    // Private canonical state - the single source of truth
    private readonly List<GameLogEntryBase> _gameHistoryLog = new();

	// Transient execution state
	private GamePhaseStateCache PhaseStateCache { get; }
	public GamePhase GetCurrentPhase() => PhaseStateCache.GetCurrentPhase();
	public T? GetSubPhase<T>() where T : struct, Enum => PhaseStateCache.GetSubPhase<T>();
    public void SetSubPhase<T>(T subPhase) where T : struct, Enum => PhaseStateCache.SetSubPhase<T>(subPhase);
	public GameHook? GetActiveHook() => PhaseStateCache.GetActiveHook();
    public void SetActiveHook(GameHook hook) => PhaseStateCache.SetActiveHook(hook);
	public ListenerIdentifier? GetCurrentListener() => PhaseStateCache.GetCurrentListener();
	public T? GetCurrentListenerState<T>(ListenerIdentifier listener) where T : struct, Enum =>
        PhaseStateCache.GetCurrentListenerState<T>(listener);
    public void SetCurrentListenerState<T>(ListenerIdentifier listener, T state) where T : struct, Enum =>         
        PhaseStateCache.SetCurrentListenerState<T>(listener, state);

	// Derived cached state (computed from log, mutated only by Apply methods)
	public int TurnNumber { get; private set; }
    public Team? WinningTeam { get; private set; }

    public Guid? PendingVoteOutcome { get; private set; }

    public ModeratorInstruction? PendingModeratorInstruction { get; internal set; } = null;

    

    [SetsRequiredMembers]
    internal GameSession(Dictionary<Guid, Player> players, List<Guid> playerSeatingOrder, List<RoleType> rolesInPlay)
    {
        _players = players;
        _playerSeatingOrder = playerSeatingOrder;
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

    public int RoleInPlayCount(RoleType type) => _rolesInPlay.Count(r => r == type);
    #endregion

    #region Public Command API

    public void PerformNightActionNoTarget(NightActionType type, object? actionOutcome = null) =>
        PerformNightActionCore(type, null, actionOutcome);

    public void PerformNightAction(NightActionType type, Guid targetId, object? actionOutcome = null) =>
        PerformNightActionCore(type, [targetId], actionOutcome);

    public void PerformNightAction(NightActionType type, List<Guid> targetIds, object? actionOutcome = null) =>
        PerformNightActionCore(type, targetIds, actionOutcome);

    // Public command methods - these create log entries and apply them
    public void EliminatePlayer(Guid playerId, EliminationReason reason)
    {
        var entry = new PlayerEliminatedLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = PhaseStateCache.GetCurrentPhase(),
            PlayerId = playerId,
            Reason = reason
        };
        
        _gameHistoryLog.Add(entry);
        entry.Apply(new StateMutator(this));
    }

    public void RevealPlayerRole(Guid playerId, RoleType roleType)
    {
        var entry = new RoleRevealedLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = PhaseStateCache.GetCurrentPhase(),
            PlayerId = playerId,
            RevealedRole = roleType
        };
        
        _gameHistoryLog.Add(entry);
        entry.Apply(new StateMutator(this));
    }

    public void AssignRole(Guid playerId, RoleType roleType) =>
        AssignRole([playerId], roleType);


	public void AssignRole(List<Guid> playerIds, RoleType roleType)
    {
        var entry = new AssignRoleLogEntry()
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = PhaseStateCache.GetCurrentPhase(),
            PlayerIds = playerIds,
            AssignedRole = roleType
        };
        
        _gameHistoryLog.Add(entry);
        entry.Apply(new StateMutator(this));
    }

    public void PerformDayVote(Guid reportedOutcomePlayerId)
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

    public void TransitionMainPhase(GamePhase newPhase, PhaseTransitionReason reason)
    {
        var oldPhase = GetCurrentPhase();

        var entry = new PhaseTransitionLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber ,
            PreviousPhase = oldPhase,
            CurrentPhase = newPhase,
            Reason = reason
        };

        entry.Apply(new StateMutator(this));

        //todo: can we make this go away?
		//hack to ensure that if TurnNumber changed during Apply, the log reflects the new value
		entry = entry with { TurnNumber = TurnNumber }; 

		_gameHistoryLog.Add(entry);
        
    }

    public void VictoryConditionMet(Team winningTeam, string description)
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

	private IEnumerable<TLogEntry> FindLogEntries<TLogEntry>(int? turnsAgo = null, GamePhase? phase = null, Func<TLogEntry, bool>? filter = null) where TLogEntry : GameLogEntryBase
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
        void SetPlayerRole(Guid playerId, RoleType? role);
        void SetWinningTeam(Team? winningTeam);
        void SetPendingVoteOutcome(Guid? pendingVoteOutcome);
        void SetCurrentPhase(GamePhase newPhase);
    }

    internal class StateMutator : IStateMutator
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

        public void SetPlayerRole(Guid playerId, RoleType? role)
        {
            var player = _session.GetPlayerInternal(playerId);
            player.State.Role = role;
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
