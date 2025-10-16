using System; // Required for Tuple
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Werewolves.Core.Enums;
using Werewolves.Core.Extensions;
using Werewolves.Core.Interfaces; // Required for Dictionary, List, HashSet
using Werewolves.Core.Models.Log;
using Werewolves.Core.Models.StateMachine;
using Werewolves.Core.Roles;
using Werewolves.Core.Services;

namespace Werewolves.Core.Models;

/// <summary>
/// Represents the tracked state of a single ongoing game.
/// </summary>
public class GameSession
{
	

	// --- Night Order Configuration ---
	// Defines the strict order in which roles with night actions wake up.
	private static readonly List<RoleType> _masterNightWakeUpOrder =
    [
        RoleType.SimpleWerewolf,
        RoleType.Seer
    ];

    // Defines roles that explicitly have no night action.
    private static readonly HashSet<RoleType> _rolesWithoutNightAction =
    [
        RoleType.SimpleVillager
        // Add other roles without night actions here
    ];
    // --- End Night Order Configuration ---

	public Guid Id { get; } = Guid.NewGuid();
    public required Dictionary<Guid, Player> Players { get; init; }
    public GamePhase GamePhase { get; private set; }
    public int TurnNumber { get; private set; } = 0;
    public Team? WinningTeam { get; internal set; }

    // Core collections defined in Phase 0 roadmap
    public List<Guid> PlayerSeatingOrder { get; init;  }
    public List<RoleType> RolesInPlay { get; init; }
    public List<RoleType> ActiveNightRoles { get; init;  }
	public List<GameLogEntryBase> GameHistoryLog { get; } = new();

    // --- Phase 1 Additions ---
    /// <summary>
    /// Stores the reported outcome of the current day vote phase temporarily.
    /// Null if no vote outcome reported yet, Guid.Empty for a reported tie, PlayerId otherwise.
    /// </summary>
    public Guid? PendingVoteOutcome { get; private set; } = null; // Using null initially, Guid.Empty for tie

	/// <summary>
	/// Tracks the index of the role currently acting within the night wake-up order.
	/// Reset at the beginning of each Night phase.
	/// </summary>
	private int _currentNightActingRoleIndex { get; set; } = -1;

	public bool IsCurrentNightComplete => _currentNightActingRoleIndex >= ActiveNightRoles.Count - 1;

	public IRole CurrentNightRole
	{
		get
		{
			RoleType? roleType = _currentNightActingRoleIndex == -1 ? null : ActiveNightRoles[_currentNightActingRoleIndex];

			if (roleType.HasValue && GameService._roleImplementations.TryGetValue(roleType.Value, out var role))
			{
				return role;
			}

			throw new Exception($"Error: Bad role configuration for acting role index {_currentNightActingRoleIndex}");
		}
	}

	public IRole? AdvanceToNextNightRole()
	{
		while (_currentNightActingRoleIndex < ActiveNightRoles.Count)
		{
			if (Players.WithRole(CurrentNightRole.RoleType).Any())
				return CurrentNightRole;

			_currentNightActingRoleIndex++;
		}

		return null;
	}

	// --- End Phase 1 Additions ---

	// Pending instruction for the moderator
	public ModeratorInstruction? PendingModeratorInstruction { get; set; }

    public int GetRoleCount(RoleType roleType) => RolesInPlay.Count(x => x == roleType);
    public int GetAliveRoleCount(RoleType roleType)
    {
        var totalRoleCount = GetRoleCount(roleType);

        // App should always know the role of dead players.
        var killedRoleCount = Players.WithRole(roleType).WithHealth(PlayerHealth.Dead).Count();

        return totalRoleCount - killedRoleCount;
    }

    public GamePhase PreviousPhase => FindLogEntries<PhaseTransitionLogEntry>().LastOrDefault()?.PreviousPhase ?? GamePhase.Setup;

    [SetsRequiredMembers]
    public GameSession(Dictionary<Guid, Player> players, List<Guid> playerSeatingOrder, List<RoleType> rolesInPlay, GamePhase gamePhase, int turnNumber)
    {
        // Validate that all roles in play are accounted for in the master lists
        foreach (var roleInPlay in rolesInPlay)
        {
            if (!_masterNightWakeUpOrder.Contains(roleInPlay) && !_rolesWithoutNightAction.Contains(roleInPlay))
            {
                throw new InvalidOperationException($"Configuration Error: Role '{roleInPlay}' is in play but is not defined in either the master night wake-up order or the list of roles without night actions in GameService.");
            }
        }

		Players = players;
        PlayerSeatingOrder = playerSeatingOrder;
        RolesInPlay = rolesInPlay;
        ActiveNightRoles = _masterNightWakeUpOrder.Intersect(rolesInPlay).ToList();
		GamePhase = gamePhase;
        TurnNumber = turnNumber;
	}

