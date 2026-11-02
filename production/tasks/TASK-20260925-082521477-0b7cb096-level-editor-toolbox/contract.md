# TASK-20260925-082521477-0b7cb096-level-editor-toolbox — Level Editor: icon toolbox, pointer tool badge, centred piece thumbnails with delete

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-082521477-0b7cb096-level-editor-toolbox
- Supersedes: none
- Title: Level Editor: icon toolbox, pointer tool badge, centred piece thumbnails with delete
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: UI
- Area: Level authoring (Level Editor window tools column and canvas)
- Workflow: direct

## Objective and baseline

- Observable objective: A designer picks the canvas tool from an icon toolbox (Select, Pen, Rotate, Eraser) and applies transforms from icon buttons (rotate clockwise, rotate counter-clockwise, mirror horizontally, mirror vertically). The active tool's icon follows the pointer at its lower right over the canvas, tinted with the selected colour while the Pen is active. The Blocks palette shows each piece centred without empty squares, and a small × on each tile deletes that piece from the piece library.
- Current behavior/evidence:
  - Task 3 (`TASK-20260924-230915888-a8788bab-level-canvas-block-tools`) is still being built by another live session. Line numbers below were read at 10:56 on 2026-09-25 and will move.
  - `Canvas/LevelShapePalette.cs` `DrawMiniGrid` (lines 144-169) fills at least a 5x5 grid of empty squares and draws each shape at its raw cells with row 0 at the bottom, so a Single sits in the lower-left corner. The user's screenshot 1 on 2026-09-25 shows this.
  - `Canvas/LevelEditorToolState.cs` has only `Select`, `Place` and `Eraser`. Select and Eraser are text buttons in the Edit group (`LevelDatabaseWindow.cs` `CreateEditGroup`, about lines 414-462). Place is entered only by picking a palette tile.
  - The Blocks group has the text buttons `rotate-block` ("Rotate 90°"), `flip-block-h` and `flip-block-v`. These act only on the selected block. The R, F and Shift+F keys act on the armed ghost first (window about lines 1073-1094 and 1332-1343).
  - `LevelPieceTools.Rotate90` maps (x, y) to (-y, x), which is counter-clockwise on the y-up canvas. There is no clockwise helper.
  - The canvas has no cursor or overlay code. It repaints only on model, hover-cell, selection, tool or geometry change (canvas-view AC-3).
  - `EditorGUIUtility.FindTexture` icon buttons with a text fallback exist in `CreateIconButton` (about lines 566-583). No custom PNG icons exist under `Editor/`.
  - Icon sources are 512x512 RGBA PNGs in `C:/Users/e-fur/Downloads` (user, 2026-09-25): `cursor.png`, `pen.png`, `rotation.png`, `eraser.png`, `rotate-left.png`, `rotate-right.png`, `reflect-horizontal.png`, `reflect-vertical.png`. Every visible pixel is black. UI Toolkit tint multiplies colours, so black icons cannot be tinted or lightened for the dark skin.
