using Werewolves.Core.Enums;

namespace Werewolves.Core.Models.Log;

/// <summary>
/// Log entry recording the start of the game and initial configuration.
/// Based on Roadmap Phase 0 and Architecture doc log structure.
/// </summary>
public record GameStartedLogEntry : GameLogEntryBase
{
    /// <summary>
    /// The list of RoleTypes included in the game deck at the start.
    /// </summary>
    public required IReadOnlyList<RoleType> InitialRoles { get; init; }

    /// <summary>
    /// A snapshot of the players at the start of the game (ID and Name).
    /// </summary>
    public required IReadOnlyList<PlayerInfo> InitialPlayers { get; init; }

    /// <summary>
    /// The IDs of event cards included in the deck at the start (if any).
    /// </summary>
    public IReadOnlyList<string>? InitialEvents { get; init; }
}

/// <summary>
/// Represents basic player identification for logging purposes.
/// </summary>
/// <param name="Id">Player's unique ID.</param>
/// <param name="Name">Player's name.</param>
public record PlayerInfo(Guid Id, string Name); 