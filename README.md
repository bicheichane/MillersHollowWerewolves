# Werewolves of Miller's Hollow - Core Game Logic

## Overview

This repository contains the .NET class library (`Werewolves.Core`) responsible for implementing the core game logic, state management, and rules engine for the "The Werewolves of Miller's Hollow" board game, specifically tailored for use in a moderator helper application.

The primary goal is to provide a robust, maintainable, and efficient backend logic layer that is completely decoupled from any specific user interface (UI). This library can then be consumed by various front-end applications (Web APIs, Blazor apps, MAUI apps, Console apps, etc.) to provide a helpful tool for game moderators.

## Initial Constraints & Goals

This project was initiated with the following key constraints and goals:

1.  **Efficiency & Maintainability:** Prioritize clean, understandable code that is easy to maintain and extend.
2.  **Minimal Complexity:** Implement the required features with the least necessary complexity.
3.  **Moderator Helper:** The core logic is designed to *assist* a human moderator, not replace them. It relies on moderator input to update the game state.
4.  **UI Agnostic:** The `Werewolves.Core` library has no dependencies on specific UI frameworks (like ASP.NET, Blazor, MAUI). Communication happens through defined data contracts (`ModeratorInstruction`, `ModeratorInput`).
5.  **Scope:** Initially focus on the base game roles and a specific subset of New Moon event cards as detailed during the planning phase. The "Village" (Buildings) expansion and other New Moon content are explicitly excluded *for now*.

## Architectural Approach

The core architecture follows a state machine pattern orchestrated by a central service:

1.  **`GameService`:** Acts as the main entry point and orchestrator. It manages game sessions, processes moderator inputs, advances the game state, and applies rules. It is designed to be largely stateless itself, operating on `GameSession` objects.
2.  **`GameSession`:** Represents the complete state of a single game instance, including players, roles, game phase, active events, and various tracking flags.
3.  **`Player`:** Represents a participant in the game, holding their assigned role, status, and individual state flags.
4.  **`IRole` Interface & Implementations:** Defines the contract for all character roles. Concrete implementations encapsulate the unique logic, powers, night actions, and victory conditions for each role (e.g., `SeerRole`, `SimpleWerewolfRole`).
5.  **`EventCard` Abstract Class & Implementations:** Defines the contract for New Moon event cards. Concrete implementations encapsulate the logic for applying event effects, modifying game rules temporarily or permanently, and interacting with the game loop at specific phases.
6.  **`ActiveEventState`:** A helper class stored within `GameSession` to track the runtime state and remaining duration of currently active New Moon events.
7.  **Data Contracts (`ModeratorInstruction`, `ModeratorInput`):** These classes define the clear communication boundary between the core logic and any external system (like a UI layer acting on behalf of the moderator). `ModeratorInstruction` tells the moderator what to do or ask, and `ModeratorInput` carries the moderator's response back to the `GameService`.

## Core Components Summary

*   **`GameSession`:** Central state object for a game.
*   **`Player`:** Represents a game participant and their individual state.
*   **`IRole`:** Interface defining character roles; implemented by specific role classes.
*   **`EventCard`:** Abstract class defining New Moon events; implemented by specific event classes.
*   **`ActiveEventState`:** Tracks active event status within a `GameSession`.
*   **`GameService`:** Orchestrates game flow, manages sessions, processes input.
*   **`ModeratorInstruction`:** Data sent *to* the moderator/UI.
*   **`ModeratorInput`:** Data received *from* the moderator/UI.
*   **Enums:** Define discrete states like `GamePhase`, `PlayerStatus`, `Team`, `EventTiming`, `EventDuration`, `ExpectedInputType`.

## Game Loop High-Level Flow

The `GameService` manages the game through distinct phases:

1.  **Setup:** Initialize game, deal roles, handle initial setup roles (Thief, Cupid).
2.  **Night:** Call roles in order, generate instructions, process actions (modified by active Night events).
3.  **Day - Resolve Night:** Determine night outcomes (deaths, infections), apply protections/effects (modified by active Night resolution events like Backfire, Miracle, Specter).
4.  **Day - Event:** Reveal victims (modified by Burial), handle death triggers (Hunter/Lovers), draw and apply a New Moon event card.
5.  **Day - Debate:** Manage player discussion (modified by active Debate events like Eclipse, Good Manners, Not Me).
6.  **Day - Vote:** Conduct the village vote (process modified by active Vote events like Nightmare, Influences, Great Distrust).
7.  **Day - Resolve Vote:** Determine elimination, reveal role (modified by Executioner), handle triggers (Idiot, Hunter/Lovers, Sheriff), check for event consequences (Enthusiasm, Dissatisfaction, Punishment). Check for Game Over.
8.  **Repeat:** Loop back to Night phase or end in Game Over.

Active `EventCard` implementations hook into specific points in this loop to modify behaviour.

## Supported Features (Initial Scope)

*   **Base Game Roles:** As described in the rulebook pages used for planning (Simple Villager, Seer, Witch, Werewolf, Hunter, Cupid, etc., including roles like Fox, Bear Tamer, Knight).
*   **New Moon Events:** Includes the specific list of 19 event cards provided during planning (e.g., Full Moon Rising, Somnambulism, Enthusiasm, Backfire, Nightmare, Executioner, Spiritualism, Burial, etc.).

## Excluded Features (Initial Scope)

*   The "Village" expansion (Buildings, associated roles like Pyromaniac/Scandalmonger).
*   Any New Moon Event cards *not* explicitly included in the planning phase requirements.
*   Advanced variants not directly tied to the included Roles/Events.

## Future Direction

1.  **Implementation:** Fully implement the `Werewolves.Core` logic based on the defined architecture.
2.  **Testing:** Develop comprehensive unit and integration tests to ensure rules accuracy.
3.  **UI Development:** Create separate projects for UI applications (e.g., Web API + Frontend, Blazor Server/WASM, MAUI) that consume this core library via the `GameService` and data contracts.
4.  **Expansion:** Potentially incorporate the Village/Buildings expansion and remaining New Moon content in future iterations.
5.  **Refinement:** Continuously refine state management, error handling, and logging based on usage and testing.