# TASK-20260924-210811262-93892dee-cbj-level-import — Import CBJ level data into the project level JSON

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-210811262-93892dee-cbj-level-import
- Supersedes: none
- Title: Import CBJ level data into the project level JSON
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level authoring tools (import)
- Workflow: direct

## Objective and baseline

- Observable objective: A designer imports a CBJ-format level file (the `LM`/`BMS`/`DMS`/`WMS`/`CMS` structure) and gets an equivalent, validated level in the project's own level JSON, or a precise error naming what cannot be represented.
- Current behavior/evidence: The user shared a CBJ level on 2026-09-25, stored unchanged as `docs/levels/cbj-example-level.json`. It carries Newtonsoft `$type` metadata, absolute coordinates, an explicit floor-cell list (`CMS`), ring walls (`WMS`), ring door cells with colour and `IH` (horizontal), blocks with colour `BCT` and cells `BPMS` plus mechanic fields (`BIC`, `BAD`, `KID`, `LID`, `BHS`, `BD`, `ILE`, `LBCT`, `LIC`, `AD`), several mechanic arrays (`IWMS`, `GMS`, `EMS`, `CLMS`, `CCMS`, `GRM`, `CBMS`, `IBG`, `CCLMS`, `IMS`) and a ring origin `BSP`. No importer exists.
- Expected behavior (decision on 2026-09-25, delegated by the user: keep the project schema, import CBJ data): a pure C# converter in the editor assembly plus a menu command.
  1. **Safe parsing:** read the file as a `JObject` with `TypeNameHandling.None`; `$type` keys are ignored and never resolve a type.
  2. **Board:** floor = `CMS` cells. Width/height = their bounding box; coordinates shift so the box's lower-left cell is `(0,0)`; `voidCells` = box cells missing from `CMS`, written as `formatVersion` 2 void cells (docs/Level-Data-Format.md) now that the level JSON supports them (corrected 2026-09-28; see the Contract change transition below). Check that `BSP` is the ring origin (box minimum minus one).
  3. **Doors:** every `DMS` cell lies on the ring. Top = row above the box, Bottom = row below, Left/Right = columns beside it; `start` is the column (Top/Bottom) or row (Left/Right) in board coordinates, `length` the contiguous cell count; `IH` must agree with the side. Non-contiguous or corner cells are rejected.
  4. **Walls:** `WMS` must equal the ring cells not covered by doors (a consistency check; the project derives walls itself). Any other `WMS` cell is reported as unsupported.
  5. **Blocks:** colour from `BCT`; duplicate `BPMS` cells are removed with a warning naming the block; `origin` = lower-left footprint cell; `cells` relative; `shapeId` = the `BlockShapeLibrary` shape with the same footprint, otherwise the block is rejected naming its footprint.
  6. **Colours:** an explicit, editable `BCT` → `PaletteColor` table. Defaults `0..10` = Red, Orange, Yellow, LightGreen, DarkGreen, LightTurquoise, DarkTurquoise, LightBlue, Blue, Purple, Pink (the CBJ Template `BlockColor` order; confirm against the real data). Unmapped values (the example uses `BCT 11`) fail with the value and where it appears.
  7. **Mechanics:** any non-default mechanic field or non-empty mechanic array is rejected naming the field, until the matching project effect exists; the error lists every such field in one pass.
  8. **Timer:** not present in CBJ data; the import dialog asks for it with the PRD default (60 s).
  9. **Output:** the imported levels are appended to a Level Database JSON the designer picks, through the existing validated save path (`LevelDatabaseJson`, `LevelDatabaseFile`, stale-write check), with unique IDs. Nothing is written when any level fails.
- Worked example (`docs/levels/cbj-example-level.json`, box x 3–7, y 6–12 → 5 x 7, no void cells): doors Top 0:1 (`BCT 10`), Top 1:3 (`BCT 0`), Top 4:1 (`BCT 11`), Left 0:3 (`BCT 7`), Left 3:2 (`BCT 2`); blocks five `TripleVertical` at columns 0–4 row 0 (`BCT` 10, 11, 10, 11, 10), `CornerLT` at (0,3) (`BCT 0`, duplicate cell (3,10) removed), `TTypeT` at (1,3) (`BCT 2`), `CornerRT` at (3,3) (`BCT 7`). With the default table it fails only on `BCT 11`.
- Behavior that must remain unchanged: The project level schema and validation, existing level files, the Level Editor window, Addressables setup, runtime loading.

