using Werewolves.GameLogic.Models;
using Werewolves.GameLogic.Models.GameHookListeners;
using Werewolves.GameLogic.Models.Instructions;
using Werewolves.StateModels;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;

namespace Werewolves.GameLogic.Roles.MainRoles;

/// <summary>
/// Simple Werewolf role implementation using the polymorphic hook listener pattern.
/// Inherits from StandardNightRoleHookListener for standard target selection workflow.
/// </summary>
internal class SimpleWerewolfRole : StandardNightRoleHookListener
{
    
    internal override string PublicName => GameStrings.SimpleWerewolfRoleName;
    public override ListenerIdentifier Role => ListenerIdentifier.Listener(MainRoleType.SimpleWerewolf);
    protected override bool HasNightPowers => true;

    protected override ModeratorInstruction GenerateTargetSelectionInstruction(GameSession session, ModeratorResponse input)
    {
        var werewolves = GetAliveRolePlayers(session);
        if (werewolves == null || !werewolves.Any())
        {
            throw new InvalidOperationException("No alive werewolves found for target selection.");
        }

        var potentialTargets = GetPotentialTargets(session, false);

        return new SelectPlayersInstruction(
            publicAnnouncement: GameStrings.WerewolvesChooseVictimPrompt,
            selectablePlayerIds: potentialTargets,
            affectedPlayerIds: werewolves.Select(w => w.Id).ToList(),
            constraint: SelectionConstraint.Single
        );
    }

    protected override void ProcessTargetSelection(GameSession session, ModeratorResponse input)
    {
        var victimId = input.SelectedPlayerIds!.First();

        session.PerformNightAction(NightActionType.WerewolfVictimSelection, victimId);
    }
}
