# todo: 
 
## add helper functions to validate expected input types 
 
## instead of having GameSession._masterNightWakeupOrder, move that instead to be a Dictionary<GamePhase,List<RoleType>> where each game phase can have its own call order for different roles? 
 
### **Architectural Refinement Proposal: A Hook-Based Dispatch System (Revision 2.1)** 
 
#### **1. Abstract** 
 
This proposal is a targeted refinement of the "Role-Centric State Machine Model" (Revision 2). It builds upon that foundation by addressing a limitation in its proposed dispatch mechanism. While the previous proposal successfully decouples role-specific state logic from the `GameFlowManager`, its reliance on phase-based triggers is not granular enough to handle complex, event-driven role activations (e.g., priority interrupts). This refinement introduces a **Hook-Based Dispatch System**. The `GameFlowManager` is further simplified into a linear process orchestrator that **fires specific, named "Game Hooks"** at critical moments in the game loop. The master activation logic is moved from a phase-based map to a declarative, hook-based dictionary, enabling precise, event-driven activation of role state machines. This change achieves a more robust and complete decoupling of the `GameFlowManager` from any role-specific implementation knowledge. 
 
--- 
 
#### **2. Motivation** 
 
The transition to a role-centric state machine is a sound architectural direction. However, analysis of complex role interactions reveals a critical flaw in using `GamePhase` as the primary trigger for role activation. The trigger for many abilities is not the *start* of a phase, but a discrete *event* that occurs within or at the conclusion of a phase. 
 
*   **Limitation of Phase-Based Triggers:** A `Dictionary<GamePhase, ...>` structure is too coarse. For example: 
    *   The **Hunter's** ability triggers upon their elimination, an event that can occur as a result of either the `Day_ResolveNight` or `Day_ResolveVote` phase. Tying the Hunter to a single phase is incomplete and would require the `GameFlowManager` to retain special-case logic to handle the alternate scenario. 
    *   The **Stuttering Judge's** ability to force a re-vote must be triggered *after* a vote outcome is submitted but *before* that vote is resolved. This is a specific moment at the boundary between the `Day_Vote` and `Day_ResolveVote` phases. A phase-based trigger lacks the required precision. 
 
Attempting to model these event-driven abilities with phase-based triggers would inevitably lead to the re-introduction of conditional, role-aware logic within the `GameFlowManager`, undermining the primary goals of the original proposal. A more granular dispatch mechanism is required. 
 
--- 
 
#### **3. Proposed Architectural Changes** 
 
This refinement replaces the phase-based dispatch map with a system of explicit Game Hooks. The `GameFlowManager`'s responsibility shifts from interpreting the game state to executing a linear process that fires these hooks, delegating all event-based responses to the roles registered to them. 
 
##### **3.1. Introduction of the `GameHook` Enum** 
 
A new enum will be created to define the specific, instantaneous moments in the game loop that can trigger role logic. This enum becomes the formal contract between the `GameFlowManager` and the role activation system. 
 
*   **Proposed Enum Definition:** 
    ```csharp 
    public enum GameHook 
    { 
        /// <summary> 
        /// Fired once at the start of the night phase to begin the sequence of night actions. 
        /// </summary> 
        NightSequenceStart, 
 
        /// <summary> 
        /// Fired after the moderator submits the vote outcome, but before the GFM 
        /// resolves that outcome and eliminates a player. 
        /// </summary> 
        OnVoteOutcomeSubmitted, 
 
        /// <summary> 
        /// Fired immediately after a player's status has been definitively changed to Dead, 
        /// regardless of the cause (night attack, vote, etc.). 
        /// </summary> 
        OnPlayerEliminationFinalized, 
 
        // Additional hooks can be defined as needed (e.g., OnRoleRevealed, OnSheriffAppointed). 
    } 
    ``` 
 
##### **3.2. Redefinition of the Master Activation Order** 
 
The master role activation logic will be stored in a new data structure that maps hooks to an ordered list of `RoleType` identifiers. 
 
*   **New Data Structure:** 
    `private readonly Dictionary<GameHook, List<RoleType>> _masterRoleActivationOrder;` 
 
*   **Rationale:** 
    *   This structure allows multiple roles to react to the same event. 
    *   The use of a `List<RoleType>` is intentional, as the order of activation can be critical for resolving rule interactions. The sequence in the list defines the order of operations. 
 
*   **Example Configuration:** 
    ```csharp 
    _masterRoleActivationOrder = new() 
    { 
        // The night wake-up order is triggered by a single hook. 
        [GameHook.NightSequenceStart] = new List<RoleType> 
        { 
            RoleType.SimpleWerewolf, 
            RoleType.Seer, 
            RoleType.Defender, 
            // ... etc. 
        }, 
 
        // The Stuttering Judge is the only role that acts on this specific event. 
        [GameHook.OnVoteOutcomeSubmitted] = new List<RoleType> 
        { 
            RoleType.StutteringJudge 
        }, 
 
        // Multiple roles could potentially react to a player's elimination. 
        [GameHook.OnPlayerEliminationFinalized] = new List<RoleType> 
        { 
            RoleType.Hunter 
            // A theoretical "Lovers" role could also be added here to handle heartbreak. 
        } 
    }; 
    ``` 
 
