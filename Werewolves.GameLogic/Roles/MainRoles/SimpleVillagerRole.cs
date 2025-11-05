using Werewolves.GameLogic.Models;
using Werewolves.GameLogic.Models.GameHookListeners;
using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;

namespace Werewolves.GameLogic.Roles.MainRoles;

/// <summary>
/// Simple Villager role implementation using polymorphic hook listener pattern.
/// Inherits from RoleHookListener as a stateless role.
/// </summary>
internal class SimpleVillagerRole : RoleHookListener
{
    internal override string PublicName => GameStrings.SimpleVillagerRoleName;
    public override ListenerIdentifier Role => ListenerIdentifier.Listener(MainRoleType.SimpleVillager);

	protected override HookListenerActionResult AdvanceCoreStateMachine(GameSession session, ModeratorResponse input)
    {
        return HookListenerActionResult.Complete();
    }

    
}