- Expected behavior:
  1. **Thumbnails.** Each palette tile draws only the piece's cells, with no empty squares. The piece is centred on its bounding box inside the tile grid. All tiles share one cell size, taken from the largest bounding box in the catalog, so relative piece sizes stay comparable. The layout is computed by a pure helper with EditMode tests.
  2. **Tile delete.**
     - Every library tile has a small × in its top-right corner with the tooltip "Delete {DisplayName} from the piece library". Clicking × never arms the piece.
     - The window asks for confirmation in a dialog that names the piece and the number of blocks in the active level database that use its `shapeId`. On confirm it calls the existing `ShapeLibraryAuthoring.Delete(library, index)` transaction, which reserves the enum identity and prefab and records the library's own undo. The palette is then rebuilt from a fresh catalog read. Cancel changes nothing.
     - If the deleted piece was armed, the Pen disarms. A refused delete (for example while scripts compile) shows the exception message in the status line and changes nothing.
  3. **Tools group.**
     - A Tools group above Colors holds four exclusive icon buttons: `tool-select` (cursor), `tool-pen` (pen), `tool-rotate` (rotation) and `tool-eraser` (eraser). Their tooltips name the keys 1, 2, 3 and 4, and the keys switch tools, ignored while a text or integer field has focus.
     - The active tool is highlighted. Picking a palette tile selects the Pen and arms that piece. Selecting the Pen re-arms the last armed piece while it is still in the catalog.
     - Undo, Redo, the nudge buttons and Delete stay in the Edit group.
  4. **Tool behaviour.**
     - Select is unchanged: select a block or door, drag to move a block.
     - Pen with an armed piece stamps it in the selected colour, as Place does today. Pen with no armed piece only shows the status hint "Pick a piece in Blocks". Ring gates and freehand drawing arrive in the successor tasks.
     - Rotate: a left click on a block turns it 90° clockwise, and Shift+click turns it counter-clockwise, through `TryReplaceBlockCells`. Each is one undo step, and an off-board or overlapping result is refused with the cell named.
     - Eraser is unchanged.
     - Right click removes in every tool.
  5. **Transform buttons.**
     - The Blocks group replaces the three text buttons with four icon buttons: `rotate-block-cw` (rotate-right, "Rotate clockwise (R)"), `rotate-block-ccw` (rotate-left, "Rotate counter-clockwise (Shift+R)"), `flip-block-h` (reflect-horizontal, "Mirror horizontally (F)") and `flip-block-v` (reflect-vertical, "Mirror vertically (Shift+F)").
     - Buttons and keys follow one rule: they transform the armed ghost while the Pen has an armed piece, and otherwise the selected block. They are enabled when either target exists.
     - `LevelPieceTools` gains a clockwise rotation. Four clockwise turns return the original cells.
  6. **Pointer badge.**
     - One 20 px `Image` child of `LevelCanvasElement` with `pickingMode = Ignore` shows the active tool's icon 14 px right of and below the pointer while the pointer is over the canvas.
     - It is hidden on pointer leave, on capture out, and when the canvas has no geometry. It stays above the block labels.
     - Its tint is the selected palette colour while the Pen is active and the skin's neutral icon colour otherwise (`EditorGUIUtility.isProSkin`). A swatch change updates the tint immediately.
     - Moving the badge changes only its `style.translate`. The canvas does not repaint for badge moves.
  7. **Icons.**
     - The eight icons are converted to white strokes that keep the source alpha, downscaled to 64x64 and stored with their source file names under `Assets/ColorBlockJam/Editor/Levels/Icons/`.
     - They import as Editor GUI and Legacy GUI, alpha is transparency, no mipmaps, clamp, bilinear, max size 64, uncompressed. The importer is set through Unity APIs, never by hand-editing `.meta`.
     - One small editor helper loads them with `AssetDatabase.LoadAssetAtPath<Texture2D>`. A missing icon falls back to its text label without a Console error.
     - Buttons tint their icons with the skin's neutral colour.
  8. **Docs.** `docs/Level-Editor-Design.md` sections 2 and 5 describe the Tools group, the transform rule, the badge, the thumbnail layout, tile delete and the icon source and settings. The same sections also record the ownership change and the successor tasks.
- Behavior that must remain unchanged:
  - Every task 1-3 behavior, including one undo group per gesture, right-click removal, nudge, colour swatches and `[` / `]` cycling.
  - Canvas-view AC-3: hover inside one cell requests no canvas repaint.
  - The piece library, its JSON, enum and prefabs are changed only by a designer's confirmed × click through the existing transaction; tests never touch the real library.
  - The level JSON is written only by Save Level.

## Ownership

