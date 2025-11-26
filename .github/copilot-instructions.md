# Copilot Instructions for MillersHollowWerewolves

## Architecture & Design Philosophy
- **Two-Assembly Structure**: 
  - `Werewolves.StateModels`: Pure state representation, data models, and enums. Contains NO game logic.
  - `Werewolves.GameLogic`: Rules engine, flow management, and `IGameHookListener` implementations.
- **State Management (Facade/Kernel Pattern)**:
  - **GameSession (Facade)**: A stateless wrapper implementing `IGameSession`. Delegates all state queries to the Kernel.
  - **GameSessionKernel (Kernel)**: The "Fort Knox" of state. Holds `GameHistoryLog`, `Player` objects, and caches.
  - **Zero-Leakage Mutation**: Direct state modification is compile-time impossible. All changes MUST occur via `GameLogEntryBase` applied through the `ISessionMutator`.
  - **Event Sourcing**: `GameSessionKernel.GameHistoryLog` is the SINGLE source of truth.
- **Game Loop**:
  - Driven by `GameFlowManager` using a declarative hook system.
  - Roles and Events implement `IGameHookListener` to respond to specific game phases (hooks).
- **UI Agnostic**:
  - Communication is strictly via `ModeratorInstruction` (Output) and `ModeratorInput` (Input).
  - Do not introduce UI-specific dependencies into the Core or GameLogic assemblies.

## Coding Conventions
- **String Management**: All user-facing strings MUST be in `Werewolves.StateModels/Resources/GameStrings.resx`. Use `GameStrings` class for access.
- **Enums**: Use `enum` types for internal logic identifiers, not strings.
- **Encapsulation**: 
  - `Player` and `PlayerState` are `private nested` classes within `GameSessionKernel`, exposed only via `IPlayer`/`IPlayerState` interfaces.
  - Use `internal` visibility to hide implementation details from the UI layer.
- **Error Handling**: Use `ProcessResult` to return success/failure with instructions, rather than throwing exceptions for game logic errors.

## Critical Workflows
- **Adding a Role/Event**:
  1. Create a class implementing `IGameHookListener` in `Werewolves.GameLogic`.
  2. Register it in `GameFlowManager.HookListeners` and `ListenerImplementations`.
  3. Implement `AdvanceStateMachine` to handle state transitions.
- **Modifying State**:
  1. Define a new `GameLogEntryBase` in `Werewolves.StateModels/Log`.
  2. Implement `InnerApply(ISessionMutator mutator)` to perform the actual state mutation.
  3. Append this entry to the log via `GameSession` (which delegates to Kernel).

## Key Files
- `Werewolves.StateModels/Core/GameSession.cs`: Public API facade.
- `Werewolves.StateModels/Core/GameSessionKernel.cs`: Internal state container and mutation engine.
- `Werewolves.GameLogic/Services/GameFlowManager.cs`: Central hub for game phases and hook listeners.
- `Werewolves.GameLogic/Services/GameService.cs`: Main entry point for the application.
- `Werewolves.GameLogic/Documentation/architecture.md`: Detailed architectural reference.
