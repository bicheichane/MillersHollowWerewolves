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
    public List<Guid>? SelectedPlayerIds { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RoleType? SelectedRole { get; init; }
    public string? SelectedOption { get; init; }
    public bool? Confirmation { get; init; }

	public static ModeratorInput Confirm(bool confirmation) => new ModeratorInput()
	{
		InputTypeProvided = ExpectedInputType.Confirmation,
		Confirmation = confirmation
	};

    public static ModeratorInput SelectRole(RoleType role) => new ModeratorInput()
	{
		InputTypeProvided = ExpectedInputType.RoleSelection,
		SelectedRole = role
	};

    public static ModeratorInput SelectPlayer(Guid playerId) => new ModeratorInput()
	{
		InputTypeProvided = ExpectedInputType.PlayerSelectionSingle,
		SelectedPlayerIds = new List<Guid> { playerId }
	};

	public static ModeratorInput SelectPlayers(Guid playerId) => new ModeratorInput()
	{
		InputTypeProvided = ExpectedInputType.PlayerSelectionMultiple,
		SelectedPlayerIds = new() { playerId }
	};

	public static ModeratorInput SelectPlayers(List<Guid> playerIds) => new ModeratorInput()
	{
		InputTypeProvided = ExpectedInputType.PlayerSelectionMultiple,
		SelectedPlayerIds = playerIds
	};

    public static ModeratorInput SelectOption(string option) => new ModeratorInput()
	{
		InputTypeProvided = ExpectedInputType.OptionSelection,
		SelectedOption = option
	};
} 