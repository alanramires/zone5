# Zone 5 — Refactor Plan

**Version:** 1.1 | **Date:** 2026-04-21 | **Status:** Active Design Document
**Codebase:** ~47 scripts | **Engine:** Unity 2022+ | **Language:** C# | **Namespace:** Zone5

> **v1.1 — UI overhaul:** cockpit panels removidos. Sistema de cartas ao estilo Ticket to Ride substitui o cockpit. Panels deletados: `PanelHudDefaults`, `ButtonSelectionGroup`, `PanelManeuverSubmitter`, `PanelMancheController`, `PanelMancheDefaults`, `PanelThrottleController`, `PanelWeaponsController`, `PanelWeaponsDefaults`.

---

## Executive Summary

Zone 5 is a turn-based simultaneous air combat game that has grown organically from MVP toward a feature-complete system. The code works, but structural debt is compounding. `MatchControllerMvp.cs` at 987 lines is doing the work of five systems. The FSM in `TurnManager.cs` is a flat switch-case that cannot accommodate sub-flows or modal UI. Two parallel data formats (`ManeuverDef`/`ManeuverProfile` and `MissilePathDef`/`MissilePathProfile`) coexist with fallback branching logic. The new stance/direction UI panels exist as isolated files that fire visual events but are not yet wired to the game loop. And 15+ panel scripts all discover their dependencies via `FindFirstObjectByType<>()` at runtime, with no guaranteed execution order.

This document defines a four-phase refactor that addresses each problem area in priority order, with zero breaking behavior changes in Phase 0 and incremental integration thereafter. It is the primary design document for the next several weeks of work.

---

## Current Dependency Map (Abbreviated)

```
TurnManager.OnStateChanged
    ├── MatchControllerMvp      (game logic + movement + collisions + scoring)
    ├── PanelPhaseController        (exibe fase e turno)
    ├── PanelGroupController        (agrega seleções de manobra)
    ├── PanelStanceController       (cartas de stance — novo sistema)
    ├── PanelDirectionsController   (cartas de direção — novo sistema)
    ├── PanelPilot / PilotRowView   (status do piloto ativo)
    ├── PanelTurn                   (contador de turno)
    ├── TurnStateManager            (redundant relay — singleton, a deletar)
    └── Panel Tactical              (visão tática)

MatchControllerMvp
    ├── ManeuverManager         (resolves ManeuverProfile; also falls back to ManeuverCatalog)
    ├── MissileManager          (spawns + resolves; also falls back to MissilePathCatalog)
    ├── TrailManager            (draws paths)
    ├── MovementCore            (static math utilities)
    └── AircraftUnit[]          (found via FindObjectsByType every call)
```

---

## Problem Register

| ID | Area | Severity | Description |
|----|------|----------|-------------|
| P1 | Architecture | Critical | `MatchControllerMvp` is a 987-line God Object |
| P2 | FSM | High | `TurnManager.NextState()` is a flat switch with no modal sub-flow support |
| P3 | Data | High | Dual legacy systems (`ManeuverDef`+`ManeuverProfile`, `MissilePathDef`+`MissilePathProfile`) |
| P4 | UI | High | ~8 panels subscrevem a `OnStateChanged` independentemente; execução sem ordem garantida |
| P5 | Design | High | No interfaces (`IState`, `IResolvable`, `ITurnPhase`) — everything concrete |
| P6 | DI | Medium | `FindFirstObjectByType<>()` used as dependency injection at Awake/runtime |
| P7 | UI | Active | `PanelStanceController`, `PanelDirectionsController`, `PanelStanceCardClick` are unconnected to game logic |
| P8 | Duplication | Low | Geometry math (`MinDistanceSegmentToSegment2D`, `SegmentsIntersect2D`, `CatmullRom`) duplicated across `MissileManager`, `CollisionSystem`, `ManeuverProfile` |

---

## Phase Overview

| Phase | Name | Goal | Risk | Duration Estimate |
|-------|------|------|------|-------------------|
| 0 | Foundations | Add interfaces + stack FSM + UI orchestrator; no behavior change | Low | 1–2 weeks |
| 1 | Break up MatchControllerMvp | Extract 4 resolver classes; thin out god object | Medium | 1–2 weeks |
| 2 | Deprecate Legacy Systems | Remove `ManeuverDef`/`MissilePathDef` fallback logic | Medium | 1 week |
| 3 | Wire Stance UI | Connect stance+direction panel to maneuver selection | Medium | 1–2 weeks |
| 4 | Polish and Hardening | Replace `FindFirst`, add guards, naming, perf | Low | 1 week |

---

## Phase 0: Foundations

### Motivation

Every other phase depends on a clean contract layer and a trustworthy FSM. Adding interfaces first means Phase 1 extractions can be coded against contracts instead of concrete classes. Adding the stack-based FSM first means Phase 3's stance-select sub-flow has somewhere to push states. Adding the UI orchestrator first means Phase 3's new panels have a clear integration point. None of this changes runtime behavior — it is scaffolding.

---

### 0.1 — Define Core Interfaces

**Files to create:**

- `Assets/Scripts/Game/Interfaces/ITurnPhase.cs`
- `Assets/Scripts/Game/Interfaces/IResolvable.cs`
- `Assets/Scripts/Game/Interfaces/ITurnResolver.cs`

**Signatures:**

