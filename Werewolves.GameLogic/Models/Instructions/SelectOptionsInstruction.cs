using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Models;

namespace Werewolves.GameLogic.Models.Instructions;

/// <summary>
/// Instruction that requires the moderator to select from a list of text options.
/// Commonly used for event card selections, alignment choices, or other text-based decisions.
/// </summary>
public class SelectOptionsInstruction : ModeratorInstruction
{
    /// <summary>
    /// The list of selectable options.
    /// </summary>
    public IReadOnlyList<string> SelectableOptions { get; }

    /// <summary>
    /// Whether multiple selections are allowed.
    /// </summary>
    public bool AllowMultipleSelections { get; }

    /// <summary>
    /// Initializes a new instance of SelectOptionsInstruction.
    /// </summary>
    /// <param name="selectableOptions">The list of selectable options.</param>
    /// <param name="allowMultipleSelections">Whether multiple selections are allowed.</param>
    /// <param name="publicAnnouncement">The text to be read aloud to players.</param>
    /// <param name="privateInstruction">Private guidance for the moderator.</param>
    /// <param name="affectedPlayerIds">Optional list of affected player IDs for context.</param>
    public SelectOptionsInstruction(
        IReadOnlyList<string> selectableOptions,
        bool allowMultipleSelections = false,
        string? publicAnnouncement = null,
        string? privateInstruction = null,
        IReadOnlyList<Guid>? affectedPlayerIds = null)
        : base(publicAnnouncement, privateInstruction, affectedPlayerIds)
    {
        SelectableOptions = selectableOptions ?? throw new ArgumentNullException(nameof(selectableOptions));
        AllowMultipleSelections = allowMultipleSelections;

        if (selectableOptions.Count == 0)
        {
            throw new ArgumentException("SelectableOptions cannot be empty.", nameof(selectableOptions));
        }

        // Check for duplicate options
        if (selectableOptions.Distinct().Count() != selectableOptions.Count)
        {
            throw new ArgumentException("SelectableOptions contains duplicate entries.", nameof(selectableOptions));
        }
    }

    /// <summary>
    /// Creates a ModeratorResponse with the provided option selection.
    /// Performs contractual validation to ensure the selection is valid.
    /// </summary>
    /// <param name="selectedOptions">The selected option(s).</param>
    /// <returns>A validated ModeratorResponse.</returns>
    /// <exception cref="ArgumentException">Thrown when the selection is invalid.</exception>
    public ModeratorResponse CreateResponse(params string[] selectedOptions)
    {
        return CreateResponse((IReadOnlyList<string>)selectedOptions);
    }

    /// <summary>
    /// Creates a ModeratorResponse with the provided option selection.
    /// Performs contractual validation to ensure the selection is valid.
    /// </summary>
    /// <param name="selectedOptions">The selected option(s).</param>
    /// <returns>A validated ModeratorResponse.</returns>
    /// <exception cref="ArgumentException">Thrown when the selection is invalid.</exception>
    public ModeratorResponse CreateResponse(IReadOnlyList<string> selectedOptions)
    {
        ValidateSelection(selectedOptions);

        return new ModeratorResponse
        {
            Type = ExpectedInputType.OptionSelection,
            SelectedOption = selectedOptions.Count == 1 ? selectedOptions[0] : string.Join(",", selectedOptions)
        };
    }

    /// <summary>
    /// Validates that the provided selection is valid according to the instruction constraints.
    /// </summary>
    /// <param name="selectedOptions">The selection to validate.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    private void ValidateSelection(IReadOnlyList<string> selectedOptions)
    {
        if (selectedOptions == null)
        {
            throw new ArgumentNullException(nameof(selectedOptions));
        }

        if (selectedOptions.Count == 0)
        {
            throw new ArgumentException("At least one option must be selected.");
        }

        if (!AllowMultipleSelections && selectedOptions.Count > 1)
        {
            throw new ArgumentException("Multiple selections are not allowed for this instruction.");
        }

        // Check that all selected options are in the selectable list
        foreach (var option in selectedOptions)
        {
            if (!SelectableOptions.Contains(option))
            {
                throw new ArgumentException($"Option '{option}' is not in the list of selectable options.");
            }
        }

        // Check for duplicates in selection
        if (selectedOptions.Distinct().Count() != selectedOptions.Count)
        {
            throw new ArgumentException("Selection contains duplicate options.");
        }
    }
}
