# Missing Features for Basic Game (Villagers, Werewolves, Seer)

This document outlines the missing components needed to get a minimal functional game running with just Villagers, Werewolves, and Seer roles.

## Current Implementation Status

### ✅ Already Implemented
- **Basic Architecture**: Hook system, state management, phase definitions, State Mutator Pattern
- **Core Roles**: SimpleWerewolfRole, SeerRole, SimpleVillagerRole implementations (framework exists)
- **Game Flow**: Complete phase structure (Setup → Night → Dawn → Debate → Vote → Dusk) with declarative sub-phases
- **State Models**: GameSession, Player, PlayerState, GamePhaseStateCache (foundational structure complete)
- **Log System**: Comprehensive log entry types with State Mutator Pattern integration
- **Instructions**: Polymorphic instruction system with validation
- **Victory Conditions**: Basic werewolf vs villager win checking (implemented)
- **String Resources**: Most GameStrings are implemented (verified in GameStrings.Designer.cs)
- **Hook System**: Complete hook dispatch and listener management
- **Phase Definitions**: Declarative phase and sub-phase state machine implementation

## ❌ Critical Missing Features

### 2. Role Implementation Issues

#### SimpleWerewolfRole Problems
- **Missing Log Application**: Creates `WerewolfVictimChoiceLogEntry` but doesn't add it to the log

#### SeerRole Problems
- **Incomplete Team Detection**: `DoesPlayerWakeWithWerewolves()` has TODO comments for infection, Wild Child, etc.
- **No Log Integration**: Uses `session.PerformNightAction()` but this may not be fully implemented

### 3. GameFlowManager Implementation Gaps

#### Missing Sub-Phase Handlers
Several sub-phase handlers are declared but have placeholder implementations:

```csharp
// These methods contain TODO comments or placeholder logic
HandleDawnCalculateVictims() // Only returns placeholder instruction
HandleDawnAnnounceVictims()   // Only returns placeholder instruction  
HandleDawnProcessRoleReveals() // Only returns placeholder instruction
HandleDayDuskResolveVote()     // Only returns placeholder instruction
HandleDayDuskTransitionToNext() // Always assumes tie, goes to night
```

#### Missing Night Action Processing
- **No Victim Calculation**: `HandleDawnCalculateVictims()` doesn't actually process night actions
- **No Role Reveal Logic**: `HandleDawnProcessRoleReveals()` doesn't handle actual role reveals
- **No Vote Resolution**: `HandleDayDuskResolveVote()` doesn't process vote outcomes

#### Missing Hook System Integration
- **No State Persistence**: Hook results aren't properly persisted to game state

### 4. GameService Integration Issues

#### StartNewGame Status
- ✅ **Basic GameSession Creation**: Creates GameSession with players and roles
- ✅ **Initial State Setup**: Sets up initial phase state
- ❓ **Role Assignment**: May need enhancement for initial role distribution
- ❓ **GameStartedLogEntry**: May need to be added for complete audit trail
- ❓ **Initial Instruction**: May need refinement for better moderator guidance

#### Input Validation Status
**UPDATE: Basic validation exists but may need enhancement**
- ✅ **Type Validation**: Basic input type checking is implemented
- ❓ **Rule Validation**: May need comprehensive game rule validation
- ❓ **Error Instructions**: May need more specific error messages

**Priority**: MEDIUM - Basic functionality exists but may need refinement

### 5. Log Entry Implementation Gaps

#### Missing Apply() Methods
Several log entries have empty or incomplete `Apply()` methods:

```csharp
// These need proper state mutation logic
WerewolfVictimChoiceLogEntry.Apply() // Empty implementation
NightActionLogEntry.Apply()          // Only has default case
AssignRoleLogEntry.Apply()           // Comment says "doesn't set actual role"
InitialRoleLogAssignment.Apply()     // Same as above
```

### 6. State Model Missing Properties

#### PlayerState Missing Fields
The architecture defines many PlayerState properties that aren't implemented:

```csharp
public bool CanVote { get; internal set; }
```


**Priority**: LOW - Most critical strings are already implemented

## 🔧 Priority Implementation Order

### Phase 1: Critical Blockers (Highest Priority)
1. **Fix WerewolfRole Log Integration** - Verify all the log integration is implemented as it should
2. **Complete Log Entry Apply() Methods** - Essential for state persistence and reconstruction
3. **Implement Dawn Victim Calculation** - Process night actions to determine eliminations

### Phase 2: Core Game Logic (High Priority)
5. **Add Basic Vote Resolution** - Handle vote outcomes and player eliminations
6. **Complete Sub-Phase Handlers** - Replace placeholder implementations with actual logic
7. **Implement Role Reveal Logic** - Handle actual role reveals during dawn phase

### Phase 3: Game Flow Integration (Medium Priority)
9. **Enhance GameService StartNewGame** - Add role assignment and initial instruction if needed
10. **Add Input Validation** - Implement comprehensive rule validation


### Architecture Compliance Assessment
- ✅ **State Mutator Pattern**: Properly implemented in most log entries
- ✅ **Hook System**: Correctly implemented with proper dispatch mechanism
- ✅ **Phase Definitions**: Declarative sub-phase structure is properly implemented
- ❓ **Log Entry Comments**: Some comments about "not setting actual role" may need clarification
- ❓ **Hook Listener Skipping**: Current behavior of skipping unimplemented listeners is appropriate for development

### Testing Considerations
- **Runtime Exceptions**: Missing query methods will cause immediate failures in role logic
- **State Reconstruction**: Without proper `Apply()` methods, game state cannot be reconstructed from logs
- **Game Flow**: Placeholder phase handlers will prevent actual game progression
- **Victory Conditions**: Basic victory checking is implemented but may need enhancement for edge cases

### Key Insights from Code Analysis
1. **Foundation is Solid**: The core architecture, hook system, and phase management are well-implemented
2. **Missing "Last Mile" Logic**: Most components exist but need specific business logic implementation
3. **Incremental Development**: The architecture supports incremental implementation of missing features

This analysis provides a realistic roadmap for implementing the missing features needed to get a basic functional game running with the three core roles. The situation is significantly less critical than initially assessed - the foundation is solid and primarily needs specific logic implementation and completion of placeholder handlers.
