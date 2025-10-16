using Werewolves.Core.Enums;
using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Resources;
using System.Linq;
using System;
using System.Collections.Generic;
using Werewolves.Core.Extensions;
using Werewolves.Core.Models.StateMachine;

namespace Werewolves.Core.Roles;

/// <summary>
/// Represents the Simple Werewolf role.
/// </summary>
public class SimpleWerewolfRole : IRole
{
    public RoleType RoleType => RoleType.SimpleWerewolf;
    public string Name => GameStrings.SimpleWerewolfRoleName;
    public string Description => GameStrings.SimpleWerewolfRoleDescription;

    public bool RequiresNight1Identification() => true; // Need moderator to ID who the WWs are

    /// <summary>
    /// Generates the instruction to identify all players holding the Simple Werewolf role.
    /// </summary>
    public ModeratorInstruction GenerateIdentificationInstructions(GameSession session)
    {
        int werewolfCount = session.RolesInPlay.Count(rt => rt == RoleType.SimpleWerewolf);
        return new ModeratorInstruction
        {
            PublicText = string.Format(GameStrings.IdentifyWerewolvesPrompt, werewolfCount),
            ExpectedInputType = ExpectedInputType.PlayerSelectionMultiple,
            SelectablePlayerIds = session.Players.Keys.ToList() // Select from all players
        };
    }

	/// <summary>
	/// Processes the moderator input for identifying Werewolves on Night 1.
	/// Validates the count and assigns the role.
	/// </summary>
	public PhaseHandlerResult ProcessIdentificationInput(GameSession session, ModeratorInput input)
	{
		int expectedWerewolfCount = session.GetRoleCount(RoleType.SimpleWerewolf);

		if (input.SelectedPlayerIds?.Count != expectedWerewolfCount)
		{
			string errorMsg = string.Format(GameStrings.WerewolfIdentifyInvalidPlayerCount, expectedWerewolfCount, input.SelectedPlayerIds?.Count ?? 0);
			return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, errorMsg));
		}

		// Assign roles
		var identifiedPlayers = new List<Player>();
		foreach (var playerId in input.SelectedPlayerIds)
		{
			if (!session.Players.TryGetValue(playerId, out var player))
			{
				return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidInput,
					GameErrorCode.InvalidInput_PlayerIdNotFound,
					string.Format(GameStrings.PlayerIdNotFound, playerId)));
			}
			if (player.Health != PlayerHealth.Alive)
			{
				return PhaseHandlerResult.Failure(new GameError(ErrorType.RuleViolation,
					GameErrorCode.RuleViolation_TargetIsDead,
					string.Format(GameStrings.TargetIsDeadError, player.Name)));
			}
			if (player.Role != null)
			{
				// Already assigned during this ID phase or previously? Indicates unexpected state.
				return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidOperation,
					GameErrorCode.InvalidOperation_UnexpectedInput,
					string.Format(GameStrings.WerewolfIdentifyPlayerAlreadyHasRole, player.Name)));
			}
			player.Role = this;
			identifiedPlayers.Add(player);
		}

		var confirmationInstruction = new ModeratorInstruction
		{
			ExpectedInputType = ExpectedInputType.None // No immediate input needed after success
		};
		return PhaseHandlerResult.SuccessStayInPhase(confirmationInstruction); // GameService will proceed to generate action instruction
	}

	/// <summary>
	/// Generates the instruction for Werewolves to select a victim.
	/// </summary>
	public ModeratorInstruction GenerateNightInstructions(GameSession session)
	{
		var livingPlayers = session.Players.Values
			.Where(p => p.Health == PlayerHealth.Alive)
			.ToList();

		// Find werewolves based on their assigned Role
		var werewolves = livingPlayers.WithRole(RoleType.SimpleWerewolf)
			.Select(p => p.Id)
			.ToHashSet();

		var potentialVictims = livingPlayers
			.Where(p => !werewolves.Contains(p.Id))
			.Select(p => p.Id)
			.ToList();

		return new ModeratorInstruction
		{
			PublicText = GameStrings.WerewolvesChooseVictimPrompt,
			ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
			AffectedPlayerIds = werewolves.ToList(), // Indicate who this applies to
			SelectablePlayerIds = potentialVictims
		};
	}

	/// <summary>
	/// Processes the Werewolves' chosen victim.
	/// </summary>
	/// <returns>A PhaseHandlerResult indicating success and logging the action, or failure.</returns>
	public PhaseHandlerResult ProcessNightAction(GameSession session, ModeratorInput input)
    {
        if (input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidInput,
                GameErrorCode.InvalidInput_InvalidPlayerSelectionCount,
                GameStrings.ExactlyOnePlayerMustBeSelected));
        }

        Guid targetPlayerId = input.SelectedPlayerIds[0];

        // Validate Target (re-use validation logic or call a helper)
        if (!session.Players.TryGetValue(targetPlayerId, out var targetPlayer))
        {
            return PhaseHandlerResult.Failure(new GameError(ErrorType.InvalidInput,
                GameErrorCode.InvalidInput_PlayerIdNotFound,
                string.Format(GameStrings.PlayerIdNotFound, targetPlayerId)));
        }
        if (targetPlayer.Health == PlayerHealth.Dead)
        {
            return PhaseHandlerResult.Failure(new GameError(ErrorType.RuleViolation,
                GameErrorCode.RuleViolation_TargetIsDead,
                string.Format(GameStrings.TargetIsDeadError, targetPlayer.Name)));
        }
        // Cannot target allies (requires knowing who the werewolves are)
        var werewolves = session.Players.Values
        .Where(p => p.Role?.RoleType == RoleType.SimpleWerewolf)
        .Select(p => p.Id)
        .ToHashSet();

        if (werewolves.Contains(targetPlayerId))
        {
            return PhaseHandlerResult.Failure(new GameError(ErrorType.RuleViolation,
                GameErrorCode.RuleViolation_TargetIsAlly,
                string.Format(GameStrings.TargetIsAllyError, targetPlayer.Name)));
        }

        // Log the action directly to the main history log
        session.GameHistoryLog.Add(new NightActionLogEntry
        {
            ActorId = Guid.Empty, // Represents the collective Werewolf action for now
            TargetId = targetPlayerId,
            ActionType = NightActionType.WerewolfVictimSelection, // Use Enum
            TurnNumber = session.TurnNumber,
            CurrentPhase = session.GamePhase
        });

        return PhaseHandlerResult.SuccessTransitionUseDefault(PhaseTransitionReason.RoleActionComplete);
    }

    public ModeratorInstruction GenerateDayInstructions(GameSession session) => throw new NotImplementedException();

    public PhaseHandlerResult ProcessDayAction(GameSession session, ModeratorInput input) =>
        throw new NotImplementedException();
}