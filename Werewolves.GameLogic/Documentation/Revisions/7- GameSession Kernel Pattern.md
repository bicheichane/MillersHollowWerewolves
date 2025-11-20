# Architectural Proposal: The State Engine Pattern & Zero-Leakage Mutation

## 1. Abstract

This proposal mandates a structural refactoring of the `Werewolves.StateModels` assembly to enforce a strict "Zero-Leakage" policy regarding state mutation.

Currently, the `GameSession` class acts as a "God Object," simultaneously functioning as the public API, the state container, and the rules enforcer. Due to C# scoping rules regarding nested classes, `GameSession` currently possesses privileged access to modify `PlayerState` directly, bypassing the architectural requirement that all changes must flow through the `IStateMutator` via the `GameHistoryLog`.

We propose decoupling these concerns by introducing a dedicated, internal **`StateMutationEngine`**. This engine will act as the hermetically sealed kernel of the application, encapsulating all concrete state classes (`Player`, `PlayerState`) as **private nested types**. `GameSession` will be reduced to a pure **Facade**, interacting with the state solely through read-only interfaces (`IPlayer`, `IPlayerState`) and dispatching commands via `GameLogEntry` objects. This architecture renders direct state mutation by the Session physically impossible at compile time.

## 2. Motivation

### 2.1. The "Privileged Scope" Leak
The current architecture relies on `PlayerState` being a `protected nested class` of `GameSession`. While this prevents external assemblies from modifying state, C# language specifications grant an outer class full access to the private members of its nested classes. This loophole allows `GameSession` logic to imperatively modify properties (e.g., `player.State.Health = Dead`) without generating a log entry, creating a divergence between the *Game History* and the *Memory Heap*.

### 2.2. Integrity of Consequential State Logging
With the adoption of "Consequential State Logging" (Revisions/6), the system relies on the `GameHistoryLog` to be the single source of truth for deterministic outcomes (e.g., Infection, Protection usage). If `GameSession` retains the ability to bypass the log, the integrity of the replay system is compromised. We require a system where the compiler enforces that **creation of a Log Entry is the only mechanism** to trigger a state change.

### 2.3. Separation of Intent vs. Mechanism
The current design conflates **Intent** (The API call `EliminatePlayer`) with **Mechanism** (The heap manipulation `player.Health = Dead`). By separating these into a Facade (Session) and a Kernel (Engine), we isolate the messy implementation details of mutation, storage, and caching from the clean, high-level logic of the Session.

## 3. Proposed Architectural Changes

### 3.1. Component: `StateMutationEngine` (The Kernel)
A new `internal class` in `Werewolves.StateModels.Core` that acts as the sole owner of the game's mutable memory.

*   **Data Ownership:** The Engine owns the `GameHistoryLog`, the `GamePhaseStateCache`, the `Players` dictionary, and configuration lists (`SeatingOrder`, `RolesInPlay`).
*   **Private Concrete Types:** The concrete classes `Player` and `PlayerState` are moved inside the Engine as **private nested classes**.
    *   They implement `IPlayer` and `IPlayerState`.
    *   Their properties are auto-implemented (`public get; set;`). Because the class types themselves are private to the Engine, these public setters are inaccessible to any other class, including `GameSession`.
*   **The Factory:** The Engine exposes a method `IPlayer CreateAndRegisterPlayer(...)`. It returns the interface, effectively "masking" the mutable concrete type from the consumer.
*   **The Proxy Mutator:** A `private nested class EngineMutator : IStateMutator`.
    *   This class is instantiated *only* within the Engine's `ApplyLogEntry` method.
    *   Because it is nested within the Engine, it has privileged access to cast `IPlayer` back to the private concrete `Player` type, unlocking write access to the state.

### 3.2. Component: `GameSession` (The Facade)
`GameSession` is stripped of all state storage fields. It retains `Id` and `TurnNumber` (derived from Engine) but delegates all other operations.

*   **Interface Consumer:** `GameSession` holds a view of `Dictionary<Guid, IPlayer>`. It does not know that the class `Player` exists.
*   **Read-Only:** Since `IPlayerState` only defines `get` accessors, `GameSession` cannot compile code that attempts to mutate state.
*   **Command Dispatch:** To change state, `GameSession` must construct a `GameLogEntry` and pass it to `_engine.ApplyLogEntry()`.

### 3.3. Migration of Shared State
To ensure the Engine is the single source of truth, `PlayerSeatingOrder` and `RolesInPlay` are moved from `GameSession` to `StateMutationEngine`. This allows future flexibility for `GameLogEntry` types that might need to mutate these lists (e.g., swapping seats or adding roles mid-game), which would be impossible if they remained immutable fields on the Session.

## 4. Documentation Changes

### 4.1. `architecture.md` Updates

*   **State Management Philosophy:** Update to define the **Facade/Kernel** relationship. Explicitly state that `GameSession` is a read-only projection of the `StateMutationEngine`.
*   **Core Components:**
    *   **`GameSession`:** Redefine as a stateless API wrapper.
    *   **`StateMutationEngine`:** Add section detailing its role as the "Fort Knox" of state, holding the Log, Cache, and concrete Player objects.
    *   **`IStateMutator`:** Clarify that this interface is implemented privately within the Engine to prevent casting attacks.
*   **State Models:** Remove references to "Protected Nested Classes" in `GameSession` and replace with "Private Nested Classes" in `StateMutationEngine`.

## 5. Impact Analysis

### 5.1. Benefits
*   **Absolute Encapsulation:** It is compile-time impossible for `GameSession` or `GameLogic` to mutate state directly. The `StateMutationEngine` acts as a perfect firewall.
*   **Interface Segregation:** Consumers (Session/Logic) deal only with clean contracts (`IPlayer`), never implementation details.
*   **Testability:** The `StateMutationEngine` can be unit-tested in isolation to verify log application logic without instantiating the full Session overhead.
*   **Simplified "Undo/Fork":** Because the Engine owns 100% of the mutable state, creating a "Simulated Future" (e.g., for AI decision making) requires only cloning the Engine, not the entire Session wrapper.

### 5.2. Considerations
*   **Delegation Boilerplate:** `GameSession` requires pass-through methods for every piece of data it exposes (e.g., `public IReadOnlyList<Guid> SeatingOrder => _engine.SeatingOrder;`).
*   **Serialization Constraints:** Standard JSON serializers cannot serialize the `GameSession.Players` dictionary directly because the concrete types are private.
    *   *Mitigation:* This reinforces the architecture's philosophy that the **Log** is the save file. We do not save the "Heap"; we save the "History." Loading a game requires replaying the log through the Engine.

### 5.3. Conclusion
This architecture provides the strongest possible guarantee of data integrity in C#. It eliminates the risk of "Developer Bypass" (modifying state without logging) and ensures that the `GameHistoryLog` remains the undisputed, reproducible source of truth for the game's execution.