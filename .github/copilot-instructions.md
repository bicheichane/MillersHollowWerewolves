# Copilot Instructions for MillersHollowWerewolves

## Architecture & Design Philosophy
- **Two-Assembly Structure**: 
  - `Werewolves.StateModels`: Pure state representation, data models, and enums. Contains NO game logic.
  - `Werewolves.GameLogic`: Rules engine, flow management, and `IGameHookListener` implementations.
- **State Management**:
  - **Event Sourcing**: `GameSession.GameHistoryLog` is the SINGLE source of truth.
  - **Derived State**: `Player` and `PlayerState` are in-memory caches derived from the log.
  - **Mutation**: NEVER modify state directly. Create a `GameLogEntryBase` subclass and implement its `Apply` method. The `StateMutator` pattern enforces this.
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
  - `Player` is a `protected nested` class within `GameSession`.
  - Use `internal` visibility to hide implementation details from the UI layer.
- **Error Handling**: Use `ProcessResult` to return success/failure with instructions, rather than throwing exceptions for game logic errors.

## Critical Workflows
- **Adding a Role/Event**:
  1. Create a class implementing `IGameHookListener` in `Werewolves.GameLogic`.
  2. Register it in the `GameFlowManager` (or relevant factory).
  3. Implement `AdvanceStateMachine` to handle state transitions.
- **Modifying State**:
  1. Define a new `GameLogEntryBase` in `Werewolves.StateModels/Log`.
  2. Implement the `Apply` method to mutate `GameSession` or `PlayerState`.
  3. Append this entry to `GameHistoryLog` within the logic.

## Key Files
- `Werewolves.StateModels/Core/GameSession.cs`: Central state container and mutator logic.
- `Werewolves.GameLogic/Services/GameService.cs`: Main entry point for the application.
- `Werewolves.GameLogic/Documentation/architecture.md`: Detailed architectural reference.
