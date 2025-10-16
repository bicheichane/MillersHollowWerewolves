using Werewolves.Core.Models;
using Werewolves.Core.Enums;
using Werewolves.Core.Models.StateMachine;

namespace Werewolves.Core.Interfaces;

/// <summary>
/// Defines the contract for character roles.
/// Represents the *rules* of the role.
/// </summary>
public interface IRole
{
    RoleType RoleType { get; }
    string Name { get; }
    string Description { get; }

    /// <summary>
    /// Indicates if this role needs to be identified by the moderator on Night 1.
    /// </summary>
    bool RequiresNight1Identification();

    /// <summary>
    /// Generates the instruction prompt for the moderator to identify the player(s)
    /// holding this role, if required.
    /// </summary>
    ModeratorInstruction GenerateIdentificationInstructions(GameSession session);

    /// <summary>
    /// Processes the moderator input provided for Night 1 role identification.
    /// Validates the input (e.g., correct number of players selected).
    /// Updates the Role and IsRoleRevealed status for the identified players in the session.
    /// Returns a ProcessResult indicating success (potentially with identified players) or failure.
    /// </summary>
    PhaseHandlerResult ProcessIdentificationInput(GameSession session, ModeratorInput input);

    /// <summary>
    /// Generates the instruction prompt for the moderator if this role acts at night.
    /// </summary>
    ModeratorInstruction GenerateNightInstructions(GameSession session);

	/// <summary>
	/// Processes the moderator input for the role's night action.
	/// Updates GameSession state and returns the result (success/failure).
	/// </summary>
	PhaseHandlerResult ProcessNightAction(GameSession session, ModeratorInput input);

    /// <summary>
    /// Generates the instruction prompt for the moderator if this role acts during the day.
    /// (e.g., Hunter's last shot)
    /// </summary>
    ModeratorInstruction GenerateDayInstructions(GameSession session);

	/// <summary>
	/// Processes the moderator input for the role's day action.
	/// </summary>
	PhaseHandlerResult ProcessDayAction(GameSession session, ModeratorInput input);
} 