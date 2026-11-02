# Evidence: TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects

Implementer run 2026-09-25 (single writer). Live Editor: MCP for Unity HTTP
127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`, Unity
2022.3.62f2, Plastic workspace cs:7 plus pending local changes. No scene was
opened or saved; the open untitled scene stayed clean (`dirty=False`).

## Changed files (all inside the contract's allowed paths)

Runtime (`Assets/ColorBlockJam/Runtime`):

- `Levels/LevelData.cs`: `m_voidCells`, `m_cellObjects`, block/door `m_effects`
  (all `[SerializeField]`); new constructor overloads, old ones kept; `VoidCellCount`,
  `GetVoidCellAt`, `IsFloor`, `CellObjectCount`, `GetCellObjectAt`,
  `EffectCount`/`GetEffectAt`/`TryGetEffect<T>` on blocks and doors,
  `ValidateRuntimeSupport`, `MaxDoorLength = 3`. `Validate` adds void-cell rules,
  door span 1-3, gate colour, door-onto-void, block-on-void, cell-object rules and
  effect validation.
- `Levels/LevelDatabaseJson.cs`: reads `formatVersion` 1 and 2, writes 2 with explicit
  empty arrays; `LevelReadMode` (`Runtime` default, `Authoring`); effect and cell-object
  DTOs; canonical param order; parameter keys/values must be JSON strings.
- New: `Levels/LevelEffectData.cs`, `LevelEffectDefinition.cs`, `LevelEffectRegistry.cs`,
  `LevelEffectParameters.cs`, `LevelEffectValidator.cs`, `LevelEffectChecks.cs`,
  `LevelCellObjectData.cs` (+ Unity-generated `.meta`).
- `Levels/link.xml`: new DTOs, serialized records and all 35 typed parameter classes.
- `Layout/BoardLayout.cs`: `TryCreate(LevelData, ...)` passes the level's void cells.

Tests: `Tests/EditMode/LevelDatabaseJsonTests.cs` (updated), new
`Tests/EditMode/LevelEffectTests.cs`, `Tests/EditMode/LevelCellObjectTests.cs`;
`Tests/Environment/EditMode/EnvironmentComposerTests.cs` (reads the v2 reference file
through `LevelDatabaseJson` authoring mode and `BoardLayout.TryCreate(LevelData)`,
replacing its private v2 reader); `Tests/Environment/EditMode/BoardLayoutTests.cs`
(+1 test).

Docs: new `docs/Level-Data-Format.md`; one bullet each in `README.md` and `PROJECT.md`.

Not touched: `Data/Levels/*.json`, `LevelDatabase.cs`, Editor/**, Gameplay/**,
Presentation/**, Environment/**, Packages, ProjectSettings, Addressables.

## Peer requirements

1. Runtime-support flag: one `private const bool` per definition in
   `Assets/ColorBlockJam/Runtime/Levels/LevelEffectRegistry.cs` (lines 14-55).
   Barrier: **line 51**, member `k_barrierRuntimeImplemented`:
   `private const bool k_barrierRuntimeImplemented = false;` The gameplay session flips
   it to `true`. It feeds `LevelEffectDefinition.IsRuntimeImplemented`.
2. Validation messages: listed verbatim in `docs/Level-Data-Format.md` (Validation messages).
3. `voidCells`, block/door `effects` and `cellObjects` are Unity-serializable
   `LevelData` fields; `JsonUtility` round trip verified by
   `LevelEffectTests.TypedAccessorsAndUnitySerializationKeepEffects` and
   `LevelCellObjectTests.CellObjectsSurviveUnitySerializationAndClone`.

## Compile and Console

- `refresh_unity {"mode":"force","scope":"all","compile":"request"}` after each edit batch;
  final compile clean. Console after compile: no compile errors or new warnings (only the
  pre-existing `VacuumMotionAssets.cs(54,13) CS0618` warning and MCP WebSocket notices).
- Console errors after the full test run are the four expected
  `Could not restore piece JSON ... PrefabTests_*/Library.json` lines logged by the
  board-kit piece tests (their temporary folders were removed; `Data/Board` holds no
  `PrefabTests_*` folder).

## Tests (MCP `run_tests` / `get_test_job`)

| Run | Job id | Result |
| --- | --- | --- |
| Focused: `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests` | `041088aceecd4869beccf67c5384f515` | 120/120 passed |
| Environment EditMode assembly (incl. reference level 9 with void cells) | `7dc4f05512024d2db6a1dff39d041f71` | 61/61 passed |
| Gameplay EditMode assembly | `2fce296e5fd14e72a607d1191f06c416` | 215/215 passed |
| All EditMode assemblies (Editor, Environment, Gameplay, LevelPreview) | `763a193dd7004cb0ad6d8d89287102d3` | 667/670; 3 failures, see below |
| All PlayMode assemblies (Gameplay, Presentation incl. `SceneFlowSmokeTests`, Environment) with the v1 game database | `8253bbdfc2f1408c9cb4cf6defcfb034` | 38/38 passed |

The 3 failures are `BoardKitTests.ActivePieceDatabaseHasOneRecordPerRotationAndMirrorFamily`,
`CreatingPieceDatabaseActivatesCloneAndRejectsStaleDisk` and
`EveryLibraryShapeInEveryRotationHasExactOccupancyWithoutOverlappingTiles`: "Expected: 11 But
was: 12". `Data/Board/PieceDatabases/BlockShapeLibrary.json` holds 12 shapes since
11:34 (another session's piece data); unrelated to level data. An earlier full run
(`2a4bad108b3a47d6bdf1042a3dc24149`, 665/669) also showed the timing-sensitive
`LevelEditorShellTests.OneFieldCommitPublishesTheDraftOnce` (publish count 3); it passed
when the fixture ran alone (14/14) and in the later full run.

## Acceptance mapping

- AC-1: `VersionOneDatabaseLoadsUnchangedAndSavesAsVersionTwo` (v1 file loads with no void
  cells/effects/objects; written v2 equals the original plus version 2 and explicit
  `voidCells: []`), `VersionOneRejectsVersionTwoContent`,
  `ProjectScopeDatabaseLoadsVersionOneAndVersionTwoAndRefusesUnimplementedEffects`
  (`LevelDatabase.LoadJson` of the real `Data/Levels/LevelDatabase.json` and of its v2
  rewrite). Live capture (`execute_code`): `gameDB v1 read=True levels=5 write=True v2=True
  voidCellsKey=True reread=True`.
- AC-2: `ReferenceLevelsLoadForAuthoringWithVoidCellsTypedArrowsAndIdenticalRoundTrip`
  (10 levels valid; level-09 void cells (0,0),(5,0); typed `ArrowEffect` axes; written JSON
  `JToken.DeepEquals` the file; second write identical).
- AC-3: `LevelEffectTests.Invalid_*` (one invalid fixture per registered type plus unknown
  and empty type; exact `Level fx ...` messages; `TryWrite` refuses, `json` stays null),
  `Undecided_*`, `LevelCellObjectTests.CellObjectStructureRulesRejectWithLevelMessages`,
  `ReadRejectsUnknownEffectTypesAndIncompleteRecords`.
- AC-4: `RuntimeSupportGateNamesTheUnimplementedEffectWhileAuthoringAccepts` (now one case per
  valid fixture, expectation from each definition's flag),
  `ProjectScopeRefusesTheBarrierUntilGameplayImplementsIt`,
  `RuntimeReadRefusesReferenceLevelsWhoseArrowIsNotImplemented`.
- AC-5: `VoidCellAndDoorRulesRejectWithLevelMessages` (bounds, duplicate, no floor,
  four-connected, block on void, door onto void, door colour), `DoorLongerThanThreeCellsIsRejected`,
  `EveryCurrentLevelKeepsDoorsWithinOneToThreeCells` (game database plus 10 + 9 reference levels),
  `VoidCellsRoundTripInOrderThroughTheLevelApi`, `BoardLayoutTests.LevelDataVoidCellsBecomeHolesInTheLayout`,
  `EnvironmentComposerTests.ReferenceLevelNineTurnsItsVoidNotchesIntoInnerCorners` (now through
  `BoardLayout.TryCreate(LevelData)`: 34 floors, 2 inner and 6 outer corners). Live capture:
  `level-09 voids=2 floor00=False floor50=False floor10=True`.
- AC-6: `BarrierRoundTripsAndExposesTypedStartsOpen(true|false)`, `BarrierStartsOpenDefaultsToFalse`,
  `ClosedBarrierOnABlockIsRejectedAndAnOpenOneIsAccepted`, `BarrierCellsMustBeFourConnected`.
- AC-7: link.xml updated; Android IL2CPP smoke build not run (pending the next device build).
- Valid fixtures: `LevelEffectTests.Valid_*` (35 decided types) validate, round-trip
  byte-identically and expose their typed parameter class.
- Play-area environment task AC-2: `BoardLayout.TryCreate(LevelData)` passes void cells;
  Environment EditMode 61/61. Its Blocker, Next action and AC-2 row were updated in the
  review fix round (below).
- Elevator stacks: `LevelCellObjectTests.ElevatorStackSharesItsCellsWithOneStackIndexPerBlock`,
  `CrateContentsMayNotOverlap`; new invalid fixtures `Invalid_chained-keys`,
  `Invalid_moving-obstacle-only`, `Invalid_moving-door-lock-one-door`,
  `Invalid_colorful-path-under-block`, `Invalid_contained-crate-outside`,
  `Invalid_contained-moveable-crate-footprint`, `Invalid_contained-elevator-stack-index`.

## Open questions for the user (stop condition: ambiguous catalog rules)

Registered but refused by authoring until decided: list values in one string
(`layer.layerColors`, `ropes.ropeColors`, `colorful-door.colorSequence`,
`size-changing-door.spanSequence`, `colorful-jumping-door.route`, `flower-crate.leaves`),
`size-changing-door.anchor` spelling, `button-door.buttonCell` encoding. Accepted with a
recorded assumption: no maximum for `locked-door.keysRequired`, `crate.clearsToOpen`,
`moveable-crate.gatePassesToOpen` (1 or more); `curtain`/`toggle-door` `startsOpen`
required; `moving-obstacle` neutral colour not checked (no Neutral palette entry).

## Review fix round (2026-09-25, same implementer)

All edits in this round used the file edit tools. The first round rewrote
`LevelDatabaseJson.cs` through a scratchpad Python script (`v2_json_edit.py`);
its full diff against `scratchpad/LevelDatabaseJson.v2-before.cs` is reported to
the orchestrator.

- Editor reads and the runtime gate (review findings 1 and 7): **not fixed in code;
  Expected 5 is not met for editor Save.** The 3-argument `TryRead` stays the
  project-scope (runtime-gated) read because `LevelDatabase.LoadJson`, its only
  runtime caller, is outside the allowed paths. Making it Authoring would let
  project scope load unimplemented effects silently. Fix options: widen the
  contract by `Runtime/Levels/LevelDatabase.cs` (one line: pass
  `LevelReadMode.Runtime`) and make the 3-argument overload Authoring, or the
  Editor session switches its `TryRead` calls to `LevelReadMode.Authoring` first.
  Needs a user or orchestrator decision. `README.md` and
  `docs/Level-Data-Format.md` now state the real behaviour and the gap.
- Elevator stacks (finding 2): blocks `contained` in the same elevator are exempt
  from the block overlap rule; `stackIndex` is required and unique per elevator
  and stacked blocks must lie inside its cells. Other overlaps stay errors.
- Hard-coded runtime flags (finding 3): gate tests derive their expectation from
  `LevelEffectDefinition.IsRuntimeImplemented`; the registry test no longer
  asserts the Barrier flag. Flipping any `k_*RuntimeImplemented` line keeps them green.
- Exact level count (finding 4): only the two fixed reference files keep counts
  (10 and 9); the live game database is checked for door spans only.
- Missing catalog errors (finding 5): added `chained` key total, `moving-obstacle`
  on every block, `moving-door-lock` with one door, `colorful-path` under a block
  of another colour, crate content outside its cells, moveable-crate footprint
  and single content. Crate content overlaps are refused by the block overlap rule.
  Remaining assumptions are listed under Open questions in the doc.
- Reader messages (finding 6): `EffectDto.type`/`params` are no longer
  `Required.Always`, so a missing one gives `Level {L} {entity} has an effect
  without type or params.`; a non-string parameter gives `Level {L} {entity} effect
  {T} parameter {K} needs a JSON string key and value.` (parameters are read as
  raw tokens; `DateParseHandling.None`).
- Wording (finding 8): "every catalog type" claims now say "whose value format is
  decided" in `LevelData.cs`, `LevelDatabaseJson.cs`, `LevelEffectRegistry.cs`
  (still 3 comment lines, so the Barrier flag stays on line 51), the doc and README.
- Message list (finding 9): `docs/Level-Data-Format.md` lists every message of
  `Validate`, the effect validator, `TryRead` and `TryWrite`, marking the
  pre-version-2 texts "(unchanged)".
- Environment task (finding 10): its Blocker, Next action and AC-2 row now point at
  this task with the test evidence; AC-2 stays UNPROVEN until this task is verified.
  `docs/Environment-Layout.md` (Open items) and `docs/Reference-Levels.md` are
  outside this task's paths and still describe v2 as planned: handed off.

Tests after the fix round (live Editor, MCP):

| Run | Job id | Result |
| --- | --- | --- |
| Focused: `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests` | `000cf98994b9465eb84c8614037f1c13` | 163/163 passed |
| All EditMode assemblies (Editor, Environment incl. reference level 9, Gameplay, LevelPreview) | `bfcfb84756b34a81b5196d2bd5fbc6db` | 710/713; the same 3 `BoardKitTests` "Expected: 11 But was: 12" failures (piece data, unrelated) |
| All PlayMode assemblies (Gameplay, Presentation incl. `SceneFlowSmokeTests`, Environment) | `2a92a6abdd124bb4b0dc0a9c4275ec5d` | 38/38 passed (first attempt `340e4688...` did not start within the MCP timeout) |

Compile clean: no `error CS` entries after `refresh_unity`. Open scene untitled,
`dirty=False`, not playing, before and after the runs.

## Integration notes for peers (not changed here; their paths)

- Editor (`Editor/Levels/LevelDatabaseFile.cs`, `LevelDatabaseInspector.cs`) calls the
  3-argument `TryRead`, which is now the project-scope (runtime-gated) read. Switch to
  `TryRead(json, LevelReadMode.Authoring, ...)` before a saved file contains any effect or
  Barrier, or Reload/Save's pre-read refuses it.
- `Editor/Levels/Canvas/LevelRecordCopy.cs` rebuilds records with the old constructors, which
  drop void cells, effects and cell objects on edits; use the new overloads.
- `LevelData.Validate` now rejects door spans above 3 and non-gate door colours; the editor
  currently shows both as warnings.
- Typed parameters convert lazily on first access; read them while building the attempt
  (outside `Playing`) so no conversion allocates in the tick.

## Independent verification (2026-09-25, verifier session, read-only on code)

Live Editor over MCP for Unity HTTP 127.0.0.1:8082, instance
`ColorBlockJamCase2026@99077e4df1ecfaa4`. Before and after: compile not failed, not
compiling, not playing, one untitled scene `dirty=False`. No asset changed on disk after the
last domain reload, so no refresh was needed. No scene was opened or saved. No production or
test file was edited. Captures are in `captures/verify-*`.

### Tests (MCP `run_tests`)

| Run | Job id | Result |
| --- | --- | --- |
| Focused `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests` | `37da4f82f5a84873846907fd417c6b67` | 163/163 passed |
| All EditMode assemblies (Editor, Environment incl. reference level 9, Gameplay, LevelPreview) | `673eeeadaa0148efb087519385123b98` | 710/713; only the 3 `BoardKitTests` "Expected: 11 But was: 12" |
| All PlayMode assemblies (Gameplay, Presentation incl. `SceneFlowSmokeTests`, Environment) | `707bd79b724d49b689b7a4c3f5a09842` | 38/38 passed |

The 3 `BoardKitTests` failures are unrelated. `Data/Board/PieceDatabases/BlockShapeLibrary.json`
holds 12 shapes, unchanged since 11:34, and the tests assert 11. The Console holds only their
`Could not restore piece JSON ... PrefabTests_*` errors.

### Live captures (`execute_code`, in memory; no project file written)

- `captures/verify-ac1-ac2-ac3-ac5-live.txt`:
  - AC-1: the game database file is `formatVersion` 1 with 5 levels, and it reads with 0 void
    cells, 0 effects and 0 cell objects. The write is version 2 with explicit `voidCells: []`,
    `cellObjects: []` and block and door `effects: []`. Apart from the version and
    `voidCells`, the written content `DeepEquals` the file. `LevelDatabase.LoadJson` loads
    both the v1 file and the v2 rewrite (5 levels each). The file's SHA-256 did not change.
  - AC-2: the Authoring read passes: 10 levels, 10 valid, 12 typed `ArrowEffect`
    (7 Horizontal, 5 Vertical). Level 9 has void cells (0, 0) and (5, 0). The write
    `DeepEquals` the file, and a second write is byte-identical. The runtime-gated read refuses
    the file with `Level level-07 block b1 effect arrow is not implemented in gameplay yet.`
  - AC-5: `BoardLayout.TryCreate(level-09)` builds a 6x6 board with 34 floor cells;
    `IsFloor(0,0)` and `IsFloor(5,0)` are false.
  - AC-3: `TryWrite` of a closed barrier on a block returns false with a null JSON.
  - Peer requirement 3: `JsonUtility` round trip keeps void cells, effects and cell objects,
    with their typed accessors.
- `captures/verify-variants-read.txt` reads 38 fixtures of my own
  (`verify-variants-fixtures.json`) in both modes:
  - Every Expected 4 rule is rejected with a `Level vf ...` message that names the entity,
    the type and the key:
    - unknown type, wrong target, same type twice;
    - unknown, duplicate or missing key;
    - enum value, integer range, non-string value;
    - missing block reference, exclusive pair;
    - void cell outside or listed twice, disconnected floor, block on void, door onto void;
    - non-gate door colour, door span 4;
    - cell object: duplicate ID, no cells, outside the board, on void, shared cell, cell listed twice;
    - barrier: closed on a block (explicit or default), not four-connected, bad bool;
    - block effect on a cell object;
    - version 1 carrying v2 content.
  - The valid Barrier fixtures (`startsOpen` true, false, absent) pass Authoring and
    round-trip identically; typed `StartsOpen` reads True, False and False. Runtime refuses
    them with `... effect barrier is not implemented in gameplay yet.`
- `captures/verify-editor-save-reload-barrier.txt`: the peer Editor's `LevelDatabaseFile`,
  run on scratchpad files only:
  - `firstSave=True`;
  - then `editorReloadAfterSave=False` and `secondSave=False`, both with
    `Level bar cell object o1 effect barrier is not implemented in gameplay yet.`;
  - `authoringReadOfSavedFile=True`.

  The editor's pre-read uses the 3-argument, runtime-gated `TryRead`.

### Verdict per acceptance scenario

| AC | Result | Basis |
| --- | --- | --- |
| AC-1 | PASS | `VersionOneDatabaseLoadsUnchangedAndSavesAsVersionTwo`, `VersionOneRejectsVersionTwoContent`, `ProjectScopeDatabaseLoadsVersionOneAndVersionTwoAndRefusesUnimplementedEffects`; live AC-1 capture |
| AC-2 | PASS | `ReferenceLevelsLoadForAuthoringWithVoidCellsTypedArrowsAndIdenticalRoundTrip`; live AC-2 capture |
| AC-3 | PASS | `Invalid_*` (one per decided type), `CellObjectStructureRulesRejectWithLevelMessages`, `VoidCellAndDoorRulesRejectWithLevelMessages`; 32 live invalid fixtures; `TryWrite` refuses with null JSON |
| AC-4 | FAIL | See below |
| AC-5 | PASS | Void and door rule tests; `DoorLongerThanThreeCellsIsRejected`; `EveryCurrentLevelKeepsDoorsWithinOneToThreeCells`; `LevelDataVoidCellsBecomeHolesInTheLayout`; `ReferenceLevelNineTurnsItsVoidNotchesIntoInnerCorners` (predicted counts unchanged from the pre-task file: 34 floors, 2 inner and 6 outer corners); live AC-5 capture |
| AC-6 | PASS | `BarrierRoundTripsAndExposesTypedStartsOpen(true/false)`, `BarrierStartsOpenDefaultsToFalse`, `ClosedBarrierOnABlockIsRejectedAndAnOpenOneIsAccepted`, `BarrierCellsMustBeFourConnected`; live Barrier fixtures |
| AC-7 | UNPROVEN | `link.xml` preserves all 9 DTOs, 3 records and all 35 typed parameter classes (checked by name); the Android IL2CPP smoke build was not run |

AC-4 is FAIL for two reasons:

- Project scope refuses the Barrier and names it, and the `LevelReadMode.Authoring` API accepts
  the same level.
- Expected 5 defines authoring validation to include editor Save, and that path refuses the
  level after its first save. Reload and every later Save fail with the runtime-gate message.
  Authoring a Barrier in the editor therefore dead-ends. The implementer reported this
  (review findings 1 and 7).

The fix needs a file outside this contract's allowed paths. Either:

- `Runtime/Levels/LevelDatabase.cs` passes `LevelReadMode.Runtime` and the 3-argument overload
  becomes Authoring; or
- `Editor/Levels/LevelDatabaseFile.cs` and `LevelDatabaseInspector.cs` call
  `LevelReadMode.Authoring`.

A user or orchestrator decision is needed.

Also noted, not graded as a scenario:

- Stop condition: seven catalog types are registered but refused by authoring because their
  value format is undecided. They are listed as open questions.
- PlayMode ran only with the v1 game database; the v2 path is covered in EditMode through
  `LevelDatabase.LoadJson`.

### Changed files and scope

Plastic cannot isolate this task, because the workspace holds many sessions' pending changes
since cs:7. I attributed files by the pre-task baselines in the implementer's scratchpad and by
timestamps:

- Runtime: `Runtime/Levels/`
  - `LevelData.cs`
  - `LevelDatabaseJson.cs`
  - `LevelEffectData.cs`, `LevelEffectDefinition.cs`, `LevelEffectRegistry.cs`,
    `LevelEffectParameters.cs`, `LevelEffectValidator.cs`, `LevelEffectChecks.cs`
  - `LevelCellObjectData.cs`
  - `.meta` files for the new scripts
  - `link.xml`
- `Runtime/Layout/BoardLayout.cs`: only `TryCreate(LevelData, ...)` changed.
- Tests:
  - `Tests/EditMode/LevelDatabaseJsonTests.cs`
  - `Tests/EditMode/LevelEffectTests.cs` and `LevelCellObjectTests.cs`, with their `.meta` files
  - `Tests/Environment/EditMode/EnvironmentComposerTests.cs` and `BoardLayoutTests.cs`
- Docs: `docs/Level-Data-Format.md`, `README.md`, `PROJECT.md`.
- Production: this task folder, and the environment task's `contract.md` (AC-2 evidence and
  lifecycle only).

All of these are inside the allowed paths. No out-of-scope write was found:

- `Data/Levels/LevelDatabase.json`: last modified 01:00, content hash stable.
- `LevelDatabase.cs`: 2026-09-24 21:55.
- `Packages`, `ProjectSettings`, `Environment/**` and `Presentation/**` show no change from this task.
- Every public `LevelData` and `LevelDatabaseJson` declaration from before the task still
  exists; only private DTO field types changed.
- `TypeNameHandling.None`; no `System.Linq` or `System.Reflection` in `Runtime/Levels`.

## Fix round 2 after verification (2026-09-25, implementer)

Same live Editor over MCP. Before and after: one untitled scene, `dirty=False`, not
playing, not compiling. No scene was opened or saved.

### Changed in this round

- `Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseJsonTests.cs`: new test
  `LinkXmlPreservesEveryJsonDtoAndTypedEffectParameterClass`, plus the `using` lines it
  needs. It reads `Runtime/Levels/link.xml` and checks both directions:
  - every `link.xml` entry resolves to a real `ColorBlockJam.Runtime` type, so there are
    no stale names;
  - every private `*Dto` nested in `LevelDatabaseJson`, `LevelEffectData`,
    `LevelEffectParamData`, `LevelCellObjectData`, `LevelEffectParameters` and every
    subclass of `LevelEffectParameters` (35 today) has a `preserve="all"` entry.

  Reflection is used only in the test assembly. `Runtime/Levels` still has none.
- No production file changed in this round.

### AC-7 (IL2CPP): still UNPROVEN, now statically guarded

The new test turns the verifier's by-name check into a regression test. Adding or renaming
a DTO or a typed parameter class without its `link.xml` entry now fails EditMode. The
criterion still needs the Android IL2CPP smoke build: load a level JSON that has effects
and cell objects on the device. A player build is a High-risk action that needs the
user's go, and it cannot run while the Editor holds the project, so this round did not
run one.

### AC-4 (editor Save counts as authoring): still FAIL, blocked on scope

This round could not fix AC-4 without leaving the contract's allowed paths:

- The only runtime caller of the 3-argument `LevelDatabaseJson.TryRead` is
  `LevelDatabase.LoadJson` (`Runtime/Levels/LevelDatabase.cs:71`). That file is not in
  the allowed paths, and the level-pipeline session's remote-level task works near it.
- The editor callers are `Editor/Levels/LevelDatabaseFile.cs:37` and
  `LevelDatabaseInspector.cs:79`. `Editor/**` is forbidden and owned by the Level Editor
  session.
- `LevelDatabaseJson.cs` cannot tell the two callers apart. Rejected alternatives:
  - Switching the overload on `Application.isPlaying` would make
    `LevelDatabase.LoadJson` in EditMode tests skip the gate. It is hidden global state.
  - Changing the 3-argument default to Authoring alone would let project scope load
    unimplemented effects without an error, which breaks the other half of AC-4.

Either option below fixes AC-4. Each needs the user's scope approval (widened ownership
counts as Medium risk):

- **Option A (Level Editor session, no change here).** Make both editor calls
  Authoring:
  - `LevelDatabaseFile.cs:37`:
    `LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out levels, out error)`
  - `LevelDatabaseInspector.cs:79`:
    `LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out LevelData[] levels, out string error)`

  After it lands, the verifier's `captures/verify-editor-gap.cs.txt` should report
  `editorReloadAfterSave=True` and `secondSave=True`.
- **Option B (this task, if the contract adds `Runtime/Levels/LevelDatabase.cs`).**
  Make the gate explicit in project scope and the default Authoring:
  - `LevelDatabase.cs:71` passes `LevelReadMode.Runtime`.
  - The 3-argument `TryRead` in `LevelDatabaseJson.cs` becomes `LevelReadMode.Authoring`.
  - The doc and README text about the gap is updated.

  This covers every editor caller at once and leaves no Editor change.

Option B is the recommended one: the permissive default serves every authoring caller,
and the one project-scope call names its gate explicitly.

### Tests (MCP `run_tests`)

| Run | Job id | Result |
| --- | --- | --- |
| Focused `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests` | `35d6217048ef4c2c9d556553ea67d194` | 164/164 passed (163 plus the new link.xml test) |
| All EditMode assemblies (Editor, Environment incl. reference level 9, Gameplay, LevelPreview) | `81f6532ffab340589e42b5a57bd66a50` | 707/714; see below (`captures/fix2-job-editmode-all.json`) |
| Rerun of the two failing fixtures alone | `9a35506ef4ce41a5bfd3d5bee79b6131` | 20/21 (`captures/fix2-job-editmode-rerun.json`) |
| All PlayMode assemblies (Gameplay, Presentation incl. `SceneFlowSmokeTests`, Environment) | `05252e3b560843c79c9798910f0a250e` | 38/38 passed (`captures/fix2-job-playmode-all.json`) |

- The 3 `BoardKitTests` failures from earlier runs now pass.
- In the full run, 6 `Gameplay.ZeroAllocationTests` failed in their `OneTimeSetUp`. The
  warm-up asserts that an empty lambda does not allocate. They all pass when the fixture
  runs alone, so this is recorder noise from the busy Editor, not a code allocation.
- `LevelCanvasBlockToolsTests.PlacingAShapeAddsOneBlockAndPublishesItOnce` fails in both
  runs ("One place reaches the preview once. Expected 1 but was 0"). It passed in the
  verifier's run. Since then, only the Level Editor session's
  `Editor/Levels/Canvas/LevelCanvasElement.cs` and `LevelCanvasGeometry.cs` have changed
  (15:24). `Runtime/Levels/*` is unchanged since 14:56. The failure is in that session's
  in-progress work; it is reported to them and not changed here.
- Compile clean: no `error CS` entries after `refresh_unity`.

## Independent verification 2 (2026-09-25 ~15:35-15:55, verifier, read-only on code)

Live Editor over MCP for Unity HTTP 127.0.0.1:8082, instance
`ColorBlockJamCase2026@99077e4df1ecfaa4`. Before and after: compile not failed, not
compiling, not playing, one untitled scene `dirty=False`. I opened and saved no scene and
edited no production or test file. One `refresh_unity` (force, compile) ran after another
session's test run finished; the compile was clean, and the Console held no `error CS`, only
the board-kit tests' `Could not restore piece JSON ... PrefabTests_*` lines and one
"Already added touchscreen". Captures are in `captures/verify2-*`.

State of the code under test: `Runtime/Levels/*` last modified 14:56, `BoardLayout.cs` 14:21,
`LevelDatabase.cs` 2026-09-24 21:55 (untouched). `Data/Levels/LevelDatabase.json` SHA-256
`02c013a5...` is unchanged (last written 01:00). The Editor callers
(`LevelDatabaseFile.cs:37`, `LevelDatabaseInspector.cs:79`) still call the 3-argument
runtime-gated `TryRead`, so neither AC-4 option has landed.

### Tests (MCP `run_tests`)

| Run | Job id | Result |
| --- | --- | --- |
| Focused `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests` | `4be710a3189d43eabb6dd31a0382c51e` | 164/164 passed |
| Environment `EnvironmentComposerTests` + `BoardLayoutTests` (reference level 9 void notches, void-cell holes) | `67c821a3af414524b301d440a9a487a9` | 38/38 passed |
| All EditMode assemblies (Editor, Environment, Gameplay, LevelPreview) | `4b8a8157d2744675b9c0db5335d6f3c3` | 713/715; 2 Level Editor failures, below |
| Rerun of `LevelCanvasBlockToolsTests` + `LevelEditorShellTests` alone | `eb8635f139664b9e81bbd7815d35acc7` | 28/29 |
| All PlayMode assemblies (Gameplay, Presentation incl. `SceneFlowSmokeTests`, Environment), v1 game database | `b1ad8bc44a994c85881f5b66ea9e9d5f` | 38/38 passed |

The two EditMode failures are not caused by this task:

- `LevelCanvasBlockToolsTests.PlacingAShapeAddsOneBlockAndPublishesItOnce` failed in the full
  run only and passed when rerun alone.
- `LevelEditorShellTests.UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview` ("Undo
  publishes the draft to the preview. Expected 300 But was null") fails both times. It passed
  in the first verification and in fix round 2, both on the same `Runtime/Levels` code as now.
  The only files changed since then are the Level Editor session's in-progress
  `Editor/Levels/Canvas/LevelCanvasElement.cs`, `LevelCanvasGeometry.cs` (15:39),
  `LevelCanvasHitTest.cs` (15:46) and their tests. Hand this failure to that session.
- All Gameplay EditMode tests passed in the full run.

### Live captures (`execute_code`, in memory; the editor check wrote scratchpad files only)

These reuse the first verifier's scripts (`captures/verify-ac-capture.cs.txt`,
`verify-editor-gap.cs.txt`, `verify-variants-fixtures.json`). All three outputs match the first
verification exactly:

- `captures/verify2-live-ac1-ac2-ac3-ac5-editorgap.txt`: AC-1, AC-2, AC-3 and AC-5 as before,
  and the JsonUtility round trip (peer requirement 3). The editor check still returns
  `firstSave=True`, then `editorReloadAfterSave=False` and `secondSave=False` with
  `Level bar cell object o1 effect barrier is not implemented in gameplay yet.`
- `captures/verify2-variants-read.txt`: all 38 fixtures return the same results as before.

Static checks:

- No public declaration of `LevelData.cs`, `LevelDatabaseJson.cs` or `BoardLayout.cs` was
  removed; each file was compared with its pre-task copy.
- The `BoardLayout.cs` diff is limited to `TryCreate(LevelData, ...)`, which now passes the
  level's void cells.
- `LevelEffectRegistry.cs` has 42 `k_*RuntimeImplemented` lines, all `false`. Barrier is at
  line 51.
- The registry covers every catalog id: BLK-01..18, GTE-01..11, OBJ-01..09, and the
  companions `key`, `scissors`, `star-door` and `contained`.
- `TypeNameHandling.None` is set, and there is no `System.Linq` or `System.Reflection` in
  `Runtime/Levels`.
- `link.xml` has 48 `preserve="all"` entries, guarded by
  `LinkXmlPreservesEveryJsonDtoAndTypedEffectParameterClass` (passed).

### Verdict per acceptance scenario

| AC | Result | Basis |
| --- | --- | --- |
| AC-1 | PASS | Focused tests; live capture: the v1 file reads with 0 void cells, effects and cell objects, and writes v2 with explicit `[]`. `LevelDatabase.LoadJson` loads both the v1 file and its v2 rewrite. PlayMode smoke 38/38 with the v1 database. A PlayMode run with a v2 database was not possible without writing the forbidden `Data/Levels` JSON; the v2 project-scope path is covered in EditMode through `LevelDatabase.LoadJson`. |
| AC-2 | PASS | `ReferenceLevelsLoadForAuthoringWithVoidCellsTypedArrowsAndIdenticalRoundTrip`. Live: 10/10 valid, 12 typed arrows, level-09 void cells (0,0) and (5,0), `DeepEquals` round trip, second write identical. |
| AC-3 | PASS | `Invalid_*` fixtures and the structure-rule tests. Live: 32 invalid fixtures give exact `Level vf ...` messages naming the entity, type and key; `TryWrite` refuses with null JSON. |
| AC-4 | FAIL | Project scope names and refuses the unimplemented Barrier and Arrow, and the `LevelReadMode.Authoring` API accepts them. Expected 5 counts editor Save as authoring, but after the first save the Level Editor's Reload and later Saves are refused by the runtime gate. The fix is outside the allowed paths: Option A (Editor session) or Option B (add `LevelDatabase.cs` to this contract). A user decision is needed. |
| AC-5 | PASS | Void-cell, door-colour and door-span tests. Environment 38/38, including reference level 9: 34 floors, 2 inner and 6 outer corners. Live: `BoardLayout.TryCreate(level-09)` gives 6x6 with 34 floor cells, and `IsFloor(0,0)` and `IsFloor(5,0)` are false. |
| AC-6 | PASS | Barrier tests. Live: `startsOpen` true, false and absent round-trip, and the typed `StartsOpen` reads True, False and False. A closed barrier on a block (explicit or by default), non-connected cells and a bad bool are refused. |
| AC-7 | UNPROVEN | `link.xml` is complete, and the regression test guards it. The Android IL2CPP smoke build was not run: it is High risk, needs the user's go, and cannot run while the Editor holds the project. |

Stop condition: seven catalog types (`layer`, `ropes`, `colorful-door`,
`size-changing-door`, `colorful-jumping-door`, `button-door`, `flower-crate`) are registered but
refused by authoring. Their value format needs a user decision (Open questions in
`docs/Level-Data-Format.md`).

### Changed files and scope

This matches the first verification's list, with one addition: fix round 2 changed only
`Tests/EditMode/LevelDatabaseJsonTests.cs` (15:24) and this task folder.

All files are inside the allowed paths:

- `Runtime/Levels/`: `LevelData.cs`, `LevelDatabaseJson.cs`, `LevelEffect*.cs` and
  `LevelCellObjectData.cs` with their `.meta` files, and `link.xml`
- `Runtime/Layout/BoardLayout.cs`
- `Tests/EditMode/`: `LevelDatabaseJsonTests.cs`, `LevelEffectTests.cs` and
  `LevelCellObjectTests.cs` with their `.meta` files
- `Tests/Environment/EditMode/`: `EnvironmentComposerTests.cs` and `BoardLayoutTests.cs`
- `docs/Level-Data-Format.md`, `README.md`, `PROJECT.md`
- this task folder, and the environment task's `contract.md` (AC-2 evidence and lifecycle only)

No out-of-scope write was found.

## Fix round 3 (2026-09-25 ~15:55-16:15, implementer, sole writer)

Scope: D-001 per the clarified Expected 5, the user's list/cell encoding decision
(Expected 6a) for the seven pending types, and the reviewer minors. Live Editor over
MCP for Unity HTTP 127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`,
Plastic cs:7 plus pending changes. Before and after: one untitled scene, `dirty=False`,
not playing. No scene was opened or saved; no player build ran. All source edits used
the file edit tools.

Starting state note: `LevelDatabaseJson.cs` was already modified at 15:54, before this
round began: the 3-argument `TryRead` passed `LevelReadMode.Authoring`. This round kept
that change and completed the rest of the D-001 fix.

### Changed in this round

- `Runtime/Levels/LevelDatabase.cs` (allowed narrowly by the revised contract):
  additive `LoadJson(string json, LevelReadMode mode, out string error)`;
  `LoadJson(json, out error)` now forwards `LevelReadMode.Authoring`; `LoadAsync`
  calls `LoadJson(text, LevelReadMode.Runtime, ...)`. Public API otherwise unchanged.
- `Runtime/Levels/LevelDatabaseJson.cs`: `LevelReadMode` comments only (3-argument
  `TryRead` = Authoring, as found).
- `Runtime/Levels/LevelEffectDefinition.cs`: `LevelEffectValueKind.Cell`; `LevelEffectKey`
  gains `IsList`, `MinItems`, `MaxItems`, factories `Cell`, `ColorList`, `IntegerList`,
  `ReferenceList`, `FindProblem` (precise list/cell messages), `DescribeItem`; strict
  canonical parsing (`,` items, no trimming, `""` = 0 items, `"x:y"` cells with canonical
  integers). `LevelEffectValues` gains `GetCell`, `GetTexts`, `GetIntegers`, `GetColors`.
  `LevelEffectDefinition.HasDecidedFormat` removed (every type now has a converter).
- `Runtime/Levels/LevelEffectRegistry.cs`: the seven definitions now have keys,
  converters and level checks: `layer` (`layerColors` 1-3 colours), `ropes`
  (`ropeColors` 1-3 colours, scissors check), `colorful-door` (`colorSequence` 1+
  colours, first = door colour), `size-changing-door` (`spanSequence` 1+ integers 1-3,
  each <= span; `anchor` Start/Center/End), `colorful-jumping-door` (`tokenColorId`,
  `route` 2+ door IDs starting with this door), `button-door` (`buttonColorId`,
  `buttonCell` one cell inside the board on floor), `flower-crate` (`leaves` 1+ block
  IDs, no repeat, no leaf inside this crate). `ReadValues` uses `FindProblem`.
  **`k_barrierRuntimeImplemented` stays on line 51** (`private const bool
  k_barrierRuntimeImplemented = false;`); all 42 flags are `false`.
- `Runtime/Levels/LevelEffectValidator.cs`: the "no decided parameter format" branch is
  removed; reference checks walk list items.
- `Runtime/Levels/LevelEffectChecks.cs`: `CheckRopeScissors`, `CheckColorSequence`,
  `CheckSpanSequence`, `CheckRoute`, `CheckButtonCell`, `CheckFlowerCrateLeaves`;
  flower-crate contents must lie inside its cells.
- `Runtime/Levels/LevelEffectParameters.cs`: `LevelDoorAnchor`; immutable `LayerEffect`,
  `RopesEffect`, `ColorfulDoorEffect`, `SizeChangingDoorEffect`,
  `ColorfulJumpingDoorEffect`, `ButtonDoorEffect`, `FlowerCrateObject` (arrays copied,
  Count/GetAt accessors).
- `Runtime/Levels/LevelData.cs`: `Validate` summary comment only.
- `Runtime/Levels/link.xml`: the seven new parameter classes.
- Tests: `LevelEffectTests.cs` (undecided cases removed; 7 valid + 19 invalid fixtures
  for the seven types and malformed lists/cells; `ListAndCellParametersExposeTypedItemsInAuthoredOrder`;
  runtime-gate test reads explicitly with `LevelReadMode.Runtime` and asserts the
  3-argument read is authoring), `LevelDatabaseJsonTests.cs` (new
  `EditorPathsSaveReloadAndSaveAgainAnArrowAndBarrierDatabase`; runtime tests read with
  `LevelReadMode.Runtime` and derive the expected message from the registry flags;
  `EveryCurrentLevelKeepsDoorsWithinOneToThreeCells` asserts no level total; link.xml
  lower bound 42), `LevelCellObjectTests.cs` (Barrier runtime refusal through
  `LevelReadMode.Runtime` and `LevelDatabase.LoadJson(json, LevelReadMode.Runtime, ...)`).
  No test hard-codes a runtime-support flag value.
- Docs: `docs/Level-Data-Format.md` (value rules incl. List and Cell, encoding
  paragraph, the seven rows, assumptions and open questions incl. catalog-declared
  errors not enforced and the Elevator stacked-contents case, read modes after D-001,
  message list: removed the unreachable "no decided parameter format" text, added the
  reference/cell/list rules and the new level-check texts, and qualified which reader
  messages only a `null` entry can reach), `README.md` (v2 bullet).

Not touched: `Editor/**`, `Gameplay/**`, `Presentation/**`, `Environment/**`,
`Data/Levels/*.json`, Packages, ProjectSettings, Addressables, `BoardLayout.cs`.

### D-001 verification

- Test `EditorPathsSaveReloadAndSaveAgainAnArrowAndBarrierDatabase` (temp folder):
  `LevelDatabaseFile.TryCreate` -> `TrySave` (Arrow block + Barrier) -> `TryRead`
  (reload; typed `ArrowEffect`/`BarrierObject`) -> `TrySave` again from the reloaded
  levels (identical JSON) -> `LevelDatabaseInspector.Describe` = `1 level` ->
  `LevelDatabase.LoadJson(json, out)` true -> `LevelDatabase.LoadJson(json,
  LevelReadMode.Runtime, out)` refuses with `Level fx block b3 effect arrow is not
  implemented in gameplay yet.` (expectation derived from the flags). Passed.
- Live capture `captures/fix3-editor_gap_result.txt` (the verifier's script plus
  Describe and both LoadJson modes; scratchpad files only):
  `firstSave=True`, `editorReloadAfterSave=True`, `secondSave=True`,
  `thirdSaveFromReloaded=True identical=True`, `inspectorDescribe=1 level`,
  `LevelDatabase.LoadJson(authoring)=True`,
  `LevelDatabase.LoadJson(Runtime, as LoadAsync)=False [Level bar cell object o1 effect barrier is not implemented in gameplay yet.]`,
  `TryRead(Runtime)=False [same message]`.

### Compile, Console and tests (MCP)

- `refresh_unity` (force, compile) twice; `EditorUtility.scriptCompilationFailed=False`;
  no `error CS`/`warning CS` in the Console (`captures`: none new; only MCP WebSocket
  notices, board-kit `Could not restore piece JSON ... PrefabTests_*` and older lines).

| Run | Job id | Result |
| --- | --- | --- |
| Focused `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests` | `fe9018bfba7c4a0fab0ef18d2c82c84c` | 192/192 passed |
| All EditMode assemblies | `6013211d988142c1ad18c41840296576` | 747/749; 2 Level Editor failures below (`captures/fix3-job_editmode_all.json`) |
| All PlayMode assemblies (incl. `SceneFlowSmokeTests`, v1 game database) | `e45319263747487bb611d914c4702cfd` | 38/38 passed (first attempt `f4434c04...` did not start: another session's tests were running) |
| Focused again after the final compile | `5ae733b4ec174dad81f63b6a02fcb248` | 192/192 passed |
| Environment EditMode assembly (reference level 9 void notches) | `2a350c9bca9243ee89b1a5fd6d84a0a5` | 61/61 passed |
| Rerun `LevelEditV2FieldsTests`, `LevelEditorShellTests`, `LevelDatabaseFileTests`, `LevelDatabaseInspectorTests`, `LevelDatabaseTests` | `ae2908fa307b4c30b21afe3e7ab230eb` | 35/35 passed |

The two full-run failures belong to the Level Editor session's in-progress work, not
this task: `LevelEditorShellTests.UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview`
(the known "Expected 300 But was null", reported by verifier 2) and
`LevelEditV2FieldsTests.ShrinkingDropsVoidCellsInRemovedLinesAndRemovesCellObjectsWithTheirLine`
(expected text changed in their test file at 16:07, during the run). Both passed in the
rerun after the final compile.

### Acceptance status after this round (implementer view; verifier to confirm)

- AC-1, AC-2, AC-3, AC-5, AC-6: still green (focused 192/192, Environment 61/61,
  PlayMode 38/38).
- AC-4: D-001 fixed; editor paths accept, project scope (`LoadAsync` -> Runtime) refuses
  naming the effect.
- AC-7: UNPROVEN by user decision (no device build run); link.xml guard test passes with
  the seven new classes.

### Open items

- Open questions recorded in `docs/Level-Data-Format.md` (anchor spelling, lists without
  a maximum, repeated route doors, Elevator stacked contents, GTE-05 "own target" key
  error not enforced).
- Peers: `LevelEffectDefinition.HasDecidedFormat` no longer exists (no Editor or
  Gameplay caller found by grep). `LevelEffectKey.IsValid` now also covers list/cell
  keys; `FindProblem`/`DescribeItem` are new.

## Fix round 4 (2026-09-25 ~16:20-16:45, implementer, sole writer)

Scope: five reviewer findings on fix round 3. Live Editor over MCP for Unity HTTP
127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`, Plastic cs:7 plus
pending changes. Before and after: one untitled scene, `dirty=False`, not playing. No
scene opened or saved; no player build. Source edits used the file edit tools.

### Verdicts

- Major, `LevelDatabase.cs` Play Mode cache (confirmed:
  `ProjectSettings/EditorSettings.asset` `m_EnterPlayModeOptions: 3`; the window has no
  `playModeStateChanged` handler). Fixed in `LoadAsync` only: it no longer clears the
  cache before the Addressables read, and when the runtime read refuses the file it
  restores the previous cache before throwing. `LoadJson` in both modes still clears on
  failure, so `LevelDatabaseTests.FailedReloadClearsOldLevelCache` (outside this task's
  paths; "LevelDatabase public API unchanged") stays true. The reviewer's first option
  (never clear in `LoadJson`) would have broken that test, so it was not taken. The game
  stays protected: `GameAttemptLoader` and `EnvironmentPreview` both require
  `ProjectContext.ReadyTask` to succeed. The reverse direction (a Level Editor Save during
  Play Mode reloads the cache with an authoring read) is recorded as open and
  Editor-owned in `docs/Level-Data-Format.md`, read modes.
- Minor, BLK-05 arrow axes (confirmed). Recorded, not enforced (option a): the rule, and
  whether it also covers `combined-locked` and `turn-based-arrow`, is listed under
  catalog-declared validation errors not enforced.
- Minor, completeness of that list (confirmed). Audited every catalog "error"/"warning"
  line: the not-enforced list is BLK-05 axes and GTE-05 own target; BLK-09 neutral moved
  out of the open list as a confirmed decision; the warnings sentence now names all nine
  catalog warnings (BLK-02, BLK-06, BLK-07, BLK-18, GTE-02, GTE-03, GTE-04, GTE-11,
  OBJ-06); solver checks name BLK-01 and BLK-04. Every other catalog-declared validation
  error has a message in the list (checked against `LevelEffectChecks.cs` and the
  registry ranges).
- Minor, unreachable Barrier/Colorful Path open question (confirmed). Replaced with the
  statement that contents lie inside their container and cell objects never share a
  cell, so no contained block can overlap those cells at the start.
- Minor, `CheckInside` comment (confirmed). Now names OBJ-02, OBJ-04 and OBJ-09.

### Changed in this round

- `Assets/ColorBlockJam/Runtime/Levels/LevelDatabase.cs`: `LoadAsync` summary comment,
  no leading `m_levels = null`, previous cache restored on a runtime refusal.
- `Assets/ColorBlockJam/Runtime/Levels/LevelEffectChecks.cs`: comment only.
- `Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseJsonTests.cs`: new
  `FailedProjectScopeLoadKeepsTheAuthoringCache` (authoring load of an Arrow+Barrier
  level, failing `LoadAsync`, `Count` still 1 and the Barrier still readable). It covers
  the missing-source failure; the runtime-refusal restore branch runs after an
  Addressables read and needs a Level Database group entry, which this task may not
  create, so that branch is covered by review only.
- `docs/Level-Data-Format.md`: open questions and not-enforced lists as above; Play Mode
  interplay paragraph under read modes.
- `README.md`: the v2 bullet says the project-scope refusal leaves the editor's levels.

`k_barrierRuntimeImplemented` is still `LevelEffectRegistry.cs` line 51
(`private const bool k_barrierRuntimeImplemented = false;`).

### Compile, Console and tests (MCP)

- `refresh_unity` (force, compile): `scriptCompilationFailed=False`, the new test is in
  the loaded test assembly. Console after the runs: no `error CS`/`warning CS`; only
  expected log lines other tests emit (LevelEditor Play Mode guard, board-kit
  `Could not restore piece JSON ... PrefabTests_*`, level preview 13-cell note). One
  `Import Error Code:(4)` line appeared once right after the refresh and was gone on the
  next read; it is not in `Editor.log`.

| Run | Job id | Result |
| --- | --- | --- |
| Focused `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests`, `LevelDatabaseTests`, `LevelDatabaseFileTests`, `LevelDatabaseInspectorTests` | `ccbfd64409ee4fa48d732b0b7e3b919f` | 208/208 passed |
| All EditMode assemblies | `052a1e6079a54d6fa6354e7eb8ab89be` | 752/752 passed (`captures/fix4-job_editmode_all.txt`) |
| All PlayMode assemblies (incl. `SceneFlowSmokeTests`) | `e7cc96abeb104b8ea523dbb3d551e941` | 38/38 passed (`captures/fix4-job_playmode_all.txt`) |

AC-1/2/3/5/6 stay green; AC-4 as in fix round 3 plus the Play Mode cache fix; AC-7
UNPROVEN by user decision.

## Independent verification 3 (2026-09-25 ~16:34-16:50, verifier, read-only on code)

Live Editor over MCP for Unity HTTP 127.0.0.1:8082, instance
`ColorBlockJamCase2026@99077e4df1ecfaa4`, Plastic cs:7 plus pending changes. Before and
after: compile not failed, not compiling, not playing, one untitled scene `dirty=False`.
The last domain reload (16:32:31) came after the last task edit (README 16:29), so no
refresh was needed. I opened and saved no scene, edited no production or test file and ran
no player build. Captures are in `captures/verify3-*`; the live scripts wrote only
scratchpad files.

### Tests (MCP `run_tests`)

| Run | Job id | Result |
| --- | --- | --- |
| Focused `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests`, `LevelDatabaseTests`, `LevelDatabaseFileTests`, `LevelDatabaseInspectorTests` | `c5c82a27fec34216ba0971528f2d0a83` | 208/208 passed (`captures/verify3-job-focused.txt`) |
| All EditMode assemblies (incl. Environment: `ReferenceLevelNineTurnsItsVoidNotchesIntoInnerCorners`, `LevelDataVoidCellsBecomeHolesInTheLayout`) | `1db73655632c46868f167d81a06a9334` | 752/752 passed (`captures/verify3-job-editmode-all.txt`) |
| All PlayMode assemblies (incl. `SceneFlowSmokeTests.ProjectScopeLoadsActiveAddressableLevelJson`, v1 game database through `LoadAsync`) | `3c6b36b575d043f8b5c99cbc3033d7a6` | 38/38 passed (`captures/verify3-job-playmode-all.txt`) |

- `Valid_*` and `Invalid_*` cover all 42 registered types: 42 unique `Valid_` names and all
  42 type ids among the `Invalid_` names (plus 21 extra structure/list/cell cases).
- Console after the runs (`captures/verify3-console.txt`): no `error CS`/`warning CS`; only
  the known LevelEditor Play Mode guard, board-kit `Could not restore piece JSON ...
  PrefabTests_*` and 13-cell preview lines, and one MCP WebSocket notice. No `PrefabTests_*`
  folder is left under `Data/Board`.

### Live captures (`execute_code`)

- `captures/verify3-ac1-ac2-ac3-ac5-live.txt`: the first verifier's script, with the
  runtime read now explicit (`LevelReadMode.Runtime`) and the 3-argument read added:
  - AC-1: game file `formatVersion` 1, 5 levels, 0 void cells/effects/cell objects; written
    v2 with explicit `voidCells`, `cellObjects`, block and door `effects` `[]`; content
    otherwise equal; `LevelDatabase.LoadJson` loads v1 and the v2 rewrite (5 each); file
    SHA-256 `02C013A5...` unchanged.
  - AC-2: authoring read 10/10 valid, 12 typed arrows (7 H, 5 V), level-09 void cells (0, 0)
    and (5, 0), write `DeepEquals` the file, second write identical, 3-argument read True,
    Runtime read refuses `Level level-07 block b1 effect arrow is not implemented in gameplay yet.`
  - AC-5: `BoardLayout.TryCreate(level-09)` 6x6, 34 floor cells, `IsFloor(0,0)` and
    `IsFloor(5,0)` false.
  - AC-3: `TryWrite` of a closed barrier on a block returns false with null JSON.
- `captures/verify3-variants-read.txt` (75 fixtures of my own,
  `verify3-variants-fixtures.json`, generator `verify3-make-variants.py.txt`), each read in
  Runtime, Authoring and the 3-argument mode:
  - The earlier 38 fixtures give the same results as verifications 1 and 2. The 3-argument
    read equals Authoring for all 75.
  - The seven list/cell types (Expected 6a), written independently of the implementer's
    fixtures: valid `layer` (`"Blue,Yellow"`, `"Blue"`), `ropes` (`"Blue,Blue"` with two Blue
    scissors), `colorful-door` (`"Red,Blue,Red"`), `size-changing-door` (`"1,2"`, `Center`),
    `colorful-jumping-door` (`"d1,d2"`), `button-door` (`"2:3"`), `flower-crate` (`"b1,b2"`)
    pass Authoring, round-trip identically and expose typed items
    (`LayerEffect[Blue,Yellow]`, `SizeChangingDoorEffect[1,2] Center`, `ButtonCell=(2, 3)`,
    ...). Runtime refuses each, naming it. The size-changing input with `anchor` first is
    rewritten in definition key order (canonical writing, Expected 6).
  - Rejected with `Level vf ...` messages naming entity, type and key: `"Blue, Yellow"`,
    `"Blue,,Yellow"`, `"Blue,"`, 4 layer colours, `Wall` item, rope/scissors shortfall,
    colour sequence not starting with the door colour, width 3 on a 2-cell door, `0` and
    `01` widths, `center`, missing anchor, route starting elsewhere, 1-item route, missing
    route door, button cell outside/negative/on void, `"2: 3"`, `"02:3"`, `"2:3;3:3"`, leaf
    twice, missing leaf, leaf inside the crate, crate content outside its cells.
- `captures/verify3-editor-save-reload.txt` (D-001; scratchpad files only): Barrier level
  first save, Reload, second save and third save from the reloaded levels all succeed
  (identical output); `LevelDatabaseInspector.Describe` = `1 level`;
  `LevelDatabase.LoadJson(json, out error)` True; `LoadJson(json, LevelReadMode.Runtime, ...)`
  and `TryRead(Runtime)` refuse with `Level bar cell object o1 effect barrier is not
  implemented in gameplay yet.` A second level with `layer`, `button-door` and
  `flower-crate` also saves, reloads (2 levels) and saves again identically through
  `LevelDatabaseFile`; the file holds `"Blue,Yellow"`, `"3:3"` and `"b1,b2"`.
- `captures/verify3-empty-string-read.txt` (6 probes, `verify3-empty-string-fixtures.json`):
  **new finding, below.**

### New finding (defect candidate for `/task-cycle`; not fixed here)

An empty-string parameter key or value in a level JSON is rejected by the reader with a
Newtonsoft message instead of the documented validation message:

- `{"key": "axis", "value": ""}` and `{"key": "layerColors", "value": ""}` give
  `Invalid level JSON: Required property 'value' expects a value but got null. Path
  'levels[0].blocks[0].effects[0].params[0]', line 1, position 450.` in every read mode.
  `{"key": "", ...}` gives the same for `key`.
- `" "` (a blank, non-empty string) reaches the precise messages
  (`... key axis has value ' '; expected one of Horizontal, Vertical.`,
  `... effect arrow has an empty key.`).
- Cause: `ParamDto.key`/`value` are `JToken` fields with `Required.Always`
  (`LevelDatabaseJson.cs:605-606`); Newtonsoft turns the empty string into `null` for that
  member, so the reader never reaches `IsText` or `LevelData.Validate`.
- Impact: the data is still refused and nothing is saved, and the Level Editor writer
  never emits `""` (the API path gives `... has 0 items in ''` / `... has value ''`).
  But for hand-edited or remote JSON the message says `null`, names a JSON path instead of
  level, entity, type and key (AC-3), and contradicts `docs/Level-Data-Format.md`
  ("`""` has 0 items"; "has an empty key ... a blank key"). The implementer's
  `Invalid_list-empty-value` fixture builds the level through the API, so no test reads
  `""` from JSON.
- Dedupe key suggestion: `v2/reader/empty-string-param-null-message`, AC_FAILURE on AC-3.

### Verdict per acceptance scenario

| AC | Result | Basis |
| --- | --- | --- |
| AC-1 | PASS | Focused and full EditMode; live AC-1 capture; PlayMode `ProjectScopeLoadsActiveAddressableLevelJson` loads the v1 game database through `LoadAsync` (Runtime read). |
| AC-2 | PASS | `ReferenceLevelsLoadForAuthoringWithVoidCellsTypedArrowsAndIdenticalRoundTrip`; live AC-2 capture. |
| AC-3 | FAIL | Every enumerated Expected 4 case, including the seven list/cell types, is refused with a precise `Level {id} ...` message on read and save, and `TryWrite` saves nothing. One read case is not: an empty-string parameter value or key (new finding above). |
| AC-4 | PASS | D-001 fixed: `EditorPathsSaveReloadAndSaveAgainAnArrowAndBarrierDatabase`, `FailedProjectScopeLoadKeepsTheAuthoringCache`, flag-derived runtime tests; live editor Save/Reload/Save and inspector succeed, the runtime read refuses naming the effect. `LoadAsync` passes `LevelReadMode.Runtime` (`LevelDatabase.cs:64`, static check); its refusal-and-restore branch is not executed end to end, because that needs a Level Database Addressables entry. |
| AC-5 | PASS | Void/door rule tests, Environment tests (reference level 9: 34 floors, 2 inner, 6 outer corners), live `BoardLayout` capture. |
| AC-6 | PASS | Barrier tests; live fixtures (open, closed, default round-trip and typed; closed-on-block, not four-connected and bad bool refused). |
| AC-7 | UNPROVEN | By user decision; `link.xml` guard test passes with the seven new classes; no player build run. |

Reviewer minors: no test hard-codes a runtime flag (every expectation derives from
`LevelEffectDefinition.IsRuntimeImplemented`; the one literal message at
`LevelDatabaseJsonTests.cs:288-291` is guarded by `!IsImplemented("arrow")`);
`EveryCurrentLevelKeepsDoorsWithinOneToThreeCells` asserts spans only. The doc's message
list matches every message string in `LevelData.cs`, `LevelEffectValidator.cs`,
`LevelEffectRegistry.ReadValues`, `LevelEffectKey.FindProblem`, `LevelEffectChecks.cs` and
`LevelDatabaseJson.cs`, apart from the empty-string cases above. Open questions (anchor
spelling, open-ended lists, repeated route doors, Elevator stacked contents) and the
not-enforced catalog errors (BLK-05 axes, GTE-05 own target) are recorded.
`k_barrierRuntimeImplemented` is `LevelEffectRegistry.cs` line 51, `false`; all 42 flags are
`false`.

### Changed files and scope

Attributed by timestamps and the implementer reports (Plastic holds several sessions'
pending changes). Everything is inside the allowed paths:

- `Runtime/Levels/`: `LevelData.cs`, `LevelDatabaseJson.cs`, `LevelDatabase.cs`,
  `LevelEffectData.cs`, `LevelEffectDefinition.cs`, `LevelEffectRegistry.cs`,
  `LevelEffectParameters.cs`, `LevelEffectValidator.cs`, `LevelEffectChecks.cs`,
  `LevelCellObjectData.cs`, their new `.meta` files, and `link.xml`
- `Runtime/Layout/BoardLayout.cs`
- `Tests/EditMode/`: `LevelDatabaseJsonTests.cs`, `LevelEffectTests.cs(.meta)`,
  `LevelCellObjectTests.cs(.meta)`; `Tests/Environment/EditMode/EnvironmentComposerTests.cs`,
  `BoardLayoutTests.cs`
- `docs/Level-Data-Format.md`, `README.md`, `PROJECT.md`
- this task folder; the environment task's `contract.md` (earlier rounds)

Notes:

- `LevelDatabase.cs` is allowed "read mode only". Fix round 4 also changed `LoadAsync`'s
  cache handling (no clear before the Addressables read; the previous cache is restored on
  a runtime refusal) to answer a reviewer finding. Public API is unchanged apart from the
  additive `LoadJson(json, LevelReadMode, out error)`. Recorded for the orchestrator.
- Not this task: `Editor/Levels/Canvas/*` (15:58), `Tests/EditMode/LevelRecordCopyTests.cs`
  (16:00), `LevelEditV2FieldsTests.cs` (16:15) are the Level Editor session's.
- Test side effects, not task edits: the full EditMode runs (fix round 4 at 16:31 and
  mine at 16:37) rewrite `Assets/AddressableAssetsData/AddressableAssetSettings.asset` and
  `AssetGroups/Level Database.asset` (same sizes; the group still holds only
  `LevelDatabase/LevelDatabase.json`) and `Data/Board/BlockShapeLibrary.asset`. The
  PlayMode run rewrote `ProjectSettings/ProjectSettings.asset` and
  `EntitiesClientSettings.asset` with content identical to cs:7.
- `Data/Levels/LevelDatabase.json` SHA-256 `02c013a5...` and `LevelDatabase.asset`
  (2026-09-24 21:55) are unchanged.

## Fix round 5 (2026-09-25 ~16:48-16:58, implementer, sole writer)

Scope: verifier 3's AC-3 finding (suggested key
`v2/reader/empty-string-param-null-message`; the ledger row is `/task-cycle`'s to write)
and the out-of-scope note on `LevelDatabase.cs`. Live Editor over MCP for Unity HTTP
127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`, Plastic cs:7 plus
pending changes. Before and after: one untitled scene, `dirty=False`, not playing. No
scene opened or saved; no player build. Source edits used the file edit tools.

### Root cause and fix

- Cause (confirmed): `ParamDto.key`/`value` were `JToken` members with
  `Required.Always`. Newtonsoft's reader coerces an empty string to `null` for any
  nullable member type other than `string` and `object`, so `""` became `null` and the
  `Required.Always` check threw before `TryReadEffect` or `LevelData.Validate` ran.
- Fix (`Runtime/Levels/LevelDatabaseJson.cs`): `ParamDto.key`/`value` are now `object`
  members with `Required.Default` (lines 604-605). `object` is the member type for which
  Newtonsoft keeps `""`; `DateParseHandling.None` (already set) keeps date-like text a
  string. `TryReadEffect` (line 340) accepts only `string` key and value, so a number,
  bool, object, array, `null` or missing `key`/`value` gets the existing
  `Level {L} {entity} effect {T} parameter {K} needs a JSON string key and value.`
  instead of a Newtonsoft path message. `""` reaches `Validate`. The unused `IsText`
  helper was removed.
- Result: a JSON `""` value now reads exactly as the API path does
  (`... key axis has value ''; expected one of Horizontal, Vertical.`,
  `... key layerColors has 0 items in ''; expected 1 to 3 items.`), and a `""` key gives
  `... has an empty key.`, matching `docs/Level-Data-Format.md`.

### LevelDatabase.cs out-of-scope note: kept, justified

The `LoadAsync` cache handling from fix round 4 is kept. It is part of the read-mode
change the contract allows ("read mode only: the LoadAsync game load applies the runtime
gate"): with Enter Play Mode Options skipping the domain reload, the new runtime refusal
would otherwise clear the authoring cache the Level Editor shares on the same asset, which
is the D-001 symptom returning through Play Mode. The public API is unchanged apart from
the additive `LoadJson(json, LevelReadMode, out error)`; load timing is unchanged (still one
Addressables read in project scope); `LoadJson` in both modes still clears on failure
(`LevelDatabaseTests.FailedReloadClearsOldLevelCache` passes). The orchestrator can narrow
it by reverting the two lines (`LevelData[] previous = m_levels;` and the restore) if it
prefers the literal reading; `FailedProjectScopeLoadKeepsTheAuthoringCache` would then
have to go too.

### Changed in this round

- `Assets/ColorBlockJam/Runtime/Levels/LevelDatabaseJson.cs`: `ParamDto` members and
  comment, `TryReadEffect` string check and comments, `IsText` removed.
- `Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseJsonTests.cs`: new
  `ReadGivesLevelMessagesForEmptyNullAndMissingParameterStrings` reads six door-effect
  records from JSON (`""` bool value, `""` list value, `""` key, `null` value, missing
  value, bool token) and asserts each exact message, then reads a valid
  `toggle-door startsOpen "true"`; helper `ReadDoorEffect`.
- `docs/Level-Data-Format.md`: `has an empty key` covers `""` from JSON; the value
  message notes `""` from JSON prints as `''`; the reader's `parameter {K} needs a JSON
  string key and value` lists what triggers it and that `""` reaches `Validate`; the
  `Invalid level JSON` bullet names parameter key/value as the exception.

`k_barrierRuntimeImplemented` is still `LevelEffectRegistry.cs` line 51
(`private const bool k_barrierRuntimeImplemented = false;`).

### Compile, Console and tests (MCP)

- `refresh_unity` (force, compile) after the edits: `scriptCompilationFailed=False`, no
  `error CS`/`warning CS` from this task's files.
- Live probes: `captures/fix5-empty-string-read.txt` (verifier 3's six empty-string
  fixtures, script `fix5-empty-string-probe.cs.txt`): all six now give `Level vf ...`
  validation messages in Runtime, Authoring and 3-argument reads. Verifier 3's 75-fixture
  probe rerun (`captures/fix5-variants-read.txt`) differs from
  `verify3-variants-read.txt` only in the three `*_empty` list fixtures
  (`s7_inv_layer_empty`, `s7_inv_colorful_door_empty`, `s7_inv_flower_empty`), which also
  used to give the Newtonsoft message and now give `... has 0 items in ''; expected ...`.
  Every other line is identical.

| Run | Job id | Result |
| --- | --- | --- |
| Focused `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests`, `LevelDatabaseTests`, `LevelDatabaseFileTests`, `LevelDatabaseInspectorTests` | `34b0172ccabd460d996c4fca576ee5bd` | 209/209 passed (208 + the new test; `captures/fix5-job-focused.json`) |
| All EditMode assemblies | `713675106c6d4ee686f56a28658653cb` | 749/753; the 4 failures are Level Editor UI and piece-library tests outside this task (`LevelCanvasBlockToolsTests.PlacingAShapeAddsOneBlockAndPublishesItOnce`, `LevelEditorShellTests.UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview`, `ShapePrefabTests.JsonImportAndUndoRestore...`, `ShapePrefabTests.LibraryUndoRefreshes...` with `IOException: Unable to remove the file to be replaced`) |
| Rerun of those three classes | `8013a193b5a241a6bf669876e168516a` | 45/46; only `PlacingAShapeAddsOneBlockAndPublishesItOnce` failed (`captures/fix5-job-editor-rerun.json`) |
| That test alone | `00f27cc8b8e04f5d8770a477f02c9ef4` | 1/1 passed: timing-dependent window test (1.5 s publish wait), not related to the reader; the game database has no params |
| All PlayMode assemblies | `d6924ad6587b4b2daa68989fea02a9bd` | 38/38 passed (`captures/fix5-job-playmode-all.json`) |

Concurrent writer: at ~16:57, after these runs, the Level Editor session edited
`Editor/Levels/Canvas/LevelBoardResize.cs` and `Editor/Levels/LevelDatabaseWindow.cs`;
the Console then shows `LevelEditorShellTests.cs(436,42): error CS0117: 'LevelBoardResize'
does not contain a definition for 'ClampWidth'` (`captures/fix5-console.txt`). That
compile break is in that session's in-progress files, not in this task's paths; the
probes above ran on the last good domain, which contains this round's reader change.

`Data/Levels/LevelDatabase.json` SHA-256 `02c013a5...` unchanged.

AC-1/2/4/5/6 stay green; AC-3's remaining counterexample is fixed (demonstrated by the
new test and both probes); AC-7 UNPROVEN by user decision (no device build).

## Independent verification 4 (2026-09-25 ~17:00-17:13, verifier, read-only on code)

Live Editor over MCP for Unity HTTP 127.0.0.1:8082, instance
`ColorBlockJamCase2026@99077e4df1ecfaa4`, Plastic cs:7 plus pending changes. I edited no
production or test file, opened and saved no scene and ran no player build. Captures are
in `captures/verify4-*`; the live scripts wrote only scratchpad files (deleted afterwards).

Editor state:

- On arrival: one untitled scene, `dirty=False`, not playing, but
  `scriptCompilationFailed=True` from the Level Editor session's in-progress
  `LevelEditorShellTests.cs(436,42): CS0117 ... ClampWidth` (fix round 5 note). The file on
  disk no longer referenced `ClampWidth`, so I ran one `refresh_unity` (force, compile):
  compile clean, Console without `error CS`/`warning CS`. The task's last source edit
  (`LevelDatabaseJson.cs` 16:49:18, tests 16:49:52, doc 16:50:13) predates that reload.
- On leaving: compile clean, not playing, one scene
  `Assets/ColorBlockJam/Scenes/LevelEditor.unity` `dirty=False`. I did not open it: the
  Editor.log shows it opened after my last test run, while the Level Editor session was
  working. I left it as it is rather than close another session's scene.
- Console (`captures/verify4-console.txt`): `Level Editor: unsaved level changes were
  discarded by an editor reload.` was logged at a domain reload shortly before my first
  PlayMode attempt, while another session was running and editing Level Editor code; the
  reload's trigger cannot be attributed from the log. `Files generated by test without
  cleanup ... LevelCanvasCornerVoidsTests.cs` is that session creating a new test file
  during a run. The rest are MCP notices and the LevelEditor Play Mode guard.

### Tests (MCP `run_tests`)

| Run | Job id | Result |
| --- | --- | --- |
| Focused `LevelDatabaseJsonTests`, `LevelEffectTests`, `LevelCellObjectTests`, `LevelDatabaseTests`, `LevelDatabaseFileTests`, `LevelDatabaseInspectorTests` | `7034e51d0f704004932df19e76a64048` | 209/209 passed (`captures/verify4-job-focused.txt`) |
| All EditMode assemblies | `81011948a3d743a49b29c6a0b7ceba3e` | 751/753 (`captures/verify4-job-editmode-all.json`). Failures: `LevelCanvasBlockToolsTests.PlacingAShapeAddsOneBlockAndPublishesItOnce` ("Expected: 1 But was: 0") and `LevelEditorShellTests.UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview` ("Expected: 300 But was: null") |
| Rerun of those two | `e43955fbbf604af6ab863b5be02d55ad` | Undo/Redo test passed; `PlacingAShape...` failed again (`captures/verify4-job-editor-rerun.json`) |
| Environment EditMode assembly | `75c2295173dc47fe956b65827f3f79bd` | 61/61 passed, including `LevelDataVoidCellsBecomeHolesInTheLayout` and `ReferenceLevelNineTurnsItsVoidNotchesIntoInnerCorners` (`captures/verify4-job-environment.txt`) |
| All PlayMode assemblies | `fe51440bb7b94a56a5bab159c00f9e74` | succeeded, 38/38 completed, no failure, last test `SceneFlowSmokeTests.ProjectScopeLoadsActiveAddressableLevelJson` (`captures/verify4-job-playmode-all.json`; the job's per-test list is empty after the Play Mode domain reload). An earlier attempt `d5186908...` was auto-failed by MCP's 15 s start timeout while the run entered Play Mode; it produced no result. |

- The two EditMode failures are outside this task. Both are Level Editor window tests that
  wait on `EditorApplication.delayCall` publishing (`LevelDatabaseWindow.PublishDraft`
  does not call `Validate` or the reader), with the Editor unfocused and the Level Editor
  session editing those files during the runs. Fix rounds 3 and 5 reported the same pair.
  Neither runs through `LevelDatabaseJson` or `LevelData.Validate`.
- `Valid_*`/`Invalid_*` cover all 42 registry types: 42 unique `Valid_` type ids and every
  type among the 63 `Invalid_` names (checked against `new LevelEffectDefinition("...")`
  in `LevelEffectRegistry.cs`).

### Live captures (`execute_code`)

- `captures/verify4-ac1-ac2-ac3-ac5-live.txt` (verifier 3's script, unchanged): identical to
  `verify3-ac1-ac2-ac3-ac5-live.txt`. AC-1 v1 file (5 levels, 0 void cells/effects/cell
  objects) writes v2 with explicit `[]`, content otherwise equal, `LoadJson` loads both,
  file SHA-256 `02C013A5...` unchanged. AC-2 reference file: 10/10 valid, 12 typed arrows
  (7 H, 5 V), level-09 void cells (0, 0) and (5, 0), write `DeepEquals` the file, second
  write identical, 3-argument read True, Runtime read refuses `Level level-07 block b1
  effect arrow is not implemented in gameplay yet.` AC-5 `BoardLayout.TryCreate(level-09)`
  6x6, 34 floor cells, `IsFloor(0,0)`/`(5,0)` false. AC-3 `TryWrite` of a closed barrier
  on a block returns false, null JSON, `... cell object o1 effect barrier starts closed on
  block b1 at (0, 0).`
- `captures/verify4-editor-save-reload.txt` (D-001, script
  `verify4-editor-save-reload.cs.txt`): identical to `verify3-editor-save-reload.txt`.
  Barrier level: `LevelDatabaseFile` first save, Reload, second save and third save from
  the reloaded levels succeed with identical output; `LevelDatabaseInspector.Describe` =
  `1 level`; `LevelDatabase.LoadJson(json, out error)` True; `LoadJson(json,
  LevelReadMode.Runtime, ...)` and `TryRead(Runtime)` refuse with `Level bar cell object o1
  effect barrier is not implemented in gameplay yet.` A second level with `layer`,
  `button-door` and `flower-crate` saves, reloads (2 levels) and saves again identically.
- `captures/verify4-variants-read.txt` (verifier 3's 75 fixtures,
  `verify3-variants-fixtures.json`, three read modes): identical to
  `fix5-variants-read.txt`, so the seven list/cell types (Expected 6a) still accept valid
  instances with typed values and refuse the malformed ones with `Level vf ...` messages.
- `captures/verify4-empty-string-read.txt` (verifier 3's six empty-string fixtures): all
  six give `Level vf ...` validation messages in Runtime, Authoring and the 3-argument
  read (`has value ''`, `has an empty key.`, `has 0 items in ''`, `has an empty effect.`).
  The verifier 3 finding is fixed.
- `captures/verify4-param-token-read.txt` (16 new fixtures of mine,
  `verify4-param-token-fixtures.json`, script `verify4-param-token-probe.cs.txt`) on the
  fix round 5 reader change: a number, float, object, array or `false` value, a number
  key and a `null` or missing key each give `Level vf block b1 effect arrow parameter {K}
  needs a JSON string key and value.` (`{K}` empty for a null or missing key, as the doc
  says); a `null` parameter gives `has an empty parameter.`; a date-like string stays a
  string (`has value '2026-09-25T10:00:00Z'`); `""` on `ice.clearsToThaw` gives the value
  message; `" Vertical"` and `Axis` are refused; the valid arrow passes Authoring and is
  refused by Runtime. An unknown or duplicated member inside a parameter object gives
  `Invalid level JSON: ...` with a JSON path, which the doc lists under that message
  ("a duplicate or unknown property"). The 3-argument read equals Authoring in all 16.

### Static checks

- `LevelDatabaseJson.TryRead(json, out, out)` passes `LevelReadMode.Authoring`
  (`LevelDatabaseJson.cs:47-50`); `LevelDatabase.LoadJson(json, out error)` passes
  Authoring (`LevelDatabase.cs:77-80`); only `LoadAsync` passes `LevelReadMode.Runtime`
  (`LevelDatabase.cs:64`). The refusal-and-restore branch of `LoadAsync` is still checked
  by reading only: it needs a Level Database Addressables entry holding a refused file,
  and this task may not add one.
- Every reader message string in `LevelDatabaseJson.cs` (lines 57-484) appears in the
  doc's Reader and Writer lists; the removed "no decided parameter format" text and
  `HasDecidedFormat` appear nowhere in `Assets`, `docs` or `README.md`.
- No test hard-codes a runtime flag: expectations come from
  `LevelEffectTests.IsImplemented` (registry `IsRuntimeImplemented`); the one literal
  message (`LevelDatabaseJsonTests.cs:316-319`) is guarded by `!IsImplemented("arrow")`.
  `EveryCurrentLevelKeepsDoorsWithinOneToThreeCells` asserts door spans only.
- `docs/Level-Data-Format.md` records the open questions (anchor spelling, open-ended
  lists, repeated route doors, Colorful Path colour, Elevator stacked contents) and the
  catalog errors not enforced (BLK-05 arrow axes, GTE-05 own target); README's v2 bullet
  matches the read-mode behaviour.
- No `System.Linq` or `string.Format` in `Runtime/Levels`.
- `k_barrierRuntimeImplemented`: `LevelEffectRegistry.cs` line 51,
  `private const bool k_barrierRuntimeImplemented = false;`; all 42 runtime flags are
  `false`.

### Verdict per acceptance scenario

| AC | Result | Basis |
| --- | --- | --- |
| AC-1 | PASS | `VersionOneDatabaseLoadsUnchangedAndSavesAsVersionTwo`, `ProjectScopeDatabaseLoadsVersionOneAndVersionTwo...`; live AC-1 capture; PlayMode `ProjectScopeLoadsActiveAddressableLevelJson` loads the v1 game database through `LoadAsync`. |
| AC-2 | PASS | `ReferenceLevelsLoadForAuthoringWithVoidCellsTypedArrowsAndIdenticalRoundTrip`; live AC-2 capture. |
| AC-3 | PASS | 42 `Valid_`/63 `Invalid_` fixtures, void/door/cell-object rule tests and `ReadGivesLevelMessagesForEmptyNullAndMissingParameterStrings` pass; the verifier 3 empty-string counterexample now gives `Level {id} ...` messages; 16 new token probes give level/entity/type/key messages; `TryWrite` saves nothing for invalid data. |
| AC-4 | PASS | D-001 fixed: `EditorPathsSaveReloadAndSaveAgainAnArrowAndBarrierDatabase`, `FailedProjectScopeLoadKeepsTheAuthoringCache`, `RuntimeReadRefusesReferenceLevelsWithAnUnimplementedEffect`, `ProjectScopeRefusesTheBarrierUntilGameplayImplementsIt`; live editor Save/Reload/Save and inspector pass while the Runtime read refuses naming the effect. `LoadAsync`'s own refusal branch is verified by reading only (above). |
| AC-5 | PASS | Void-cell and door-length tests, Environment 61/61, live `BoardLayout` capture. |
| AC-6 | PASS | `BarrierRoundTripsAndExposesTypedStartsOpen(True/False)`, `BarrierStartsOpenDefaultsToFalse`, `ClosedBarrierOnABlockIsRejectedAndAnOpenOneIsAccepted`, `BarrierCellsMustBeFourConnected`; live AC-3 barrier capture. |
| AC-7 | UNPROVEN | By user decision; `LinkXmlPreservesEveryJsonDtoAndTypedEffectParameterClass` passes; no player build run. |

### Changed files and scope

Attributed by timestamps and the implementer reports (Plastic holds several sessions'
pending changes). Everything this task changed is inside the allowed paths:

- `Assets/ColorBlockJam/Runtime/Levels/`: `LevelData.cs`, `LevelDatabaseJson.cs`,
  `LevelDatabase.cs`, `LevelEffectData.cs`, `LevelEffectDefinition.cs`,
  `LevelEffectRegistry.cs`, `LevelEffectParameters.cs`, `LevelEffectValidator.cs`,
  `LevelEffectChecks.cs`, `LevelCellObjectData.cs`, their `.meta` files, `link.xml`
- `Assets/ColorBlockJam/Runtime/Layout/BoardLayout.cs`
- `Assets/ColorBlockJam/Tests/EditMode/`: `LevelDatabaseJsonTests.cs`,
  `LevelEffectTests.cs(.meta)`, `LevelCellObjectTests.cs(.meta)`;
  `Tests/Environment/EditMode/EnvironmentComposerTests.cs`, `BoardLayoutTests.cs`
- `docs/Level-Data-Format.md`, `README.md`, `PROJECT.md`
- this task folder; the environment task's `contract.md` (earlier rounds)

Notes:

- `LevelDatabase.cs` is allowed "read mode only". Its `LoadAsync` also keeps the previous
  cache when the runtime read refuses (fix round 4, kept and justified in fix round 5).
  Public API changes only by the additive `LoadJson(json, LevelReadMode, out error)`. This
  stays an orchestrator decision, not a failure.
- Not this task: `Editor/Levels/**` and `Tests/EditMode/LevelCanvas*`, `LevelEditor*`,
  `LevelEditV2FieldsTests.cs`, `LevelRecordCopyTests.cs` (Level Editor session, edited
  during this verification).
- Test side effects, not task edits: `Assets/AddressableAssetsData/AddressableAssetSettings.asset`
  and `AssetGroups/Level Database.asset` were rewritten at 17:06:04 during an EditMode run
  (the group still holds only `LevelDatabase/LevelDatabase.json`);
  `ProjectSettings/ProjectSettings.asset` (17:08:44) and `EntitiesClientSettings.asset`
  (17:11:37) are byte-identical to the cs:7 copies.
- `Data/Levels/LevelDatabase.json` SHA-256 `02c013a5...` and `LevelDatabase.asset`
  (2026-09-24 21:55) are unchanged.

Minor observation, no action required: for a missing or `null` parameter key the reader
message has two spaces (`parameter  needs a JSON string key and value.`), because `{K}` is
empty. The doc states `{K}` is empty in that case.
