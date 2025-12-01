# Werewolves of Miller's Hollow - Core Game Logic

## Overview

This repository contains the .NET class library (`Werewolves.Core`) responsible for implementing the core game logic, state management, and rules engine for the "The Werewolves of Miller's Hollow" board game, specifically tailored for use in a moderator helper application.

The primary goal is to provide a robust, maintainable, and efficient backend logic layer that is completely decoupled from any specific user interface (UI). This library can then be interacted with by various front-end applications (MAUI/Blazor apps, or through Web APIs) to provide a helpful tool for game moderators.

## Initial Constraints & Goals

This project was initiated with the following key constraints and goals:

1.  **Efficiency & Maintainability:** Prioritize clean, understandable code that is easy to maintain and extend.
2.  **Minimal Complexity:** Implement the required features with the least necessary complexity.
3.  **Moderator Helper:** The core logic is designed to *assist* a human moderator, not replace them. It relies on moderator input to update the game state.
4.  **UI Agnostic:** The `Werewolves.Core` library has no dependencies on specific UI frameworks (like ASP.NET, Blazor, MAUI). Communication happens through defined data contracts (`ModeratorInstruction`, `ModeratorInput`).
5.  **Scope:** Initially focus on the base game roles and a specific subset of New Moon event cards as detailed during the planning phase. The "Village" (Buildings) expansion and other New Moon content are explicitly excluded *for now*.

## Supported Features (Initial Scope)

*   **Base Game Roles:** As described in the rulebook pages used for planning (Simple Villager, Seer, Witch, Werewolf, Hunter, Cupid, etc., including roles like Fox, Bear Tamer, Knight).
*   **New Moon Events:** Includes the specific list of 19 event cards provided during planning (e.g., Full Moon Rising, Somnambulism, Enthusiasm, Backfire, Nightmare, Executioner, Spiritualism, Burial, etc.).

## Excluded Features (Initial Scope)

*   The "Village" expansion (Buildings, associated roles like Pyromaniac/Scandalmonger).
*   Any New Moon Event cards *not* explicitly included in the planning phase requirements.
*   Advanced variants not directly tied to the included Roles/Events.