```csharp
// ITurnPhase.cs
namespace Zone5
{
    public interface ITurnPhase
    {
        GameEnum.TurnState State { get; }
        void Enter(TurnContext context);
        void Exit(TurnContext context);
        bool IsComplete(TurnContext context);
    }
}

// IResolvable.cs
namespace Zone5
{
    public interface IResolvable
    {
        System.Collections.IEnumerator Resolve(TurnContext context);
    }
}

// ITurnResolver.cs
namespace Zone5
{
    public interface ITurnResolver : IResolvable
    {
        bool IsFinished { get; }
        void Reset();
    }
}
```

**Also create:**

- `Assets/Scripts/Game/TurnContext.cs` — lightweight value object passed between phases/resolvers to avoid direct `TurnManager` coupling.

```csharp
// TurnContext.cs
namespace Zone5
{
    public class TurnContext
    {
        public TurnSheet Sheet { get; }
        public int TurnIndex => Sheet.turnIndex;
        public GameEnum.TurnState Phase => Sheet.phase;
        public TurnContext(TurnSheet sheet) { Sheet = sheet; }
    }
}
```

**Complexity:** S
**Risks:** None — pure additions, no existing code touched.
**Acceptance Criteria:**
- All files compile without warnings.
- No existing class modified.
- `TurnContext` can be constructed from `TurnSheet`.

---

### 0.2 — Stack-Based FSM for TurnManager

**Motivation:** The current `NextState()` switch has 12 hardcoded transitions. Adding the stance UI sub-flow (Phase 3) requires pushing a temporary sub-state without losing the parent context. The sister project's `Stack<CursorState>` pattern with `Advance/Retreat/ExecuteAndReset` is the proven solution.

**Files to modify:**
- `Assets/Scripts/Game/TurnManager.cs` — add stack fields and three public navigation methods; keep existing `NextState()` as internal delegate.

**Files to create:**
- `Assets/Scripts/Game/TurnStateStack.cs` — the stack mechanics, extracted so TurnManager stays a MonoBehaviour.
- `Assets/Scripts/Game/StateStackGuards.cs` — runtime invariant validation.

**TurnStateStack signatures:**

```csharp
namespace Zone5
{
    public class TurnStateStack
    {
        private readonly Stack<GameEnum.TurnState> _stack = new();

        public GameEnum.TurnState Current =>
            _stack.Count > 0 ? _stack.Peek() : GameEnum.TurnState.SelectManeuver;

        public int Depth => _stack.Count;

        public void Advance(GameEnum.TurnState state) { _stack.Push(state); }

        public GameEnum.TurnState Retreat()
        {
            if (_stack.Count > 0) _stack.Pop();
            return Current;
        }

        public void ExecuteAndReset(GameEnum.TurnState root)
        {
            _stack.Clear();
            _stack.Push(root);
        }

        public bool Contains(GameEnum.TurnState state) => _stack.Contains(state);
    }
}
```

**StateStackGuards signatures:**

```csharp
namespace Zone5
{
    public static class StateStackGuards
    {
        public static bool IsValidTransition(
            GameEnum.TurnState parent,
            GameEnum.TurnState child) { ... }

        public static void AssertValid(
            GameEnum.TurnState parent,
            GameEnum.TurnState child) { ... }
    }
}
```

**TurnManager additions (do not remove existing NextState/EnterState):**

```csharp
private TurnStateStack _stateStack = new TurnStateStack();

public void PushState(GameEnum.TurnState state);
public void PopState();
public void ResetToState(GameEnum.TurnState state);
public GameEnum.TurnState CurrentStateFromStack => _stateStack.Current;
```

**Partial class split of TurnManager:**

```
Assets/Scripts/Game/
├── TurnManager.cs                  (class declaration, fields, Awake/Start)
├── TurnManager.Registration.cs     (RegisterUnit, GetOrCreatePlayerRow, SetAlive)
├── TurnManager.Transitions.cs      (NextState, EnterState, TryAdvance, AdvanceButton)
├── TurnManager.Stack.cs            (PushState, PopState, ResetToState — new)
└── TurnManager.Events.cs           (NotifySheetChanged, SetMatchEnded, event declarations)
```

**Complexity:** M
**Risks:**
- Partial class files must all share the same namespace and class name — straightforward in C#.
- Existing subscribers (15+ panels) continue to work because `OnStateChanged` signature does not change.
- The stack operates in parallel with `sheet.phase` initially; they are reconciled in Phase 3.

**Acceptance Criteria:**
- `TurnStateStack` unit-testable in isolation (no MonoBehaviour dependency).
- `TurnManager.PushState/PopState` compile and fire `OnStateChanged` correctly.
- All existing panel subscribers still receive events.
- `StateStackGuards.AssertValid` catches at least 3 known illegal transitions.

---

### 0.3 — UI Orchestrator Layer

**Motivation:** Os panels restantes (`PanelPhaseController`, `PanelGroupController`, `PanelStanceController`, `PanelDirectionsController`, `PanelPilot`, `PanelTurn`, `Panel Tactical`) subscrevem `OnStateChanged` independentemente. Execução sem ordem garantida. O orchestrator vira o único subscriber e distribui estado via `IPanelController`, com ordem controlada pelo array serializado no Inspector.

**Files to create:**
- `Assets/Scripts/Panels/UIStateOrchestrator.cs`
- `Assets/Scripts/Panels/IPanelController.cs`

**Signatures:**

