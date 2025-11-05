using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Interfaces;

public interface IGameSession
{
	public Guid Id { get; }
	public GamePhase GetCurrentPhase();
	public int TurnNumber { get; }
	public Team? WinningTeam { get; }
	public IPlayer GetPlayer(Guid playerId);
	public IPlayerState GetPlayerState(Guid playerId);
	public IEnumerable<IPlayer> GetPlayers();
	public int RoleInPlayCount(MainRoleType type);
}