- Owner/writer role: Level Editor session "Parça template boş kareler" (user decision 2026-09-25: this session takes over Level Editor work after task 3), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Editor/Levels/Icons.meta
  - Assets/ColorBlockJam/Editor/Levels/Icons/**
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260925-082521477-0b7cb096-level-editor-toolbox/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Editor/BoardKit/** (call only its public `ShapeLibraryAuthoring` API)
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef
  - Assets/AddressableAssetsData/**
  - Deleting or editing a real piece library entry during verification (use a scratch library in tests; a live check deletes only with the user's go, or in an isolated copy)
  - Writing level JSON except through the existing Save Level path during manual checks
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/cursor.png
    Sole writer: this session through file copy plus Unity TextureImporter API
    Matching `.meta` owner: this session (importer API only)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/pen.png
    Sole writer: this session through file copy plus Unity TextureImporter API
    Matching `.meta` owner: this session (importer API only)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/rotation.png
    Sole writer: this session through file copy plus Unity TextureImporter API
    Matching `.meta` owner: this session (importer API only)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/eraser.png
    Sole writer: this session through file copy plus Unity TextureImporter API
    Matching `.meta` owner: this session (importer API only)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/rotate-left.png
    Sole writer: this session through file copy plus Unity TextureImporter API
    Matching `.meta` owner: this session (importer API only)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/rotate-right.png
    Sole writer: this session through file copy plus Unity TextureImporter API
    Matching `.meta` owner: this session (importer API only)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/reflect-horizontal.png
    Sole writer: this session through file copy plus Unity TextureImporter API
    Matching `.meta` owner: this session (importer API only)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/reflect-vertical.png
    Sole writer: this session through file copy plus Unity TextureImporter API
    Matching `.meta` owner: this session (importer API only)
- Other active writers/worktrees checked: At 10:56-10:57 on 2026-09-25 the session "Ruled tile level sistem" was writing `LevelDatabaseWindow.cs`, `Canvas/**` and `Tests/EditMode/Level*Tests.cs` for task 3. On the user's decision it was asked at about 11:10 to finish task 3, release those paths and not start tasks 4 or 5. This session is subscribed to its idle notice.
- Execution dependency: TASK-20260924-230915888-a8788bab-level-canvas-block-tools handed off (lifecycle recorded, or its owner idle with no Level Editor file changed for 10 minutes) and its owner has released the Level Editor paths
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the base piece library, when the Blocks palette is built, then every tile draws only filled cells, all tiles share one cell size, and each piece's bounding-box centre lies within 1 px of its tile grid centre. The helper is tested with EditMode tests.
2. Given a scratch piece library with two pieces, when × is clicked on one and the dialog is confirmed, then `ShapeLibraryAuthoring.Delete` removes it, the palette rebuilds with one tile, and an armed deleted piece disarms. When the dialog is cancelled, nothing changes. Clicking × never arms a piece.
3. Given the window, then the Tools group holds `tool-select`, `tool-pen`, `tool-rotate` and `tool-eraser` icon buttons whose tooltips name keys 1-4. Clicking a button or pressing its key switches and highlights the tool, and picking a palette tile selects the Pen.
4. Given the Rotate tool, when a block is clicked, then its cells turn clockwise as one undo step, and Shift+click turns them counter-clockwise. When the result would leave the board or overlap, the rotation is refused with the cell named and the draft is unchanged.
5. Given a Pen with an armed piece, the transform buttons and R, Shift+R, F and Shift+F transform the ghost. Otherwise they transform the selected block. Four clockwise turns restore the original cells.
6. Given the pointer over the canvas, then the badge shows the active tool's icon at the pointer plus the offset. With the Pen, its tint equals the selected palette colour and follows a swatch change. Leaving the canvas hides it, and moving inside one cell requests no canvas repaint.
7. Given the eight icon assets, then each is a 64x64 white-on-alpha PNG with the recorded import settings, and the icons are visible on both editor skins. A missing icon falls back to text with no Console error.
8. Given the updated task 1-3 tests, then all pass. Right click removes in every tool, and undo and redo behave as before.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User messages and screenshots 2026-09-25 in session "Parça template boş kareler": the palette thumbnail screenshot, the eight flat icons, the reference tools/colours/blocks screenshot and the rotate/mirror crop.
  - User answers 2026-09-25: this session owns Level Editor work after task 3; icon files approved; the × deletes the piece from the library enum.
  - `docs/Level-Editor-Design.md` sections 2, 3 and 5.
  - PROJECT.md Architecture Rules on the typed board authoring and shared palette (the delete reserves enum identities and prefabs).
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - UI Toolkit only (`Image`, `generateVisualContent` + `Painter2D`).
  - Plain C# tool state and layout helpers with EditMode tests; thin VisualElements.
  - No new package, no new asmdef, and no asmdef change.
  - Repaint only on change; no `schedule` loops.
  - `ColorBlockJam.Editor` must not reference `ColorBlockJam.Editor.LevelPreview`.
  - Clear English names and the `m_`/`s_`/`k_` field convention.
- Explicit out of scope:
  - Pen on ring segments (gates), per-side edge resize, rule-set walls and the issues list: successor `TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls`.
  - Freehand Pen drawing and custom JSON pieces: `TASK-20260925-082521498-8e54a900-level-editor-custom-pieces`.
  - Effects on blocks and gates: task 5 (`TASK-20260924-230915891-b289bba3-level-canvas-v2-tools`), gated on level JSON v2.
  - The spare `flip.png` icon.
  - A custom OS cursor texture: the badge is an overlay and the system arrow stays.

## Verification and risk

- Focused test path/command: new `LevelShapePaletteTests` (layout helper, ×), `LevelEditorToolStateTests`, `LevelCanvasBlockToolsTests` (updated names, Rotate tool, transforms), `LevelCanvasElementTests` (badge), `LevelDatabaseTests` (clockwise rotation); scratch-library delete following the `BoardKitTests` pattern
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console in the live Editor (MCP HTTP 127.0.0.1:8082), or in an isolated batchmode copy when the bridge is down
- EditMode: the focused tests above, plus `DatabaseControlsTests`, `LevelDatabaseAddressablesTests`, `LevelDatabaseInspectorTests`, `BoardKitTests` and the LevelPreview tests stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - A window capture at 960x640 and larger showing the Tools group, the transform row, centred thumbnails with ×, and the Pen badge in the selected colour.
  - Tools and transforms exercised in the live Editor with the level JSON hash unchanged unless Save is part of the scenario.
  - User acceptance of the look and feel.
- Risk: medium (new editor-only icon assets; a designer-triggered library delete through the existing transaction)
- Approval already granted (exact target only): user answers 2026-09-25 for this toolbox, the eight icon files under `Editor/Levels/Icons/`, and the × library delete through `ShapeLibraryAuthoring.Delete`
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: $implement-task TASK-20260925-082521477-0b7cb096-level-editor-toolbox, then an independent verifier

## Stop conditions

- A Level Editor path changed within the last 10 minutes by another session, or task 3's owner has not released the paths: wait, do not edit.
- The delete needs a change in `Editor/BoardKit/**`: stop and record it for the board-kit owner.
- A required change falls outside the allowed paths: stop and record it.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: READY_TO_CLOSE
- Contract fingerprint: sha256:0fcf2f104ef1f4f90bd2880e566d2aae31f4271047aa294e48722aebd55818c9
- Evidence fingerprint: sha256:2ae98147c0daadb802ff2ceb4b349fdc24039e093fcca92bd3ac566bb00698fb
- Reviewed revision: vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71
- Attempt count: 1
- Last feedback: 2026-09-25 user answer 'Hepsi': accept and close
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71
- Closed at: 2026-09-25T13:55Z
- Closure mode: criteria_verified
- Closed by: implement-task (automatic)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Centred shared-scale thumbnails | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated test (full EditMode run 2026-09-25 14:31 (live Editor): 667/670, the 3 BoardKitTests failures are the piece-count baseline): LevelShapePaletteTests | vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71 |
| AC-2 Tile × deletes through the library transaction | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated test (full EditMode run 2026-09-25 14:31 (live Editor): 667/670, the 3 BoardKitTests failures are the piece-count baseline): TileDeleteAsksFirstThenDeletesThroughTheLibraryTransaction, EveryTileHasADeleteButton... | vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71 |
| AC-3 Icon Tools group with keys 1-4 | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated test (full EditMode run 2026-09-25 14:31 (live Editor): 667/670, the 3 BoardKitTests failures are the piece-count baseline): ToolsColumnHolds..., ToolButtonsAndNumberKeys... | vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71 |
| AC-4 Rotate tool clockwise and counter-clockwise | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated test (full EditMode run 2026-09-25 14:31 (live Editor): 667/670, the 3 BoardKitTests failures are the piece-count baseline): RotateToolTurnsTheClickedBlock..., ClockwiseRotation... | vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71 |
| AC-5 One transform rule for buttons and keys | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated test (full EditMode run 2026-09-25 14:31 (live Editor): 667/670, the 3 BoardKitTests failures are the piece-count baseline): TransformButtonsAndKeys..., RotateKeyKeepsTheDefaultSelection... | vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71 |
| AC-6 Pointer badge without canvas repaint | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated test (full EditMode run 2026-09-25 14:31 (live Editor): 667/670, the 3 BoardKitTests failures are the piece-count baseline): ToolBadgeFollowsThePointerInAFrame... (framed badge per user follow-up) | vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71 |
| AC-7 White icons with recorded import settings | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated test (LevelEditorIconsTests) for files and import; visibility on both editor skins needs manual check or user acceptance | vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71 |
| AC-8 Task 1-3 behaviour preserved | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated test (full EditMode run 2026-09-25 14:31 (live Editor): 667/670, the 3 BoardKitTests failures are the piece-count baseline); full Editor+LevelPreview run 204/207, the 3 BoardKitTests failures are the piece-count baseline | vcs:cs:7+owned-sha256:e287915e6945606913dc5bf37097be14f0bfc847b987681ee9b41ddb5fef6f71 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's Level Editor feedback items 1 and 2 of 2026-09-25
- Started — implementation — 2026-09-25T09:09:24Z: task 3 released by its owner (RELEASE message); direct implement-task run
- Verification — 2026-09-25T10:02:38Z: implemented; independent two-lens review found one major (R marked the default selection as chosen) and minor issues, all fixed with tests; live Editor 69/69; user follow-up (framed badge, no Select badge, wrapping square buttons, white active frame) folded in; AC-7 skins and look and feel await the user
- Feedback — 2026-09-25T11:35Z: relayed tools-pane requests applied in LevelDatabaseWindow.cs (transforms under the canvas tools, no delete-block button), tests updated; full EditMode suite green apart from the BoardKit baseline; AC-7 and look and feel still await the user
- Closed — 2026-09-25T13:55Z: the user accepted every criterion (answer "Hepsi"); all criteria PASS and every defect VERIFIED at the reviewed revision; closed automatically (criteria_verified)
<!-- TASK-LIFECYCLE:END -->