```csharp
// IPanelController.cs
namespace Zone5
{
    public interface IPanelController
    {
        void OnPhaseEntered(GameEnum.TurnState state, TurnContext context);
    }
}

// UIStateOrchestrator.cs
namespace Zone5
{
    public class UIStateOrchestrator : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private MonoBehaviour[] panelControllers;

        private void OnEnable()  { turnManager.OnStateChanged += HandleStateChanged; }
        private void OnDisable() { turnManager.OnStateChanged -= HandleStateChanged; }

        private void HandleStateChanged()
        {
            var ctx = new TurnContext(turnManager.sheet);
            foreach (var panel in panelControllers)
            {
                if (panel is IPanelController pc)
                    pc.OnPhaseEntered(ctx.Phase, ctx);
            }
        }
    }
}
```

**Migration plan for existing panels:**
- Each panel adds `: IPanelController`, implements `OnPhaseEntered`, and unsubscribes from `OnStateChanged` directly.
- Migration can be done panel-by-panel, one commit at a time.
- `PanelPhaseController` is the first candidate.

**Complexity:** S (orchestrator itself) + M (migrating panels one-by-one)
**Risks:**
- Until all panels migrate, two subscription paths coexist — acceptable temporarily.
- Panel order matters (e.g. phase label before score panel). Serialized array order is sufficient.

**Acceptance Criteria:**
- `UIStateOrchestrator` is in scene and subscribed to `TurnManager`.
- `PanelPhaseController` implements `IPanelController` and no longer subscribes directly.
- No regressions in panel behavior.
- Pelo menos `PanelPhaseController` e `PanelTurn` migrados para `IPanelController` ao final da Fase 0.

---

### Phase 0 File Summary

| Action | File |
|--------|------|
| CREATE | `Assets/Scripts/Game/Interfaces/ITurnPhase.cs` |
| CREATE | `Assets/Scripts/Game/Interfaces/IResolvable.cs` |
| CREATE | `Assets/Scripts/Game/Interfaces/ITurnResolver.cs` |
| CREATE | `Assets/Scripts/Game/TurnContext.cs` |
| CREATE | `Assets/Scripts/Game/TurnStateStack.cs` |
| CREATE | `Assets/Scripts/Game/StateStackGuards.cs` |
| CREATE | `Assets/Scripts/Game/TurnManager.Registration.cs` |
| CREATE | `Assets/Scripts/Game/TurnManager.Transitions.cs` |
| CREATE | `Assets/Scripts/Game/TurnManager.Stack.cs` |
| CREATE | `Assets/Scripts/Game/TurnManager.Events.cs` |
| MODIFY | `Assets/Scripts/Game/TurnManager.cs` (reduce to class shell + partial keyword) |
| CREATE | `Assets/Scripts/Panels/IPanelController.cs` |
| CREATE | `Assets/Scripts/Panels/UIStateOrchestrator.cs` |
| MODIFY | `Assets/Scripts/Panels/PanelPhaseController.cs` (first panel migration) |

---

## Phase 1: Break Up MatchControllerMvp

### Motivation

`MatchControllerMvp.cs` at 987 lines handles: movement execution, trail rendering, segment caching for collision, aircraft-aircraft collision detection, aircraft-missile collision, evasion rolls, damage application, score computation, kill attribution, and victory detection. These are seven distinct responsibilities. A bug in evasion logic requires reading past movement animation code. Adding a new weapon type requires touching scoring. This cannot continue.

The extraction strategy is additive: new classes are created, logic moves into them, and `MatchControllerMvp` becomes a thin coordinator that delegates.

**Note:** `MissileManager` already contains `ApplyEvasionRoll` and `ApplyDamage` — a partial extraction started but not completed. Phase 1 finishes that work.

---

### 1.1 — Extract MovementOrchestrator

**Responsibility:** Owns the movement execution loop. Resolves `ManeuverProfile`, builds world paths, calls `MovementCore`, drives `AircraftView` animations, caches path segments for collision detection.

**File to create:** `Assets/Scripts/Game/Resolvers/MovementOrchestrator.cs`

**Moved from MatchControllerMvp:**
- `RevealAndMoveFighters()`
- `ExecuteManeuver()`
- `ExecuteManeuverLegacy()`
- `StartAircraftAnimation()`
- `ClearTurnSegments()`, `AddTurnSegment()`
- `CacheLastEnds()`, `TryGetLastEnd()`, `SetLastEnd()`, `SyncLastEndFromTransform()`
- Helpers: `ParseDirFromRaw()`, `GetManeuverCodesFromRow()`, `AddFlightHistoryEntries()`, `ParseSpeedFromCode()`, `MapDirFromRaw()`, `GetStrongTeamColor()`

**Signature shape:**

```csharp
namespace Zone5
{
    public class MovementOrchestrator : MonoBehaviour, ITurnResolver
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private ManeuverManager maneuverManager;
        [SerializeField] private TrailManager trailManager;
        [SerializeField] private bool animateMovement = true;
        [SerializeField] private float movementDurationSeconds = 2.0f;

        public bool IsFinished { get; private set; }

        public System.Collections.IEnumerator Resolve(TurnContext context);
        public bool TryGetLastEnd(AircraftUnit unit, out Vector3 end);
        public IReadOnlyList<Segment> GetSegmentsForUnit(AircraftUnit unit);
        public void Reset();
    }
}
```

---

### 1.2 — Extract CollisionResolver

