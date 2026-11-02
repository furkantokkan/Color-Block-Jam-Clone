# TASK-20260925-185129336-2df1d5a7-piece-editor-live-level-sync — Piece Editor live level-piece sync and Save Level template batch

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-185129336-2df1d5a7-piece-editor-live-level-sync
- Supersedes: none
- Title: Piece Editor live level-piece sync and Save Level template batch
- Repository (absolute path): C:\Users\e-fur\Documents\Repos\ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: Plastic workspace ColorBlockJamCase2026, branch /main@ColorBlockJamCase2026@efurk64@unity
- Base revision: vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity (planning provenance only; the three owned BoardKit sources and PieceEditorLinkTests.cs have no pending change against cs:8)
- Task kind: feature
- Area: Board-kit authoring: Piece Editor level-piece mode and piece-library batch commit (the Piece Editor side of the Level Editor link)
- Workflow: direct

## Objective and baseline

- Observable objective: While the Piece Editor shows a level piece, each edit reaches the Level Editor as it happens, with no Apply to level button, and the Level Editor can add a level's new pieces to the active Piece Database in one library transaction when the designer clicks Save Level.
- Current behavior/evidence:
  - User redesign, 2026-09-25, relayed verbatim by the Level Editor session "Parça template boş kareler" (Turkish): "new piece e tıkladığımızda o levele özel bir template oluşur ve name piece editor ile eşleşir çizeriz tekrar yani yeni element eklenir bölümü save ettiğimizde aynı zamanda save as template olur yani apply to level butonu da kalkabilir demek oluyor bu". Meaning: clicking "+ New piece" creates a template that belongs to that level and whose name matches the Piece Editor's Name field; we draw it and a new element is added; saving the level also saves it as a template; so the Apply to level button can be removed. The Level Editor contract (`TASK-20260925-082521498-8e54a900-level-editor-custom-pieces`, transition 2026-09-25T18:32Z) records the same redesign.
  - `PieceEditorWindow.cs`: in level-piece mode the Save button reads "Apply to level" (`SyncFields`, lines 362-365), and `ApplyToLevel` raises `LevelPieceApplied` only on that click (lines 685-704). Grid strokes, Clear grid, Rotate 90°, grid undo and Name edits do not reach the level.
  - `LoadLevelPiece` always sets the Name to "New Piece" (line 441). `LevelPieceRequest` carries the level id, the piece id and the cells, but no name.
  - `ShowLevelPiece(request)` calls `GetWindow`, which focuses the window (lines 80-81). The Level Editor mirrors every released "+" stroke through it (`LevelDatabaseWindow.cs` line 2250), so drawing in the Level Editor moves the focus to the Piece Editor.
  - `ShapeLibraryAuthoring` adds one piece per `Commit` (`Save`, `AddType`) and has no batch entry point. One `Commit` already gives one transaction, one library undo step, one `LibraryCommitted` and one enum import for all the records it writes (`ShapeLibraryTransaction.Save`).
  - Consumers outside this task: `LevelDatabaseWindow.cs` subscribes to `LevelPieceApplied` (lines 191 and 244); `LevelPlusTileTests.cs` asserts that subscription (lines 706 and 714); `LevelDatabaseWindow.cs` line 2399 already names "the planned LevelPieceChanged event". `CreateNamedPiece` (lines 2153-2177) names a "+" piece after the level plus "Piece" and a number (for example "Level 1 Piece 1") with `TypeName = ShapeLibraryAuthoring.GetTypeName(name)`, unique in the level, the library and the session, and the piece's blocks use that TypeName as their shape id.
  - The active `BlockShapeLibrary.json` holds the 11 base records; `BlockShapeType.cs` also reserves `NewPieceTest = 30` from the user's deleted test piece.
