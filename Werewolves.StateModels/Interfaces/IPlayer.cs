namespace Werewolves.StateModels.Interfaces;

public interface IPlayer
{
    public Guid Id { get; }
    public string Name { get; init; }
    public IPlayerState State { get; }
}