**Responsibility:** Given path segments from `MovementOrchestrator`, detect aircraft-aircraft collisions (bounds-overlap and path-intersection), apply instant death, report results.

**File to create:** `Assets/Scripts/Game/Resolvers/CollisionResolver.cs`

**Moved from MatchControllerMvp:**
- `ResolveCollisionsRoutine()`
- `CollectBoundsCollisions()`
- `CollectPathCollisions()`
- `HasPathCollision()`
- `BoundsIntersect()`

**Note:** The geometry primitives already live in `CollisionSystem.cs`. The `CollisionResolver` calls `CollisionSystem` directly. Duplicate private methods in `MissileManager` should be deleted in Phase 4.

**Signature shape:**

```csharp
namespace Zone5
{
    public class CollisionResolver : MonoBehaviour, ITurnResolver
    {
        [SerializeField] private TurnManager turnManager;
        [Range(0.10f, 1.00f)] public float aircraftHitRadiusFU = 0.10f;

        public bool IsFinished { get; private set; }

        public System.Collections.IEnumerator Resolve(TurnContext context);
        public IReadOnlySet<AircraftUnit> GetVictims();
        public void Reset();
    }
}
```

---

### 1.3 — Extract DamageResolver

**Responsibility:** For confirmed missile hits, compute evasion rolls, apply HP delta, register damage records for scoring, call `unit.Die()` where appropriate.

**File to create:** `Assets/Scripts/Game/Resolvers/DamageResolver.cs`

**Moved from MissileManager:**
- `ApplyEvasionRoll()`
- `ApplyDamage()`

**Moved from MatchControllerMvp:**
- DODGE CHECK and EVALUATING sections of `SpawnMissilesAndResolveEvasionRoutine()`

**Signature shape:**

```csharp
namespace Zone5
{
    public class DamageResolver : MonoBehaviour, ITurnResolver
    {
        [SerializeField] private TurnManager turnManager;

        public bool IsFinished { get; private set; }

        public System.Collections.IEnumerator Resolve(
            TurnContext context,
            IReadOnlyList<MissileManager.MissileHit> pendingHits);

        public void ApplyInstantKill(AircraftUnit unit, string killerUnitId);
        public void Reset();
    }
}
```

---

### 1.4 — Extract ScoringResolver

**Responsibility:** Read `roundDamageReceived` from all dead units, compute kill attribution (highest damage = kill), increment scores, check match-end conditions.

**File to create:** `Assets/Scripts/Game/Resolvers/ScoringResolver.cs`

**Moved from MatchControllerMvp:**
- `CalculateDiesAndScores()`
- `CleanupBattlefield()`
- `CheckVictoryCondition()`
- `CheckVictory()`
- `ParsePlayerId()`

**Signature shape:**

```csharp
namespace Zone5
{
    public class ScoringResolver : MonoBehaviour, ITurnResolver
    {
        [SerializeField] private TurnManager turnManager;

        public bool IsFinished { get; private set; }

        public System.Collections.IEnumerator Resolve(TurnContext context);
        public bool CheckVictory();
        public void Reset();
    }
}
```

---

### 1.5 — MatchControllerMvp After Extraction

After all four extractors are wired, `MatchControllerMvp.cs` becomes a thin coordinator (~150 lines). Its only job: listen to `OnStateChanged`, dispatch to the right resolver, call `TurnManager.AdvanceButton()` when done.

```csharp
namespace Zone5
{
    public class MatchControllerMvp : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private MovementOrchestrator movementOrchestrator;
        [SerializeField] private CollisionResolver collisionResolver;
        [SerializeField] private MissileManager missileManager;
        [SerializeField] private DamageResolver damageResolver;
        [SerializeField] private ScoringResolver scoringResolver;

        public void SubmitManeuver(int playerId, string raw);
        public void SubmitManeuverList(int playerId, List<string> codes);
        public void SubmitWeapon(int playerId, string raw);
        public void SubmitMissileProfile(int playerId, string profileCode);

        private void HandleTurnStateChanged() { ... }
    }
}
```

---

### Phase 1 File Summary

| Action | File |
|--------|------|
| CREATE | `Assets/Scripts/Game/Resolvers/MovementOrchestrator.cs` |
| CREATE | `Assets/Scripts/Game/Resolvers/CollisionResolver.cs` |
| CREATE | `Assets/Scripts/Game/Resolvers/DamageResolver.cs` |
| CREATE | `Assets/Scripts/Game/Resolvers/ScoringResolver.cs` |
| MODIFY | `Assets/Scripts/Game/MatchControllerMvp.cs` (gutted to thin coordinator) |
| MODIFY | `Assets/Scripts/Systems/MissileManager.cs` (remove `ApplyEvasionRoll`, `ApplyDamage`) |

**Complexity:** L
**Risks:**
- `SpawnMissilesAndResolveEvasionRoutine()` currently does spawning, hit-check, evasion, scoring, and cleanup in one coroutine. Splitting requires careful coroutine chaining. The `didResolveAttacksThisTurn` guard must not be lost.
- `MovementOrchestrator` caches segments that `CollisionResolver` reads. Execution order contract must be respected.
- `WarnProfileFallbackOnce()` is a static flag. Must remain `static` if moved to preserve once-per-session semantics.

