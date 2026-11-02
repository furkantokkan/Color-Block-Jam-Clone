# TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects — Level JSON v2: void cells, validated block/door effects and board objects (Barrier first)

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects
- Supersedes: TASK-20260924-215645373-33a6a7a4-level-json-v2-effects
- Title: Level JSON v2: void cells, validated block/door effects and board objects (Barrier first)
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7
- Task kind: feature
- Area: Level data schema
- Workflow: direct

## Objective and baseline

- Observable objective: A level JSON can:
  - mark void cells;
  - attach typed, validated effects to blocks and doors;
  - list board objects (`cellObjects`), each a set of cells plus one effect, starting with the Barrier.
  Wrong or unsupported data is rejected with a message that names the level, entity, effect and key. The play area builds walls around void cells.
- Current behavior/evidence:
  - User decisions 2026-09-25: "colors altına açılabilir bir tane daha obstacles diye ek olarak ve bir tane obstacle da yapabiliriz" ("add another collapsible Obstacles section under Colors, and let's build one obstacle"). The user chose Barrier (OBJ-05) and assigned this schema work to this session ("You take it over"), because the level-pipeline session had not started `TASK-20260924-215645373-33a6a7a4-level-json-v2-effects` (ready and planning since 2026-09-24; `Runtime/Levels` unchanged since 21:00 that day).
  - `LevelDatabaseJson` accepts only `formatVersion` 1. It requires `cellObjects` to be present but rejects any entry ("this foundation cannot represent yet", `LevelDatabaseJson.cs:111-119`), and rejects non-empty block/door `effects`.
  - The superseded contract's expected behaviour, validation list and tests are carried over below unchanged. The void-cell design is in `production/tasks/TASK-20260924-185655569-dea430a7-play-area-environment/handoff-voidcells-schema.md`.
  - `docs/Color-Block-Jam-PRD-Mechanics-Catalog.md` gives:
    - the effect record shape (DEC-MECH-1);
    - the block (BLK-01..18) and gate (GTE-01..11) types;
    - board objects (section 5, OBJ-01..09), where each cell object is "a set of cells plus one effect" with an occupancy role (Wall, ColorGate, Floor, Movable);
    - the Barrier, OBJ-05 (`barrier`, `startsOpen`, role Wall when closed and Floor when open, flips on each `BlockCleared`).
