using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Log;

internal record StatusEffectLogEntry : GameLogEntryBase
{
	public required StatusEffectTypes EffectType { get; init; }
	public required Guid PlayerId { get; init; }
	/// <summary>
	/// Applies the status effect to the game state.
	/// </summary>
	protected override GameLogEntryBase InnerApply(ISessionMutator mutator)
	{
		switch (EffectType)
		{
			case StatusEffectTypes.ElderProtectionLost:
				mutator.SetElderExtraLife(PlayerId, false);
				break;
			case StatusEffectTypes.LycanthropyInfection:
				mutator.SetPlayerInfected(PlayerId, true);
				break;
			case StatusEffectTypes.WildChildChanged:
				mutator.SetPlayerRole(PlayerId, MainRoleType.SimpleWerewolf);
				break;
			default:
				throw new ArgumentOutOfRangeException($"Unhandled status effect type: {EffectType}");
		}

		return this;
	}
}