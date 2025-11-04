using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Models;

public interface IPlayer
{
    public Guid Id { get; }
    public string Name { get; init; }
    public IPlayerState State { get; }
}

/// <summary>
/// Represents a participant in the game and the tracked information about them.
/// </summary>
internal class Player(string name) : IPlayer
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; init; } = name;
    public PlayerState State { get; set; } = new();
    IPlayerState IPlayer.State => State;

}