- Expected behavior:
  - Level-piece mode has no Apply to level control. Each change raises the new `internal static event Action<LevelPieceRequest> PieceEditorWindow.LevelPieceChanged` with the request's level id and piece id, the draft's normalized cells and the current name: the end of a stroke that changed cells, Rotate 90°, a grid Undo or Redo that changes the draft, and a committed Name edit. The event always carries at least one cell and a non-blank name; a change that would break either raises nothing. It is raised before the Piece Editor closes the stroke's undo group, so a listener's undo record joins that group. Each listener runs in its own try/catch, as `LibraryCommitted` listeners do.
  - `LevelPieceRequest` gains `Name` through an optional last constructor parameter (`string name = null`), kept in a serialized field, so existing callers compile. `ShowLevelPiece` shows it in the Name field, or "New Piece" when it is absent. A request equal to the shown level piece (level id, piece id, normalized cells and name) changes nothing. A level-piece draft never asks the discard question, because every valid edit has already reached the level.
  - New overload `internal static void ShowLevelPiece(LevelPieceRequest request, bool focus)`. It passes `focus` to `GetWindow`, so `false` updates an open Piece Editor without taking the focus and opens one when none is open. The one-argument form keeps its behavior and still converts to `Action<LevelPieceRequest>`.
  - New `internal static bool ShapeLibraryAuthoring.TryAddPieces(BlockShapeLibrary library, IReadOnlyList<(string TypeName, string DisplayName, IReadOnlyList<Vector2Int> Cells)> pieces, out string issue)`. It validates every entry first and then adds all new records in one `Commit`: one library undo step, one `LibraryCommitted`, one enum change and import, and one prefab per new record. The caller supplies the TypeName, because the Level Editor's blocks already use it as their shape id and a later rename in the Piece Editor keeps a record's TypeName. A TypeName that names a compiled `BlockShapeType` value that no record uses reuses that ID, as `AddType` does; every other new TypeName gets the next ID above the highest compiled or library ID, in input order. An entry equal to an existing record is skipped. An invalid entry, Play Mode, a running compilation, a piece JSON changed on disk or a failed write returns false with a message and leaves every output as it was. Only the designer's Save Level click calls it; no agent or test calls it on the live project library.
  - `LevelPieceApplied` and the Apply to level code are deleted once the Level Editor no longer references them. Until then the event stays declared and is never raised.
- Behavior that must remain unchanged:
  - Library mode: Save piece, New piece, Delete selected piece, the Piece Database dropdown with its + and × buttons, the Piece library dropdown and stepper, Clear grid, Rotate 90°, grid undo and redo with their shortcuts, and the discard question for a dirty library draft.
  - `ShowLibraryPiece`, `LoadLibraryPiece`, the pending library request before the GUI exists, and the one-cell start of `LoadLevelPiece` for a request without cells.
  - Level-piece mode survives a GUI rebuild and a domain reload (serialized mode, request and draft). Delete selected piece stays disabled in it. Save as template still adds a new library piece from the Name field and returns to library mode.
  - `LibraryCommitted` is raised once per successful `Commit`, library undo and redo included, and the window's `OnLibraryCommitted` keeps its draft and selection.
  - `Save`, `AddType`, `Delete`, `Import`, `Export`, `Reload`, `SyncPrefabs` and `GetTypeName`, the transaction's validation and rollback, and the enum's append-only identities.
  - No test or agent writes the live project's piece JSON, `BlockShapeLibrary.asset`, shape prefabs or `BlockShapeType.cs`.

## Ownership

