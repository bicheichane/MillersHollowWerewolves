using System;
using System.Collections.Generic;
using Werewolves.Core.Roles; // Required for PlaceholderRole

namespace Werewolves.Core
{
    public class Player
    {
        public Guid Id { get; }
        public string Name { get; }
        public IRole? KnownRole { get; internal set; } // What the moderator knows about the role
        public PlayerStatus Status { get; internal set; } = PlayerStatus.Alive; // Modifiable by GameService
        public bool IsRoleRevealed { get; internal set; } // Whether the moderator knows the role

        // Add StateFlags dictionary later when needed by roles/events

        // Constructor for initial creation by GameService
        public Player(string name)
        {
            Id = Guid.NewGuid();
            Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentNullException(nameof(name));
            KnownRole = null; // Moderator doesn't know the role initially
            IsRoleRevealed = false;
        }

        /// <summary>
        /// Updates the moderator's knowledge of this player's role.
        /// </summary>
        public void RevealRoleToModerator(IRole role)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            KnownRole = role;
            IsRoleRevealed = true;
        }

        /// <summary>
        /// Returns the role that the moderator knows about for this player.
        /// </summary>
        public IRole GetRoleForModerator() => KnownRole ?? new PlaceholderRole(RoleType.Unknown);
    }
} 