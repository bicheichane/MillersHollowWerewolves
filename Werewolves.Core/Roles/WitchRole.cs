using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Enums;
using Werewolves.Core.Models.Log;
using Werewolves.Core.Resources;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis; // Added for TryGetValue

namespace Werewolves.Core.Roles;

public class WitchRole : IRole
{
    public RoleType RoleType => RoleType.Witch;
    public string Name => GameStrings.RoleWitchName;
    public string Description => GameStrings.RoleWitchDescription;

    public int GetNightWakeUpOrder() => 15; // Example: After WW

    public bool RequiresNight1Identification() => true;

    public ModeratorInstruction? GenerateIdentificationInstructions(GameSession session)
    {
        var assignablePlayers = session.Players.Values
            .Where(p => p.Status == PlayerStatus.Alive && p.Role == null)
            .Select(p => p.Id)
            .ToList();

        return new ModeratorInstruction
        {
            InstructionText = GameStrings.InstructionIdentifyWitch,
            ExpectedInputType = ExpectedInputType.PlayerSelectionSingle,
            SelectablePlayerIds = assignablePlayers
        };
    }

    public ProcessResult ProcessIdentificationInput(GameSession session, ModeratorInput input)
    {
        if (input.InputTypeProvided != ExpectedInputType.PlayerSelectionSingle || input.SelectedPlayerIds == null || input.SelectedPlayerIds.Count != 1)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_InvalidPlayerSelectionCount, "Expected exactly one player ID for Witch identification."));
        }

        var witchId = input.SelectedPlayerIds.First();
        if (!session.Players.TryGetValue(witchId, out var witchPlayer) || witchPlayer.Status != PlayerStatus.Alive)
        {
            return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_PlayerIdNotFound, "Selected player for Witch not found or is not alive."));
        }
        if (witchPlayer.Role != null)
        {
            return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_TargetIsInvalid, "Selected player already has a role assigned."));
        }

        witchPlayer.AssignRole(this);

        session.GameHistoryLog.Add(new InitialRoleLogAssignment
        {
            PlayerId = witchId,
            AssignedRole = this.RoleType,
            Timestamp = DateTimeOffset.UtcNow,
            TurnNumber = session.TurnNumber,
            Phase = session.GamePhase
        });

        return ProcessResult.Success();
    }

    public ModeratorInstruction? GenerateNightInstructions(GameSession session)
    {
        var witchPlayer = session.Players.Values.FirstOrDefault(p => p.Role is WitchRole && p.Status == PlayerStatus.Alive);
        if (witchPlayer == null) return null; // No witch in play or witch is dead.

        Guid? wwVictimId = FindWerewolfVictimThisTurn(session);
        bool canHeal = !witchPlayer.State.PotionsUsed.HasFlag(WitchPotionType.Healing) && wwVictimId.HasValue;
        bool canPoison = !witchPlayer.State.PotionsUsed.HasFlag(WitchPotionType.Poison);

        // State 1: Awaiting Poison Target decision
        if (session.IsAwaitingWitchPoisonTarget)
        {
            if (canPoison)
            {
                var potentialTargets = session.Players.Values
                    .Where(p => p.Status == PlayerStatus.Alive && p.Id != witchPlayer.Id && p.Id != wwVictimId)
                    .Select(p => p.Id)
                    .ToList();

                if (potentialTargets.Any())
                {
                    // Prompt for poison target or skip
                    return new ModeratorInstruction
                    {
                        InstructionText = GameStrings.InstructionWitchPoisonPrompt,
                        ExpectedInputType = ExpectedInputType.PlayerSelectionSingle, // Player ID for target
                        SelectablePlayerIds = potentialTargets,
                        // Add a 'Skip' mechanism - How? For now, rely on UI sending a non-PlayerSelection input if skip is chosen.
                        // Alternative: Use OptionSelection with player names + "Skip"? Complex.
                        // Alternative: Use PlayerSelectionSingleOrConfirmation? Not standard.
                        // Decision: Expect PlayerSelectionSingle for target, handle other inputs as skip in ProcessNightAction.
                         AffectedPlayerIds = new List<Guid> { witchPlayer.Id } // Indicate who is acting
                    };
                }
                else
                {
                    // No valid targets for poison, reset state and end turn
                    session.IsAwaitingWitchPoisonTarget = false;
                    return null;
                }
            }
            else
            {
                // Poison already used or somehow unavailable, reset state and end turn
                session.IsAwaitingWitchPoisonTarget = false;
                return null;
            }
        }

        // State 2: Initial Witch prompt (Heal or proceed to Poison/Skip)
        string wwVictimName = wwVictimId.HasValue && session.Players.TryGetValue(wwVictimId.Value, out var victim) ? victim.Name : GameStrings.InfoVictimNone;

        if (canHeal)
        {
            // Prompt for Heal Potion use
            return new ModeratorInstruction
            {
                InstructionText = string.Format(GameStrings.InstructionWitchHealPrompt, wwVictimName),
                ExpectedInputType = ExpectedInputType.Confirmation,
                AffectedPlayerIds = new List<Guid> { witchPlayer.Id }
            };
        }
        else if (canPoison)
        {
            // Heal not possible/available, but poison is. Transition directly to poison state.
            session.IsAwaitingWitchPoisonTarget = true;
            // Call self again to generate the poison prompt immediately
            return GenerateNightInstructions(session);
        }
        else
        {
            // No potions available, Witch does nothing.
            return null;
        }
    }

    public ProcessResult ProcessNightAction(GameSession session, ModeratorInput input)
    {
        var witchPlayer = session.Players.Values.FirstOrDefault(p => p.Role is WitchRole && p.Status == PlayerStatus.Alive);
        if (witchPlayer == null) return ProcessResult.Failure(GameError.InvalidOperation(GameErrorCode.InvalidInput_PlayerIdNotFound, "Witch role actor not found during night action processing."));

        // State 1: Processing Poison Input
        if (session.IsAwaitingWitchPoisonTarget)
        {
            // Expected Input: PlayerSelectionSingle for target, or anything else implies skip.
            if (input.InputTypeProvided == ExpectedInputType.PlayerSelectionSingle && input.SelectedPlayerIds?.Count == 1)
            {
                Guid targetId = input.SelectedPlayerIds.First();
                 Guid? wwVictimId = FindWerewolfVictimThisTurn(session); // Re-check victim for validation

                // Validate Poison Target
                 if (witchPlayer.State.PotionsUsed.HasFlag(WitchPotionType.Poison))
                    return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_WitchPotionAlreadyUsed, GameStrings.ErrorWitchPoisonUsed));
                if (!session.Players.TryGetValue(targetId, out var poisonTargetPlayer) || poisonTargetPlayer.Status != PlayerStatus.Alive)
                    return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_PlayerIdNotFound, GameStrings.ErrorTargetNotFoundOrDead));
                if (targetId == witchPlayer.Id)
                     return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_TargetIsInvalid, GameStrings.ErrorWitchCannotTargetSelf));
                if (targetId == wwVictimId) // Check if target is the WW victim (even if heal was skipped/used)
                    return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_TargetIsInvalid, GameStrings.ErrorWitchCannotTargetVictimWithPoison));

                // Process Poison
                witchPlayer.State.PotionsUsed |= WitchPotionType.Poison;
                LogWitchPotionUse(session, witchPlayer.Id, WitchPotionType.Poison, targetId);
                session.IsAwaitingWitchPoisonTarget = false; // Reset state
                // Confirmation text can be added if needed, using Success(instruction)
                return ProcessResult.Success();
            }
            else
            {
                // Assume any other input type (or invalid PlayerSelectionSingle) means Skip
                session.IsAwaitingWitchPoisonTarget = false; // Reset state
                // Log skip? Optional. For now, just succeed silently.
                return ProcessResult.Success();
            }
        }

        // State 2: Processing Heal Input
        else
        {
            // Expected Input: Confirmation (true = Heal, false/null = Skip)
            if (input.InputTypeProvided != ExpectedInputType.Confirmation)
            {
                return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_TypeMismatch, "Expected a confirmation input for Witch heal decision."));
            }

            Guid? wwVictimId = FindWerewolfVictimThisTurn(session);

            if (input.Confirmation == true) // Check the Confirmed flag
            {
                // Validate Heal
                if (witchPlayer.State.PotionsUsed.HasFlag(WitchPotionType.Healing))
                    return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_WitchPotionAlreadyUsed, GameStrings.ErrorWitchHealUsed));
                if (!wwVictimId.HasValue)
                    return ProcessResult.Failure(GameError.RuleViolation(GameErrorCode.RuleViolation_TargetIsInvalid, GameStrings.ErrorWitchHealNoVictim));
                 if (!session.Players.TryGetValue(wwVictimId.Value, out _)) // Check victim exists
                     return ProcessResult.Failure(GameError.InvalidInput(GameErrorCode.InvalidInput_PlayerIdNotFound, "WW victim player not found for Witch heal."));


                // Process Heal
                witchPlayer.State.PotionsUsed |= WitchPotionType.Healing;
                LogWitchPotionUse(session, witchPlayer.Id, WitchPotionType.Healing, wwVictimId.Value);
                // Success. GameService will call GenerateNightInstructions again, which will now check poison.
                return ProcessResult.Success();
            }
            else // Confirmed is false or null
            {
                // Skipped Heal. GameService will call GenerateNightInstructions again, which will now check poison.
                return ProcessResult.Success();
            }
        }
    }

    // Helper to centralize logging
    private void LogWitchPotionUse(GameSession session, Guid actorId, WitchPotionType potionType, Guid targetId)
    {
         session.GameHistoryLog.Add(new WitchPotionUseAttemptLogEntry
                {
                    ActorId = actorId,
                    PotionType = potionType,
                    TargetId = targetId,
                    Timestamp = DateTimeOffset.UtcNow, // Use required init
                    TurnNumber = session.TurnNumber,   // Use required init
                    Phase = session.GamePhase          // Use required init
                });
    }

     private Guid? FindWerewolfVictimThisTurn(GameSession session)
    {
        // Use the helper method in GameSession for consistency
        var wwLog = session.FindLogEntries<WerewolfVictimChoiceLogEntry>(turnsAgo: 0, phase: GamePhase.Night)
                         .LastOrDefault(); // Get the latest WW choice for the current night turn

        return wwLog?.VictimId; // Return the VictimId from the log entry
    }

    public ModeratorInstruction? GenerateDayInstructions(GameSession session)
    {
        return null; // No day action
    }

    public ProcessResult ProcessDayAction(GameSession session, ModeratorInput input)
    {
        // Provide a message for the error
        return ProcessResult.Failure(GameError.InvalidOperation(GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, "Witch has no day actions."));
    }
} 