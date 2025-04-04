namespace Werewolves.Core.Roles
{
    /// <summary>
    /// A placeholder role implementation used during initial setup
    /// and before specific role logic is implemented or assigned.
    /// </summary>
    internal class PlaceholderRole : IRole
    {
        public string Name { get; } // Display name
        public RoleType RoleType { get; } // Specific role enum
        public Team Team { get; } // Team alignment

        // Constructor now takes RoleType
        public PlaceholderRole(RoleType roleType = RoleType.Placeholder, Team team = Team.Ambiguous)
        {
            RoleType = roleType;
            // Generate a display name from the enum
            Name = Enum.GetName(typeof(RoleType), roleType) ?? roleType.ToString();
            Team = team;
        }
    }
} 