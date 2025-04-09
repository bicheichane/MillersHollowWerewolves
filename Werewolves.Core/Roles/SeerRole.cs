using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Enums;
using Werewolves.Core.Resources;
using System.Linq;

namespace Werewolves.Core.Roles;

public class SeerRole : IRole
{
    public RoleType RoleType => RoleType.Seer;
    public string Name => GameStrings.RoleSeerName;
    public string Description => GameStrings.RoleSeerDescription;

    public int GetNightWakeUpOrder() => 5; // Example: Before Defender/WW

    public bool RequiresNight1Identification() => true;

    public ModeratorInstruction? GenerateIdentificationInstructions(GameSession session)
    {
        var assignablePlayers = session.Players.Values
            .Where(p => p.Status == PlayerStatus.Alive && p.Role == null)
            .Select(p => p.Id)
            .ToList();

        return new ModeratorInstruction
        {
            InstructionText = GameStrings.InstructionIdentifySeer,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
            SelectablePlayerIds = assignablePlayers
        };
    }

    public ProcessResult ProcessIdentificationInput(GameSession session, ModeratorInput input)
    {
        if (input.InputTypeProvided != ExpectedInputType.PlayerSelectionSingle || input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, "Expected exactly one player ID for Seer identification."));
        }

        var seerId = input.SelectedPlayerIds.First();
        if (!session.Players.TryGetValue(seerId, out var seerPlayer) || seerPlayer.Status != PlayerStatus.Alive)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_PlayerIdNotFound, "Selected player for Seer not found or is not alive."));
        }
        if (seerPlayer.Role != null)
        {
            return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_TargetIsInvalid, "Selected player already has a role assigned."));
        }

        seerPlayer.AssignRole(this);

        session.GameHistoryLog.Add(new InitialRoleLogAssignment
        {
            PlayerId = seerId,
            AssignedRole = this.RoleType,
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = session.TurnNumber,
            Phase = session.GamePhase
        });

        return ProcessResult.Success();
    }

    public ModeratorInstruction? GenerateNightInstructions(GameSession session)
    {
        var seerPlayer = session.Players.Values.FirstOrDefault(p => p.Role is SeerRole && p.Status == PlayerStatus.Alive);
        if (seerPlayer == null) return null;

        var potentialTargets = session.Players.Values
            .Where(p => p.Status == PlayerStatus.Alive && p.Id != seerPlayer.Id)
            .Select(p => p.Id)
            .ToList();

        if (!potentialTargets.Any())
        {
            return new ModeratorInstruction
            {
                InstructionText = GameStrings.InstructionSeerNoTargets,
                ExpectedInputType = ExpectedInputType.Confirmation
            };
        }

        return new ModeratorInstruction
        {
            InstructionText = GameStrings.InstructionSeerChooseTarget,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
            SelectablePlayerIds = potentialTargets,
            AffectedPlayerIds = new List<Guid> { seerPlayer.Id }
        };
    }

    public ProcessResult ProcessNightAction(GameSession session, ModeratorInput input)
    {
        var seerPlayer = session.Players.Values.FirstOrDefault(p => p.Role is SeerRole && p.Status == PlayerStatus.Alive);
        if (seerPlayer == null) return ProcessResult.Failure(GameError.InvalidOperation(GameErrorCode.InvalidInput_PlayerIdNotFound, "Seer role actor not found during night action processing."));

        if (input.InputTypeProvided == ExpectedInputType.Confirmation)
        {
            return ProcessResult.Success(new ModeratorInstruction
            {
                InstructionText = GameStrings.InfoSeerSkippedNoTargets,
                ExpectedInputType = ExpectedInputType.None
            });
        }

        if (input.InputTypeProvided != ExpectedInputType.PlayerSelectionSingle || input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, "Expected exactly one player ID for Seer target."));
        }

        var targetId = input.SelectedPlayerIds.First();
        if (!session.Players.TryGetValue(targetId, out var targetPlayer) || targetPlayer.Status != PlayerStatus.Alive)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_PlayerIdNotFound, "Selected target for Seer not found or is not alive."));
        }

        if (targetId == seerPlayer.Id)
        {
            return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_TargetIsSelf, "Seer cannot target themselves."));
        }

        session.GameHistoryLog.Add(new SeerViewAttemptLogEntry
        {
            ActorId = seerPlayer.Id,
            TargetId = targetId,
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = session.TurnNumber,
            Phase = session.GamePhase
        });

        string revealedRoleName = targetPlayer.Role?.Name ?? GameStrings.RoleUnknownName;

        var resultInstruction = new ModeratorInstruction
        {
            InstructionText = string.Format(GameStrings.InfoSeerResult, targetPlayer.Name, revealedRoleName),
            ExpectedInputType = ExpectedInputType.None
        };

        return ProcessResult.Success(resultInstruction);
    }

    public ModeratorInstruction? GenerateDayInstructions(GameSession session)
    {
        return null; // No day action
    }

    public ProcessResult ProcessDayAction(GameSession session, ModeratorInput input)
    {
        return ProcessResult.Failure(GameError.InvalidOperation(GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, "Seer has no day actions."));
    }
} 