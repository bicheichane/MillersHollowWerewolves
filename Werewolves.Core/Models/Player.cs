using Werewolves.Core.Enums;
using Werewolves.Core.Interfaces;

namespace Werewolves.Core.Models;

/// <summary>
/// Represents a participant in the game and the tracked information about them.
/// </summary>
public class Player
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; set; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Alive;
    public bool IsRoleRevealed => Role != null;
    public PlayerState State { get; } = new PlayerState();

    // Role property added in Phase 1
    public IRole? Role { get; internal set; } = null;
}
