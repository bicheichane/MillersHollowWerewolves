using Werewolves.Core.Enums;
using System.Text.Json.Serialization;

namespace Werewolves.Core.Models;

/// <summary>
/// Data structure for communication FROM the moderator.
/// Based on Roadmap Phase 0 and Architecture doc.
/// </summary>
public partial class ModeratorInput
{
    public ExpectedInputType InputTypeProvided { get; init; }

    // Optional fields, presence depends on InputTypeProvided
    public List<Guid>? SelectedPlayerIds { get; protected set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Dictionary<Guid, RoleType>? AssignedPlayerRoles { get; init; }
    public string? SelectedOption { get; init; }
    public bool? Confirmation { get; init; }
} 