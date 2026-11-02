# TASK-20260924-215645373-33a6a7a4-level-json-v2-effects — Level JSON v2: void cells and validated block/door effects

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-215645373-33a6a7a4-level-json-v2-effects
- Supersedes: none
- Title: Level JSON v2: void cells and validated block/door effects
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level data schema
- Workflow: direct

## Objective and baseline

- Observable objective: A level JSON can mark void cells and attach effects to blocks and doors; every effect is parsed into typed data and validated at load and save, so a wrong or unsupported effect is rejected with a message that names the level, entity, effect and key.
- Current behavior/evidence: `LevelDatabaseJson` accepts only `formatVersion` 1, has no void cells, and rejects any non-empty block/door `effects` or `cellObjects` ("this foundation cannot represent yet"). The void-cell design is in `production/tasks/TASK-20260924-185655569-dea430a7-play-area-environment/handoff-voidcells-schema.md`. The effect record shape and every type id with its parameters are specified in `docs/Color-Block-Jam-PRD-Mechanics-Catalog.md` (DEC-MECH-1; block effects BLK-01..18, gate effects GTE-01..11, stacking rules). `docs/levels/reference-levels.json` already uses v2 void cells and `arrow` effects. User request 2026-09-25: "bir bloğun ya da gate'in üstünde efekt varsa bu da json'dan eklenebilmeli, kontrol edilmeli".
- Expected behavior:
  1. **One version bump:** `formatVersion` 2 adds `board.voidCells` exactly as the handoff specifies and allows non-empty `effects` on blocks and doors. Version 1 files still load (no void cells, no effects); writes emit version 2.
  2. **Record shape (DEC-MECH-1):** `{ "type": "<id>", "params": [ { "key": "<name>", "value": "<string>" } ] }`. `TypeNameHandling` stays `None`; no `$type`.
  3. **Effect registry:** one definition per catalog type id (all 18 block and 11 door types): allowed target (block or door), required and optional keys, and a value rule for each key (enum, integer range, palette colour id, entity id reference). An explicit converter per type turns the string pairs into an immutable typed parameter object held by `LevelData` (for example `ArrowEffect(Axis)`, `IceEffect(ClearsToThaw 1..20)`).
  4. **Validation (load and save, outside `Playing`):** unknown type; type on the wrong target; the same type twice on one entity; unknown, duplicate or missing required key; value outside its rule; a reference to a block or door id that does not exist (for example `partner`, key `target`); the catalog's exclusive sets and stacking rules (for example `arrow` with `turn-based-arrow`, `moving-obstacle` with exit effects).
  5. **Runtime support gate:** each definition states whether gameplay implements it. Project scope rejects a level that uses an effect whose gameplay is not implemented yet, naming it, so no level loads with an effect that silently does nothing. Authoring validation (editor Save, tests, importers) still accepts every catalog type.
  6. **Canonical writing:** params are written in the definition's key order; round-trip keeps effect order per entity.
  7. **IL2CPP:** new DTOs and converter targets are preserved in `Runtime/Levels/link.xml`.
- Behavior that must remain unchanged: Existing v1 level files load unchanged; the Level Editor window's current fields and Save/Reload/Restore; `LevelDatabase` public API; runtime loading timing; the Addressables setup.

## Ownership