- Owner/writer role: Piece Editor owner session "Database oluşturma popup" (board-kit area), then a sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/BoardKit/PieceEditorWindow.cs
  - Assets/ColorBlockJam/Editor/BoardKit/ShapeLibraryAuthoring.cs
  - Assets/ColorBlockJam/Editor/BoardKit/LevelPieceRequest.cs
  - Assets/ColorBlockJam/Tests/EditMode/PieceEditorLinkTests.cs
  - docs/art/board-kit/README.md
  - .agents/skills/board-piece-authoring/SKILL.md
  - production/tasks/TASK-20260925-185129336-2df1d5a7-piece-editor-live-level-sync/**
- Forbidden paths/actions:
  - Assets/ColorBlockJam/Editor/Levels/**
  - Assets/ColorBlockJam/Editor/Controls/**
  - Assets/ColorBlockJam/Editor/Search/**
  - Assets/ColorBlockJam/Editor/BoardKit/ShapeLibraryTransaction.cs
  - Assets/ColorBlockJam/Editor/BoardKit/ShapePrefabAuthoring.cs
  - Assets/ColorBlockJam/Editor/BoardKit/BoardKitAssets.cs
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Tests/EditMode/BoardKitTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/ShapeLibraryJsonTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/ShapePrefabTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*.cs
  - Assets/ColorBlockJam/Tests/PlayMode/**
  - Packages/**
  - ProjectSettings/**
  - PROJECT.md
  - docs/Level-Editor-Design.md
  - Any other path not listed under Allowed paths, including new files
  - Calling TryAddPieces, Save, AddType, Save as template or Save Level on the live project's library, in a test or as an agent
  - Hand edits to BlockShapeType.cs, the piece JSON, BlockShapeLibrary.asset or shape prefabs
  - Checkins, shelves or branch changes
- Serialized assets: none
- Other active writers/worktrees checked: `TASK-20260924-224656127-e361499a-board-art-dependency-injection` (session "Ruled tile level sistem") released `PieceEditorWindow.cs` on 2026-09-25 after its independent verification; it keeps one pending edit in `BoardKitAssets.cs` (forbidden here) and awaits its user's acceptance. `TASK-20260925-082521498-8e54a900-level-editor-custom-pieces` (session "Parça template boş kareler") writes `Editor/Levels/**` and the Level tests (in progress, the Level Editor side of this redesign). `ShapeLibraryAuthoring.cs` and `LevelPieceRequest.cs` were last written by this session's link task. Plastic has no worktrees; the live Editor `ColorBlockJamCase2026@99077e4df1ecfaa4` is shared through MCP for Unity HTTP 8082 (stdio shows 0 instances) and was idle, not compiling, with `LevelEditor.unity` open.
- Execution dependency: none to start. Closure: AC-10 waits until `TASK-20260925-082521498-8e54a900-level-editor-custom-pieces` stops referencing `LevelPieceApplied` in `LevelDatabaseWindow.cs` and `LevelPlusTileTests.cs` (signal: a message from session "Parça template boş kareler", or no reference outside `Editor/BoardKit`).
- Queue policy: none

## Acceptance scenarios

1. Given the Piece Editor in level-piece mode, then no control reads "Apply to level": `save-piece` is hidden, `save-template` is visible (enabled when a database is selected), `delete-piece` is disabled, and the info text names the level and the piece and says that edits reach the level as they happen and that Save Level adds the piece to the library. In library mode `save-piece` reads "Save piece" as before.
2. Given level-piece mode for a request with level id L, piece id P and name N, when a paint or erase stroke that changed at least one cell ends, when Rotate 90° runs on a non-empty grid, or when a grid Undo or Redo changes the draft, then `LevelPieceChanged` is raised exactly once with L, P, the draft's normalized cells and the current name. A stroke that changed nothing raises nothing. Clear grid, or any change that leaves the grid empty, raises nothing, and the status says that the level keeps its last footprint. Library mode never raises it. A listener that throws is logged and does not stop the other listeners or the Piece Editor. The piece JSON, `BlockShapeLibrary.asset` and `BlockShapeType.cs` keep their bytes.
3. Given level-piece mode and a `LevelPieceChanged` listener that records an undo step on its own object inside the handler, when a stroke ends or Rotate 90° runs, then that record joins the Piece Editor's grid undo group: one Undo restores both the grid and the listener's object, and the Piece Editor then raises `LevelPieceChanged` once with the restored cells.
4. Given level-piece mode with at least one cell, when a changed Name is committed (Enter, or the focus leaves the Name field), then `LevelPieceChanged` is raised once with that name and the current cells. Typing without committing raises nothing. A blank name raises nothing and the status asks for a name.
5. Given `ShowLevelPiece` or `LoadLevelPiece` with a request named "Level 1 Piece 1", then the Name field shows that name; a request without a name shows "New Piece". The mode, the request with its name and the draft survive a GUI rebuild and are serialized. When a request equal to the shown level piece arrives (same level id, piece id, normalized cells and name), then the draft, its grid undo history and the status stay as they are; a different request replaces the draft.
6. Given level-piece mode after edits, when another level-piece request arrives, or the designer chooses New piece or a library piece, then no discard question appears. A dirty library-mode draft still asks before a request or a choice replaces it.
7. Given an open Piece Editor and the focus in another window, when `ShowLevelPiece(request, false)` runs, then that same Piece Editor instance shows the request and the focus stays in the other window. With no Piece Editor open, it opens one. `ShowLevelPiece(request)` still focuses the window and still converts to `Action<LevelPieceRequest>`.
8. Given a library and valid entries, when `TryAddPieces` runs, then it returns true with no issue after exactly one `Commit`: the new records follow the existing ones in input order with normalized cells; a TypeName that names a compiled `BlockShapeType` value that no record uses keeps that ID; every other new TypeName gets the next ID above the highest compiled or library ID; one library Undo restores the previous records; `LibraryCommitted` is raised exactly once. For a saved project library, that one `Commit` writes the piece JSON, the library bindings, one prefab per new record and one enum change, imported once.
9. Given a blank TypeName or DisplayName, a TypeName that `BlockShapeLibraryJson.IsTypeName` rejects, a TypeName repeated in the batch, a TypeName already in the library with another name or footprint, empty or disconnected cells, Play Mode, a running compilation, a piece JSON changed on disk, or a read-only output, when `TryAddPieces` runs, then it returns false with an issue that names the problem and the entry, writes nothing, records no undo step and raises no `LibraryCommitted`. An entry equal to an existing record (TypeName, name and footprint) is skipped; when nothing is left to add, an empty batch included, it returns true without a `Commit`.
10. Given that `LevelDatabaseWindow.cs` and `LevelPlusTileTests.cs` no longer reference `LevelPieceApplied`, then `LevelPieceApplied`, `ApplyToLevel` and the "Apply to level" texts are deleted, no file under `Assets` references `LevelPieceApplied`, and the owned files compile without a warning. Before that signal the event stays declared and is never raised.
11. Given the change, when the focused suites run, then `PieceEditorLinkTests` (updated), `BoardKitTests`, `ShapeLibraryJsonTests` and `ShapePrefabTests` pass, every item under "Behavior that must remain unchanged" holds, and the live project's piece JSON, `BlockShapeLibrary.asset`, shape prefabs and `BlockShapeType.cs` keep their bytes. The three 11-record pins in `BoardKitTests` count as a pre-existing condition, not a regression, when the active library holds designer-added pieces at run time.
12. Given the change, then `docs/art/board-kit/README.md` (Level Editor link) and `.agents/skills/board-piece-authoring/SKILL.md` describe the live sync, the name link, the focus overload, `TryAddPieces`, and that only the designer's Save Level or Save as template click writes the library; neither mentions Apply to level.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - The user redesign quoted under Current behavior/evidence, relayed with the Piece Editor API request by the Level Editor session "Parça template boş kareler" on 2026-09-25.
  - `production/tasks/TASK-20260925-082521498-8e54a900-level-editor-custom-pieces/contract.md`: the consumer. Its forbidden list still excludes library writes from the Level Editor, so Save Level calling `TryAddPieces` needs that task's own contract change.
  - `production/tasks/TASK-20260925-145158848-5858b722-piece-editor-level-link/contract.md`: READY_TO_CLOSE; this task replaces its Apply to level behavior. Its reviewed evidence is stale because the dependency-injection task changed `PieceEditorWindow.cs` after that review.
  - `production/tasks/TASK-20260924-224656127-e361499a-board-art-dependency-injection/contract.md`: released `PieceEditorWindow.cs`; its preservation list named the level-piece members that this task changes after the release.
  - `production/tasks/TASK-20260924-191842745-88e64a0d-board-kit/contract.md`: board-kit parent; owns `BoardKitTests` and the 11-record decision.
  - `docs/art/board-kit/README.md` (Level Editor link; "Rotation-equivalent and mirrored pieces share one base record").
  - PROJECT.md decision row "Typed board authoring and shared palette"; the authoring-window naming rule (show human names, keep enum IDs internal).
- Unity version/platform: Unity 2022.3.62f2, Windows Editor; editor-only code in the `ColorBlockJam.Editor` assembly; no player impact.
- Package/architecture constraints: UI Toolkit editor window; no new package and no new file; internal API only, within `ColorBlockJam.Editor` and its existing test access; static editor events follow the existing pattern (each subscriber names its owner and unsubscribes in `OnDisable`); library writes go through the unchanged `ShapeLibraryAuthoring.Commit` and `ShapeLibraryTransaction`; no `System.Linq`; match the surrounding code's style.
- Explicit out of scope:
  - All Level Editor changes: subscribing to `LevelPieceChanged`, calling `TryAddPieces` from Save Level, naming "+" pieces, relabel offers and level JSON writes (the consumer task).
  - Removing or changing Save as template.
  - Enforcing one record per rotation or mirror family in `TryAddPieces` (Save piece does not enforce it either); the Level Editor maps a footprint that matches a library piece before it saves.
  - Changing the 11-record pins in `BoardKitTests` (board-kit task decision; recommended: pin the 11 base identities instead of the exact count, because Save Level will add templates to the active database).
  - A `ShowLibraryPiece(typeName, focus)` overload (not requested).

## Verification and risk

- Focused test path/command: EditMode `ColorBlockJam.Tests.EditMode.PieceEditorLinkTests` (`Assets/ColorBlockJam/Tests/EditMode/PieceEditorLinkTests.cs`), then `BoardKitTests`, `ShapeLibraryJsonTests` and `ShapePrefabTests`; through MCP for Unity `run_tests` on the live Editor (HTTP 8082, after `mcpforunity://editor/state` shows no running test, compile or import), or `Unity.exe -batchmode -runTests -testPlatform EditMode` with `-testFilter` set to those class names, `-projectPath` set to an isolated %TEMP% project copy and `-logFile` inside that copy
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: `ColorBlockJam.Editor` and the EditMode test assembly compile with no error and no new warning from the owned files (live Editor Console or the isolated copy's log)
- EditMode: the focused suites above. AC-8's saved-library path (new enum identities, prefabs, piece JSON, bindings, one enum import) runs only in an isolated %TEMP% project copy through a copy-only test or `-executeMethod`, or is accepted through the user's own Save Level check once the Level Editor calls `TryAddPieces`.
- PlayMode: not applicable (editor tooling only)
- Manual/built-player/profiler/device evidence: AC-7's focus check in the live Editor (`EditorWindow.focusedWindow` before and after `ShowLevelPiece(request, false)` through MCP `execute_code`, or the user's own check); built-player, profiler and device evidence: not applicable
- Risk: medium (cross-window internal API: a new static event, a request field, a focus overload and a batch library writer that another window calls). Each successful `TryAddPieces` with new identities writes prefabs, `BlockShapeLibrary.asset`, the piece JSON and `BlockShapeType.cs`; only the designer's own Save Level click runs it, and that click is the approval for the write, as with Save as template.
- Approval already granted (exact target only): none. The user's relayed redesign sets the product intent; one plan approval from the user in this conversation is required before the first write.
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260925-185129336-2df1d5a7-piece-editor-live-level-sync after the user's plan approval, then an independent verifier; at handoff, send the final API to the Level Editor session "Parça template boş kareler"

## Stop conditions

- The user has not approved the plan in this conversation: write nothing.
- The Level Editor session asks for a different event or batch API shape (for example derived TypeNames): reconcile before writing that part.
- The work needs a change to `ShapeLibraryTransaction.cs`, `ShapePrefabAuthoring.cs`, `BoardKitAssets.cs`, `Editor/Levels/**`, a Level test or any other path outside Allowed paths: stop with CONTRACT_CHANGE_REQUIRED.
- A test or a step would write the live project's piece JSON, `BlockShapeLibrary.asset`, shape prefabs or `BlockShapeType.cs`: stop.
- The live Editor is compiling, importing or running another session's tests: wait, or use an isolated copy.
- At closure `LevelPieceApplied` is still referenced outside `Editor/BoardKit`: keep AC-10 open and wait for the Level Editor session; do not delete the event and do not edit that session's files.
- A cross-window undo mismatch appears that only the Level Editor can fix (for example its handler closes the undo group): report it to that session and do not change its code.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:285fc53c3236d6830a344f3ff05085ca7ec8badcc8ff166b6bd7c9c684b77fba
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: after the independent verification copy was taken, another session edited the owned PieceEditorWindow.cs (22:51:34) and PieceEditorLinkTests.cs (22:52:10) with no recorded contract. It was session "Grid highlight ve tools menüsü", found by a transcript search for SetLevelTool; the three Level Editor sessions each said no, and its user request has been asked for. The edit wires the Level Editor's Tools choice into the Piece Editor grid through SetLevelTool and TransformPiece, adds hover outlines, shortens the status text and removes the Rotate 90° button, which this contract lists as preserved behavior. The current sources compile, but the verified evidence no longer covers these bytes.
- Next action: None
- Closed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity (unreviewed; workspace has pending changes)
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Level-piece mode has no Apply to level control | STALE | automated: PieceEditorLinkTests on the handoff bytes (live, implementer copy and verifier copy); stale after the 22:51 foreign edit | none |
| AC-2 Grid edits raise LevelPieceChanged once, never empty, no library writes | STALE | automated: GridEditsRaiseLevelPieceChangedOnceAndWriteNoLibraryData, LibraryModeEditsRaiseNothing, ThrowingListenerDoesNotStopOtherListeners on the handoff bytes; stale (rotate now through TransformPiece) | none |
| AC-3 Listener undo joins the grid undo group | STALE | automated: ListenerUndoJoinsTheStrokeUndoStep on the handoff bytes; stale | none |
| AC-4 Committed Name edits raise LevelPieceChanged | STALE | automated: CommittedNameEditSendsTheNewName on the handoff bytes; stale | none |
| AC-5 Request name shown and serialized; equal request changes nothing | STALE | automated: LevelPieceModeSurvivesAGuiRebuildAndIsSerialized, EqualRequestKeepsTheDraftAndItsUndoHistory on the handoff bytes; stale | none |
| AC-6 No discard question in level-piece mode | STALE | automated: LevelPieceModeNeverAsksToDiscard on the handoff bytes; stale | none |
| AC-7 ShowLevelPiece(request, false) keeps the focus | UNPROVEN | automated routing test passed in both copies; visible focus not checked live because the user's Piece Editor is open; needs the user's check | none |
| AC-8 TryAddPieces adds a batch in one Commit | STALE | automated tests plus copy-only saved-library checks by the implementer and the verifier (identical output); ShapeLibraryAuthoring.cs is unchanged since review, but the test file changed | none |
| AC-9 TryAddPieces refusal writes nothing; equal entries skipped | STALE | automated refusal matrix plus copy changed-on-disk, read-only and compiling refusals; Play Mode refusal by review; stale with the review set | none |
| AC-10 LevelPieceApplied deleted after the consumer switch | PASS | automated: grep of Assets shows no reference; declaration and pragma deleted; out-of-Editor build and live compile clean; live reflection LevelPieceApplied=False; live job ae2bb3df167c4be9a753ea571c570faf 100/100 with 0 failures (including LevelPlusTileTests) | none |
| AC-11 Preserved behavior and focused suites green, no live library writes | STALE | automated: EditMode 74/74 in two isolated copies on the handoff bytes; live library bytes unchanged; the foreign edit removed Rotate 90°, a preserved behavior | none |
| AC-12 Board-kit README and authoring skill updated | STALE | review by the verifier; README Level Editor link still names Rotate 90° grid undo semantics that the foreign edit changed | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's redesign relayed by the Level Editor session "Parça template boş kareler" (2026-09-25) and that session's Piece Editor API request; PieceEditorWindow.cs released by TASK-20260924-224656127-e361499a-board-art-dependency-injection; the batch API takes explicit TypeNames instead of the requested derived names (reason under Expected behavior)
- in_progress — implementation — the user approved the plan with "devam et" in this conversation (2026-09-25); the Level Editor session accepted the API shape; lifecycle serialized manually in this conversation (no Plastic lock service, no cross-terminal claim); preflight READY WITH WARNINGS: base now cs:9 with the owned files unchanged, the Unity CLI sees only BRN-Game-2 (version gate), MCP 8082 serves two projects so every call pins ColorBlockJamCase2026@99077e4df1ecfaa4, and the Console holds Editor/Levels compile errors from another session's in-flight edit
- in_progress — verification — implementation handed off to an independent verifier: live compile clean, live PieceEditorLinkTests 17 passed and 1 self-skipped (user Piece Editor open), isolated-copy EditMode 74/74, copy-only saved-library TryAddPieces check as expected, live library bytes unchanged (evidence.md); final signatures sent to the Level Editor session; AC-10 waits for its LevelPieceApplied switch
- in_progress — verification — independent verifier (fresh copy cbj-verify-1cadb2f8, taken before 22:51) returned "VERIFY: PASS (10/12)" with no defects: EditMode 74/74, saved-library check byte-identical, live library files unchanged, AC-7 visible focus and AC-10 open by design. At 22:51-22:52, before this finalization, another session edited the owned PieceEditorWindow.cs and PieceEditorLinkTests.cs (Level Editor Tools drive the grid, Rotate 90° removed) without a recorded contract; the passing evidence is stale for the current bytes, and the edit's writer and the user's intent were asked. Verdict CONTRACT_CHANGE_REQUIRED
- in_progress — verification — the writer was session "Grid highlight ve tools menüsü" (a direct user request, no contract). Its files were PieceEditorWindow.cs, PieceEditorLinkTests.cs, BoardKitTests.cs and a SetLevelTool call in LevelDatabaseWindow.cs; it has stopped and routes further Piece Editor changes here. The user's words were relayed verbatim by that session and by "Parça template boş kareler". Live re-run on the combined current bytes (job 3f2e0e10605f418094eb5902a52c9d6b): 74 total, 73 passed, 1 skipped (focus test, user Piece Editor open). LevelDatabaseWindow.cs has switched to LevelPieceChanged; only LevelPlusTileTests.cs still references LevelPieceApplied
- in_progress — verification — AC-10: the Level Editor session reported its tests switched to LevelPieceChanged (511/511 Level EditMode live). The grep of Assets found only the declaration, so LevelPieceApplied and its CS0067 pragma were deleted. Out-of-Editor and live compiles are clean. The live assembly has no LevelPieceApplied. Live job ae2bb3df167c4be9a753ea571c570faf (the four board-kit classes plus LevelPlusTileTests): 100/100, 0 failures
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
