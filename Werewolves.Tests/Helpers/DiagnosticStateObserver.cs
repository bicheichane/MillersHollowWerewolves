using System.Text;
using System.Text.RegularExpressions;
using Werewolves.StateModels.Core;
using Werewolves.StateModels.Enums;
using Werewolves.StateModels.Log;
using Werewolves.StateModels.Models;

namespace Werewolves.Tests.Helpers;

/// <summary>
/// Captures state changes for diagnostic output in tests.
/// </summary>
internal partial class DiagnosticStateObserver : IStateChangeObserver
{
    private readonly List<string> _log = new();
    private readonly object _lock = new();
    private IGameSession? _session;

    /// <summary>
    /// Sets the session reference for resolving player GUIDs to names.
    /// </summary>
    public void SetSession(IGameSession session) => _session = session;

    public IReadOnlyList<string> Log
    {
        get { lock (_lock) return _log.ToList(); }
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    private string ReplaceGuidsWithNames(string text)
    {
        if (_session == null) return text;

        var playerLookup = _session.GetPlayers().ToDictionary(
            p => p.Id.ToString(),
            p => p.Name);

        return GuidRegex().Replace(text, match =>
        {
            var guid = match.Value;
            return playerLookup.TryGetValue(guid, out var name)
                ? $"{name}"
                : guid;
        });
    }

    public void OnMainPhaseChanged(GamePhase newPhase)
    {
        lock (_lock) _log.Add($"[Phase] → {newPhase}");
    }

    public void OnSubPhaseChanged(string? newSubPhase)
    {
        lock (_lock) _log.Add($"[SubPhase] → {newSubPhase ?? "(cleared)"}");
    }

    public void OnSubPhaseStageChanged(string? newSubPhaseStage)
    {
        lock (_lock) _log.Add($"[SubPhaseStage] → {newSubPhaseStage ?? "(cleared)"}");
    }

    public void OnListenerChanged(ListenerIdentifier? listener, string? listenerState)
    {
        var listenerStr = listener?.ToString() ?? "(none)";
        var stateStr = listenerState ?? "(none)";
        lock (_lock) _log.Add($"[Listener] → {listenerStr} | State: {stateStr}");
    }

    public void OnTurnNumberChanged(int newTurnNumber)
    {
        lock (_lock) _log.Add($"[Turn] → {newTurnNumber}");
    }

    public void OnPendingInstructionChanged(ModeratorInstruction? instruction)
    {
        var instructionStr = instruction?.GetType().Name ?? "(null)";
        lock (_lock) _log.Add($"[Instruction] → {instructionStr}");
    }

    public void OnLogEntryApplied(GameLogEntryBase entry)
    {
        lock (_lock) _log.Add($"[Log] {entry.GetType().Name}: {entry}");
    }

    public string GetFormattedLog()
    {
        lock (_lock)
        {
            if (_log.Count == 0) return "(no state changes recorded)";

            var sb = new StringBuilder();
            sb.AppendLine("=== State Change Timeline ===");
            for (int i = 0; i < _log.Count; i++)
            {
                // Apply GUID-to-name replacement at output time when session is available
                var line = ReplaceGuidsWithNames(_log[i]);
                sb.AppendLine($"{i + 1,4}. {line}");
            }
            return sb.ToString();
        }
    }

    public void Clear()
    {
        lock (_lock) _log.Clear();
    }
}
