using System;
using System.Collections.Generic;
using System.Linq;
using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Enums; // Correct namespace for enums
using Werewolves.Core.Models.Log;
using Werewolves.Core.Models.LogEntries;
using Werewolves.Core.Resources;

namespace Werewolves.Core.Roles;

public class SeerRole : IRole
{
    // Requires RoleType.Seer to be defined in Werewolves.Core.Enums.RoleType
    public RoleType RoleType => RoleType.Seer;
    // Requires GameStrings.SeerRoleName to be defined in Resources/GameStrings.resx
    public string Name => GameStrings.SeerRoleName;
    // Requires GameStrings.SeerRoleDescription to be defined in Resources/GameStrings.resx
    public string Description => GameStrings.SeerRoleDescription;

    public bool RequiresNight1Identification() => true;

    // GetNightWakeUpOrder removed - Not part of IRole, should be handled by GameService.

    public ModeratorInstruction? GenerateIdentificationInstructions(GameSession session)
    {
        var selectablePlayers = session.Players.Values
                                       .Where(p => p.Status == PlayerStatus.Alive)
                                       .Select(p => p.Id)
                                       .ToList();

        return new ModeratorInstruction
        {
            // Requires GameStrings.SeerIdentificationPrompt
            InstructionText = GameStrings.SeerIdentificationPrompt,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
            SelectablePlayerIds = selectablePlayers
        };
    }

    public ProcessResult ProcessIdentificationInput(GameSession session, ModeratorInput input)
    {
        if (input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, "Exactly one player must be selected as the Seer."));
        }

        var seerId = input.SelectedPlayerIds.First();
        if (!session.Players.TryGetValue(seerId, out var seerPlayer))
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                GameErrorCode.InvalidInput_PlayerIdNotFound, $"Selected Seer ID '{seerId}' not found."));
        }

        if (seerPlayer.RoleType != null)
        {
            // Requires GameErrorCode.RuleViolation_PlayerAlreadyHasRole to be defined in Werewolves.Core.Enums.GameErrorCode
            return ProcessResult.Failure(new GameError(ErrorType.RuleViolation,
                GameErrorCode.RuleViolation_PlayerAlreadyHasRole, $"Player {seerPlayer.Name} already has a role assigned."));
        }

        seerPlayer.RoleType = this;

        return ProcessResult.Success();
    }

    public ModeratorInstruction? GenerateNightInstructions(GameSession session)
    {
        var seerPlayer = session.Players.Values.FirstOrDefault(p => p.RoleType is SeerRole && p.Status == PlayerStatus.Alive);
        if (seerPlayer == null) { return null; }

        var potentialTargets = session.Players.Values
                                    .Where(p => p.Status == PlayerStatus.Alive && p.Id != seerPlayer.Id)
                                    .Select(p => p.Id)
                                    .ToList();

        if (!potentialTargets.Any())
        {
            return new ModeratorInstruction
            {
                // Requires GameStrings.SeerNoTargetsAvailable
                InstructionText = GameStrings.SeerNoTargetsAvailable,
                ExpectedInputType = ExpectedInputType.Confirmation
            };
        }

        return new ModeratorInstruction
        {
            // Requires GameStrings.SeerNightActionPrompt
            InstructionText = GameStrings.SeerNightActionPrompt,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
            SelectablePlayerIds = potentialTargets,
            AffectedPlayerIds = new List<Guid> { seerPlayer.Id }
        };
    }

    public ProcessResult ProcessNightAction(GameSession session, ModeratorInput input)
    {
        var seerPlayer = session.Players.Values.FirstOrDefault(p => p.RoleType is SeerRole && p.Status == PlayerStatus.Alive);
        if (seerPlayer == null)
        {
            return ProcessResult.Failure(new GameError(ErrorType.Unknown,
                GameErrorCode.Unknown_InternalError, "Seer player not found during night action processing."));
        }

        if (input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, "Exactly one player must be selected as the Seer's target."));
        }

        var targetId = input.SelectedPlayerIds.First();
        if (!session.Players.TryGetValue(targetId, out var targetPlayer))
        {
            return ProcessResult.Failure(new GameError(ErrorType.InvalidInput,
                GameErrorCode.InvalidInput_PlayerIdNotFound, $"Selected target ID '{targetId}' not found."));
        }

        if (targetPlayer.Status != PlayerStatus.Alive)
        {
            return ProcessResult.Failure(new GameError(ErrorType.RuleViolation,
                GameErrorCode.RuleViolation_TargetIsDead, $"Target {targetPlayer.Name} is not alive."));
        }

        if (targetId == seerPlayer.Id)
        {
            return ProcessResult.Failure(new GameError(ErrorType.RuleViolation,
                GameErrorCode.RuleViolation_TargetIsSelf, "The Seer cannot target themselves."));
        }

        bool targetWakesWithWerewolves = DoesPlayerWakeWithWerewolves(targetPlayer, session);
        // Requires GameStrings.SeerResultWerewolfTeam and GameStrings.SeerResultNotWerewolfTeam
        string privateFeedbackFormat = targetWakesWithWerewolves ? GameStrings.SeerResultWerewolfTeam : GameStrings.SeerResultNotWerewolfTeam;

        var logEntry = new SeerViewAttemptLogEntry
        {
             Timestamp = DateTime.UtcNow,
             TurnNumber = session.TurnNumber,
             CurrentPhase = session.GamePhase,
             SeerPlayerId = seerPlayer.Id,
             TargetPlayerId = targetId,
             WasTargetAffiliatedWithWerewolves = targetWakesWithWerewolves
        };
        session.GameHistoryLog.Add(logEntry);

        var nextInstruction = new ModeratorInstruction
        {
            // Requires GameStrings.SeerCheckCompleteForTarget
            InstructionText = string.Format(GameStrings.SeerCheckCompleteForTarget, targetPlayer.Name),
            PrivateModeratorInfo = string.Format(privateFeedbackFormat, targetPlayer.Name),
            ExpectedInputType = ExpectedInputType.None
        };

        return ProcessResult.Success(nextInstruction);
    }

    private bool DoesPlayerWakeWithWerewolves(Player player, GameSession session)
    {
        if (player.Status != PlayerStatus.Alive) { 
          return false; 
        }

        // Requires PlayerState.IsInfected property to exist
        //if (player.State.IsInfected) { return true; }

        // TODO: Add checks for Wild Child, Wolf Hound, Events in later phases

        if (player.RoleType != null)
        {
            // Requires various RoleType members (SimpleWerewolf, BigBadWolf etc.) to be defined
            return player.RoleType.RoleType switch
            {
                RoleType.SimpleWerewolf => true,
                //RoleType.BigBadWolf => true,
                //RoleType.WhiteWerewolf => true,
                //RoleType.AccursedWolfFather => true,
                _ => false
            };
        }

        return false;
    }

    public ModeratorInstruction? GenerateDayInstructions(GameSession session) => null;
    public ProcessResult ProcessDayAction(GameSession session, ModeratorInput input) =>
        ProcessResult.Failure(new GameError(ErrorType.InvalidOperation,
            GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, "Seer does not have day actions."));
} 