- Expected behavior:
  1. **One version bump.** `formatVersion` 2 adds `board.voidCells` exactly as the handoff specifies, allows non-empty `effects` on blocks and doors, and allows non-empty `cellObjects`. Version 1 files still load (no void cells, effects or cell objects). Writes emit version 2 with explicit empty arrays.
  2. **Record shapes (DEC-MECH-1).**
     - An effect is `{ "type": "<id>", "params": [ { "key": "<name>", "value": "<string>" } ] }`.
     - A cell object is `{ "id": "<id>", "cells": [ { "x": 0, "y": 0 } ], "effect": { "type": "<id>", "params": [...] } }`.
     - `TypeNameHandling` stays `None`; there is no `$type`.
  3. **Effect registry.** One definition per catalog type id: all 18 block, 11 door and 9 board-object types. Each definition gives:
     - the allowed target (block, door or cell object) and, for board objects, the occupancy role;
     - required and optional keys, and a value rule for each key (enum, integer range, bool, palette colour id, entity id reference).
     An explicit converter per type turns the string pairs into an immutable typed parameter object held by `LevelData`, for example `ArrowEffect(Axis)`, `IceEffect(ClearsToThaw 1..20)`, `BarrierObject(StartsOpen)`.
  4. **Validation (load and save, outside Playing).**
     - Effects:
       - unknown type;
       - type on the wrong target;
       - the same type twice on one entity;
       - unknown, duplicate or missing required key;
       - value outside its rule;
       - a reference to a missing block or door id;
       - the catalog's exclusive sets and stacking rules.
     - Void cells: the handoff's rules. These cover what the Level Editor's void-edge tools rely on: floor four-connected, no block or door on a void cell, and no door facing a void cell.
     - Doors: a door spans 1 to 3 cells (user decision 2026-09-25, "gate maksimum üç alan ile 1 alan arasında olabilir"). Longer doors are rejected by `LevelData.Validate` and the v2 reader with "Level {id} door {doorId} spans {n} cells; gates span 1 to 3." All 24 current levels (the game database and the reference files) comply.
     - Cell objects:
       - unique ids;
       - non-empty unique cells inside the board and on floor (not void);
       - no two cell objects share a cell.
     - Barrier: cells are four-connected. With `startsOpen` false, no block may overlap a barrier cell at the start. `startsOpen` is a bool and defaults to false when absent.
  5. **Runtime support gate** (clarified 2026-09-25 after verification D-001). The default reads, `LevelDatabaseJson.TryRead(json, out levels, out error)` and `LevelDatabase.LoadJson(json, out error)`, are authoring reads, so every Level Editor path works unchanged. Only the game's project-scope load, `LevelDatabase.LoadAsync`, reads with `LevelReadMode.Runtime` and applies the gate. Each definition states whether gameplay implements it. Project scope rejects a level that uses an unimplemented effect or object, naming it, so no level loads with something that silently does nothing. Authoring validation (editor Save, tests, importers) accepts every catalog type. At the end of this task Barrier is authoring-valid but runtime-unimplemented; the gameplay owner flips it when its rule lands.
  6a. **List and cell parameter encoding** (user decision 2026-09-25, "My proposed format"). A parameter whose value is a list is written as comma-separated items inside the single string value (for example colours `"Red,Blue"`). A parameter whose value is a list of cells is written as `"x:y;x:y"`. This enables the seven types that authoring refused pending the decision: `layer`, `ropes`, `colorful-door`, `size-changing-door`, `colorful-jumping-door`, `button-door`, `flower-crate`. The four recorded assumptions stand:
     - `locked-door`, `crate` and `moveable-crate` counts are 1 or more, with no maximum;
     - `curtain` and `toggle-door` require `startsOpen`;
     - only the Barrier defaults `startsOpen` to false;
     - the `moving-obstacle` block colour is not checked (the palette has no Neutral entry).
  6. **Canonical writing.** Params are written in the definition's key order, and round-trip keeps effect and cell-object order.
  7. **IL2CPP.** New DTOs and converter targets are preserved in `Runtime/Levels/link.xml`.
  8. **LevelData API.**
     - Void cells: `VoidCellCount`, `GetVoidCellAt`.
     - Effects: effect accessors per block and door.
     - Cell objects: `CellObjectCount`, `GetCellObjectAt` (id, cells, typed effect).
     - The existing constructor signatures are kept for current callers.
     - Everything is Unity-serializable, so the Level Editor draft, Undo and the JsonUtility dirty check see the new fields.
  9. **Play area.** `Runtime/Layout/BoardLayout.TryCreate(LevelData, …)` passes the level's void cells. This is the environment task's pending AC-2.
- Behavior that must remain unchanged:
  - Existing v1 level files load unchanged.
  - The Level Editor window's current fields, Save/Reload/Restore and the preview hook (`DraftLevelPublished`).
  - The `LevelDatabase` public API, runtime loading timing and the Addressables setup.
  - Environment placements for levels without void cells.

## Ownership

