using Werewolves.GameLogic.Models;
using Werewolves.GameLogic.Models.GameHookListeners;
using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;

namespace Werewolves.GameLogic.Roles;

/// <summary>
/// Simple Villager role implementation using polymorphic hook listener pattern.
/// Inherits from RoleHookListener as a stateless role.
/// </summary>
internal class SimpleVillager : RoleHookListener
{
    internal override string PublicName => GameStrings.SimpleVillagerRoleName;
    public override ListenerIdentifier Role => ListenerIdentifier.Create(RoleType.SimpleVillager);

	protected override HookListenerActionResult AdvanceCoreStateMachine(GameSession session, ModeratorResponse input)
    {
        return HookListenerActionResult.Complete();
    }

    
}
