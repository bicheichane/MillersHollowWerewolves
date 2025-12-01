namespace Werewolves.StateModels.Core;

using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models;

/// <summary>
/// Optional observer for state change diagnostics.
/// Used by tests to capture intermediate state mutations.
/// </summary>
internal interface IStateChangeObserver
{
    void OnMainPhaseChanged(GamePhase newPhase);
    void OnSubPhaseChanged(string? newSubPhase);
    void OnSubPhaseStageChanged(string? newSubPhaseStage);
    void OnListenerChanged(ListenerIdentifier? listener, string? listenerState);
    void OnTurnNumberChanged(int newTurnNumber);
    void OnPendingInstructionChanged(ModeratorInstruction? instruction);
    void OnLogEntryApplied(GameLogEntryBase entry);
}
