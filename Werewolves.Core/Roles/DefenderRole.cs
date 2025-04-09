using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Enums;
using Werewolves.Core.Resources;
using System.Linq;

namespace Werewolves.Core.Roles;

public class DefenderRole : IRole
{
    public RoleType RoleType => RoleType.Defender;
    public string Name => GameStrings.RoleDefenderName;
    public string Description => GameStrings.RoleDefenderDescription;

    public int GetNightWakeUpOrder() => 10; // Example: Before WW, after Seer

    public bool RequiresNight1Identification() => true;

    public ModeratorInstruction? GenerateIdentificationInstructions(GameSession session)
    {
        var assignablePlayers = session.Players.Values
            .Where(p => p.Status == PlayerStatus.Alive && p.Role == null)
            .Select(p => p.Id)
            .ToList();

        return new ModeratorInstruction
        {
            InstructionText = GameStrings.InstructionIdentifyDefender,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
            SelectablePlayerIds = assignablePlayers
        };
    }

    public ProcessResult ProcessIdentificationInput(GameSession session, ModeratorInput input)
    {
        if (input.InputTypeProvided != ExpectedInputType.PlayerSelectionSingle || input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, "Expected exactly one player ID for Defender identification."));
        }

        var defenderId = input.SelectedPlayerIds.First();
        if (!session.Players.TryGetValue(defenderId, out var defenderPlayer) || defenderPlayer.Status != PlayerStatus.Alive)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_PlayerIdNotFound, "Selected player for Defender not found or is not alive."));
        }
        if (defenderPlayer.Role != null)
        {
            return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_TargetIsInvalid, "Selected player already has a role assigned."));
        }

        // Assign role
        defenderPlayer.AssignRole(this);

        // Log identification
        session.GameHistoryLog.Add(new InitialRoleLogAssignment
        {
            PlayerId = defenderId,
            AssignedRole = this.RoleType,
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = session.TurnNumber,
            Phase = session.GamePhase
        });

        return ProcessResult.Success(); // GameService handles next prompt
    }

    public ModeratorInstruction? GenerateNightInstructions(GameSession session)
    {
        var defenderPlayer = session.Players.Values.FirstOrDefault(p => p.Role is DefenderRole && p.Status == PlayerStatus.Alive);
        if (defenderPlayer == null) return null;

        // Get potential targets (all living players, including self)
        var potentialTargets = session.Players.Values
            .Where(p => p.Status == PlayerStatus.Alive)
            .Select(p => p.Id)
            .ToList();

        // Add context about who cannot be protected (last night's target)
        string instructionText = GameStrings.InstructionDefenderChooseTarget;
        if (session.LastProtectedPlayerId.HasValue && session.Players.TryGetValue(session.LastProtectedPlayerId.Value, out var lastProtectedPlayer))
        {
            instructionText += " " + string.Format(GameStrings.InstructionDefenderCannotRepeat, lastProtectedPlayer.Name);
        }

        return new ModeratorInstruction
        {
            InstructionText = instructionText,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
            SelectablePlayerIds = potentialTargets,
            AffectedPlayerIds = new List<Guid> { defenderPlayer.Id }
        };
    }

    public ProcessResult ProcessNightAction(GameSession session, ModeratorInput input)
    {
        var defenderPlayer = session.Players.Values.FirstOrDefault(p => p.Role is DefenderRole && p.Status == PlayerStatus.Alive);
        if (defenderPlayer == null) return ProcessResult.Failure(GameError.InvalidOperation(GameErrorCode.InvalidInput_PlayerIdNotFound, "Defender role actor not found during night action processing."));

        if (input.InputTypeProvided != ExpectedInputType.PlayerSelectionSingle || input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, "Expected exactly one player ID for Defender target."));
        }

        var targetId = input.SelectedPlayerIds.First();
        if (!session.Players.TryGetValue(targetId, out var targetPlayer) || targetPlayer.Status != PlayerStatus.Alive)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_PlayerIdNotFound, "Selected target for Defender not found or is not alive."));
        }

        // Check the no-repeat rule
        if (targetId == session.LastProtectedPlayerId)
        {
            return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_DefenderRepeatTarget, 
                string.Format(GameStrings.ErrorDefenderCannotRepeat, targetPlayer.Name)));
        }

        // Log the choice
        session.GameHistoryLog.Add(new DefenderProtectionChoiceLogEntry
        {
            ActorId = defenderPlayer.Id,
            TargetId = targetId,
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = session.TurnNumber,
            Phase = session.GamePhase
        });

        // Update the session state - GameService resolution phase will use this
        session.ProtectedPlayerId = targetId;

        // Return confirmation instruction
        var resultInstruction = new ModeratorInstruction
        {
            InstructionText = string.Format(GameStrings.InfoDefenderProtectionSet, targetPlayer.Name),
            ExpectedInputType = ExpectedInputType.None // Signal completion
        };

        return ProcessResult.Success(resultInstruction);
    }

    public ModeratorInstruction? GenerateDayInstructions(GameSession session)
    {
        return null; // No day action
    }

    public ProcessResult ProcessDayAction(GameSession session, ModeratorInput input)
    {
        return ProcessResult.Failure(GameError.InvalidOperation(GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, "Defender has no day actions."));
    }
} 