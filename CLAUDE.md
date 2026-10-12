# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Zone 5 is a turn-based, simultaneous-resolution 2D air combat game (hotseat MVP) built in **Unity 6000.4.3f1** with URP and the new Input System. All gameplay code lives in `Assets/Scripts` under the `Zone5` namespace. The only scene is `Assets/Scenes/SampleScene.unity`. The user writes in Brazilian Portuguese; code comments, docs and commit messages mix PT-BR and English.

## Building / running

There is no CLI build, lint or test setup. Work happens in the Unity Editor: open the project, load `SampleScene` and press Play. No test assemblies or `.asmdef` files exist. Everything compiles into `Assembly-CSharp` / `Assembly-CSharp-Editor`, so a compile error anywhere breaks the whole project. To check compilation you need Unity (or the IDE diagnostics against the generated `.csproj`). The generated `.csproj`/`.slnx` files are not authoritative.

Versioning: the old series `v0.0.1`–`v0.0.15` (tags `v0.0.x`, summarized in `docs/outros/changelog.md`) is closed. Since 2026-10-11 the project uses the **z-series** (`z0.0.1`, `z0.0.2`, …): commits are `zX.Y.Z - short description`, each version gets a git tag of the same name and a closing note in `docs/fechamentos/zX.Y.Z.md`.

## Architecture

**Turn loop.** `TurnManager` (`Assets/Scripts/Game/TurnManager.cs`) owns the FSM. The current phase lives in `TurnSheet.phase` (`GameEnum.TurnState`), and player inputs and ready flags live in `TurnSheet.PlayerRow`. `NextState()` is a flat switch, and the hotseat MVP skips the `Wait*` states. `TryAdvance()` only moves on when every player is ready for the phase. Changes are broadcast through `OnStateChanged` / `OnSheetChanged`. `TurnStateManager` is a redundant singleton relay of the same state and is slated for deletion.

**Phase execution.** `MatchControllerMvp` (~1000 lines, a known god object) reacts to `OnStateChanged` and runs the active phases as coroutines: reveal and move fighters, aircraft collisions, missile spawn plus evasion rolls, damage, scoring (kills/assists from temporary hit points), cleanup and victory check. UI submits player choices through its `Submit*` methods, which write to the `TurnSheet` row and call `turnManager.TryAdvance()`. It also owns each aircraft's authoritative "last end" position. Debug controllers (`DebugManeuverController`) must sync with it through `SetLastEnd` / `SyncLastEndFromTransform`.

**Data: two parallel formats.** Maneuvers and missile paths each have a new ScriptableObject format and a legacy hardcoded format, and code falls back from new to old:
- `ManeuverProfile` (SO assets in `Assets/DB/Maneuver`, resolved by `ManeuverDatabase` → `ManeuverManager`, fallback `ManeuverProfileCatalog` via Resources) vs legacy `ManeuverDef`/`ManeuverCatalog` (`ExecuteManeuverLegacy`).
- `MissilePathProfile` (`Assets/DB/MissilePath`, `MissilePathDatabase`) vs legacy `MissilePathDef`/`MissilePathCatalog`.
- `UnitProfile` (`Assets/DB/Plane`) and `MissileProfile` (`Assets/DB/Missile`) have no legacy counterpart.
- Custom inspectors with path previews live in `Assets/Editor` (`ManeuverProfileEditor`, `MissilePathProfileEditor`).

Maneuver/weapon input is string-coded (e.g. maneuver codes joined with `+`, missile path codes like `L1`, `S2`). `MvpRules` sanitizes these codes. Distances use "FU" (field units).

**Units/views.** `AircraftUnit` / `MissileUnit` hold runtime state. `AircraftView` / `MissileView` animate paths and draw live trails. `TrailManager` draws persistent path segments. `MovementCore` holds static geometry helpers, some of which are duplicated in `MissileManager`, `CollisionSystem` and `ManeuverProfile`. `UnitSpawner` places aircraft at match start.

**UI panels** (`Assets/Scripts/Panels`) each subscribe to `TurnManager.OnStateChanged` independently, with no guaranteed order. Dependencies are found with `FindFirstObjectByType<>()` in `Awake` (the project-wide pattern), unless they are assigned in the inspector. The old cockpit panels (Manche/Throttle/Weapons) were removed. They are being replaced by a card system inspired by Ticket to Ride (`PanelStanceController`, `PanelDirectionsController`, `PanelStanceCardClick`, card art in `Assets/acervo/cartas` and `Assets/img/cartas`). These new panels are **not yet wired to game logic**. They only fire visual events.

**Debug.** `DebugManager` holds global toggles (hitboxes, trails, aim cones). Debug views live in `Assets/Scripts/Debug` and `Assets/Scripts/Game/Debug`.

## Design docs

- `REFACTOR_PLAN.md` is the active refactor roadmap (v1.1, 2026-04-21). It covers interfaces and a stack-based FSM (phase 0), breaking up `MatchControllerMvp` into resolvers (1), removing the legacy Def/Catalog formats (2), wiring the stance/direction cards to maneuver selection (3), and DI/cleanup (4). As of its writing, none of the phases had been implemented. Check it before making structural changes.
- `docs/pendencias/` holds open findings/evaluations (e.g. `avaliacao_fsm.md`: fixed waits per turn, full tactical-panel rebuilds).
- `docs/outros/` (PT-BR) contains the game rules (`Zone 5 - Air Combat rules.md`, `combate.md`, `manobras.md`), the turn FSM description and an example of a full combat round with scoring (`maquina de estados.md`), and per-file notes (`notas do sistema.md`, `controllers.md`, which may be outdated).

## Unity conventions

- Keep the `.meta` file alongside any asset or script you add, move or delete.
- Serialized field renames break scene/prefab references unless you add `[FormerlySerializedAs]`.
