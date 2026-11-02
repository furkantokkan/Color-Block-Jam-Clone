# TASK-20260926-212320271-98b25efa-level-editor-arrow-markers — Arrow blocks show their direction on the Level Editor canvas and in the scene preview

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-212320271-98b25efa-level-editor-arrow-markers
- Supersedes: none
- Title: Arrow blocks show their direction on the Level Editor canvas and in the scene preview
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 plus the uncommitted Level Editor work of 2026-09-26/27 (planning provenance only)
- Task kind: feature
- Area: Level Editor canvas rendering (`ColorBlockJam.Editor`, Canvas) and the LevelEditor scene preview (`ColorBlockJam.Editor.LevelPreview`), with one pure run rule in `ColorBlockJam.Runtime`
- Workflow: direct

## Objective and baseline

- Observable objective: A block with an Arrow effect shows a double-headed arrow along its axis, centred on the block: on the Level Editor canvas, in 3D in the LevelEditor scene preview, and in the Game scene (revision 1, see Contract history). The canvas block label gets an outline.
- Current behavior/evidence:
  - User message of 2026-09-27: "effect olarak arrow ekledim ama directional olarak gözükmüyor level editörde ve preview de gözükmeli". Screenshot: block b2 has Arrow, axis Vertical, in the Effect Editor. Neither the canvas nor the scene preview shows a direction.
  - Level JSON stores the effect as `arrow` with `axis` = `Horizontal` | `Vertical` (`LevelEffectRegistry`).
  - `LevelCanvasModel.Create(level)` builds `LevelCanvasBlock`s without effect data. `LevelCanvasElement` draws no arrow.
  - `Prefabs/Board/Pieces/Arrow_1.prefab`, `Arrow_2.prefab` and `Arrow_3.prefab` exist, calibrated by `BoardArrowAssets` (original `Arrows.fbx` meshes, `BoardPieceView`, centred, facing local Z, just above the studs). `BoardArt.ArrowColor` (palette, white by default) exists. No code places them: PROJECT.md says "No automatic marker presenter ... is installed by the prefab preparation step".
  - Spec, docs/Color-Block-Jam-PRD-Mechanics-Catalog.md BLK-01:
    - "The grid draws a double-headed arrow badge along the axis."
    - "Pick the variant by the block's run along the axis through its bounds center, clamped to 3 (longer runs reuse `Arrow_3`, no stretching). Pooled, parented to the block view, one shared material."
    - Edge cases: 1x1 uses `Arrow_1`; an axis across a long block uses the 1-cell run; L or T footprints use the run through the bounds center, with ties to the lower index.