- Owner/writer role: environment/play-area session (this conversation; user reassignment 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Levels/LevelData.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelDatabaseJson.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelDatabase.cs (read mode only: the LoadAsync game load applies the runtime gate, and a refused game load keeps the previous authoring cache so the Level Editor survives Play Mode without a domain reload; public API unchanged apart from an additive LoadJson overload)
  - Assets/ColorBlockJam/Runtime/Levels/LevelEffect*.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelEffect*.cs.meta
  - Assets/ColorBlockJam/Runtime/Levels/LevelCellObject*.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelCellObject*.cs.meta
  - Assets/ColorBlockJam/Runtime/Levels/link.xml
  - Assets/ColorBlockJam/Runtime/Layout/BoardLayout.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseJsonTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEffectTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEffectTests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelCellObjectTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCellObjectTests.cs.meta
  - Assets/ColorBlockJam/Tests/Environment/**
  - docs/Level-Data-Format.md
  - README.md
  - PROJECT.md
  - production/tasks/TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects/**
  - production/tasks/TASK-20260924-185655569-dea430a7-play-area-environment/** (AC-2 evidence and lifecycle only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Runtime/Board/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Editor/**
  - Assets/AddressableAssetsData/**
  - Assets/ColorBlockJam/Data/Levels/*.json content (existing files become v2 only through a normal editor Save)
  - Gameplay implementation of any effect or object (separate owners)
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked:
  - The level-pipeline session owned the superseded task and still owns `TASK-20260924-210432886-2f31f26b-remote-level-json` and `TASK-20260924-210811262-93892dee-cbj-level-import`; it is notified of the takeover.
  - The Level Editor session (`Parça template boş kareler`) owns `Editor/Levels/**` and consumes the new `LevelData` API for its v2 tools, Effect Editor and Obstacles brush.
  - This session's block chain (block-runtime-objects, board-art DI, runtime-shapes-from-json) runs first in the same Editor.
- Execution dependency: TASK-20260925-005313341-47831bb7-runtime-shapes-from-json has handed off (same Editor, sequential)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a v1 file (the current `Data/Levels/LevelDatabase.json`, levels 1-5), when it loads, then it loads unchanged with no void cells, effects or cell objects. When saved, it is written as v2 with explicit empty `voidCells`, `effects` and `cellObjects`.
2. Given `docs/levels/reference-levels.json` (v2, void cells in level 9, `arrow` effects in levels 7-9), when it loads with authoring validation, then every level is valid, void cells and typed arrow axes are exposed, and a write/read round-trip is identical.
3. Given each invalid case in Expected 4 (effects, void cells, cell objects, Barrier), when a level is read or saved, then it is rejected with a `Level {id} ...` message naming the entity, type and key; nothing is saved.
4. Given a level using an effect or object whose gameplay is not implemented (Barrier included at the end of this task), when project scope loads it, then loading fails naming it; the same level passes authoring validation.
5. Given the void-cell rules (bounds, duplicates, at least one floor cell, four-connected floor, blocks on floor, doors opening onto floor, door colour ids) and the door length rule (1 to 3 cells), when violated, each is rejected; `LevelData` exposes `VoidCellCount`/`GetVoidCellAt`; and `BoardLayout.TryCreate(LevelData)` builds walls around void cells (reference level 9 composes as the environment fixtures predict).
6. Given a level with a Barrier cell object (`startsOpen` true and false), then it round-trips, the typed `BarrierObject(StartsOpen)` is exposed, and a closed barrier overlapping a block at the start is rejected.
7. Given an IL2CPP player build, then level JSON with effects and cell objects loads (link.xml preserves the DTOs and converters). Recorded as a smoke check on the next device build.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - `docs/Color-Block-Jam-PRD-Mechanics-Catalog.md`: DEC-MECH-1, sections 3-5, OBJ-05 Barrier, stacking rules.
  - `handoff-voidcells-schema.md`; `docs/levels/reference-levels.json`.
  - The superseded contract `TASK-20260924-215645373-33a6a7a4-level-json-v2-effects`.
  - User decisions 2026-09-25.
- Unity version/platform: 2022.3.62f2; Android and iOS (IL2CPP)
- Package/architecture constraints: Newtonsoft `com.unity.nuget.newtonsoft-json` 3.2.2; explicit converters, `TypeNameHandling.None`; parsing only at load/save, never in a Playing tick; plain C# typed parameters; no reflection-based registry.
- Explicit out of scope:
  - Gameplay for any effect or object.
  - Effect and object visuals (the Barrier view is `TASK-20260925-102657924-08cc082b-barrier-board-view`).
  - Level Editor UI.
  - CBJ import and remote updates.

## Verification and risk

- Focused test path/command: `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests`, Environment EditMode tests (void cells)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1-6, including one valid and one invalid fixture per registered type
- PlayMode: existing scene-flow smoke tests with the v1 and a v2 database
- Manual/built-player/profiler/device evidence: acceptance 7 on the next Android IL2CPP smoke build
- Risk: high (save-format change)
- Approval already granted (exact target only):
  - User requests 2026-09-24 (void cells) and 2026-09-25 (block/door effects addable and validated).
  - User decision 2026-09-25 to build the Barrier and assign this schema to this session.
  - User decisions 2026-09-25 relayed by the Level Editor session: gates span 1 to 3 cells; void edges carved from the editor need Unity-serializable, validated void cells.
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects, then an independent verifier

## Stop conditions

- A catalog entry's parameter rule is ambiguous: implement the others, record the entry and ask the user.
- Another writer edits `Runtime/Levels/**`: re-read and serialize.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:bfd7326e66b32e448c8527ccf82b22d0e0098077d75122db4aea05bf9d904e08
- Evidence fingerprint: sha256:139856f75cbeb3428b6049d7825431e82d68a37daf364a50c7d85144f5523997
- Reviewed revision: vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7
- Attempt count: 2
- Last feedback: verifier 4: AC-1..AC-6 PASS, D-001 VERIFIED; AC-7 unproven by user decision
- Blocker: AC-7 needs an Android IL2CPP smoke build; the user chose to keep this task open until a device build (2026-09-25)
- Next action: None
- Closed revision: vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 v1 files load and save as v2 | PASS | independent verifier 4 (evidence.md, Verification 4): focused 209/209, PlayMode 38/38, live v1->v2 capture | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7 |
| AC-2 Reference v2 file loads with void cells and arrows | PASS | independent verifier 4 (evidence.md, Verification 4): reference round trip test + live capture | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7 |
| AC-3 Invalid effects and objects rejected precisely | PASS | independent verifier 4 (evidence.md, Verification 4): 42 Valid_/63 Invalid_ fixtures, empty-string and parameter-token probes | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7 |
| AC-4 Unimplemented effects rejected at project load | PASS | independent verifier 4 (evidence.md, Verification 4): editor Save/Reload/Save capture, Runtime refusal, FailedProjectScopeLoadKeepsTheAuthoringCache | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7 |
| AC-5 Void-cell rules, API and play-area walls | PASS | independent verifier 4 (evidence.md, Verification 4): void/door rule tests, Environment 61/61, live BoardLayout level-09 | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7 |
| AC-6 Barrier cell object round-trip and validation | PASS | independent verifier 4 (evidence.md, Verification 4): LevelCellObjectTests + live closed-barrier refusal | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7 |
| AC-7 IL2CPP smoke | UNPROVEN | static link.xml guard test only; Android IL2CPP smoke build not run (user decision: keep open until a device build) | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| D-001 | v2/read-mode/editor-runtime-gated | VERIFIED | AC_FAILURE | AC-4 | The 3-argument TryRead applied the runtime gate. Level Editor Reload/Save and the inspector refused levels with effects or cell objects after the first save | [defects/D-001.md](defects/D-001.md); independent verifier 4 (evidence.md, Verification 4) | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:535e6f0afe19f78f33879dc52a3aca4fcbfcf69d2dc59c1f9cd917f14e288ea7 |

### Transition history

- Created — planning — user took the v2 schema over for this session and chose the Barrier; supersedes TASK-20260924-215645373-33a6a7a4-level-json-v2-effects
- 2026-09-25T11:00Z — contract revised before execution — door length 1 to 3 validation added (user decision); void-cell rules confirmed for the editor's void-edge tools; AC-5 updated
- 2026-09-25T12:52Z — in_progress — chain: implement, 3 reviewers (11 findings), fix, verify, retry-fix, re-verify; AC-1/2/3/5/6 PASS, AC-4 FAIL (D-001), AC-7 unproven
- 2026-09-25T12:52Z — contract clarified — default reads are authoring; only LevelDatabase.LoadAsync applies the runtime gate; LevelDatabase.cs added narrowly
- 2026-09-25T12:54Z — user decisions — list and cell parameter encoding adopted (enables the seven pending types); AC-7 stays open until a device build (the task is not closed by acceptance)
- 2026-09-25T14:20Z — contract revised — LevelDatabase.cs line: a refused game load keeps the previous authoring cache (review finding: the Level Editor broke after Play Mode; game readers gate on the project-scope ready task)
- 2026-09-25T14:20Z — verification — fix round 2 chain: implement, 3 reviewers (5 findings), fix, verify (AC-3 FAIL: empty-string parameter), retry-fix, re-verify: AC-1..AC-6 PASS, D-001 VERIFIED; AC-7 open until a device build
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
