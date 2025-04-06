using Werewolves.Core.Enums;
using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Resources;

namespace Werewolves.Core.Roles;

/// <summary>
/// Represents the Simple Villager role.
/// </summary>
public class SimpleVillagerRole : IRole
{
    public RoleType RoleType => RoleType.SimpleVillager;
    public string Name => GameStrings.SimpleVillagerRoleName; // Assuming resource strings exist
    public string Description => GameStrings.SimpleVillagerRoleDescription;

    public int GetNightWakeUpOrder() => int.MaxValue; // No night action

    public bool RequiresNight1Identification() => false;

    // Villagers don't need identification
    public ModeratorInstruction? GenerateIdentificationInstructions(GameSession session) => null;

    public ProcessResult ProcessIdentificationInput(GameSession session, ModeratorInput input)
    {
        // Simple Villager doesn't need identification input.
        return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation,
                                                 GameErrorCode.InvalidOperation_ActionNotInCorrectPhase,
                                                 GameStrings.SimpleVillagerNoIdentification));
    }

    public ModeratorInstruction? GenerateNightInstructions(GameSession session)
    {
        // No night action
        return null;
    }

    public ProcessResult ProcessNightAction(GameSession session, ModeratorInput input)
    {
        return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, GameStrings.SimpleVillagerNoNightAction));
    }

    public ModeratorInstruction? GenerateDayInstructions(GameSession session)
    {
        // No day action
        return null;
    }

    public ProcessResult ProcessDayAction(GameSession session, ModeratorInput input)
    {
        return ProcessResult.Failure(new GameError(ErrorType.InvalidOperation, GameErrorCode.InvalidOperation_ActionNotInCorrectPhase, GameStrings.SimpleVillagerNoDayAction));
    }
}