- Expected behavior:
  - One pure rule, shared by both views: a plain C# helper (for example `ArrowMarkerRun` in `Assets/ColorBlockJam/Runtime/Board`, no Unity scene or Onity dependency) takes a block's cells and axis and returns the marker.
    - Revision 1, superseding the run rule: the marker's centre is the footprint's bounding-box centre on both axes (cell space; a 2x2 block's arrow sits on the line between its columns). Its length is the bounding-box extent along the axis in cells, clamped to 1 to 3. That length picks the variant (Arrow_1 to Arrow_3), with no stretching.
    - It returns that centre, the extent, the variant and the axis.
    - Blocks without a valid Arrow effect return none.
    - It allocates nothing per call where practical.
  - Canvas: each `LevelCanvasBlock` carries its arrow marker from `LevelCanvasModel.Create`. `LevelCanvasElement` draws a double-headed arrow along the axis, centred on the block's bounding box (revision 1). The block label ("B2") gets a dark outline so it reads over the arrow and every block colour (revision 1). The arrow is white with a dark outline, readable at every zoom, not taking pointer input, and drawn in the existing single `generateVisualContent` pass with no per-frame allocation. Hover, selection, overlap marks and block labels stay readable.
  - Scene preview: `LevelPreviewHost` places the matching `Arrow_N` prefab on each Arrow block when it builds a published draft.
    - The marker sits at the run centre, rotated for the axis. PROJECT.md: the prefabs face local Z, and horizontal placement uses a Y90 root rotation. It is parented to the block's view root and tinted with `BoardArt.ArrowColor` through the existing `BoardPieceView.SetColor` MPB path, sharing `MAT_Gameplay`.
    - Markers come from pools prepared with the preview's other pools, sized to the level. They are returned on every rebuild, on `null`/clear, on scene close and on domain reload, like the preview's other pieces. No `Instantiate` per rebuild beyond pool warm-up, no material instances, and the scene file never changes (DontSave root, DEC-102).
    - An invalid draft keeps the last valid preview, including its markers.
  - Shared marker owner (revision 1): the preview and the Game scene place markers through one Presentation owner, for example `BlockArrowMarkers` in `Assets/ColorBlockJam/Presentation/Board`. It holds three Onity pools (one per variant) and a `Prepare(counts)`, `Attach(block root, cells, axis, cell size, colour)`, `Detach`/`ReturnAll` and `SetClipPlane` API. Placement math from `ArrowMarkerRun` lives there once. The prefabs come from `BoardPieceCatalog`: its serialized environment list already references Arrow_1 to Arrow_3, and a new read-only accessor exposes them with no serialized change. The editor preview's `LevelPreviewArrowMarkers` is replaced by, or reduced to a thin use of, this owner.
  - Game scene (revision 1): `BoardPresenter` gives each Arrow block its marker.
    - Pools are warmed on Home before Playing, sized from the level's Arrow blocks per variant (Performance Rule 3: no Instantiate in Playing; exhaustion rejects rather than grows).
    - The marker is parented to the block root, so it moves with drag and exit motion. It is clipped with the block whenever the block's exit or effect clip plane is set, returned when the block exits or is removed (Vacuum, Hammer) and on teardown, Restart and next level. No marker survives an attempt (PROJECT.md Teardown and Restart rules).
    - No Update and no subscription without an owner and stop point. Tint through `BoardPieceView.SetColor` with `BoardArt.ArrowColor`. Zero allocation per frame (the marker is a static child).
    - If the presenter already recomposes a block's cells during play (Rocket, `SetCells`), the marker re-centres from the new footprint in the same step. Otherwise that stays with the Rocket work.
  - Docs: docs/Level-Editor-Design.md (canvas rendering) and docs/Level-Editor-Scene-Preview.md describe the badge, the marker and the run rule.
- Behavior that must remain unchanged:
  - Level JSON format and schema; the effect registry; Effect Editor behaviour.
  - Canvas interaction (hit testing, tools, drag, labels), zoom and pan.
  - Preview lifetime rules (DEC-102), preview block and environment output apart from the added markers, and pool sizing for blocks.
  - Gameplay rules: this task adds no movement rule. The Arrow drag and exit constraint (BLK-01 gameplay) is separate work. The Game scene's presentation is in scope since revision 1.
  - Game scene lifecycle, drag, exit, booster and teardown behaviour for all blocks, apart from the added markers; the zero-allocation Playing tick.
  - Arrow prefab assets, the piece catalog, `BoardArt`, `GamePalette` and every other serialized asset.

## Ownership

- Owner/writer role: implement-task (single writer)
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Board/ArrowMarkerRun.cs
  - Assets/ColorBlockJam/Runtime/Board/ArrowMarkerRun.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasModel.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasElement.cs
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewBlock.cs
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewBlockMapper.cs
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewArrowMarkers.cs
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewArrowMarkers.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/ArrowMarkerRunTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/ArrowMarkerRunTests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/CanvasArrowBadgeTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/CanvasArrowBadgeTests.cs.meta
  - Assets/ColorBlockJam/Tests/LevelPreview/LevelPreviewTests.cs
  - Assets/ColorBlockJam/Tests/LevelPreview/PreviewArrowMarkerTests.cs
  - Assets/ColorBlockJam/Tests/LevelPreview/PreviewArrowMarkerTests.cs.meta
  - Assets/ColorBlockJam/Presentation/Board/BlockArrowMarkers.cs (revision 1)
  - Assets/ColorBlockJam/Presentation/Board/BlockArrowMarkers.cs.meta (revision 1)
  - Assets/ColorBlockJam/Presentation/Board/BoardPieceCatalog.cs (revision 1: a read-only arrow accessor only; no serialized field change)
  - Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs (revision 1)
  - Assets/ColorBlockJam/Tests/PlayMode/BlockArrowMarkersTests.cs (revision 1)
  - Assets/ColorBlockJam/Tests/PlayMode/BlockArrowMarkersTests.cs.meta (revision 1)
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/BoardPresenterArrowMarkerTests.cs (revision 1)
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/BoardPresenterArrowMarkerTests.cs.meta (revision 1)
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/Fixtures/PresenterScene.cs (revision 1: only if the arrow test needs a fixture hook; existing tests keep their outcomes)
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/Fixtures/FakeBoardPresenter.cs (revision 1: only if an interface change requires it)
  - docs/Gameplay-Drag-And-Gate.md (revision 1: a short presentation note)
  - docs/Level-Editor-Design.md
  - docs/Level-Editor-Scene-Preview.md
  - production/tasks/TASK-20260926-212320271-98b25efa-level-editor-arrow-markers/contract.md (managed lifecycle block only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs (held by TASK-20260926-204508638-a3ad6ee3-level-editor-architecture)
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs (held by TASK-20260926-204508638-a3ad6ee3-level-editor-architecture)
  - Assets/ColorBlockJam/Editor/Levels/LevelDraft.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditCommands.cs
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Gameplay/** except Board/BoardPresenter.cs (revision 1)
  - Assets/ColorBlockJam/Presentation/** except Board/BlockArrowMarkers.cs and the accessor in Board/BoardPieceCatalog.cs (revision 1)
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Runtime/Levels/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Art/**
  - Assets/ColorBlockJam/Scenes/**
  - any .asmdef change
  - Plastic checkin, shelve, branch, or undo of other sessions' pending changes
  - Changing the user's open Level Editor draft, the level JSON or the LevelEditor scene file
- Serialized assets: none. Arrow prefabs are loaded read-only. The editor-only preview may load them by their asset paths (for example through `BoardKitAssets` constants, if the LevelPreview assembly can reach them, otherwise by path strings) or from an existing data asset reference; no serialized reference is added. New `.cs.meta` files are Unity-generated script metadata owned by implement-task.
- Other active writers/worktrees checked (2026-09-26T21:23Z):
  - The TASK-20260926-204508638-a3ad6ee3-level-editor-architecture lock, held since 21:03Z by "/root task-cycle Level Editor architecture", owns `LevelDatabaseWindow.cs` and `Tests/EditMode/Level*Tests.cs`. It forbids itself `Editor/Levels/Canvas/**` and `Editor/LevelPreview/**`, so the paths here are disjoint from it.
  - No other lock is held. Session "Rotate tool rotation behavior" (staircase corners in LevelCanvasModel/LevelCanvasElement/LevelEditCommands) has released its lock. Before the first write, recheck the lock folder, and the modification times of LevelCanvasModel.cs and LevelCanvasElement.cs.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. AC-1 (revision 1): Given the pure rule, when it is given a 1x1, a 2x2, straight 3-cell and 4-cell blocks, an axis across a long block, and L, T and plus footprints on both axes, then it returns the bounding-box centre, the extent along the axis and the variant clamped to 1 to 3, and none without a valid Arrow effect.
2. AC-2 (revision 1): Given a level with an Arrow block (Horizontal or Vertical), when the Level Editor canvas shows it, then the model carries the block's marker and the canvas draws a double-headed arrow along the axis, centred on the block (a 2x2 block's arrow sits on its centre line). The block label has a dark outline. A block without Arrow shows none. An effect edit, undo or axis change updates it.
3. AC-3: Given the LevelEditor scene preview, when an Arrow block is published, then the matching `Arrow_N` instance sits centred on the block, turned for its axis, parented to the block view and tinted with the palette's Arrow colour. A rebuild, a cleared draft or a scene close returns every marker to its pool, and nothing leaks.
4. AC-4 (revision 2): transferred to TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay (its AC-3). The Game scene's marker needs that task's Arrow runtime rule and the shared effect framework (docs/Block-Effects-Architecture.md: ArrowEffectView moves the inline BoardPresenter arrow code). This task keeps the shared marker owner and the presenter-level tests it delivered.
5. AC-5: Given the existing suites (Level EditMode, LevelPreview, Environment, Gameplay EditMode and PlayMode, Presentation PlayMode), when they run after the change, then they pass, and the scene files and serialized assets are unchanged.
6. AC-6: Given the live Level Editor, the scene preview and the Game scene with the user's Arrow block, when the user looks, then they accept the arrow look and the label outline (explicit user acceptance).

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User message of 2026-09-27 (see Current behavior) and its screenshot: b2 with Arrow Vertical in the Effect Editor, with the canvas and preview showing no arrow.
  - docs/Color-Block-Jam-PRD-Mechanics-Catalog.md BLK-01 (grid badge, the view rule, edge cases).
  - PROJECT.md Architecture Rules (arrow prefabs, Y90 horizontal, Arrow Color white) and Performance Rules 1 (shared material, MPB via the owner's SetColor) and 3 (Onity pools, no Instantiate per rebuild).
  - DEC-102 (LevelEditor scene preview), docs/Level-Editor-Scene-Preview.md.
- Unity version/platform: Unity 2022.3.62f2, Windows Editor (editor tooling and the editor-only scene preview)
- Package/architecture constraints:
  - UI Toolkit `Painter2D` in the existing canvas pass.
  - Onity pools as the preview already uses them. No new package or asmdef.
  - The run rule stays in `ColorBlockJam.Runtime` (pure C#), so the Game scene can reuse it later.
  - Class summaries, `m_`/`k_`/`s_` naming, and owner/stop-point comments for any subscription or pool.
- Explicit out of scope:
  - The Arrow drag and exit constraints (BLK-01 gameplay).
  - An Arrow canvas tool that cycles axes.
  - Rocket and Hammer interaction visuals.

## Verification and risk

- Focused test path/command:
  - UnityMCP `run_tests` EditMode `ColorBlockJam.Tests.EditMode.ArrowMarkerRunTests` and `ColorBlockJam.Tests.EditMode.CanvasArrowBadgeTests`;
  - the LevelPreview test assembly (`ColorBlockJam.Tests.LevelPreview` namespace or its asmdef group);
  - then the Level suite `ColorBlockJam[.]Tests[.]EditMode[.]Level` and the Environment EditMode suite.
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: a clean compile after `refresh_unity` with no new Console errors. Check `mcpforunity://editor/state` first: no compile and no other session's test run in progress. The architecture task is active, so never refresh while its files are mid-edit (re-read the state right before refreshing).
- EditMode: the run-rule table; the canvas model marker per block; preview marker placement, rotation, parent, tint and pool return.
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: canvas and scene-view screenshots of the user's Arrow block, then the user's acceptance (AC-5).
- Risk: medium-high since revision 1: a new Runtime helper, preview and Game-scene pooling, and BoardPresenter lifecycle. No serialized or schema change.
- Revision 1 verification additions: Gameplay PlayMode `BoardPresenterArrowMarkerTests` (warm on Home, attach, exit clip, return on exit, removal, teardown and restart, no Instantiate in Playing) and Presentation PlayMode `BlockArrowMarkersTests`; then the Gameplay EditMode and PlayMode suites and the Presentation PlayMode suite. PlayMode needs the Editor out of Play Mode and no other session's run. A PlayMode run closes nothing of the user's.
- Approval already granted (exact target only): the user's request of 2026-09-27 above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: `/implement-task TASK-20260926-212320271-98b25efa-level-editor-arrow-markers`

## Contract history (conditions agreed after authoring)

- Revision 1, 2026-09-27, after the user saw the first canvas result (the arrow drawn in the left column of the 2x2 b2): "geldi ama hem preview hem oyun hemde editörde tam ortadan çıkmalı ek olarak outline verebiliriz block numarasına".
  - Centring: the marker centres on the footprint's bounding box. Its length is the bounding-box extent along the axis, clamped to 1 to 3, in the canvas, the preview and the game. This supersedes BLK-01's "run through the bounds center, ties to the lower index". On an irregular footprint (L, T) the arrow is centred on the bounding box and may cross an empty part of it; this is the literal "tam ortadan" rule, and the user is told.
  - Game scene: added to scope. One shared Presentation marker owner serves the preview and `BoardPresenter`, and the prefabs come from `BoardPieceCatalog`'s existing serialized arrow references through a new read-only accessor. The allowed paths gain BlockArrowMarkers.cs, the BoardPieceCatalog accessor, BoardPresenter.cs, their tests and a docs note. Risk rises to medium-high, because the change touches Game-scene presentation and pools, with no serialized or schema change. Approval: the user's message above.
  - Label outline: the canvas block label gets a dark outline.
  - The arrow drag and exit constraint stays out of scope.

- Revision 2, 2026-09-27T17:15Z: the user accepted the arrow look ("bunlar okay", item 4: "The arrow look on the canvas and in the 3D preview"). The Game-scene criterion (AC-4) could not pass in this contract's paths: LevelSession passed no authored data, and the Arrow runtime gate refused Arrow levels. It moved to the Arrow gameplay task by the user's choice "Add arrow gameplay" and the agreed shared effect architecture, where ArrowEffectView owns the Game-scene marker. Nothing else changes.

## Stop conditions

- A needed change falls in a forbidden path: LevelDatabaseWindow.cs, `Tests/EditMode/Level*Tests.cs`, a serialized asset, Presentation, Environment or an asmdef.
- Another live writer takes LevelCanvasModel.cs, LevelCanvasElement.cs or the LevelPreview files.
- The arrow prefabs cannot be reached from the LevelPreview assembly without an asmdef or serialized-asset change.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:079ece9256758c701d30b0ba4bc3149ddb6c589760be671b9fe595f4c04cace0
- Evidence fingerprint: sha256:aeb35508b8cbc5688aeefecb63fa384956b7beda8214a3961882dbe3b032e54f
- Reviewed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c5fa62fc6e370df551cf79df593eb9a834a4b7acffb31b5545ca2991e4f6322e
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c5fa62fc6e370df551cf79df593eb9a834a4b7acffb31b5545ca2991e4f6322e
- Closed at: 2026-09-27T17:24:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: ArrowMarkerRunTests (1x1, 2x2 between columns, I3 and I4 on both axes, L, T, plus, offset, invalid; zero allocation) (job ae5997e3) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c5fa62fc6e370df551cf79df593eb9a834a4b7acffb31b5545ca2991e4f6322e |
| AC-2 | PASS | automated: CanvasArrowBadgeTests (model centres, axis edit, one badge drawn per Arrow block above the grid, label outline style) (job ae5997e3); live canvas screenshot of b2 sent 2026-09-27 | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c5fa62fc6e370df551cf79df593eb9a834a4b7acffb31b5545ca2991e4f6322e |
| AC-3 | PASS | automated: PreviewArrowMarkerTests and LevelPreviewTests (placement, rotation, tint, prepared counts, rebuild, invalid draft, missing prefab) (job ae5997e3); live preview render of b2 sent 2026-09-27 | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c5fa62fc6e370df551cf79df593eb9a834a4b7acffb31b5545ca2991e4f6322e |
| AC-4 | UNPROVEN | presenter level only: BoardPresenterArrowMarkerTests and BlockArrowMarkersTests pass (PlayMode job 24935d9c, 64/64), but no Arrow marker reaches the real Game scene (no authored effect data in LevelSession, and the project-scope Arrow gate refuses Arrow levels); awaits the user's decision | none |
| AC-5 | PASS | automated: Level EditMode 723/723 (job afeeb376); LevelPreview, Environment and Gameplay EditMode 379/379 (job f6e7593a); Gameplay and Presentation PlayMode 64/64 (job 24935d9c); no scene or serialized asset changed | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c5fa62fc6e370df551cf79df593eb9a834a4b7acffb31b5545ca2991e4f6322e |
| AC-6 | UNPROVEN | none | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored 2026-09-26T21:25Z by session "Edit dropdown ve colors kareleri" from the user's message of 2026-09-27; ready, disjoint from the active architecture task
- Started — implementation — 2026-09-26T21:28Z: the user's message is the execution order ("tasklerini bitirene kadar devam et" stands). The lock folder showed only the architecture claim. LevelCanvasModel.cs and LevelCanvasElement.cs were last written at 19:19Z (the staircase work is released).
- Contract revision — 2026-09-26T21:50Z: the user saw the first canvas arrow in the left column of the 2x2 b2 and asked for it centred in the preview, the game and the editor, with an outline on the block label. That is revision 1 in "Contract history": bounding-box centring, Game scene in scope, and the label outline. The running workflow (wf_613258f9-31a) was stopped mid-implementation. Its partial files (ArrowMarkerRun, the canvas model and element, LevelPreviewArrowMarkers and the preview host, block and mapper, and the three new test files) are the new starting point. Fingerprint restamped.
- Verification — 2026-09-26T23:20Z: workflow wf_b1a7e6ea-80d rebuilt the partial work under revision 1. It added the bounding-box rule, the canvas badge (its own layer above the grid), the label outline, the shared Presentation BlockArrowMarkers with BoardPieceCatalog.TryGetArrow, the preview integration (LevelPreviewArrowMarkers deleted) and BoardPresenter support through TryBuild(level, authored, out error). Four read-only lenses found 8 findings, and skeptics confirmed all 8. A fixer fixed 6: the grid line over the badge, a destroyed-marker pool slot, badge-drawing and catalog-rejection tests, and doc accuracy. The remaining 2 are the same gap: the real Game scene shows no marker (LevelSession passes no authored data, and LevelEffectRegistry.k_arrowRuntimeImplemented = false refuses Arrow levels). That needs a user decision, not a fix inside this contract. Suites are recorded in AC-5. Live evidence: the canvas and preview renders of the user's b2 after a draft republish (PublishDraft; the draft and JSON are unchanged). Contract fix: the stale 'Game-scene arrow presentation' out-of-scope bullet was removed and the fingerprint restamped from sha256:a2f14185a78229950123fe411cbd1f12d280eb3dcc47bacaf28abbebd2f2d24f. Also still open: PROJECT.md and the BLK-01 catalog lines still describe the superseded run rule, with no DEC row yet.
- 2026-09-27T17:24:19Z Closed by explicit user confirmation (task-done, user_confirmed): the user accepted the live result on 2026-09-27 ('bunlar okay', item 4, the arrow look on the canvas and in the 3D preview; AC-6. AC-4, Game-scene arrows, stays UNPROVEN here and is owned by TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay) and ordered 'tasklari kapat izin veriyorum'. Criteria rows are preserved as recorded, not re-verified; owned files may have changed after the reviewed revision through other sessions.
<!-- TASK-LIFECYCLE:END -->