- Owner/writer role: level-pipeline session; sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Levels/LevelData.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelDatabaseJson.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelEffect*.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelEffect*.cs.meta
  - Assets/ColorBlockJam/Runtime/Levels/link.xml
  - Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseJsonTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEffectTests.cs
  - docs/Level-Data-Format.md
  - README.md
  - PROJECT.md
  - production/tasks/TASK-20260924-215645373-33a6a7a4-level-json-v2-effects/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Layout/** (the environment task switches `BoardLayout.TryCreate(LevelData)` to the new void cells after this lands)
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Runtime/Board/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/AddressableAssetsData/**
  - Assets/ColorBlockJam/Data/Levels/*.json content (existing files are rewritten as v2 only by a normal editor Save)
  - Gameplay implementation of any effect (separate mechanic tasks)
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: The level-pipeline session owns `Runtime/Levels/**` and `Editor/Levels/**` and also holds `TASK-20260924-210432886-2f31f26b-remote-level-json` and `TASK-20260924-210811262-93892dee-cbj-level-import`; run this task before the importer (which maps CBJ mechanics onto these effects). The play-area environment task waits on this task for its AC-2.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given a v1 file (the current `Data/Levels/LevelDatabase.json`, levels 1–5), when it loads, then it loads unchanged with no void cells and no effects; when saved, it is written as v2 with explicit empty `voidCells` and `effects`.
2. Given `docs/levels/reference-levels.json` (v2, void cells in level 9, `arrow` effects in levels 7–9), when it loads with authoring validation, then every level is valid, void cells and typed arrow axes are exposed, and a write/read round-trip is identical.
3. Given each invalid case in Expected behavior 4, when a level is read or saved, then it is rejected with a `Level {id} ...` message naming the block or door, the effect type and the key; nothing is saved.
4. Given a level that uses an effect whose gameplay is not implemented, when project scope loads it, then loading fails with a message naming the effect; the same level passes authoring validation.
5. Given the void-cell rules in the handoff (bounds, duplicates, at least one floor cell, four-connected floor, blocks on floor, doors opening onto floor, door colour ids), when violated, then each is rejected; `LevelData` exposes `VoidCellCount` and `GetVoidCellAt`.
6. Given an IL2CPP player build, then the level JSON with effects loads (link.xml preserves the DTOs and converters); recorded as a smoke check.

## References and constraints

- Story/GDD/ADR/design/log/image: `docs/Color-Block-Jam-PRD-Mechanics-Catalog.md` (DEC-MECH-1, BLK/GTE tables and each entry's JSON line, stacking rules, DEC-MECH-25); `handoff-voidcells-schema.md`; `docs/levels/reference-levels.json`; PROJECT.md level-data rules; user requests 2026-09-24/25.
- Unity version/platform: 2022.3.62f2; Android and iOS (IL2CPP)
- Package/architecture constraints: Newtonsoft `com.unity.nuget.newtonsoft-json` 3.2.2; explicit converters, `TypeNameHandling.None`; parsing only at load/save, never in a `Playing` tick; plain C# typed parameters, no reflection-based registry.
- Explicit out of scope: Gameplay for any effect, effect visuals, Level Editor UI for effects and void-cell painting, CBJ import, remote updates.

## Verification and risk

- Focused test path/command: `LevelDatabaseJsonTests` and `LevelEffectTests` (EditMode).
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Clean compile and Console.
- EditMode: Acceptance 1–5, including one valid and one invalid fixture per registered type.
- PlayMode: existing scene-flow smoke tests with the v1 and a v2 database.
- Manual/built-player/profiler/device evidence: Acceptance 6 Android IL2CPP smoke build when the next device build runs.
- Risk: high (save-format change)
- Approval already granted (exact target only): User requests on 2026-09-24 (void cells; schema owned by the level-pipeline session) and 2026-09-25 (block and door effects addable from JSON and validated).
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: level-pipeline session runs `/implement-task TASK-20260924-215645373-33a6a7a4-level-json-v2-effects`, then an independent verifier

## Stop conditions

- A catalog entry's parameter rule is ambiguous: implement the others, record the entry, and ask the user.
- Another writer is editing `Runtime/Levels/**`: re-read and serialize.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: none
- Contract fingerprint: sha256:5cc1461cab6742d6cf22d31e6bd18dd6b5f7e47290b2ee20f82a21eb27fb3cdf
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: none
- Closed at: 2026-09-25T10:28Z
- Closure mode: superseded
- Closed by: task-done --superseded-by (user reassigned the schema work to the environment session, 2026-09-25)
- Superseded by: TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 v1 files load and save as v2 | UNPROVEN | none | none |
| AC-2 Reference v2 file loads with void cells and arrows | UNPROVEN | none | none |
| AC-3 Invalid effects rejected precisely | UNPROVEN | none | none |
| AC-4 Unimplemented effects rejected at project load | UNPROVEN | none | none |
| AC-5 Void-cell rules and API | UNPROVEN | none | none |
| AC-6 IL2CPP smoke | UNPROVEN | none | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's effect request; absorbs the voidCells handoff into one formatVersion 2 change owned by the level-pipeline session
- 2026-09-25T10:28Z — closed (superseded) — never started (attempt 0); the user reassigned it to the environment session and extended it with board objects (Barrier) in TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects
<!-- TASK-LIFECYCLE:END -->
