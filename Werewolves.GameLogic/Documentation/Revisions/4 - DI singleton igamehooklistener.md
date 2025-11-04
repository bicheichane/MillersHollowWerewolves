### Architectural Proposal: DI-Powered Registry for GameFlowManager

#### 1. Abstract

This proposal outlines a plan to refactor the `GameFlowManager` to implement a **DI-Powered Registry Pattern**. This design separates the component's two distinct responsibilities: the declarative definition of game logic (the "rulebook") and the resolution of concrete listener implementations (the "toolbox").

The game's flow logic—mapping `GameHook` triggers to the `ListenerIdentifier`s that must respond—will be defined in a `static`, centralized registry within `GameFlowManager`. The actual singleton instances of the `IGameHookListener` implementations will be provided by a Dependency Injection (DI) container. The `GameFlowManager`'s constructor will then consume this collection of instances to build a fast, private, runtime lookup dictionary. This hybrid approach creates a system that is highly extensible, testable, and loosely coupled, while maintaining a single, clear source of truth for the game's operational rules.

#### 2. Motivation

The initial architectural proposal for making listeners stateless singletons correctly identified the need for a single, shared instance of each role and event. However, simply injecting a collection of these listeners (e.g., `IEnumerable<IGameHookListener>`) into `GameFlowManager` is insufficient. This provides a "bag of tools with no instructions," failing to address the critical need for a declarative map that defines which listeners act on which game hooks.

Conversely, using only a `static` dictionary to both define the map and hold the instances (the Service Locator pattern) would tightly couple the `GameFlowManager` to every concrete listener class, severely harming testability and violating core SOLID principles like Dependency Inversion and Open/Closed.

The `GameFlowManager` therefore requires a solution that satisfies two distinct needs:
1.  A **declarative, centralized "rulebook"** for game flow that is easy to read and modify.
2.  A **decoupled, testable mechanism** for resolving listener identifiers into their concrete singleton instances at runtime.

The DI-Powered Registry Pattern is designed to solve exactly this problem by combining the strengths of a static registry with the benefits of dependency injection.

#### 3. Proposed Architectural Changes

The implementation will be achieved through the following coordinated changes.

1.  **Enhance the `IGameHookListener` Interface for Self-Identification:**
    The interface will be updated to require each listener to expose its unique identifier. This is essential for the mapping logic.
    ```csharp
    public interface IGameHookListener
    {
        ListenerIdentifier ListenerId { get; }
        // ... existing methods
    }

    // Example Implementation
    public class SeerRole : IGameHookListener
    {
        public ListenerIdentifier ListenerId => ListenerIdentifier.FromRole(RoleType.Seer);
        // ... AdvanceStateMachine logic
    }
    ```

2.  **Define the Static Game Logic Registry in `GameFlowManager`:**
    A `private static readonly` dictionary will be added to `GameFlowManager` to serve as the definitive "rulebook" for game flow. This map is static because the rules are universal to all game sessions.
    ```csharp
    // In GameFlowManager.cs
    private static readonly Dictionary<GameHook, List<ListenerIdentifier>> _masterHookListeners = new()
    {
        [GameHook.NightSequenceStart] = new List<ListenerIdentifier>
        {
            ListenerIdentifier.FromRole(RoleType.SimpleWerewolf),
            ListenerIdentifier.FromRole(RoleType.Seer),
            // ... etc.
        },
        [GameHook.OnPlayerEliminationFinalized] = new List<ListenerIdentifier>
        {
            ListenerIdentifier.FromRole(RoleType.Hunter)
        }
        // ... all other hook mappings
    };
    ```

3.  **Implement a DI-Fed Runtime Instance Cache:**
    The `GameFlowManager` constructor will be updated to accept an `IEnumerable<IGameHookListener>` from the DI container. It will use this collection to build a private, instance-level dictionary that maps each `ListenerIdentifier` to its corresponding singleton instance, providing fast O(1) lookups at runtime.

    ```csharp
    // In GameFlowManager.cs
    private readonly Dictionary<ListenerIdentifier, IGameHookListener> _listenerImplementations;

    public GameFlowManager(IEnumerable<IGameHookListener> allListeners)
    {
        // Build the fast runtime lookup map from the collection provided by DI.
        _listenerImplementations = allListeners.ToDictionary(listener => listener.ListenerId);

        // A validation step can be added here to ensure all listeners
        // in the static map were found in the injected collection.
        ValidateRegistryIsComplete();
    }
    ```

4.  **Update Core Logic to Use Both Maps:**
    The internal logic, such as `FireHook`, will be modified to use the two-step lookup process. It will first consult the static "rulebook" to get the required listener IDs, then use the runtime "toolbox" to resolve those IDs into instances to be executed.
    ```csharp
    // In GameFlowManager.cs
    private void FireHook(GameHook hook)
    {
        // 1. Get the list of required listener IDs from the static rulebook.
        if (!_masterHookListeners.TryGetValue(hook, out var requiredListenerIds))
        {
            return; // No listeners for this hook.
        }

        foreach (var listenerId in requiredListenerIds)
        {
            // 2. Get the concrete instance from the runtime toolbox.
            if (_listenerImplementations.TryGetValue(listenerId, out var listenerInstance))
            {
                // 3. Execute the listener's logic.
                listenerInstance.AdvanceStateMachine(...);
            }
        }
    }
    ```
5.  **Configure the DI Container:**
    As the final step, all concrete implementations of `IGameHookListener` (all roles and events) will be registered with the application's DI container with a **singleton lifestyle**.

#### 4. Impact Analysis

##### Benefits

*   **Upholds SOLID Principles:** This design adheres to:
    *   **Single Responsibility Principle:** `GameFlowManager` is responsible for orchestration, while the DI container is responsible for object creation and lifecycle.
    *   **Open/Closed Principle:** New roles can be added to the system by creating a new class, registering it with DI, and adding its identifier to the static map. The core logic of `GameFlowManager` does not need to be modified.
    *   **Dependency Inversion Principle:** `GameFlowManager` depends on the `IGameHookListener` abstraction, not on concrete implementations.
*   **Enhanced Testability:** The `GameFlowManager` can be unit tested in complete isolation by injecting a list of mock `IGameHookListener` objects. This allows for robust and focused testing of its complex orchestration logic.
*   **Centralized and Declarative Logic:** The game's flow is defined in a single, easy-to-read static dictionary, making the rules of the game explicit and easy to maintain.
*   **High Performance:** The runtime lookup of listener instances is an O(1) dictionary operation.

##### Considerations & Mitigations

*   **Dual Registration Requirement:**
    *   **Consideration:** A developer adding a new role must remember to register it in two places: the DI container and the `_masterHookListeners` map in `GameFlowManager`.
    *   **Mitigation:** This is a minor process overhead. It can be made fail-safe by implementing the `ValidateRegistryIsComplete()` method in the `GameFlowManager` constructor. This method would iterate through the static map and throw a clear exception at application startup if any required listener was not found in the injected DI collection, immediately flagging any misconfiguration.