using Werewolves.Core.Enums;
using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Resources;
using System.Linq;
using System;
using System.Collections.Generic;

namespace Werewolves.Core.Roles;

/// <summary>
/// Represents the Simple Werewolf role.
/// </summary>
public class SimpleWerewolfRole : IRole
{
    public RoleType RoleType => RoleType.SimpleWerewolf;
    public string Name => GameStrings.SimpleWerewolfRoleName;
    public string Description => GameStrings.SimpleWerewolfRoleDescription;

    // Wakeup order (e.g., 10, relatively early but after potential info roles)
    public int GetNightWakeUpOrder() => 10;

    public bool RequiresNight1Identification() => true; // Need moderator to ID who the WWs are

    /// <summary>
    /// Generates the instruction to identify all players holding the Simple Werewolf role.
    /// </summary>
    public ModeratorInstruction? GenerateIdentificationInstructions(GameSession session)
    {
        int werewolfCount = session.RolesInPlay.Count(rt => rt == RoleType.SimpleWerewolf);
        return new ModeratorInstruction
        {
            InstructionText = string.Format(GameStrings.IdentifyWerewolvesPrompt, werewolfCount),
            ExpectedInputType = ExpectedInputType.PlayerSelectionMultiple,
            SelectablePlayerIds = session.Players.Keys.ToList() // Select from all players
        };
    }

    /// <summary>
    /// Generates the instruction for Werewolves to select a victim.
    /// </summary>
    public ModeratorInstruction? GenerateNightInstructions(GameSession session)
    {
        var livingPlayers = session.Players.Values
            .Where(p => p.Status == PlayerStatus.Alive)
            .ToList();

        // Find werewolves based on their assigned Role
        var werewolves = livingPlayers
            .Where(p => p.Role?.RoleType == RoleType.SimpleWerewolf)
            .Select(p => p.Id)
            .ToHashSet();

        var potentialVictims = livingPlayers
            .Where(p => !werewolves.Contains(p.Id))
            .Select(p => p.Id)
            .ToList();

        return new ModeratorInstruction
        {
            InstructionText = GameStrings.WerewolvesChooseVictimPrompt,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
            AffectedPlayerIds = werewolves.ToList(), // Indicate who this applies to
            SelectablePlayerIds = potentialVictims
        };
    }

    /// <summary>
    /// Processes the Werewolves' chosen victim.
    /// Adds the choice to the temporary NightActionsLog.
    /// </summary>
    /// <returns>A ProcessResult indicating success and logging the action, or failure.</returns>
    public ProcessResult ProcessNightAction(GameSession session, ModeratorInput input)
    {
        if (input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                GameErrorCode.InvalidInput_InvalidPlayerSelectionCount,
                GameStrings.ExactlyOnePlayerMustBeSelected));
        }

        Guid targetPlayerId = input.SelectedPlayerIds[0];

        // Validate Target (re-use validation logic or call a helper)
        if (!session.Players.TryGetValue(targetPlayerId, out var targetPlayer))
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                GameErrorCode.InvalidInput_PlayerIdNotFound,
                string.Format(GameStrings.PlayerIdNotFound, targetPlayerId)));
        }
        if (targetPlayer.Status == PlayerStatus.Dead)
        {
            return ProcessResult.Failure(new GameError(ErrorType.RuleViolation,
                GameErrorCode.RuleViolation_TargetIsDead,
                string.Format(GameStrings.TargetIsDeadError, targetPlayer.Name)));
        }
		// Cannot target allies (requires knowing who the werewolves are)
			var werewolves = session.Players.Values
			.Where(p => p.Role?.RoleType == RoleType.SimpleWerewolf)
			.Select(p => p.Id)
			.ToHashSet();

        if(werewolves.Contains(targetPlayerId))
		{
			return ProcessResult.Failure(new GameError(ErrorType.RuleViolation,
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
            Phase = session.GamePhase
        });

        // Return success, indicating the action is logged but resolution happens later.
        // Generate a confirmation instruction for the moderator.
        var confirmation = new ModeratorInstruction
        {
            InstructionText = string.Format(GameStrings.WerewolvesChoiceRecorded, targetPlayer.Name),
            ExpectedInputType = ExpectedInputType.None // Service will generate next step
        };
        return ProcessResult.Success(confirmation);
    }

    /// <summary>
    /// Processes the moderator input for identifying Werewolves on Night 1.
    /// Validates the count and assigns the role.
    /// </summary>
    public ProcessResult ProcessIdentificationInput(GameSession session, ModeratorInput input)
    {
        int expectedWerewolfCount = session.GetRoleCount(RoleType.SimpleWerewolf);

        // TODO: Determine expected count dynamically based on game setup/rules
        if (input.SelectedPlayerIds.Count != expectedWerewolfCount)
        {
            string errorMsg = string.Format(GameStrings.WerewolfIdentifyInvalidPlayerCount, expectedWerewolfCount, input.SelectedPlayerIds.Count);
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput, GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, errorMsg));
        }

        // Assign roles
        var identifiedPlayers = new List<Player>();
        foreach (var playerId in input.SelectedPlayerIds)
        {
            if (!session.Players.TryGetValue(playerId, out var player))
            {
                return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                    GameErrorCode.InvalidInput_PlayerIdNotFound,
                    string.Format(GameStrings.PlayerIdNotFound, playerId)));
            }
            if (player.Status != PlayerStatus.Alive)
            {
                return ProcessResult.Failure(new GameError(ErrorType.RuleViolation,
                    GameErrorCode.RuleViolation_TargetIsDead,
                    string.Format(GameStrings.TargetIsDeadError, player.Name)));
            }
            if (player.Role != null)
            {
                // Already assigned during this ID phase or previously? Indicates unexpected state.
                return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation,
                    GameErrorCode.InvalidOperation_UnexpectedInput,
                    string.Format(GameStrings.WerewolfIdentifyPlayerAlreadyHasRole, player.Name)));
            }
            player.Role = this;
            identifiedPlayers.Add(player);
        }

        // Identification successful
        // Note: Logging is handled by GameService after this returns success.
        // Return a success result with a confirmation instruction (optional)
        var confirmationInstruction = new ModeratorInstruction
        {
            InstructionText = string.Format(GameStrings.WerewolfIdentifySuccess, identifiedPlayers.Count), // Simple confirmation
            ExpectedInputType = ExpectedInputType.None // No immediate input needed after success
        };
        return ProcessResult.Success(confirmationInstruction); // GameService will proceed to generate action instruction
    }

    public ModeratorInstruction? GenerateDayInstructions(GameSession session) => null;

    public ProcessResult ProcessDayAction(GameSession session, ModeratorInput input)
    {
        return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, GameStrings.SimpleWerewolfNoDayAction));
    }
}