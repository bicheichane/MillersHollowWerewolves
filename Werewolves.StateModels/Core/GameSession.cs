using System.Diagnostics.CodeAnalysis;
using Werewolves.GameLogic.Models;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Extensions;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;

namespace Werewolves.StateModels.Core;

public interface IGameSession
{
    public Guid Id { get; }
    public GamePhase GetCurrentPhase();
    public int TurnNumber { get; }
    public IPlayer GetPlayer(Guid playerId);
    public IPlayerState GetPlayerState(Guid playerId);
    public IEnumerable<IPlayer> GetPlayers();
    public int RoleInPlayCount(MainRoleType type);
}

/// <summary>
/// Represents the tracked state of a single ongoing game.
/// This class encapsulates all game state and provides a controlled API for state mutations.
/// The GameHistoryLog is the single source of truth for all non-deterministic game events.
/// </summary>
internal class GameSession : IGameSession
{
    #region Private Fields

    // Core immutable properties

    private readonly GameSessionKernel _gameSessionKernel;

	#endregion


	#region Public Game Cache read-access

	public GamePhase GetCurrentPhase() => _gameSessionKernel.CurrentPhase;
    public int TurnNumber => _gameSessionKernel.TurnNumber;
    public ModeratorInstruction? PendingModeratorInstruction => _gameSessionKernel.PendingModeratorInstruction;
    public void SetPendingModeratorInstruction(ModeratorInstruction instruction) =>
        _gameSessionKernel.SetPendingModeratorInstruction(instruction);

	#endregion

	#region Internal Game Cache read-access
	internal T? GetSubPhase<T>() where T : struct, Enum => _gameSessionKernel.PhaseStateCache.GetSubPhase<T>();
    internal ListenerIdentifier? GetCurrentListener() => _gameSessionKernel.PhaseStateCache.GetCurrentListener();

    internal T? GetCurrentListenerState<T>(ListenerIdentifier listener) where T : struct, Enum =>
        _gameSessionKernel.PhaseStateCache.GetCurrentListenerState<T>(listener);
	#endregion

	#region Internal Game Cache write-access

    internal void TransitionSubPhase(Enum subPhase) =>
        _gameSessionKernel.TransitionSubPhase(subPhase);

    internal bool TryGetActiveGameHook(out GameHook hook) =>
        Enum.TryParse(_gameSessionKernel.PhaseStateCache.GetActiveSubPhaseStage(), out hook);

	/// <summary>
	/// Checks if the specified sub-phase stage can be entered,
	/// and starts it if entering for the first time for the current sub-phase.
	/// </summary>
	/// <param name="subPhaseStageId"></param>
	/// <returns></returns>
	internal bool TryEnterSubPhaseStage(string subPhaseStageId)
    {
        var currentSubPhaseStage = _gameSessionKernel.PhaseStateCache.GetActiveSubPhaseStage();

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
			if (_gameSessionKernel.PhaseStateCache.HasSubPhaseStageCompleted(subPhaseStageId))
            {
                return false;
            }
			// Otherwise, enter the sub-phase stage
			else
			{
				_gameSessionKernel.StartSubPhaseStage(subPhaseStageId);
			}
        }