	/// <summary>
	/// Searches the game history log for entries of a specific type, with optional filters.
	/// </summary>
	/// <typeparam name="TLogEntry">The type of log entry to search for, must derive from GameLogEntryBase.</typeparam>
	/// <param name="turnsAgo">Optional. Filters logs to a specific turn relative to the current turn. 0 for the current turn, 1 for the previous turn, etc.</param>
	/// <param name="phase">Optional. Filters logs to a specific game phase.</param>
	/// <param name="filter">Optional. A lambda function to apply additional filtering logic specific to the TLogEntry type.</param>
	/// <returns>An enumerable collection of matching log entries.</returns>
	public IEnumerable<TLogEntry> FindLogEntries<TLogEntry>(int? turnsAgo = null, GamePhase? phase = null, Func<TLogEntry, bool>? filter = null) where TLogEntry : GameLogEntryBase
    {
        IEnumerable<TLogEntry> query = GameHistoryLog.OfType<TLogEntry>();

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

	public void StartNewTurn()
	{
		TurnNumber++;
		_currentNightActingRoleIndex = -1; // Reset the acting role index for the new night phase
	}

	public PhaseHandlerResult TransitionToPhase(GamePhase newPhase, PhaseTransitionReason reason, ModeratorInstruction instruction)
	{
		TransitionToPhaseCore(newPhase, reason);

		return PhaseHandlerResult.SuccessTransition(instruction, reason);
	}

	public PhaseHandlerResult TransitionToPhaseDefaultInstruction(GamePhase newPhase, PhaseTransitionReason reason)
	{
		TransitionToPhaseCore(newPhase, reason);

		return PhaseHandlerResult.SuccessTransitionUseDefault(reason);
	}

	private void TransitionToPhaseCore(GamePhase newPhase, PhaseTransitionReason reason)
	{
		var oldPhase = GamePhase;
		GamePhase = newPhase;

		GameHistoryLog.Add(new PhaseTransitionLogEntry
		{
			Timestamp = DateTimeOffset.UtcNow,
			TurnNumber = TurnNumber,
			PreviousPhase = oldPhase,
			CurrentPhase = newPhase,
			Reason = reason
		});
	}

	public void RecordDayVote(Guid playerGuid)
	{
		PendingVoteOutcome = playerGuid;
		LogVoteOutcomeReported(playerGuid);
	}

	public void ClearPendingVoteOutcome() => PendingVoteOutcome = null;

	#region Logs

	public void LogInitialAssignment(Guid playerId, RoleType roleType)
	{
		GameHistoryLog.Add(new InitialRoleAssignmentLogEntry
		{
			Timestamp = DateTime.UtcNow,
			TurnNumber = TurnNumber,
			CurrentPhase = GamePhase,
			PlayerId = playerId,
			AssignedRole = roleType
		});
	}

	public void LogInitialAssignments(List<Guid> playerIds, RoleType roleType)
	{
		GameHistoryLog.Add(new InitialRoleAssignmentsLogEntry
		{
			Timestamp = DateTime.UtcNow,
			TurnNumber = TurnNumber,
			CurrentPhase = GamePhase,
			PlayerIds = playerIds,
			AssignedRole = roleType
		});
	}

	public void LogRoleReveal(Guid playerId, RoleType roleType)
	{
		GameHistoryLog.Add(new RoleRevealedLogEntry
		{
			Timestamp = DateTime.UtcNow,
			TurnNumber = TurnNumber,
			CurrentPhase = GamePhase,
			PlayerId = playerId,
			RevealedRole = roleType
		});
	}

	public void LogElimination(Guid playerId, EliminationReason reason)
	{
		GameHistoryLog.Add(new PlayerEliminatedLogEntry
		{
			Timestamp = DateTime.UtcNow,
			TurnNumber = TurnNumber,
			CurrentPhase = GamePhase,
			PlayerId = playerId,
			Reason = reason
		});
	}

	public void LogVoteOutcomeReported(Guid reportedOutcomePlayerId)
	{
		GameHistoryLog.Add(new VoteOutcomeReportedLogEntry
		{
			Timestamp = DateTime.UtcNow,
			TurnNumber = TurnNumber,
			CurrentPhase = GamePhase,
			ReportedOutcomePlayerId = reportedOutcomePlayerId // Guid.Empty for tie
		});
	}

	public void LogVoteResolved(Guid? eliminatedPlayerId, bool wasTie)
	{
		GameHistoryLog.Add(new VoteResolvedLogEntry
		{
			Timestamp = DateTime.UtcNow,
			TurnNumber = TurnNumber,
			CurrentPhase = GamePhase,
			EliminatedPlayerId = eliminatedPlayerId,
			WasTie = wasTie
		});
	}

	#endregion
}