**Acceptance Criteria:**
- All four resolver classes implement `ITurnResolver`.
- `MatchControllerMvp.cs` is under 200 lines.
- Full turn cycle completes without regressions.
- Score, assist, and victory conditions fire identically to pre-refactor behavior.
- No `FindFirstObjectByType<>()` calls remain in the new resolver files.

---

## Phase 2: Deprecate Legacy Systems

### Motivation

Two redundant data formats create branching logic throughout the codebase. Every path-resolution call has an if-profile-else-def pattern. Every maneuver execution has an `if (profile != null) ExecuteManeuver(...) else ExecuteManeuverLegacy(...)` split. The evasion code in `MissileManager.ApplyEvasionRoll()` still calls `ManeuverCatalog.Resolve()` directly, bypassing the profile system. `ManeuverProfile` is a strict superset of `ManeuverDef`, and `MissilePathProfile` is a strict superset of `MissilePathDef`. Migration is safe.

---

### 2.1 — ManeuverDef → ManeuverProfile

**Steps:**
1. Audit `ManeuverCatalog.cs` — enumerate every `ManeuverDef` entry not yet represented as a `ManeuverProfile` ScriptableObject. Create the missing assets.
2. Remove the fallback path in `ManeuverManager.Resolve()`.
3. Remove `WarnProfileFallbackOnce()` and `if (profile == null)` fallback in `MovementOrchestrator`.
4. Remove `ExecuteManeuverLegacy()` from `MovementOrchestrator`.
5. In `DamageResolver`, replace `ManeuverCatalog.Resolve(target.LastManeuver)` with `maneuverManager.Resolve(target.LastManeuver)`.
6. Mark `ManeuverCatalog.cs` and `ManeuverDef.cs` as `[Obsolete]`, then delete.

**Files to modify:**
- `Assets/Scripts/Systems/ManeuverManager.cs`
- `Assets/Scripts/Game/Resolvers/MovementOrchestrator.cs`
- `Assets/Scripts/Game/Resolvers/DamageResolver.cs`

**Files to delete:**
- `Assets/Scripts/Game/Maneuvers/ManeuverCatalog.cs`
- `Assets/Scripts/Game/Maneuvers/ManeuverDef.cs`

---

### 2.2 — MissilePathDef → MissilePathProfile

**Steps:**
1. Audit `MissilePathCatalog.cs` for path entries not yet in `MissilePathDatabase`.
2. Create the missing `MissilePathProfile` ScriptableObjects.
3. Remove the `pathDef` fallback branch in `MissileManager.SpawnMissile()`.
4. Remove `BuildWorldPoints(... MissilePathDef def)` overload.
5. Mark `MissilePathCatalog.cs` and `MissilePathDef.cs` as `[Obsolete]`, then delete.

**Files to modify:**
- `Assets/Scripts/Systems/MissileManager.cs`

**Files to delete:**
- `Assets/Scripts/Game/Missile/MissilePathCatalog.cs`
- `Assets/Scripts/Game/Missile/MissilePathDef.cs`

---

### Phase 2 File Summary

| Action | File |
|--------|------|
| MODIFY | `Assets/Scripts/Systems/ManeuverManager.cs` |
| MODIFY | `Assets/Scripts/Game/Resolvers/MovementOrchestrator.cs` |
| MODIFY | `Assets/Scripts/Game/Resolvers/DamageResolver.cs` |
| MODIFY | `Assets/Scripts/Systems/MissileManager.cs` |
| MARK OBSOLETE → DELETE | `Assets/Scripts/Game/Maneuvers/ManeuverCatalog.cs` |
| MARK OBSOLETE → DELETE | `Assets/Scripts/Game/Maneuvers/ManeuverDef.cs` |
| MARK OBSOLETE → DELETE | `Assets/Scripts/Game/Missile/MissilePathCatalog.cs` |
| MARK OBSOLETE → DELETE | `Assets/Scripts/Game/Missile/MissilePathDef.cs` |

**Complexity:** M (code) + S (asset authoring)
**Risks:**
- `MissileManager` reads `ManeuverDef.evasionPenalty` as `int`; `ManeuverProfile.evasionPenalty` is `EvasionPenaltyTier` (enum). Confirm enum integer values match legacy int values before removing the old path.
- ScriptableObjects not saved in project will be null at runtime. Do not remove fallback until asset audit is 100% complete.
- `TurnStateManager.cs` is a singleton relay over `TurnManager` with no added features. Evaluate deletion in Phase 2 (grep references first).

**Acceptance Criteria:**
- Zero calls to `ManeuverCatalog` anywhere in compiled code.
- Zero calls to `MissilePathCatalog` anywhere in compiled code.
- `ManeuverDef` and `MissilePathDef` files deleted.
- All existing play-throughs complete without errors.
- Evasion dice count is identical to pre-migration behavior.

---

## Phase 3: Wire Stance UI to Game Logic

### Motivation

`PanelStanceController`, `PanelDirectionsController`, and `PanelStanceCardClick` exist and are polished, but they are visual-only. Clicking a stance card updates the UI but writes nothing to `TurnSheet`. Clicking a direction card does nothing. Phase 3 closes this gap.

**Target flow:**
1. Player clicks Stance card → `PanelStanceController.OnStanceChanged` fires.
2. System pushes `SelectManeuverDirection` via `TurnStateStack.Advance`.
3. Player clicks Direction card → (Stance, Direction) pair resolves to a `ManeuverProfile` code.
4. System calls `MatchControllerMvp.SubmitManeuver(playerId, code)`.
5. `TurnStateStack.Retreat()` back to parent state. If all players ready → `NextState()`.

