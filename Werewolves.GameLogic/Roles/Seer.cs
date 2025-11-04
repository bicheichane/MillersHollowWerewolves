using Werewolves.GameLogic.Models;
using Werewolves.GameLogic.Models.GameHookListeners;
using Werewolves.GameLogic.Models.Instructions;
using Werewolves.StateModels;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Interfaces;
using Werewolves.StateModels.LogEntries;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;
using static Werewolves.StateModels.Enums.PlayerHealth;

namespace Werewolves.GameLogic.Roles;

/// <summary>
/// Seer role implementation using the polymorphic hook listener pattern.
/// Inherits from StandardNightRoleHookListener for standard target selection workflow.
/// </summary>
internal class Seer : StandardNightRoleHookListener
{
    public override ListenerIdentifier Role => ListenerIdentifier.Create(RoleType.Seer);
    internal override string PublicName => GameStrings.SeerRoleName;
    protected override bool HasNightPowers => true;

    protected override ModeratorInstruction GenerateTargetSelectionInstruction(GameSession session, ModeratorResponse input)
    {
        var seerPlayer = GetAliveRolePlayers(session)?.FirstOrDefault();
        if (seerPlayer == null)
        {
            throw new InvalidOperationException("No alive Seer found for target selection.");
        }

        var potentialTargets = GetPotentialTargets(session, false);

        return new SelectPlayersInstruction(
            publicAnnouncement: GameStrings.SeerNightActionPrompt,
            constraint: SelectionConstraint.Single, 
            selectablePlayerIds: potentialTargets,
            affectedPlayerIds: new List<Guid> { seerPlayer.Id }
        );
    }

    protected override void ProcessTargetSelection(GameSession session, ModeratorResponse input)
    {
        var seerPlayer = GetAliveRolePlayers(session)?.FirstOrDefault();

        var targetId = input.SelectedPlayerIds!.First();
        var targetPlayer = session.GetPlayer(targetId);

        // Perform the Seer's check
        
        //TODO: in the future, migrate this call into a GameSession method that goes through game logs to determine which team the player belongs to
        bool targetWakesWithWerewolves = DoesPlayerWakeWithWerewolves(targetPlayer, session);
        
        string privateFeedback = targetWakesWithWerewolves ?
            GameStrings.SeerResultWerewolfTeam : GameStrings.SeerResultNotWerewolfTeam;

        session.PerformNightAction(NightActionType.SeerCheck, targetId, privateFeedback);
    }

    private bool DoesPlayerWakeWithWerewolves(IPlayer player, GameSession session)
    {
        // TODO: Add checks for Wild Child, Wolf Hound, Events in later phases
        // TODO: Check PlayerState.IsInfected when implemented

        if (player.State.Role != null)
        {
            return player.State.Role switch
            {
                RoleType.SimpleWerewolf => true,
                // TODO: Add other werewolf types when implemented
                _ => false
            };
        }

        return false;
    }
}
