### 1. Architecture & Core Principles
*   **Form Factor:** Mobile-first, touch-centric.
*   **Units:** All dimensions must be specified in `dp` (Device Independent Pixels).
*   **Philosophy:** "Dumb Terminal." The UI does not calculate game state, adjacency, or validity. It renders the state provided by the Core and passes user input back.
*   **Navigation Paradigm:** "Carousel" Navigation.
    *   Three main views (Tabs).
    *   Support for **Infinite Horizontal Swiping** (Looping: Stats -> Roster -> Pending -> Stats).
*   **State Authority:**
    *   **Adjacency:** The Core guarantees `IGameSession.GetPlayers()` returns players in seating order. The UI renders them linearly, visually connecting the first and last elements to form the circle.
    *   **Localization:** 
        *   Game strings (Instructions, Roles, Announcements) are provided raw by the Core.
        *   UI shell strings (e.g., "Settings", "Back") are handled by the frontend framework's localization.

---

### 2. View 1: The Lobby (Setup)
*   **Roster Management:**
    *   Text input to add names.
    *   **Drag-and-Drop Reordering:** This is the *only* place reordering is allowed. The resulting index order is sent to the Core as the `SeatingOrder`.
*   **Role Configuration:**
    *   List of available roles (fetched from Core metadata).
    *   Counter interface for quantity.
*   **Start Game Trigger:** 
    *   Attempts to instantiate `GameSessionConfig` and start the game via `GameService`. 
    *   Catches and displays any validation exceptions thrown by the Core (e.g., "Mason count must be 2").

---

### 3. View 2: Gameplay - The Dashboard (The 3 Tabs)

#### Tab A: Player List (Left)
*   **Layout:** Vertical, **Infinite Scroll** list (Visual loop: Player 1 appears immediately after Player N).
*   **Item Appearance:**
    *   **Alive:** Standard opacity.
    *   **Dead:** Greyed out visually.
*   **Data Source:** Renders `IGameSession.GetPlayers()`.
*   **Interaction:**
    *   **Tap:** Expands the list item (Accordion style) to reveal:
        *   Role (Always shown if known to Moderator via `IPlayer.State`).
        *   Status Effects (e.g., "Sheriff", "Infected").
*   **Controls:**
    *   **Toggle Dead:** A switch to Hide/Show dead players to reduce clutter (local UI state only).

#### Tab B: Pending Instruction (Center - Default)
*   **Header:**
    *   **Auto-Timer:** A generic timer (MM:SS) that resets on new instruction.
    *   **Undo Button:** (Placeholder) A button to roll back the last action. *Initially disabled/hidden until Core supports Log truncation.*
*   **Body:**
    *   Renders the specific **Instruction Handler** (see Section 4).
*   **Footer/Context:**
    *   **Audio Control:** A Play/Pause button. Visible only if `ModeratorInstruction.SoundEffect` is not `None`. Maps the Enum to a local asset.

#### Tab C: Game Overview (Right)
*   **Stats:**
    *   Data fetched directly from `IGameSession` properties.
    *   Current Phase (Night/Day).
    *   Turn Count.
    *   *Future:* Victory/Team Balance (if exposed by Core).

---

### 4. Instruction Handlers (The "Pending Instruction" Tab Logic)
The Center Tab dynamically loads components based on the `ModeratorInstruction` subclass.

**General UI Elements (for all instructions):**
*   **Public Announcement:** Large text (from Core) to be read aloud.
*   **Private Note:** Italicized text (from Core) for the Moderator.

#### A. `AssignRolesInstruction` Handler
*   **UX Flow:** Wizard / Stepper.
*   **Data Requirement:** The instruction **must** contain a `Dictionary<MainRoleType, int>` representing the *Available Role Inventory* (e.g., { "Werewolf": 2, "Seer": 1 }).
*   **Process:**
    1.  UI iterates through `AffectedPlayerIds`.
    2.  **Screen:** "Assign Role for [Player Name]".
    3.  **Selection:** Shows list of Available roles.
        *   Decrements local counter as roles are selected.
        *   Disables options when count reaches 0.
    4.  **Submit:** Enabled only when all target players have a role.

#### B. `SelectPlayersInstruction` Handler
*   **Layout:** Reuses the **Player List** component.
*   **Interaction:**
    *   **Tap:** Toggles Selection state (Checkmark).
    *   **Header:** Displays constraints (e.g., "Select 1 to 2 players").
*   **Validation:**
    *   "Confirm" button enabled only when `SelectionCount >= Min` and `SelectionCount <= Max`.
    *   **Tie/Skip Handling:** If `Min == 0`, the "Confirm" button is enabled immediately (sending an empty list).

#### C. `SelectOptionsInstruction` Handler
*   **Layout:** Vertical list of text options.
*   **Interaction:** Radio buttons (if Single select) or Checkboxes (if Multi select).
*   **Validation:** Enforce Min/Max selection constraints.

#### D. `ConfirmationInstruction`
*   **Action:** Single "Proceed" / "Yes" button.
*   *Note:* Core handles the text (e.g., "Did the Seer see a Werewolf?").

---

### 5. Required Updates to Core Library
To support these requirements, the following updates are needed in `Werewolves.StateModels` and `Werewolves.GameLogic`:

1.  **`ModeratorInstruction`:**
    *   Add `SoundEffectType? SoundEffect { get; }` (Enum).
2.  **`AssignRolesInstruction`:**
    *   Add `IReadOnlyDictionary<MainRoleType, int> AvailableRoles { get; }`
    *   *Rationale:* UI cannot calculate which roles are left to be assigned during a setup or event phase; the instruction must provide the "Hand".
3.  **`Enums`:**
    *   Create `public enum SoundEffectType`.
4.  **`GameSessionConfig`:**
    *   Ensure validation logic is exposed or thrown immediately upon construction so the UI can catch it during the "Lobby" phase.