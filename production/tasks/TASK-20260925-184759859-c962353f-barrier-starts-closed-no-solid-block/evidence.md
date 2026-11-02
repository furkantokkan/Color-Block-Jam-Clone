# Evidence: TASK-20260925-184759859-c962353f-barrier-starts-closed-no-solid-block

Implementer run 2026-09-25 (single writer). Workspace cs:9, no checkin or shelve.
Live Editor: MCP for Unity HTTP 127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`.

## Dependency check

- `grep -rn "SolidBlockObject|solid-block" Assets` before the edit: hits only in this task's
  allowed paths (Registry, Parameters, Checks, link.xml, EnvironmentSolidComposer,
  LevelEffectTests, EnvironmentSolidTests, EnvironmentSolidViewTests). None in
  `Editor/Levels/**` or `Gameplay/**` (Level Editor and gameplay owners already clean).

## Changes

- `Runtime/Levels/LevelEffectRegistry.cs`: removed the `solid-block` definition and
  `k_solidBlockRuntimeImplemented`. `k_barrierRuntimeImplemented = false` stays on its own
  line, **line 51**. Every runtime gate is now `false`.
- `Runtime/Levels/LevelEffectParameters.cs`: removed `SolidBlockObject`.
- `Runtime/Levels/LevelEffectChecks.cs`: removed `CheckSolidBlock`.
- `Runtime/Levels/link.xml`: removed the `SolidBlockObject` preserve line.
- `Environment/EnvironmentSolidComposer.cs`: removed `AppendBlocks` (the solid-block path);
  `AppendBarrier` and the filled-quadrant composition are unchanged.
- `Environment/EnvironmentView.cs`: removed the `m_solidComposer` field, its creation,
  `AppendBlocks` call and release. Barrier view and flip API unchanged.
- Tests: `LevelEffectTests` catalog drops `solid-block`; new invalid fixture
  `Invalid_solid-block-removed` expects `Level fx cell object o1 effect solid-block is not a
  known effect type.` `EnvironmentSolidTests`: footprint cases now compose barriers only
  (each occupied quadrant exactly one piece, barrier tint, every fully interior quadrant is
  `BlockFill`, fill only inside the footprint); the 0 B rebuild test covers board + barriers.
  `EnvironmentSolidViewTests`: the pure solid-block test was deleted; the wide-barrier fill
  test stays (IDs renamed to `barrier`).
- Docs: `docs/Level-Data-Format.md` (removal note after the catalog table),
  `docs/Environment-Layout.md` (section "Barrier fill: EnvironmentSolidComposer"),
  `PROJECT.md` ("Barrier fill (BlockFill)" section, formerly "Solid environment blocks", and
  one sentence in the DEC-121 row).

## Compile and Console

- `refresh_unity {"mode":"force","scope":"all","compile":"request","wait_for_ready":true}`
  (retried while another session's tests ran) -> "Refresh requested", idle.
- `read_console error/warning` right after: 0 entries.
- Reflection check in the reloaded domain: `SolidBlockObject` type absent,
  `EnvironmentSolidComposer.AppendBlocks` absent, `LevelEffectRegistry.DefinitionCount = 42`.

## Tests

| Run | Job | Result |
|---|---|---|
| Full EditMode (all assemblies) | `041c3fec1c6546c0883933e4b43c460a` | 976 run, 972 passed, 4 failed (below) |
| The 4 failures alone | `2890126e8616470a9d9fb1fdbacfedc5` | same 4 fail |
| Focused EditMode (LevelEffectTests, LevelCellObjectTests, LevelDatabaseJsonTests, `ColorBlockJam.Tests.Environment.*`, LevelPreviewTests) | `82711b33c21340509086e55fc653fe7f` | 283/283 passed |
| `Invalid_solid-block-removed` + EnvironmentSolidTests with details | `f92a619b1b4d4fada1bba42421f4608b` | 10/10 passed |
| Full PlayMode (all assemblies) | `d2ff105ea9e940ab883222c2f7aa17f9` | 54/54 passed |

The 4 EditMode failures are outside this task and caused by another session's in-progress
material move (`Art/Materials/Board/MAT_Board.mat` -> `Art/Materials/Shared/MAT_Gameplay.mat`,
folders modified 22:32): `BoardKitTests.PaletteCatalogAndShaderAreComplete`,
`BoardKitTests.RepeatedFootprintChangesReusePreparedObjectsAndMaterials`,
`SharedArtTests.AtlasContainsAuthoredSwatchesAndNeutralTintRow`,
`SharedArtTests.BoardEnvironmentAndBoosterMeshesUseOneOpaqueMaterial`. They load
`MAT_Board.mat` by path; none touches level effects or barrier composition.

## Acceptance mapping

- AC-1: LevelEffectTests barrier valid/invalid fixtures (default closed, `startsOpen` true,
  "starts closed on block b3 at (3, 3)") and LevelDatabaseJsonTests pass (job 82711b33).
- AC-2: `Invalid_solid-block-removed` passes. `grep -rn "SolidBlockObject|solid-block"
  Assets/ColorBlockJam` now finds only that rejection fixture (LevelEffectTests.cs 377-378).
- AC-3: EnvironmentSolidTests/EnvironmentBarrierComposerTests (EditMode), barrier view and
  wide-fill PlayMode tests, the 0 B flip test and LevelPreviewTests barrier test pass.
  LevelEditor preview capture: `barrier-preview-raised.png` in this folder, rendered by the
  real `LevelPreviewHost` in an editor preview scene (no user scene touched) with a 3x1 and a
  2x2 closed barrier: `show=True barriers=2 raisedFills=4`; after `SetBarrierOpen("square",
  true, false)`: `sunkFillsAfterOpen=4` (y -0.8, renderer off); then restored.
- AC-4: full PlayMode green; full EditMode green except the 4 foreign material failures above.

## Review fix round (2026-09-25, docs only)

- Finding 1 (confirmed): `docs/Environment-Layout.md` EnvironmentDatabase table row
  "Block fill piece" now reads "Flat top for Barrier quadrants without an exposed edge
  (`EnvironmentSolidComposer.AppendBarrier`)"; the "solid wall" wording is gone.
- Finding 2 (confirmed): `PROJECT.md` heading renamed from "Solid environment blocks" to
  "Barrier fill (BlockFill)" and the DEC-121 row pointer updated to match. No other doc
  links the old heading or anchor.
- No code changed, so no recompile was needed. Console (error/warning) shows 3 entries, all
  from other sessions: a `VacuumMotionAssets.cs` CS0618 warning, `Import Error Code:(4)`,
  and a Level Preview message about an unknown shape `Level1Piece1` in level-01.

| Run | Job | Result |
|---|---|---|
| Focused EditMode (LevelEffectTests, LevelDatabaseJsonTests, EnvironmentSolidTests, EnvironmentBarrierComposerTests) | `1912914d5f3f4ec1a3a7a6311453caa1` | 192/192 passed |
| Full EditMode (all assemblies) | `e1904d718d3b4fbc987253fc3f65c432` | 976 run, 962 passed, 14 failed (below) |
| The 14 failures alone | (rerun, 28 tests) | the same 14 fail |
| Full PlayMode (all assemblies) | `bfbd246d8328447c827036c570fbc657` | 54/54 passed |

The four material failures from the first run now pass. The 14 new failures belong to
another session. The Level Editor session is editing named-piece and plus-tile code: its
`Editor/Levels/**` sources were modified up to 22:57, and `LevelFreehandPieceTests.cs` at
22:39. The failures are 13 in `LevelPlusTileTests` (NullReference, plus a reflection
argument mismatch `LevelData` -> `LevelPieceSavePlan`) and 1 in
`LevelFreehandPieceTests.AStrokeOverFreeCellsBecomesOneBlockAsOneUndoStepAndTheTileStaysArmed`.
None of them references solid-block, the effect registry or barrier composition. These
tests and their sources are outside this task's allowed paths.

## Notes

- The straight 3x1 barrier shows a seam between the two back-to-back wall rows (no fully
  interior quadrant, so no `BlockFill`); unchanged pre-existing geometry.
- `docs/art/solid-obstacles/README.md` and `Environment/Editor/EnvironmentSolidAssets.cs`
  (Undo label "Assign solid obstacle fill") still mention solid obstacles; both are outside
  the allowed paths and describe the `BlockFill` art history.

## Independent verification (2026-09-25 ~23:02-23:22, verifier; no production, test, scene or doc edit)

Live Editor: MCP HTTP 127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`. Start and end
state: only `LevelEditor.unity` open and active, not dirty, not playing, not compiling; no scene saved.
The capture used an editor preview scene (`NewPreviewScene`, closed in `finally`). Baseline: Plastic
`cs:9` via read-only `cm cat`. No checkin or shelve.

### Files changed by this task (diff against cs:9)

All inside the allowed paths; nothing outside them is attributable to this task.

- `Runtime/Levels/LevelEffectRegistry.cs`: `k_solidBlockRuntimeImplemented` and the `solid-block`
  definition removed; `k_barrierRuntimeImplemented = false` is on its own line, **line 51**.
- `Runtime/Levels/LevelEffectParameters.cs`: `SolidBlockObject` removed (`BarrierObject` unchanged).
- `Runtime/Levels/LevelEffectChecks.cs`: `CheckSolidBlock` removed (`CheckBarrier` unchanged).
- `Runtime/Levels/link.xml`: the `SolidBlockObject` preserve line removed.
- `Environment/EnvironmentSolidComposer.cs`: `AppendBlocks` removed; `AppendBarrier` and `Append`
  (the filled-quadrant composition) unchanged apart from two summaries.
- `Environment/EnvironmentView.cs`: only the `m_solidComposer` field, creation, `AppendBlocks` call
  and release are removed; the barrier composer, flip, pause and stop paths are untouched.
- `Tests/EditMode/LevelEffectTests.cs`, `Tests/Environment/EditMode/EnvironmentSolidTests.cs`,
  `Tests/Environment/PlayMode/EnvironmentSolidViewTests.cs`.
- `docs/Level-Data-Format.md`, `docs/Environment-Layout.md`, `PROJECT.md`, and this folder.
- Other pending changes in the same files and folders come from other sessions: the `MAT_Gameplay`
  rename in `PROJECT.md` and `docs/art/solid-obstacles/README.md`, plus `AttemptLevelConverter*` and
  `Editor/Levels/**` (owned by the gameplay and Level Editor sessions).

### Results

| AC | Result | Channel and evidence |
|---|---|---|
| AC-1 | PASS | Focused EditMode `e2a2aa6063fc4d299d2749966db867d2`: **283/283** (LevelEffectTests, LevelCellObjectTests incl. `BarrierRoundTripsAndExposesTypedStartsOpen(true/false)`, `BarrierStartsOpenDefaultsToFalse`, `ClosedBarrierOnABlockIsRejectedAndAnOpenOneIsAccepted`, LevelDatabaseJsonTests, all `ColorBlockJam.Tests.Environment.*` EditMode, LevelPreviewTests). Live `LevelDatabaseJson.TryWrite`/`TryRead` round trip: barrier JSON with no `startsOpen` reads `StartsOpen=False`, `"false"` reads False, `"true"` reads True; a closed barrier on block b1 fails with `... effect barrier starts closed on block b1 at (4, 5).` |
| AC-2 | PASS | Live `TryRead` of the same JSON with `"type":"solid-block"`: `Level vjson cell object o1 effect solid-block is not a known effect type.`; `LevelEffectRegistry.TryFind("solid-block")` False, `TryFind("barrier")` True, 42 definitions; reflection: `SolidBlockObject` type, `k_solidBlockRuntimeImplemented` and `EnvironmentSolidComposer.AppendBlocks` absent. `rg "SolidBlockObject\|solid-block" Assets/ColorBlockJam`: only `LevelEffectTests.cs:377-378`, the rejection fixture this scenario requires (it passed in the focused run). No hit under `Data/**`. Remaining hits are docs history and task folders. |
| AC-3 | PASS (see note) | Capture with the real `LevelPreviewHost` (LevelEditor preview path) on a 7x7 level with a straight 3x1 barrier (no `startsOpen`) and a 2x2 barrier (`"false"`): `barriers=2`, 4 raised BlockFill caps, 0 sunk pieces (`verifier-preview-closed.png`). `SetBarrierOpen("square", true)`: 16 pieces at y -0.8 with renderers off; plus `straight`: 28 sunk, 0 raised caps (`verifier-preview-opened.png`); closing both restores 4 raised caps and 0 sunk. A level with both `startsOpen:"true"` starts with 28 sunk; raising the square gives 4 caps and 12 sunk. PlayMode `ColorBlockJam.Environment.PlayModeTests` `5e7b899002ca4ff1846c6896921fd625` **14/14**, including `WideBarrierFillMovesWithItsEdgesAndReturnsToThePool`, the barrier view tests and `BarrierFlipsPauseAndStopAllocateNothing`. |
| AC-4 | PASS | Full EditMode (all assemblies) `e9134557d19245a4b89ac09a2795f33c`: **982 total, 981 passed, 0 failed, 1 skipped**. The skip is `PieceEditorLinkTests.ShowLevelPieceWithoutFocusUpdatesTheOpenWindow`, which ignores itself when another Piece Editor window is open (the environment's state; not a failure). The 14 Level Editor failures and 4 material failures from earlier rounds now pass. Full PlayMode `70932420b7594222bf63253a21e0bb17`: **54/54**. Console: no compile error; the error entries are test-harness logs (`Test listener failure`, PrefabTests piece-JSON restore) from other suites. |

**Note on the 0 B flip test.** `EnvironmentBarrierViewTests.BarrierFlipsPauseAndStopAllocateNothing`
is intermittent: it passed in full PlayMode `70932420…`, alone in `885fa967…` and `8c55074…`, and
in the Environment assembly run `5e7b8990…`. It failed once in the Environment assembly
(`40fc1edf…`) and once alone (`5804f909…`), each time with "Expected: not allocates GC memory".
This task did not cause it. The test file is byte-identical to cs:9, and the task's
`EnvironmentView` diff touches no flip, pause or stop line. The same failure was recorded before
this task in `TASK-20260924-224656127-e361499a-board-art-dependency-injection/evidence.md` ("Gate
finding"). The likely cause there was PrimeTween's pool: tweens stopped in one frame return to the
pool only on the next update, so the second `Flip()` can allocate a new `ColdData`. The test's owner
(the barrier-board-view task) should warm the pool across a frame or set a tween capacity.

### Other observations (not defects of this task)

- The 3x1 barrier seam, the `docs/art/solid-obstacles/README.md` history, and the
  `EnvironmentSolidAssets.cs` Undo label are as the implementer reported. The class name
  `EnvironmentSolidComposer` is kept; its summary now describes barriers only.
