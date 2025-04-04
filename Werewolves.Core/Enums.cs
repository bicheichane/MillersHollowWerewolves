namespace Werewolves.Core
{
    public enum Team
    {
        Villagers,
        Werewolves,
        Ambiguous,
        Lovers,
        Loners
    }

    public enum GamePhase
    {
        Setup,              // Initial role assignment and setup
        Night,             // Night phase begins
        Day_ResolveNight,  // Resolve night actions
        Day_Event,         // Handle any event cards
        Day_Debate,        // Day phase discussion
        Day_Vote,          // Voting phase
        Day_ResolveVote,   // Resolve voting results
        AccusationVoting,  // For Nightmare Event
        FriendVoting,      // For Great Distrust Event
        GameOver           // Game has ended
    }

    public enum PlayerStatus
    {
        Alive,
        Dead,
        Eliminated
    }

    // Minimal input types needed for initial tests
    public enum ExpectedInputType
    {
        None,
        PlayerSelection,
        RoleSelection,
        Confirmation,
        YesNo,
        OptionSelection
    }

    public enum RoleType
    {
        // Special/System Roles
        Unassigned, // Used during setup
        Unknown,    // Used when moderator doesn't know a player's role
        Placeholder, // Used for temporary role assignments

        // Werewolves
        SimpleWerewolf,
        BigBadWolf,
        AccursedWolfFather,
        WhiteWerewolf,

        // Villagers
        SimpleVillager,
        Seer,
        Cupid,
        Witch,
        Hunter,
        Defender,
        Elder,
        BearTamer,
        Knight,
        StutteringJudge,
        VillageIdiot,
        Scapegoat,
        DevotedServant,
        Actor,
        Fox,
        ThreeLittlePigs,
        Piper,
        Gypsy,
        TownCrier,

        // Ambiguous
        Thief,

        // Loners
        Angel,
        PrejudicedManipulator
    }
} 