## Ownership

- Owner/writer role: level-pipeline session; sequential independent verifier
- Allowed paths (rebased 2026-09-28 for the DEC-157 folder restructure, docs/Folder-Layout.md; exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Core/Levels/CbjLevelImport.cs
  - Assets/ColorBlockJam/Editor/Core/Levels/CbjLevelImport.cs.meta
  - Assets/ColorBlockJam/Editor/Core/Levels/CbjColorTable.cs
  - Assets/ColorBlockJam/Editor/Core/Levels/CbjColorTable.cs.meta
  - Assets/ColorBlockJam/Editor/Core/Levels/Cbj/** (single-responsibility split: CbjLevelSource.cs, CbjLevelReader.cs, CbjLevelConverter.cs, plus their .meta)
  - Assets/ColorBlockJam/Data/Levels/CbjColorTable.asset
  - Assets/ColorBlockJam/Data/Levels/CbjColorTable.asset.meta
  - Assets/ColorBlockJam/Tests/Editor/EditMode/CbjLevelImportTests.cs
  - docs/Reference-Levels.md
  - docs/levels/cbj-*.json
  - production/tasks/TASK-20260924-210811262-93892dee-cbj-level-import/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Runtime/Environment/**
  - Assets/ColorBlockJam/Editor/Core/BoardKit/**
  - Assets/AddressableAssetsData/**
  - Assets/ColorBlockJam/Editor/Core/ColorBlockJam.Editor.asmdef
  - README.md (owned by another agent as of 2026-09-28; import documentation lives in docs/Reference-Levels.md only)
  - Editing existing level JSON by hand (imports go through the validated save path only when a designer runs the command)
  - Enabling `TypeNameHandling` anywhere
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Levels/CbjColorTable.asset
    Sole writer: level-pipeline session
    Matching `.meta` owner: level-pipeline session
- Other active writers/worktrees checked: The level-pipeline session is active on `Editor/Levels/**` (edits at 00:00 local on 2026-09-25) and also owns `TASK-20260924-210432886-2f31f26b-remote-level-json` and the `voidCells` handoff; sequence this task after or between them in that session.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given `docs/levels/cbj-example-level.json` and a colour table that maps `BCT 11`, when it is converted, then the result equals the worked example (size, doors, blocks, shapes, colours) and passes `LevelData.Validate`, with one duplicate-cell warning.
2. Given the default colour table, when the example is converted, then it fails naming `BCT 11` and the blocks/doors that use it, and nothing is written.
3. Given a CBJ level with a non-contiguous or corner door, an `IH` that contradicts its side, an extra wall cell, an unknown footprint, or a non-default mechanic field, when it is converted, then each is rejected with a message naming the cell, block or field. Given a CBJ level with a floor cell missing inside the `CMS` bounding box, when it is converted, then that cell becomes a `voidCells` entry instead of a rejection (corrected 2026-09-28; formatVersion 2 supports it).
4. Given a `$type` value naming a real .NET type, when the file is read, then no type is created and the value is ignored.
5. Given a designer runs the menu command on one or more CBJ files, then valid levels are appended to the chosen Level Database JSON through the existing save path with unique IDs and the entered timer; a failure writes nothing.

## References and constraints

- Story/GDD/ADR/design/log/image: User messages on 2026-09-25 (CBJ level example; "hangisi daha mantıklıysa onu yap"); `docs/levels/cbj-example-level.json`; `docs/Reference-Levels.md`; CBJ Template `BlockColor`/`ElementType` enums; PROJECT.md level-data rules (Newtonsoft, `TypeNameHandling` never enabled).
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints: Newtonsoft already installed; editor assembly only; no `System.Linq` requirement in editor code beyond project style; UI Toolkit for any window.
- Explicit out of scope: Changing the project level schema, mechanic effects, runtime loading, remote updates. Void cells are in scope since the level JSON handoff landed (corrected 2026-09-28).

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/EditMode/CbjLevelImportTests.cs` (EditMode).
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Clean compile and Console.
- EditMode: Acceptance 1–4 with the stored fixture and small inline variants.
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: Acceptance 5 in the Editor: import the example (with a mapped `BCT 11`) into a scratch Level Database JSON and open it in the Level Editor and Game preview.
- Risk: medium
- Approval already granted (exact target only): User delegated the choice on 2026-09-25; this contract records it (keep the project schema, add an importer owned by the level-pipeline session).
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: level-pipeline session runs `/implement-task TASK-20260924-210811262-93892dee-cbj-level-import`, then an independent verifier

## Stop conditions

- The real `BCT` colour order is needed for a colour the table does not cover: ask the user rather than guessing.
- A CBJ field's meaning is unclear and the level uses a non-default value: reject it and record the field; do not invent a mapping.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:ef444de13673f41aa5f050f4807292275f36111896cea59c40f06a753d2800a2
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: cs:23 (Plastic workspace head, unreviewed)
- Closed at: 2026-09-29T01:16:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Example converts to the worked result | UNPROVEN | EditMode CbjLevelImportTests.ExampleConvertsToWorkedResult_WithMappedColorTable | none |
| AC-2 Unmapped colour fails without writing | UNPROVEN | EditMode CbjLevelImportTests.ExampleWithDefaultColorTable_FailsNamingBct11AndItsUsers | none |
| AC-3 Invalid CBJ structures are rejected precisely | UNPROVEN | EditMode CbjLevelImportTests.{NonContiguousDoorCellsRejected, CornerDoorCellRejected, DoorIhContradictingSideRejected, ExtraWallCellRejected, UnknownFootprintBlockRejected, NonDefaultMechanicFieldRejected, NonEmptyMechanicArrayRejected, MissingFloorCellBecomesVoidCell} | none |
| AC-4 `$type` never creates a type | UNPROVEN | EditMode CbjLevelImportTests.TypeMetadataNamingARealTypeIsIgnored | none |
| AC-5 Menu import appends through the validated save path | UNPROVEN | Manual Editor check (import the example via Color Block Jam > Levels > Import CBJ Level... into a scratch Level Database JSON); automated slice: EditMode CbjLevelImportTests.ConvertedLevelAppendsThroughLevelDatabaseFile | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's CBJ level example; project schema kept, importer assigned to the level-pipeline session
- Contract change (path rebase and v2 void cells) — planning — rebased every allowed/forbidden path to the DEC-157 folder restructure (docs/Folder-Layout.md: Editor/Levels -> Editor/Core/Levels, Tests/EditMode -> Tests/Editor/EditMode, Editor/BoardKit -> Editor/Core/BoardKit, the asmdef path); updated expected-behavior item 2 and acceptance scenario 3 so a CMS gap inside the bounding box converts to a formatVersion 2 voidCells entry (docs/Level-Data-Format.md) instead of being rejected, now that void cells are supported; removed README.md from allowed paths (owned by another agent as of 2026-09-28) and confined import docs to docs/Reference-Levels.md; no scope or risk widening; fingerprint recomputed from sha256:0a5db079119944aff6004acabe981818568e6391bed031963ffaa15e22d20cab
- Implementation — implementation complete — cbj-implementer session built CbjLevelImport.cs (menu command + UI Toolkit timer prompt), CbjColorTable.cs (editable BCT->PaletteColor table, defaults seeded), and the Cbj/ split (CbjLevelSource.cs, CbjLevelReader.cs, CbjLevelConverter.cs) under Editor/Core/Levels; added docs/levels/cbj-example-level.json (the fixture path docs and the contract name, matching the existing docs/levels/example-level.json content) and CbjLevelImportTests.cs under Tests/Editor/EditMode; documented the importer in docs/Reference-Levels.md. Roslyn compile check OK for ColorBlockJam.Editor and ColorBlockJam.Editor.EditModeTests. Unity EditMode test execution and the CbjColorTable.asset live-Editor creation are deferred/pending; see the implementer's handoff report for exact status.
- 2026-09-29T01:16:19Z — Closed (user confirmed) — closed — explicit user order 2026-09-29 ("açık taskları da kapat üstünde çalışılmayanları artık"); prior state in_progress / implementation complete; Unity tests deferred to the orchestrator's combined batch run (user order 2026-09-28). Closure mode user_confirmed: every acceptance criterion result, blocker note and defect row is preserved as recorded, not verified by this closure.
<!-- TASK-LIFECYCLE:END -->
