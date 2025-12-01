Here is the **Architecture Requirements Document (ARD)** for the Werewolves UI Client.
This document serves as the "Source of Truth" for you and any LLM you use to generate code. It explicitly constrains choices to ensure compatibility with your skillset and the "Thin Client" philosophy.

***

# Architecture Requirements Document: Werewolves UI Client

## 1. Project Overview & Philosophy

*   **Goal:** Build a visual frontend for the `Werewolves` game engine. The app acts as a "Dumb Terminal" (Thin Client) for the Core Logic.
*   **Target Platform:** Mobile-First (Android/iOS) via **.NET MAUI Blazor Hybrid**.
*   **Core Philosophy:** **Model-View-Adapter (MVA)**. We prioritize development speed and reduced boilerplate over strict MVVM separation. The Core Library is trusted as the robust "Source of Truth."
*   **LLM Instruction:** When generating code, prioritize **MudBlazor** components over custom HTML/CSS and bind directly to Core interfaces.

## 2. Tech Stack

*   **Framework:** .NET MAUI Blazor Hybrid (.NET 8/9).
*   **UI Component Library:** **MudBlazor**.
    *   *Constraint:* Do not write raw CSS. Use MudBlazor's utility classes (e.g., `d-flex`, `mud-width-full`, `pa-4`) and layout components (`MudStack`, `MudGrid`).
*   **Audio:** `Plugin.Maui.Audio` (Native audio handling).
*   **Device Control:** `Microsoft.Maui.Devices.IDeviceDisplay` (Screen Wake Lock).
*   **Dependencies:**
    *   `Werewolves.GameLogic` (Direct Project Reference).
    *   `Werewolves.StateModels` (Direct Project Reference).

## 3. Architecture Pattern: Model-View-Adapter (MVA)

We **reject** the standard MVVM (Model-View-ViewModel) pattern for this specific project to avoid maintaining a duplicate state layer (ViewModel) that mirrors the Event-Sourced Core.

### 3.1. The Components
1.  **The Model (Core):** The `GameSession` (Core Library). It is self-validating, reactive, and holds the only valid state.
2.  **The View (Blazor):** Razor components that define *how* data is shown.
3.  **The Adapter (Client Service):** A singleton `GameClientManager` that bridges the Core and the View.

### 3.2. The `GameClientManager` (The Bridge)
This class is the **Singleton** entry point for the UI. It holds the reference to the live game session.

**Responsibilities:**
*   Initializes the `GameService` (Core).
*   Holds the `IGameSession? ActiveSession`.
*   Exposes a C# event `event Action? OnStateChanged` that triggers UI re-renders.
*   Proxies commands from UI to Core and automatically fires `OnStateChanged`.

**Code Contract (Skeleton):**
```csharp
public class GameClientManager
{
    private readonly GameService _coreService;
    public IGameSession? ActiveSession { get; private set; }
    public event Action? OnStateChanged;

    public GameClientManager(GameService coreService) 
    {
        _coreService = coreService;
    }

    public void StartGame(List<string> players, Dictionary<MainRoleType, int> roles)
    {
        // 1. Call Core to create session
        var instruction = _coreService.StartNewGame(players, roles);
        
        // 2. Capture the LIVE session reference (The Facade)
        // Note: We assume we can retrieve the session by ID or checking the service
        // For this architecture, we assume StartNewGame returns or allows fetching the session.
        ActiveSession = _coreService.GetGameStateView(instruction.GameId);
        
        // 3. Notify UI
        NotifyStateChanged();
    }

    public void ProcessInput(ModeratorResponse response)
    {
        if (ActiveSession == null) return;
        
        // 1. Send input to Core
        _coreService.ProcessInstruction(ActiveSession.Id, response);
        
        // 2. Notify UI (Core state updated in-place via reference)
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
```

### 3.3. State Synchronization Rule
*   **Do Not** create local state variables in Razor components for game data (e.g., `int currentTurn`).
*   **Always** read directly from `GameClientManager.ActiveSession.TurnNumber`.
*   **Always** implement `IDisposable` in components to unsubscribe from `OnStateChanged`.

## 4. UI/UX Design Strategy

### 4.1. Layout System
*   **MainLayout:** Use `MudLayout`.
    *   `MudAppBar` (Fixed Top): Displays Game ID (if active) and a generic Menu.
    *   `MudMainContent`: The render body.
