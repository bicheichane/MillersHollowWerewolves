using Werewolves.Core.Enums;
using Werewolves.Core.Interfaces;
using Werewolves.Core.Models;
using Werewolves.Core.Models.StateMachine;
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

    public bool RequiresNight1Identification() => false;

    // Villagers don't need identification
    public ModeratorInstruction GenerateIdentificationInstructions(GameSession session) => throw new NotImplementedException();

    public PhaseHandlerResult ProcessIdentificationInput(GameSession session, ModeratorInput input) =>
	    throw new NotImplementedException();

    public ModeratorInstruction GenerateNightInstructions(GameSession session) => throw new NotImplementedException();

    public PhaseHandlerResult ProcessNightAction(GameSession session, ModeratorInput input) =>
	    throw new NotImplementedException();

    public ModeratorInstruction GenerateDayInstructions(GameSession session) => throw new NotImplementedException();

    public PhaseHandlerResult ProcessDayAction(GameSession session, ModeratorInput input) => throw new NotImplementedException();
}