---

### 3.1 — ManeuverSelectionBridge

**File to create:** `Assets/Scripts/Panels/ManeuverSelectionBridge.cs`

```csharp
namespace Zone5
{
    public class ManeuverSelectionBridge : MonoBehaviour
    {
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private MatchControllerMvp matchController;
        [SerializeField] private ManeuverManager maneuverManager;
        [SerializeField] private PanelStanceController stancePanel;
        [SerializeField] private PanelDirectionsController directionsPanel;
        [SerializeField] private int playerId = 1;

        public void OnDirectionSelected(PanelDirectionsController.DirectionCardKind direction);
        private string ResolveManeuverCode(Stance stance, PanelDirectionsController.DirectionCardKind direction);
        private void HandleStanceChanged(Stance stance);
    }
}
```

---

### 3.2 — (Stance, Direction) → ManeuverProfile Mapping

The lookup uses `ManeuverDatabase`. Filter by `profile.stance == selectedStance && profile.allowedDirs == mappedDir`.

**Add to ManeuverDatabase:**

```csharp
public IReadOnlyList<ManeuverProfile> QueryByStanceAndDirection(
    Stance stance, AllowedDirs direction)
{
    if (_byKey == null) BuildIndex();
    return maneuvers
        .Where(p => p != null &&
                    p.stance.HasFlag(stance) &&
                    p.allowedDirs == direction)
        .ToList();
}
```

If multiple candidates: pick lowest G-force by default; show selection grid for HighAcrobatics.

**Files to modify:**
- `Assets/Scripts/Profiles/ManeuverDatabase.cs`

---

### 3.3 — Stack FSM Sub-State Integration

**GameEnum change required:**

```csharp
public enum TurnState
{
    SelectManeuver = 0,
    // ... existing values ...

    // New sub-states for stack FSM (values > 11 to avoid collision)
    SelectManeuverDirection = 20,
    ConfirmManeuver = 21,
}
```

**Files to modify:**
- `Assets/Scripts/GameEnum.cs` — add `SelectManeuverDirection` and `ConfirmManeuver`.
- `Assets/Scripts/Game/StateStackGuards.cs` — add parent/child rules for new states.
- `Assets/Scripts/Panels/UIStateOrchestrator.cs` — handle new states to show/hide direction panel.

---

### 3.4 — PanelDirectionCardClick

**File to create:** `Assets/Scripts/Panels/PanelDirectionCardClick.cs`

```csharp
namespace Zone5
{
    public class PanelDirectionCardClick : MonoBehaviour, IPointerClickHandler
    {
        public PanelDirectionsController.DirectionCardKind kind;
        public ManeuverSelectionBridge bridge;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (bridge == null) return;
            bridge.OnDirectionSelected(kind);
        }
    }
}
```

**Files to modify:**
- `Assets/Scripts/Panels/PanelDirectionsController.cs` — auto-bind `PanelDirectionCardClick` components in `AutoBindRoots()`.

---

### Phase 3 File Summary

| Action | File |
|--------|------|
| CREATE | `Assets/Scripts/Panels/ManeuverSelectionBridge.cs` |
| CREATE | `Assets/Scripts/Panels/PanelDirectionCardClick.cs` |
| MODIFY | `Assets/Scripts/GameEnum.cs` (add sub-states) |
| MODIFY | `Assets/Scripts/Game/StateStackGuards.cs` (new transition rules) |
| MODIFY | `Assets/Scripts/Profiles/ManeuverDatabase.cs` (add `QueryByStanceAndDirection`) |
| MODIFY | `Assets/Scripts/Panels/PanelDirectionsController.cs` (auto-bind clicks) |
| MODIFY | `Assets/Scripts/Panels/UIStateOrchestrator.cs` (handle new states) |

**Complexity:** M
**Risks:**
- Multiple ManeuverProfile candidates for some (Stance, Direction) combinations. Tie-breaking rule must be defined before launch.
- Hotseat: `playerId` on `ManeuverSelectionBridge` must track which player is currently acting.
- `AllReadyForPhase()` must not block on `SelectManeuverDirection` sub-state. Guards required.

**Acceptance Criteria:**
- Clicking a stance card updates `PanelDirectionsController` to show correct direction icons.
- Clicking a direction card resolves to a `maneuverId` and calls `MatchControllerMvp.SubmitManeuver()`.
- Game advances correctly from `SelectManeuver` after both players submit.
- Stack FSM correctly tracks push/pop through the stance→direction sub-flow.
- `ManeuverSelectionBridge` has zero `FindFirstObjectByType<>()` calls.

---

## Phase 4: Polish and Hardening

### Motivation

Phase 4 addresses remaining technical debt that does not belong to any specific feature extraction: correctness, testability, performance, and long-term maintainability.

---

### 4.1 — Replace FindFirstObjectByType as Dependency Injection

**Current hot-path offenders:**
- `MissileManager.CheckCollision()` calls `FindFirstObjectByType<DebugManager>()` on every collision check.
- `MissileManager.CheckMissilePathHitAircraft()` calls it per segment per missile.
- `MissileManager.ApplyEvasionRoll()` has a `if (turnManager == null)` inline re-fetch.

**Strategy (preferred for this project scale):** Serialized references in Inspector. All managers get `[SerializeField]` fields wired in scene prefab. Awake-time `FindFirst` kept only as dev-time safety net with `Debug.LogWarning`.

