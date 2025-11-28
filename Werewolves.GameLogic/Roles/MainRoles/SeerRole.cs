using Werewolves.GameLogic.Models.GameHookListeners;
using Werewolves.GameLogic.Models.InternalMessages;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Models.Instructions;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;
using Werewolves.StateModels.Resources;

namespace Werewolves.GameLogic.Roles.MainRoles;

/// <summary>
/// Seer role implementation using the polymorphic hook listener pattern.
/// Inherits from StandardNightRoleHookListener for standard target selection workflow.
/// </summary>
internal class SeerRole : ImmediateFeedbackNightRoleHookListener
{
    public override ListenerIdentifier Id => ListenerIdentifier.Listener(MainRoleType.Seer);
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
            countConstraint: NumberRangeConstraint.Single, 
            selectablePlayerIds: potentialTargets,
            affectedPlayerIds: new List<Guid> { seerPlayer.Id }
        );
    }

    protected override ModeratorInstruction ProcessTargetSelectionWithFeedback(GameSession session, ModeratorResponse input)
    {
		var targetId = input.SelectedPlayerIds!.First();
        var targetPlayer = session.GetPlayer(targetId);

        bool targetWakesWithWerewolves = targetPlayer.State.Team == Team.Werewolves;

        string privateFeedback = targetWakesWithWerewolves ?
            GameStrings.SeerResultWerewolfTeam : GameStrings.SeerResultNotWerewolfTeam;

        session.PerformNightAction(NightActionType.SeerCheck, targetId);

        return new ConfirmationInstruction(privateInstruction: privateFeedback);
	}
}
