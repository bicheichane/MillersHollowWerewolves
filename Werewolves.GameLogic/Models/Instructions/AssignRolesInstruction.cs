using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.Instructions;

/// <summary>
/// Instruction that requires the moderator to assign roles to specific players.
/// Each player can be assigned from a specific list of available roles.
/// </summary>
public class AssignRolesInstruction : ModeratorInstruction
{
    /// <summary>
    /// Dictionary mapping player IDs to the list of roles that can be assigned to that player.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<RoleType>> SelectableRolesForPlayers { get; }

    /// <summary>
    /// Initializes a new instance of AssignRolesInstruction.
    /// </summary>
    /// <param name="selectableRolesForPlayers">Dictionary mapping player IDs to their assignable roles.</param>
    /// <param name="publicAnnouncement">The text to be read aloud to players.</param>
    /// <param name="privateInstruction">Private guidance for the moderator.</param>
    /// <param name="affectedPlayerIds">Optional list of affected player IDs for context.</param>
    public AssignRolesInstruction(
        IReadOnlyDictionary<Guid, IReadOnlyList<RoleType>> selectableRolesForPlayers,
        string? publicAnnouncement = null,
        string? privateInstruction = null,
        IReadOnlyList<Guid>? affectedPlayerIds = null)
        : base(publicAnnouncement, privateInstruction, affectedPlayerIds)
    {
        SelectableRolesForPlayers = selectableRolesForPlayers ?? throw new ArgumentNullException(nameof(selectableRolesForPlayers));

        if (selectableRolesForPlayers.Count == 0)
        {
            throw new ArgumentException("SelectableRolesForPlayers cannot be empty.", nameof(selectableRolesForPlayers));
        }

        // Validate that each player has at least one assignable role
        foreach (var kvp in selectableRolesForPlayers)
        {
            if (kvp.Value == null || kvp.Value.Count == 0)
            {
                throw new ArgumentException($"Player {kvp.Key} must have at least one assignable role.");
            }
        }
    }

    /// <summary>
    /// Creates a ModeratorResponse with the provided role assignments.
    /// Performs contractual validation to ensure assignments are valid.
    /// </summary>
    /// <param name="assignments">Dictionary mapping player IDs to their assigned roles.</param>
    /// <returns>A validated ModeratorResponse.</returns>
    /// <exception cref="ArgumentException">Thrown when assignments are invalid.</exception>
    public ModeratorResponse CreateResponse(Dictionary<Guid, RoleType> assignments)
    {
        ValidateAssignments(assignments);

        return new ModeratorResponse
        {
            Type = ExpectedInputType.AssignPlayerRoles,
            AssignedPlayerRoles = assignments
        };
    }

    /// <summary>
    /// Validates that the provided assignments are valid according to the selectable roles.
    /// </summary>
    /// <param name="assignments">The assignments to validate.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    private void ValidateAssignments(Dictionary<Guid, RoleType> assignments)
    {
        if (assignments == null)
        {
            throw new ArgumentNullException(nameof(assignments));
        }

        // Check that all assigned players are in the selectable list
        foreach (var assignment in assignments)
        {
            if (!SelectableRolesForPlayers.ContainsKey(assignment.Key))
            {
                throw new ArgumentException($"Player {assignment.Key} is not in the list of players that can be assigned roles.");
            }

            var availableRoles = SelectableRolesForPlayers[assignment.Key];
            if (!availableRoles.Contains(assignment.Value))
            {
                throw new ArgumentException($"Role {assignment.Value} is not in the list of assignable roles for player {assignment.Key}.");
            }
        }
    }
}