**Files to modify:**
- `Assets/Scripts/Game/MatchControllerMvp.cs`
- `Assets/Scripts/Systems/MissileManager.cs`
- `Assets/Scripts/Game/TurnStateManager.cs` (or delete — see 4.2)
- `Assets/Scripts/Panels/PanelPhaseController.cs`

---

### 4.2 — Delete TurnStateManager

`TurnStateManager` (69 lines) mirrors `TurnManager.sheet.phase` into its own field and re-fires a typed event. `UIStateOrchestrator` now provides that typed dispatch. It is safe to delete.

**Pre-deletion:** Grep entire project for `TurnStateManager` references.

**Files to delete:**
- `Assets/Scripts/Game/TurnStateManager.cs`

---

### 4.3 — Standardize Naming

Rules:
- All public identifiers: English, PascalCase (classes/methods/properties), camelCase (fields).
- No abbreviations: `fuWorld` → `fighterUnitWorldScale`, `dmg` → `damage`, `hp` → `hitPoints`. Exception: domain acronyms (VFX, UI, HUD, FU).
- File `Panel Tactical.cs` has a space — rename to `PanelTactical.cs` via Unity's Project window (not filesystem).

---

### 4.4 — Geometry Math Deduplication

`MissileManager.cs` contains private duplicates of methods already in `CollisionSystem.cs`:
- `MinDistanceSegmentToSegment2D`
- `DistancePointToSegment2D`
- `SegmentsIntersect2D`
- `Cross2D`, `OnSegment2D`
- `CatmullRom`, `SmoothCatmullRom`

**File to create:** `Assets/Scripts/Systems/MathUtilities.cs`

Move `CatmullRom`/`SmoothCatmullRom` here. `MissileManager` calls `CollisionSystem` for geometry; `ManeuverProfile` calls `MathUtilities` for curves.

**Files to modify:**
- `Assets/Scripts/Systems/MissileManager.cs` — delete duplicate geometry methods.
- `Assets/Scripts/Profiles/ManeuverProfile.cs` — replace private `SmoothCatmullRom` with `MathUtilities` call.

---

### 4.5 — StateStackGuards: Complete Legal Graph

```csharp
private static readonly HashSet<(TurnState parent, TurnState child)> _legal = new()
{
    (TurnState.SelectManeuver,              TurnState.SelectManeuverDirection),
    (TurnState.SelectManeuverDirection,     TurnState.ConfirmManeuver),
    (TurnState.SelectManeuver,              TurnState.DeclareWeapon),
    (TurnState.DeclareWeapon,               TurnState.RevealAndMoveFighters),
    (TurnState.RevealAndMoveFighters,       TurnState.ResolveCollisions),
    (TurnState.ResolveCollisions,           TurnState.SelectMissileProfile),
    (TurnState.ResolveCollisions,           TurnState.ApplyDamageAndCheckVictory),
    (TurnState.SelectMissileProfile,        TurnState.SpawnMissilesAndResolveEvasion),
    (TurnState.SpawnMissilesAndResolveEvasion, TurnState.ApplyDamageAndCheckVictory),
    (TurnState.ApplyDamageAndCheckVictory,  TurnState.EndRoundAndAdvance),
    (TurnState.EndRoundAndAdvance,          TurnState.SelectManeuver),
    // Any state → MatchEnded is valid (early termination)
};
```

---

### Phase 4 File Summary

| Action | File |
|--------|------|
| CREATE | `Assets/Scripts/Systems/MathUtilities.cs` |
| MODIFY | `Assets/Scripts/Systems/MissileManager.cs` (DI, remove geometry duplication) |
| MODIFY | `Assets/Scripts/Game/MatchControllerMvp.cs` (DI cleanup) |
| MODIFY | `Assets/Scripts/Panels/PanelPhaseController.cs` (DI) |
| MODIFY | `Assets/Scripts/Profiles/ManeuverProfile.cs` (use MathUtilities) |
| MODIFY | `Assets/Scripts/Game/StateStackGuards.cs` (complete legal graph) |
| DELETE | `Assets/Scripts/Game/TurnStateManager.cs` |
| RENAME | `Assets/Scripts/Panels/Panel Tactical.cs` → `PanelTactical.cs` |

**Complexity:** S per item, M total

---

## Sequencing and Dependency Graph

