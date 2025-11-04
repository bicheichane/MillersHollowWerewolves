using System.Diagnostics.CodeAnalysis;
using Werewolves.StateModels.Enums;

namespace Werewolves.StateModels.Models;

/// <summary>
/// Manages transient state within a single game phase, acting as a "program counter"
/// for resumable, multi-step actions that must pause for moderator input.
/// This becomes the single source of truth for the game's current execution point.
/// </summary>
public class GamePhaseStateCache
{
    // Tracks the GFM's current execution point.
    private GamePhase _currentPhase;
    private string? _currentGfmSubPhase;

    // Tracks the currently executing hook sequence.
    private GameHook? _activeHook;
    
    // Tracks the single listener that is currently paused awaiting input.
    private ListenerIdentifier? _currentListener;
    private string? _currentListenerState;

    /// <summary>
    /// Initializes a new IntraPhaseStateCache with the specified starting phase.
    /// </summary>
    /// <param name="initialPhase">The initial game phase.</param>
    public GamePhaseStateCache(GamePhase initialPhase)
    {
        _currentPhase = initialPhase;
    }

  // --- GFM State Accessors ---

  /// <summary>
  /// Gets the current game phase.
  /// </summary>
  /// <returns>The current game phase.</returns>
  public GamePhase GetCurrentPhase() => _currentPhase;
    
    public void TransitionMainPhase(GamePhase phase)
    {
        if (_currentPhase != phase)
        {
            ClearTransientGamePhaseState();
        }
        _currentPhase = phase;
    }
    
    /// <summary>
    /// Sets the GFM's current state with the specified sub-phase.
    /// </summary>
    /// <typeparam name="T">The enum type for the sub-phase.</typeparam>
    /// <param name="subPhase">The optional sub-phase enum value.</param>
    public void SetSubPhase<T>(T subPhase) where T : struct, Enum
    {
        _currentGfmSubPhase = subPhase.ToString();
    }

    /// <summary>
    /// Gets the current GFM sub-phase as the specified enum type.
    /// </summary>
    /// <typeparam name="T">The enum type for the sub-phase.</typeparam>
    /// <returns>The sub-phase value, or null if not set or parsing fails.</returns>
    public T? GetSubPhase<T>() where T : struct, Enum
    {
        if (_currentGfmSubPhase != null)
        {
            if (Enum.TryParse<T>(_currentGfmSubPhase, out var result))
            {
                return result;
            }
        }
        return null;
    }

    // --- Hook & Listener State Accessors ---
    
    /// <summary>
    /// Sets the currently active hook.
    /// </summary>
    /// <param name="hook">The hook that is currently being processed.</param>
    public void SetActiveHook(GameHook hook) => _activeHook = hook;
    
    /// <summary>
    /// Gets the currently active hook.
    /// </summary>
    /// <returns>The active hook, or null if no hook is active.</returns>
    public GameHook? GetActiveHook() => _activeHook;
    
    /// <summary>
    /// Marks the current hook as completed and clears it.
    /// </summary>
    public void CompleteCurrentHook()
    {
        _activeHook = null;
		_currentListener = null;
        _currentListenerState = null;
    }

    /// <summary>
    /// Sets the state for a current listener.
    /// </summary>
    /// <typeparam name="T">The enum type for the listener state.</typeparam>
    /// <param name="listener">The identifier of the current listener.</param>
    /// <param name="enumState">The state enum value for the listener.</param>
    public void SetCurrentListenerState<T>(ListenerIdentifier listener, [DisallowNull] T? enumState) where T : struct, Enum
    {
        _currentListener = listener;
        _currentListenerState = enumState.ToString();
    }

    /// <summary>
    /// Gets the state for a current listener.
    /// </summary>
    /// <typeparam name="T">The enum type for the listener state.</typeparam>
    /// <param name="listener">The identifier of the listener to check.</param>
    /// <returns>The listener's state, or null if the listener is not current or parsing fails.</returns>
    public T? GetCurrentListenerState<T>(ListenerIdentifier listener) where T : struct, Enum
    {
        if (_currentListener?.Equals(listener) == true && _currentListenerState != null)
        {
            if (Enum.TryParse<T>(_currentListenerState, out var result))
            {
                return result;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Gets the identifier of the currently active listener.
    /// </summary>
    /// <returns>The current listener identifier, or null if no listener is active.</returns>
    public ListenerIdentifier? GetCurrentListener() => _currentListener;


    /// <summary>
    /// Central cleanup method that must be called when transitioning between main GamePhases.
    /// Guarantees transient state is never leaked across phases.
    /// </summary>
    private void ClearTransientGamePhaseState()
    {
        _currentGfmSubPhase = null;
        _activeHook = null;
        _currentListener = null;
        _currentListenerState = null;
    }
}