##### **3.3. `GameFlowManager` as a Hook-Firing Orchestrator** 
 
The `GameFlowManager`'s phase handlers are simplified further. Their responsibility is to execute their core logic (e.g., process input, update state) and fire the appropriate hooks at the correct points in their lifecycle. 
 
*   A new private method, `FireHook(GameHook hook, GameSession session)`, will be implemented within the `GameFlowManager`. 
*   This method's logic is simple: 
    1.  Check if the `_masterRoleActivationOrder` contains the given `hook`. 
    2.  If so, iterate through the `List<RoleType>` in order. 
    3.  For each `RoleType`, retrieve the corresponding `IRole` instance and call its `AdvanceStateMachine` method. 
*   Crucially, the phase handler method itself remains completely agnostic to which roles, if any, are listening for the hook it fires. This achieves a complete separation of concerns. 
 
--- 
 
#### **4. Detailed Interaction Flow Example: Stuttering Judge Re-vote** 
 
This sequence illustrates how the hook-based system handles a complex, non-linear game event. 
 
1.  **Vote Submission:** The game is in phase `Day_Vote`. The moderator submits the ID of the player to be eliminated. The `GameFlowManager.HandleInput` method routes this to `HandleDayVotePhase`. 
2.  **Core Phase Logic:** The `HandleDayVotePhase` method validates the input and records the outcome in `session.PendingVoteOutcome`. 
3.  **Hook Firing:** Before transitioning to `Day_ResolveVote`, the handler executes its final responsibility: 
    `FireHook(GameHook.OnVoteOutcomeSubmitted, session);` 
4.  **Dispatch:** The `FireHook` method looks up `OnVoteOutcomeSubmitted` in the master dictionary and finds `RoleType.StutteringJudge`. It retrieves the `StutteringJudgeRole` instance and calls `stutteringJudgeRole.AdvanceStateMachine(session, initialTriggerInput)`. 
5.  **Role Logic (Scenario A - Inert):** The Judge has already used their power or is not in a state to act. The `AdvanceStateMachine` method returns `PhaseHandlerResult.Inert`. The `FireHook` method completes, and `HandleDayVotePhase` proceeds with its normal transition to `Day_ResolveVote`. 
6.  **Role Logic (Scenario B - Active):** The Judge's conditions are met. The moderator confirms they wish to trigger a re-vote. The role's state machine processes this and returns a result indicating a phase transition is required: `PhaseHandlerResult.SuccessTransition(reason: StutteringJudgeReVote, instruction: newInstruction)`. This result explicitly targets the `Day_Vote` phase again. 
7.  **Reactive GFM:** The `GameFlowManager` receives this result from the `FireHook` call. It does not need to know *why* the transition is happening; it simply obeys the result, discards its planned transition to `Day_ResolveVote`, and instead transitions the game state back to `Day_Vote` with the new instruction provided by the Judge's role. 
 
--- 
 
#### **5. Impact Analysis** 
 
*   **Benefits:** 
    *   **Precision and Granularity:** Roles are activated by the exact, specific game events that should trigger them, eliminating ambiguity. 
    *   **True Decoupling:** The `GameFlowManager` is now fully decoupled from knowledge of any specific role's special abilities. Its phase handlers become simple, linear procedures that fire hooks. 
    *   **Declarative Logic:** The game's entire event-response flow becomes explicit and is centralized in the `_masterRoleActivationOrder` dictionary, making it highly readable and maintainable. 
    *   **Enhanced Extensibility:** Adding new, complex, event-driven roles becomes a safe and isolated process of implementing the `IRole` and registering it to the correct `GameHook`. No modification to the `GameFlowManager`'s core logic is required. 
 
*   **Considerations & Mitigations:** 
    *   **Hook Proliferation:** There is a risk of creating too many hooks, leading to a different kind of complexity. 
        *   **Mitigation:** A strict discipline must be enforced. Hooks should be created for events that can trigger cross-cutting concerns or interrupt the standard game flow. They should not be created for every minor, role-internal action. 
    *   **Traceability:** Understanding why a specific role activated now requires looking at the hook dictionary, not just the GFM code. 
        *   **Mitigation:** The `GameHook` enum itself must be treated as a primary architectural artifact. Each enum member must be thoroughly documented with XML comments explaining precisely when and where it is fired from within the `GameFlowManager`. This documentation is a critical requirement for maintainability. 
    *   **Complex Hook Interactions:** If multiple roles are registered to the same hook, their interaction must be well-defined. 
        *   **Mitigation:** The ordered nature of the `List<RoleType>` in the dictionary is the explicit mechanism for managing this. The order defines priority. This interaction must be a conscious design decision, documented as part of the game's core rules, and validated with integration tests. 
 
 
 
# Implementation prompts: 
 
plan out the required changes to implement **phase 2 step 1 (seer)** in `implementation-roadmap.md` . do not start implementing it yet. seriously and thoughtfully consider the currently defined architecture in `architecture.md` , consider if there are any changes you think would make sense to make for the implementation effort in question. if you find any, run them by me and state your case first, so I can assess whether or not I want to go forward with them.  
 
 
proceed with the implementation along the lines of what you're previously described. only go one file at a time, and ask me to confirm if I approve of your implementation of that specific file before continuing to the next one. be prepared to refactor as you go