*   **The Dashboard (Gameplay View):**
    *   Use `MudTabs` with `Position="Position.Bottom"` or `Position.Top`.
    *   **Disable** Swipe gestures in MudTabs if it causes conflict with list scrolling.
    *   **Tab 1: Roster:** Uses `MudList` or `MudTable` (Read-only mode).
    *   **Tab 2: Action (Active):** The dynamic instruction container.
    *   **Tab 3: Stats:** `MudPaper` cards with `MudText` for stats.

### 4.2. Styling Rules (For LLM Generation)
*   **Responsiveness:** Use `MudGrid` with `xs="12"` (mobile) and `md="6"` (tablet) breakpoints.
*   **Spacing:** Use MudBlazor utility classes: `ma-2` (margin all 2), `pa-4` (padding all 4), `gap-4`.
*   **Colors:** Use `MudTheme` to define a "Night Mode" palette (Dark background, Light text) consistent with the Werewolf theme.

## 5. Specific Feature Implementation

### 5.1. Audio Handling
*   **Library:** `Plugin.Maui.Audio`.
*   **Pattern:** Inject `IAudioManager` into `GameClientManager` (or a dedicated `AudioService`).
*   **Trigger:** In `GameClientManager.ProcessInput`, check `ActiveSession.PendingModeratorInstruction.SoundEffect`. If not null, play the corresponding asset.

### 5.2. Screen Keep-Awake
*   **Library:** `Microsoft.Maui.Devices.IDeviceDisplay`.
*   **Implementation:**
    *   On `Dashboard.OnInitialized`: `DeviceDisplay.Current.KeepScreenOn = true;`
    *   On `Dashboard.Dispose`: `DeviceDisplay.Current.KeepScreenOn = false;`

### 5.3. Dynamic Instruction Rendering
Instead of a giant `switch` statement in the UI, use a **Polymorphic Renderer**:

*   **Component:** `InstructionRenderer.razor`
*   **Logic:**
    ```razor
    @if (Instruction is AssignRolesInstruction ari) { <AssignRolesView Data="@ari" /> }
    else if (Instruction is SelectPlayersInstruction spi) { <SelectPlayersView Data="@spi" /> }
    else if (Instruction is ConfirmationInstruction ci) { <ConfirmationView Data="@ci" /> }
    ```

## 6. Coding Conventions for LLMs

When asking an LLM to generate code, paste this section:

> **Coding Rules:**
> 1.  **Framework:** Use .NET 8, MAUI Blazor Hybrid.
> 2.  **UI Library:** Use **MudBlazor** for ALL UI elements. Do not write CSS.
> 3.  **State Access:** Inject `GameClientManager`. Bind generic UI properties to `GameClientManager.ActiveSession.{Property}`.
> 4.  **Reactivity:** In Razor components, override `OnInitialized` to subscribe to `GameClientManager.OnStateChanged += StateHasChanged`. Implement `IDisposable` to unsubscribe.
> 5.  **Null Safety:** `ActiveSession` is nullable. Handle the null state (show a "Loading" or "Lobby" view).
> 6.  **Core Integration:** Never duplicate Core logic (e.g., do not calculate who won; read it from the Log or properties).

## 7. Project Structure

```text
Werewolves.Client/
├── MauiProgram.cs             # Registers MudBlazor, Audio, GameClientManager
├── Services/
│   ├── GameClientManager.cs   # The Bridge
│   └── AudioMap.cs            # Maps Enum -> Filenames
├── Components/
│   ├── Layout/
│   │   └── MainLayout.razor
│   ├── Pages/
│   │   ├── Lobby.razor        # Setup & Drag-drop
│   │   └── Dashboard.razor    # Main Game Container
│   └── Game/
│       ├── InstructionRenderer.razor
│       ├── Views/
│       │   ├── AssignRolesView.razor
│       │   ├── SelectPlayersView.razor
│       │   └── ...
│       └── DashboardTabs/
│           ├── RosterTab.razor
│           └── StatsTab.razor
└── wwwroot/
    └── css/
        └── app.css            # (Empty/Minimal only)
```

## 8. Next Steps (Implementation Order)

1.  **Scaffold:** Create MAUI Blazor project, install `MudBlazor` and `Plugin.Maui.Audio`.
2.  **Core Link:** Reference Core projects. Register `GameService` and `GameClientManager` in DI.
3.  **Lobby UI:** Implement `Lobby.razor` with `MudList` and basic inputs.
4.  **The Bridge:** Connect `Lobby` Start button to `GameClientManager.StartGame`.
5.  **Dashboard Shell:** Create the Layout and Tabs.
6.  **Instruction Views:** Implement the specific instruction handlers one by one.