```
Phase 0 ─────────────────────────────────────────────────┐
  │                                                        │
  ├─ 0.1 Interfaces (no deps)                             │
  ├─ 0.2 Stack FSM (no deps)                              │
  └─ 0.3 UI Orchestrator (needs TurnContext from 0.1)     │
                                                           │
Phase 1 ──────────────────────────────────────────────────┤ (needs Phase 0)
  │                                                        │
  ├─ 1.1 MovementOrchestrator (needs ITurnResolver)       │
  ├─ 1.2 CollisionResolver (needs MovementOrchestrator)   │
  ├─ 1.3 DamageResolver (needs ITurnResolver)             │
  ├─ 1.4 ScoringResolver (needs DamageResolver)           │
  └─ 1.5 MatchControllerMvp thinned (needs 1.1-1.4)      │
                                                           │
Phase 2 ──────────────────────────────────────────────────┤ (needs Phase 1)
  ├─ 2.1 ManeuverDef deprecated
  └─ 2.2 MissilePathDef deprecated
                                                           │
Phase 3 ──────────────────────────────────────────────────┤ (needs Phase 0.2, Phase 2)
  ├─ 3.1 ManeuverSelectionBridge
  ├─ 3.2 ManeuverDatabase query
  ├─ 3.3 Stack FSM sub-states
  └─ 3.4 PanelDirectionCardClick
                                                           │
Phase 4 ──────────────────────────────────────────────────┘ (needs all above)
  ├─ 4.1 DI cleanup
  ├─ 4.2 TurnStateManager deleted
  ├─ 4.3 Naming
  ├─ 4.4 Geometry deduplication
  └─ 4.5 StateStackGuards complete
```

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Coroutine chain broken during Phase 1 extraction | Medium | High | Extract one resolver at a time; full play-through test after each extraction |
| `EvasionPenaltyTier` int values differ from legacy `ManeuverDef.evasionPenalty` | Low | High | Audit before removing fallback; add assertion in both paths for one sprint |
| `FindFirstObjectByType` removal breaks Awake order | Low | Medium | Keep serialized refs as fallback with warning; do not hard-remove until scene wired |
| Stance-direction mapping returns zero candidates | Medium | Medium | `ManeuverSelectionBridge.ResolveManeuverCode()` logs clear error and falls back to default |
| `TurnStateManager` has hidden external references not found by Grep | Low | Medium | Use Unity's "Find References in Scene" before deletion |
| Duplicate geometry methods have diverged numerically | Very Low | Low | Add comparison test for 5 known inputs before deleting |

---

## Acceptance Criteria — Full Suite

The refactor is complete when:

1. `MatchControllerMvp.cs` is under 200 lines and contains zero switch-case game logic.
2. `TurnManager.cs` (core file) is under 80 lines; logic lives in partial files.
3. `FindFirstObjectByType<>()` does not appear in any method body except as a `Debug.LogWarning` fallback behind a null check.
4. The stance UI flow — click Stance → click Direction → maneuver submitted — works end-to-end.
5. `ManeuverDef`, `ManeuverCatalog`, `MissilePathDef`, `MissilePathCatalog` do not exist in the project.
6. `TurnStateManager.cs` does not exist.
7. Todos os panels ativos (`PanelPhaseController`, `PanelGroupController`, `PanelStanceController`, `PanelDirectionsController`, `PanelPilot`, `PanelTurn`, `Panel Tactical`) implementam `IPanelController`.
8. A full turn cycle (SelectManeuver → DeclareWeapon → RevealAndMove → ResolveCollisions → SelectMissile → SpawnAndResolve → ApplyDamage → EndRound → SelectManeuver) completes without errors.
9. Victory conditions (draw and team win) trigger correctly.
10. `StateStackGuards.AssertValid` covers the complete state transition graph.

---

## File Inventory: Before and After

### Files That Will Be Created (19 new files)

```
Assets/Scripts/Game/Interfaces/ITurnPhase.cs
Assets/Scripts/Game/Interfaces/IResolvable.cs
Assets/Scripts/Game/Interfaces/ITurnResolver.cs
Assets/Scripts/Game/TurnContext.cs
Assets/Scripts/Game/TurnStateStack.cs
Assets/Scripts/Game/StateStackGuards.cs
Assets/Scripts/Game/TurnManager.Registration.cs
Assets/Scripts/Game/TurnManager.Transitions.cs
Assets/Scripts/Game/TurnManager.Stack.cs
Assets/Scripts/Game/TurnManager.Events.cs
Assets/Scripts/Game/Resolvers/MovementOrchestrator.cs
Assets/Scripts/Game/Resolvers/CollisionResolver.cs
Assets/Scripts/Game/Resolvers/DamageResolver.cs
Assets/Scripts/Game/Resolvers/ScoringResolver.cs
Assets/Scripts/Panels/IPanelController.cs
Assets/Scripts/Panels/UIStateOrchestrator.cs
Assets/Scripts/Panels/ManeuverSelectionBridge.cs
Assets/Scripts/Panels/PanelDirectionCardClick.cs
Assets/Scripts/Systems/MathUtilities.cs
```

### Files That Will Be Deleted (5 files)

```
Assets/Scripts/Game/Maneuvers/ManeuverCatalog.cs
Assets/Scripts/Game/Maneuvers/ManeuverDef.cs
Assets/Scripts/Game/Missile/MissilePathCatalog.cs
Assets/Scripts/Game/Missile/MissilePathDef.cs
Assets/Scripts/Game/TurnStateManager.cs
```

### Files That Will Be Significantly Modified

```
Assets/Scripts/Game/MatchControllerMvp.cs      (987 → ~150 lines)
Assets/Scripts/Game/TurnManager.cs             (282 → ~60 lines, logic moved to partials)
Assets/Scripts/Systems/MissileManager.cs       (remove duplication, DI, move damage logic)
Assets/Scripts/Systems/ManeuverManager.cs      (remove fallback)
Assets/Scripts/Profiles/ManeuverDatabase.cs    (add QueryByStanceAndDirection)
Assets/Scripts/Profiles/ManeuverProfile.cs     (use MathUtilities)
Assets/Scripts/Panels/PanelPhaseController.cs  (IPanelController, DI)
Assets/Scripts/Panels/PanelDirectionsController.cs (auto-bind click handlers)
Assets/Scripts/GameEnum.cs                     (add sub-state enum values)
```
