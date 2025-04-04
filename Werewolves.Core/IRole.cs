namespace Werewolves.Core
{
    public interface IRole
    {
        string Name { get; }
        RoleType RoleType { get; }
        Team Team { get; }
        // Add methods like GetNightWakeUpOrder, GenerateNightInstructions, ProcessNightAction, etc.,
        // as they become necessary for implementing tests and game logic.
    }
} 