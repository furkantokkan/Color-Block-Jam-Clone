# TASK-20260926-075624941-a65fbdb1-level-editor-select-mode-placement — Level Editor: pieces are placed with Select; the Pen paints gates, edits the selected piece and draws new pieces

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-075624941-a65fbdb1-level-editor-select-mode-placement
- Supersedes: none (supersedes only these clauses: transform-tools `TASK-20260925-142546927-22e06638` expected behavior "the Pen button arms the last piece again" and AC-8 "keeps the Pen"; custom-pieces `TASK-20260925-082521498-8e54a900` "a Custom tile arms the Pen"; the closed toolbox task's "picking a palette tile selects the Pen armed with that piece")
- Title: Level Editor: pieces are placed with Select; the Pen paints gates, edits the selected piece and draws new pieces
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 plus the uncommitted custom-pieces R-A/R-B/D-001 work of session "Rotate tool rotation behavior" (Level EditMode 526/526, job 0d7309de90a2420499bc15e87b417c7e)
- Task kind: UI
- Area: Level authoring (Level Editor window tool model, Blocks palette arming, canvas left-click routing)
- Workflow: direct

## Objective and baseline

- Observable objective: A Blocks library or Custom tile arms its piece with the Select tool, and a left click on a free cell places it; the Pen never places a picked piece and is used only to paint gates, to draw cells onto the selected block, and to draw a "+ New piece".
- Current behavior/evidence (read 2026-09-26 in `Editor/Levels/LevelDatabaseWindow.cs`, `Canvas/LevelEditorToolState.cs`, `Canvas/LevelCanvasElement.cs`, `Canvas/LevelShapePalette.cs`):
  - `ArmShape`, `ArmCustomPiece` and `OnLevelPieceChanged` call `LevelEditorToolState.Arm`, which sets `Tool = Pen`; a Pen left press on a cell or block raises `PlaceRequested` and `PlaceBlock` stamps the armed piece at the hovered cell minus `ArmedAnchor`.
  - The window's `SetTool(Pen)` re-arms `LastArmedShapeId`; `SelectPen` arms it again.
  - The canvas draws the armed ghost only while `m_tool == Pen` and armed cells exist.
  - Freehand ("+ New piece", `ArmFreehand`) grows and shrinks only the named piece's own drawn block (`GetDrawingBlockId`, `CanGrowPieceAt`, `PieceStrokeRequested`, `PieceCellRemoveRequested`); a Pen press on the wall ring paints gates; the Barrier tile arms the Pen with the Barrier brush.
  - Palette tooltips say "click to place with the Pen".
  - User order 2026-09-26 (verbatim, with a screenshot of the Tools group): "parçaları yerleştirme selection modunda olsun çizim modu sadece geçit çizmek ve seçli parçayı editlemek veya yeni parça oluşturmak için kullanabiliriz", then "implemente başla planladıktan sonra". Answer to the follow-up question "what should editing the selected piece with the Pen do": "Draw cells on it (Recommended)": with a block selected, the Pen adds cells next to it by left click or drag and removes cells by right click, like New piece drawing (it must stay connected); a named level piece updates everywhere it is used; a library-piece block turns into a new level piece for that block only, so the template itself is never changed.
- Expected behavior:
  - Picking a library or Custom tile (and a Piece Editor change that arms a piece) arms the piece with `Tool = Select`: Select is the highlighted tool, the tile is outlined, and the ghost (green where it fits, red where not) follows the pointer over board cells that no block covers.
  - With a piece armed, a Select left click on a free board cell places it at the hovered cell minus `ArmedAnchor` in the tool colour ("Place Block", one undo step), selects the new block, and keeps the piece armed for the next click. A refused place changes nothing and the status names the cell. A Select left press on a block selects it and may drag it as before; it never places. Right click still removes.
  - With a piece armed in Select, a left click on a gate takes that gate's colour as the tool colour (the swatch shows it and the next placed block uses it); the piece stays armed, the gate is not selected and the draft does not change (user revision of 2026-09-26, verbatim: "selection mode da küp sürüklerken eğer gate sol tıklarsam elimdeki renk değişsin"). Without an armed piece a gate click selects the gate as before.
  - Esc, the Select button or key 1 (when a piece is armed), the Pen button or key 2, the Eraser or key 3, "+ New piece" and the Barrier tile disarm the piece. The rotate and mirror buttons and R / Shift+R / F / Shift+F turn or mirror the armed ghost and keep Select with the piece armed (replaces transform-tools AC-8's "keeps the Pen"); with no piece armed the buttons pick up their tools as before.
  - The Pen button and key 2 select the plain Pen and never arm a piece; a Pen that already draws freehand or holds the Barrier brush keeps it. `LastArmedShapeId` re-arming is removed.
  - Pen with a selected block (not freehand, no brush): a left click or drag on free cells edge-adjacent to that block adds them as one undo step ("Add Piece Cells"); a click that is not adjacent is refused with "Click next to <block id>"; a right click on one of its cells takes that cell off ("Remove Piece Cell"), a split is refused, and the last cell removes the block. The ghost is green where a click would add a cell and red elsewhere, as for freehand. The same learning limits apply (25 cells, 5 x 5).
    - If the block's `shapeId` is a named level piece of the open level, the piece takes the new footprint, every block of it is re-stamped in its own orientation, and the Piece Editor is mirrored, exactly as for freehand growth.
    - Otherwise (a library piece or a `Custom_` id), the first edit makes a new named level piece "<Level Name> Piece <n>" for that block only: the block's `shapeId` becomes the new TypeName, the piece gets a Custom tile, and the library piece JSON, prefabs, enum and the other blocks keep their bytes and shape ids.
  - Pen with no selected block, not freehand and no brush: a left click on a cell changes nothing and the status says "Select a block to edit it with the Pen, or press + New piece." Pen presses on the wall ring paint gates as before.
  - "+ New piece" freehand drawing, the Barrier brush on the Pen, the Eraser and right-click removal behave as before.
  - Palette tooltips say a click arms the piece for placing with Select; the Pen tooltip names gates, drawing on the selected block and New piece.
- Behavior that must remain unchanged:
  - Gate painting, gate recolouring and gate selection; the Barrier brush; the Eraser; right click removes with every tool except the freehand/edit cell removal; Esc cancels a drag or resize.
  - Select-tool selection, drag-to-move, nudge arrows, Delete, `[` / `]`, the swatch rule (a swatch recolours only a block or gate the designer chose with Select), and one undo group per gesture.
  - The rotate and mirror tools and keys (in-place quarter turns, `GetTurnOriginShift`), the tool badge, the transform row, the icons and the key numbers 1 Select, 2 Pen, 3 Eraser.
  - The custom-pieces revisions R-A, R-B and D-001, "+ New piece" naming and Save Level's named-piece plan.
  - The level JSON is written only by Save Level; the piece library is written only by the Piece Editor or Save Level's commit.

## Ownership

- Owner/writer role: session "Rotate tool rotation behavior" as implementer; an independent verifier agent afterwards
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditorToolState.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasElement.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelShapePalette.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelPieceSession.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelNamedPiece.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260926-075624941-a65fbdb1-level-editor-select-mode-placement/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Editor/BoardKit/** (call only `PieceEditorWindow.ShowLevelPiece`, `SetLevelTool` and `ShapeLibraryAuthoring.GetTypeName`)
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditCommands.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelShapeRestamp.cs (call only)
  - Writing level JSON, the piece JSON, `BlockShapeLibrary.asset`, shape prefabs or `BlockShapeType.cs`
  - Any write to an allowed path while another session holds a lifecycle lock on a task that owns it
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: 2026-09-26 ~08:00Z. This session holds the custom-pieces lock (verification of R-A, R-B, D-001 in progress) and owns the same files; the transform-tools lock is released (user acceptance pending). Peer sessions "Parça template boş kareler", "Çakışma durumunda B1/B2 davranışı" and "Level editörü commit" are idle; no other lifecycle lock is held.
- Execution dependency: `TASK-20260925-082521498-8e54a900-level-editor-custom-pieces` finishes its independent verification and this session releases its lifecycle lock.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the Level Editor with Select active, when a library tile with a prefab or a Custom tile is clicked, then the tool stays Select (Select highlighted, Pen not), the tile is outlined, `HasArmedShape` is true, and moving the pointer over a free cell shows the ghost.
2. Given a piece armed with Select, when a free cell is left-clicked, then one block of that piece is added at the cell minus `ArmedAnchor` in the tool colour as one undo step and selected, the piece stays armed, and a second click on another free cell adds another block; a click whose footprint would overlap or leave the board changes nothing and the status names the cell; a left press on an existing block selects it without placing.
3. Given a piece armed with Select, when Esc, key 1, the Pen button, key 2, key 3, "+ New piece" or the Barrier tile is used, then no piece is armed; when a rotate or mirror button or R / Shift+R / F / Shift+F is used, then the ghost turns or mirrors in place and Select stays active with the piece armed.
4. Given any state, when the Pen button or key 2 is pressed, then the Pen is active and no piece is armed (a Pen already drawing freehand or holding the Barrier brush keeps it); a Pen stroke on the wall ring paints a gate as before.
5. Given the Pen with a selected block of a named level piece used by two blocks, when free cells next to the selected block are clicked, then the cells are added as one undo step, the other block follows the new footprint, the Custom tile shows it and the Piece Editor is mirrored; a right click takes a cell off, a split is refused, and a right click on the last cell removes the block.
6. Given the Pen with a selected block of a library piece that another block also uses, when a free cell next to it is clicked, then that block's `shapeId` becomes a new named level piece "<Level Name> Piece <n>" with the grown footprint, a Custom tile appears, the other library block keeps its shape id and cells, the piece library JSON keeps its bytes, and one Undo restores the block and removes the tile.
7. Given the Pen with no selected block, not freehand and no brush, when a board cell is left-clicked, then nothing changes and the status reads "Select a block to edit it with the Pen, or press + New piece."
8. Given "+ New piece", the Barrier tile, the Eraser, right click and the rotate/mirror tools, then they behave as before, and the Level EditMode groups pass; assertions change only where this task changes behavior (arming selects Select, the Pen no longer re-arms or places, AC-8's kept tool).

## References and constraints

- Story/GDD/ADR/design/log/image: the user's screenshot of the Tools group (2026-09-26); `docs/Level-Editor-Design.md` section 2 (Toolbox, Transform row, freehand drawing, named level pieces) and section 5; PROJECT.md DEC-119; predecessor contracts `TASK-20260925-142546927-22e06638-level-editor-transform-tools`, `TASK-20260925-082521498-8e54a900-level-editor-custom-pieces`, `TASK-20260925-082521477-0b7cb096-level-editor-toolbox`.
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - UI Toolkit only; no new package, asmdef, icon or serialized asset.
  - The canvas mutates nothing: it raises place, stroke and cell-removal requests; the window applies them through `LevelEditCommands` and `LevelShapeRestamp`.
  - `LevelEditorTool` values stay contiguous; no new tool value.
  - Plain C# tool state; ZString/ZLinq rules unchanged (no `System.Linq` or `string.Format` added).
- Explicit out of scope:
  - Editing gates with the Pen beyond today's painting; editing several blocks at once.
  - Changing a library piece (template) from the Level Editor; library changes stay in the Piece Editor.
  - A tool badge for the armed Select tool.
  - The Piece Editor window itself.

## Verification and risk

- Focused test path/command: the EditMode fixtures `LevelEditorToolStateTests`, `LevelCanvasBlockToolsTests`, `LevelFreehandPieceTests`, `LevelPlusTileTests`, `LevelObstaclesSectionTests`, `LevelEditorToolsCleanupTests`, then all `ColorBlockJam.Tests.EditMode.Level*` groups, through MCP for Unity `run_tests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: zero `error CS` after a script refresh in the live Editor
- EditMode: all Level groups pass
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: the user's look-and-feel acceptance of placing with Select and drawing on a selected block with the Pen
- Risk: low
- Approval already granted (exact target only): none
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: $implement-task TASK-20260926-075624941-a65fbdb1-level-editor-select-mode-placement after the custom-pieces lock release, then an independent verifier

## Stop conditions

- A required change falls outside the allowed paths, for example `LevelEditCommands.cs` or `LevelShapeRestamp.cs`: stop and record it.
- Editing a library block would need a library write: stop; the block becomes a level piece instead.
- A preserved behavior, including the custom-pieces R-A/R-B/D-001 and the transform-tools in-place turns, cannot be kept inside these paths.
- Another session takes a lifecycle lock on a task that owns these files.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:d58815a9cc6cd5892953ce4518bc0a277ec2073616e3b6fb71ad09d817fe9849
- Evidence fingerprint: none
- Reviewed revision: vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 A picked tile arms its piece with Select and shows the ghost | PASS | automated: LevelEditorToolStateTests.ArmingSelectsSelectAndRotatesTheGhostUntilAnotherToolDisarms, LevelCanvasBlockToolsTests.PlacingAShapeAddsOneBlockAndPublishesItOnce, LevelFreehandPieceTests.ThePenDrawsOnTheSelectedBlockAndALibraryBlockBecomesALevelPiece; implementer job 819dd1764b244a86a214518ed65edd12 526/526 on the final bytes; independent verifier job 484c09d33fd4475586a83db58aaa5760 VERIFY: PASS (526/526) | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |
| AC-2 Select places the armed piece on a free cell, stays armed, and a block press selects instead | PASS | automated: PlacingAShapeAddsOneBlockAndPublishesItOnce, AGateClickWithAnArmedPieceTakesTheGateColourWithoutSelectingIt (user revision: a gate click with a piece in hand takes its colour); Level EditMode 592/592 (job 96ae062c67ba4cb08b4704cf8fdada17); PieceEditorLinkTests+BoardKitTests 38 passed, 1 skipped (Piece Editor open; job 93f55bbfa9c34b65afb8d3532b929102); compile clean; 16 live library and level files SHA-256 unchanged | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |
| AC-3 Disarming inputs disarm; transform buttons and keys turn the ghost and keep Select | PASS | automated: LevelEditorToolStateTests.ThePenNeverHoldsAPieceAndATransformToolArmsNothing, LevelCanvasBlockToolsTests.TransformKeysTurnTheArmedGhostElseTheSelectedBlockWithoutChangingTheTool, LevelObstaclesSectionTests.BarrierTileArmsThePenAndAPieceSelectEraserOrTransformDisarmsIt; implementer job 819dd1764b244a86a214518ed65edd12 526/526 on the final bytes; independent verifier job 484c09d33fd4475586a83db58aaa5760 VERIFY: PASS (526/526) | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |
| AC-4 The Pen never arms a piece and still paints gates | PASS | automated: LevelCanvasBlockToolsTests.ToolButtonsAndNumberKeysSwitchTheToolAndThePenNeverArmsAPiece, LevelCanvasGatesEdgesTests (gate painting), LevelEditorToolsCleanupTests.PenTooltipCarriesTheGateHelp; implementer job 819dd1764b244a86a214518ed65edd12 526/526 on the final bytes; independent verifier job 484c09d33fd4475586a83db58aaa5760 VERIFY: PASS (526/526) | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |
| AC-5 The Pen draws cells onto a selected named-piece block, all its blocks follow | PASS | automated: LevelFreehandPieceTests.ThePenDrawsOnTheSelectedBlockAndALibraryBlockBecomesALevelPiece (both blocks of the piece grow, split refused, right click takes a cell off, one undo step each); implementer job 819dd1764b244a86a214518ed65edd12 526/526 on the final bytes; independent verifier job 484c09d33fd4475586a83db58aaa5760 VERIFY: PASS (526/526) | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |
| AC-6 The Pen turns a selected library block into a new level piece for that block only | PASS | automated: LevelFreehandPieceTests.ThePenDrawsOnTheSelectedBlockAndALibraryBlockBecomesALevelPiece (b1 takes FreehandFixturePiece1, piece JSON byte-identical, undo restores); implementer job 819dd1764b244a86a214518ed65edd12 526/526 on the final bytes; independent verifier job 484c09d33fd4475586a83db58aaa5760 VERIFY: PASS (526/526) | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |
| AC-7 The Pen with no selected block explains what to do and changes nothing | PASS | automated: LevelFreehandPieceTests.APenWithNoBlockToDrawOnSaysWhatItIsFor, LevelCanvasBlockToolsTests.ToolButtonsAndNumberKeysSwitchTheToolAndThePenNeverArmsAPiece; implementer job 819dd1764b244a86a214518ed65edd12 526/526 on the final bytes; independent verifier job 484c09d33fd4475586a83db58aaa5760 VERIFY: PASS (526/526) | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |
| AC-8 Freehand, Barrier, Eraser, right click and transforms unchanged; Level groups pass | PASS | automated: all Level EditMode groups 526/526 (freehand, Barrier, Eraser, right click, transform, custom-pieces tests); implementer job 819dd1764b244a86a214518ed65edd12 526/526 on the final bytes; independent verifier job 484c09d33fd4475586a83db58aaa5760 VERIFY: PASS (526/526) | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |
| V-1 User look-and-feel acceptance | UNPROVEN | user look-and-feel acceptance pending | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | task_regression:ac-8:library-reload-test-inconclusive-assumes-pen-after-arm | VERIFIED | TASK_REGRESSION | AC-8 | ALibraryReloadedElsewhereRefreshesTheShapesAndDisarmsAChangedShape ends Inconclusive at its Assume that Arm selects the Pen. | passes to the end in job 96ae062c (592/592, no Inconclusive) | vcs:cs:10@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:9710434d570f6033e5df7d039187a87ce11a76ea615ed583813137d76e10eb0c |

### Transition history

- Created — planning — contract authored from the user's order of 2026-09-26 (Select-mode placement) and the "Draw cells on it" answer; queued after the custom-pieces verification in the same session
- Started — implementation — 2026-09-26T08:02Z: custom-pieces verification done and its lock released; direct implement-task run under lock token fa07ae8e…
- Verified — verification — 2026-09-26: implemented (Arm selects Select, Pen never arms, canvas IsPlacingOn, SyncPieceDrawing/TryGetEditPiece for the Pen on the selected block); compile clean; Level EditMode 526/526 (job 819dd176, final bytes); independent verifier VERIFY: PASS (526/526), job 484c09d3; its three stale doc/comment findings fixed before the final run; AC-1..AC-8 PASS; V-1 awaits the user
- Verified — verification — 2026-09-26: D-001 fixed and verified (test runs to the end); gate-colour pick with an armed piece added on the user's order; the Pen edit target moved to TASK-20260926-140520462-2196c1d1 (Pen click only); Level EditMode 592/592 (job 96ae062c67ba4cb08b4704cf8fdada17); PieceEditorLinkTests+BoardKitTests 38 passed, 1 skipped (Piece Editor open; job 93f55bbfa9c34b65afb8d3532b929102); compile clean; 16 live library and level files SHA-256 unchanged
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