		// Either already in this sub-phase stage, or just entered it successfully
		return true;
    }

    internal void CompleteSubPhaseStage() =>
        _gameSessionKernel.CompleteSubPhaseStage();

	internal void TransitionListenerState<T>(ListenerIdentifier listener, T state) where T : struct, Enum =>
        _gameSessionKernel.TransitionListenerAndState(listener, state);

    #endregion


    public Guid Id { get; } = Guid.NewGuid();

    internal GameSession(List<string> playerNamesInOrder, List<MainRoleType> rolesInPlay, List<string>? eventCardIdsInDeck = null)
    {
        _gameSessionKernel = new GameSessionKernel(playerNamesInOrder, rolesInPlay, eventCardIdsInDeck);
	}


    // Public API for state queries

    #region Public Query API

    public IPlayer GetPlayer(Guid playerId) => _gameSessionKernel.GetPlayer(playerId);

    public IPlayerState GetPlayerState(Guid playerId) => GetPlayer(playerId).State;

    public IEnumerable<IPlayer> GetPlayers() => _gameSessionKernel.GetPlayers();

    public int RoleInPlayCount(MainRoleType type) => _gameSessionKernel.GetRolesInPlay().Count(r => r == type);

	#endregion

	#region Internal Command API

	internal void PerformNightActionNoTarget(NightActionType type) 
        => PerformNightActionCore(type, null);

    internal void PerformNightAction(NightActionType type, Guid targetId) 
        => PerformNightActionCore(type, [targetId]);

    internal void PerformNightAction(NightActionType type, List<Guid> targetIds)
        => PerformNightActionCore(type, targetIds);

    internal IEnumerable<IPlayer> GetPlayersTargetedLastNight(NightActionType actionType,
        NumberRangeConstraint countConstraint, NumberRangeConstraint? turnsAgoConstraint = null)
    {
        var turnNumber = _gameSessionKernel.TurnNumber;
		var logEntries = _gameSessionKernel.FindLogEntries<NightActionLogEntry>(NumberRangeConstraint.Exact(turnNumber), GamePhase.Night, log => log.ActionType == actionType);

        var playerList = logEntries.SelectMany(log => log.TargetIds ?? new()).ToList();

        countConstraint.Enforce(playerList);

        return playerList.Select(GetPlayer);
	}


	

    internal IEnumerable<IPlayer> GetPlayersEliminatedLastDawn()
    {
	    var turnNumber = _gameSessionKernel.TurnNumber;
		var logEntries = _gameSessionKernel.FindLogEntries<PlayerEliminatedLogEntry>(NumberRangeConstraint.Exact(turnNumber), phase: GamePhase.Dawn);

		var playerList = logEntries.Select(log => log.PlayerId).ToList();

        return playerList.Select(GetPlayer);
    }

    internal Guid GetPlayerEliminatedLastVote()
    {
	    var turnNumber = _gameSessionKernel.TurnNumber;
		var logEntries = _gameSessionKernel.FindLogEntries<PlayerEliminatedLogEntry>(NumberRangeConstraint.Exact(turnNumber), phase: GamePhase.Day,
            filter: log => log.Reason == EliminationReason.DayVote);

        var playerId = logEntries.Select(log => log.PlayerId).Last();

        return playerId;
    }

    internal List<MainRoleType> GetUnassignedRoles()
    {
        var assignedRoles = _gameSessionKernel.GetPlayers()
            .Select(p => p.State.MainRole)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();
        var unassignedRoles = _gameSessionKernel.GetRolesInPlay().ToList();
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
            CurrentPhase = _gameSessionKernel.PhaseStateCache.GetCurrentPhase(),
            PlayerId = playerId,
            Reason = reason,
        };

        _gameSessionKernel.AddEntryAndUpdateState(entry);
    }

    internal void AssignRole(Guid playerId, MainRoleType mainRoleType) =>
        AssignRole([playerId], mainRoleType);


    internal void AssignRole(List<Guid> playerIds, MainRoleType mainRoleType)
    {
        var entry = new AssignRoleLogEntry()
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = _gameSessionKernel.PhaseStateCache.GetCurrentPhase(),
            PlayerIds = playerIds,
            AssignedMainRole = mainRoleType
        };

        _gameSessionKernel.AddEntryAndUpdateState(entry);
    }

    internal void ApplyStatusEffect(StatusEffectTypes effectType, Guid playerId)
    {
        var entry = new StatusEffectLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = _gameSessionKernel.PhaseStateCache.GetCurrentPhase(),
            PlayerId = playerId,
            EffectType = effectType
        };
        _gameSessionKernel.AddEntryAndUpdateState(entry);
	}

	internal void PerformDayVote(Guid? reportedOutcomePlayerId)
    {
        var entry = new VoteOutcomeReportedLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = _gameSessionKernel.PhaseStateCache.GetCurrentPhase(),
            ReportedOutcomePlayerId = reportedOutcomePlayerId ?? Guid.Empty
        };

        _gameSessionKernel.AddEntryAndUpdateState(entry);
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

        _gameSessionKernel.AddEntryAndUpdateState(entry);

    }

    internal void VictoryConditionMet(Team winningTeam, string description)
    {
        var entry = new VictoryConditionMetLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = _gameSessionKernel.PhaseStateCache.GetCurrentPhase(),
            WinningTeam = winningTeam,
            ConditionDescription = description
        };

		_gameSessionKernel.AddEntryAndUpdateState(entry);
	}

	#endregion

	#region Private helpers

    private void PerformNightActionCore(NightActionType type, List<Guid>? targetIds)
    {
        var entry = new NightActionLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = TurnNumber,
            CurrentPhase = _gameSessionKernel.PhaseStateCache.GetCurrentPhase(),
            ActionType = type,
            TargetIds = targetIds,
        };

		_gameSessionKernel.AddEntryAndUpdateState(entry);
	}

    

    #endregion

}
