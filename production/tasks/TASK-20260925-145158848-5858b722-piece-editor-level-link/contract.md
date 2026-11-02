# TASK-20260925-145158848-5858b722-piece-editor-level-link — Piece Editor: link API and level-piece mode for Level Editor pieces

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-145158848-5858b722-piece-editor-level-link
- Supersedes: none
- Title: Piece Editor: link API and level-piece mode for Level Editor pieces
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7
- Task kind: feature
- Area: Piece authoring (Piece Editor window in `Editor/BoardKit`, Piece Editor side of the Level Editor link)
- Workflow: direct

## Objective and baseline

- Observable objective: The Level Editor can open the Piece Editor on a library piece or on a piece that belongs only to the open level. In level-piece mode the designer draws the piece, applies it back to the level without touching the library, or saves it as a library template. Every successful library commit is announced so both windows stay current.
- Current behavior/evidence:
  - User request (verbatim, relayed on 2026-09-25 by the Level Editor session "Parça template boş kareler"): "burada artı işareti olsun bu sadece o levele özel bir parça eklememizi sağlar ve istersek bunu kaydıp template yazabiliriz ve block shape library eklenir bunu da burada 2. resimde tıkladığımız parçayı solda piece editörde seçerek yapabiliriz ikisi birbirine bağlı olur böylece kaydetme kısmı orada yapılabilir tabi parça eğer soldaki editörden editlenirse sağ tarafta çakışma olursa uyarmalı bu gibi şeylere dikkat et".
  - The Level Editor side is `TASK-20260925-082521498-8e54a900-level-editor-custom-pieces` (draft; it waits on this task). It records the adopted decisions D1-A to D7-A. D5-A: Save as template writes to the active piece database. D4-A: after Save as template, the Level Editor relabels matching `Custom_` blocks itself. D7-A: a level piece has at most 25 cells inside a 5x5 box, and the Level Editor validates this.
  - `PieceEditorWindow` (`Editor/BoardKit/PieceEditorWindow.cs`) exposes only `public static void Open()`. `LoadPiece`, `NewPiece` and `Save` are private.
  - Its cached `m_loadedJson` and `m_shapes` are refreshed only by its own Save, Delete, database selection or Reload. A commit from elsewhere (a Level Editor palette delete, `PieceDatabaseFiles.SetActive` from another window) leaves the cache stale, and the next Save is refused as "changed on disk". A library undo replay does the same.
  - Every library write goes through `ShapeLibraryAuthoring.Commit` (`Editor/BoardKit/ShapeLibraryAuthoring.cs`). This covers Save, Delete, Import/Merge, Reload, SyncPrefabs, `PieceDatabaseFiles.SetActive` and the library undo replay (`ShapeLibraryUndoState.Replay` calls `Commit` with `recordUndo: false`).
  - Saving a new library piece appends a `BlockShapeType` value and imports `BlockShapeType.cs`, which reloads the domain. State that must survive Save as template therefore has to live in serialized window fields.
  - Precedent for an editor-only static event: DEC-102 (`LevelDatabaseWindow.DraftLevelPublished`).
  - Both windows compile into `ColorBlockJam.Editor`, and `EditorAssemblyInfo.cs` grants `InternalsVisibleTo` to the EditMode tests, so `internal` members suffice.
- Expected behavior:
  1. **Library link.** `internal static void ShowLibraryPiece(string typeName)` on `PieceEditorWindow`:
     - It opens or focuses the Piece Editor and asks the existing unsaved-draft question (`CanDiscardDraft`). "Keep editing" changes nothing.
     - Otherwise it leaves level-piece mode, if active, and loads the piece whose `TypeName` equals `typeName` from the active piece database.
     - An unknown name keeps the current draft and shows a status message naming the piece and the database.
  2. **Level-piece request.** A new `[Serializable]` `LevelPieceRequest` in `Editor/BoardKit/LevelPieceRequest.cs` carries the level id, the piece id and the piece cells. It is copied on construction, so no caller can change it later.
  3. **Level-piece mode.** `internal static void ShowLevelPiece(LevelPieceRequest request)`:
     - It opens or focuses the Piece Editor, asks the unsaved-draft question and then enters level-piece mode. An empty request starts from a one-cell draft, like New piece; otherwise the request's cells are loaded.
     - The mode, the request and the draft live in serialized window fields, so they survive a domain reload.
     - The status line names the level and the piece being edited.
     - The Piece library dropdown shows no library selection, and Delete selected piece is disabled.
     - Choosing a library piece, New piece or `ShowLibraryPiece` leaves the mode, after the unsaved-draft question.
  4. **Apply to level.** In level-piece mode the Save button reads "Apply to level":
     - It raises `internal static event Action<LevelPieceRequest> LevelPieceApplied` once, with the same level id and piece id and the draft's normalized occupied cells.
     - It keeps the draft and the mode and marks the draft clean.
     - It writes no library data: the piece JSON, `BlockShapeLibrary.asset`, `BlockShapeType.cs` and the shape prefabs stay byte-identical.
     - An empty grid is refused with the existing "Paint at least one cell" message. Footprint limits and conflict refusals belong to the Level Editor, which subscribes to the event.
  5. **Library commits announced.** `internal static event Action LibraryCommitted` is declared on `ShapeLibraryAuthoring`:
     - It is raised exactly once at the end of every successful `Commit`, so Save, Delete, Import/Merge, Reload, SyncPrefabs, `SetActive` and library undo/redo replay are all covered. A `Commit` that throws and rolls back raises nothing.
     - `PieceEditorWindow` subscribes in `OnEnable` and unsubscribes in `OnDisable`. On the event it re-reads the active library's JSON and piece list and keeps the current draft and the selection (by `TypeName` when the piece still exists). Its next Save is then accepted after a commit from another window.
  6. **Save as template.** Level-piece mode shows a "Save as template" button:
     - It runs the existing new-piece Save into the active piece database (D5-A): name rules, `BlockShapeType` ID allocation, footprint validation, and the `ShapeLibraryAuthoring.Commit` transaction with its rollback.
     - On success `LibraryCommitted` is raised by `Commit`, the window leaves level-piece mode and shows the new library piece.
     - On failure it stays in level-piece mode with the draft and shows the message.
     - Only the designer's own click runs it. No agent or test runs it in the live project.
- Behavior that must remain unchanged:
  - Library-mode Piece Editor behavior: New piece, Save piece, Delete selected piece (including the D-005 fix, a Delete that does not wait for the enum compile), Clear grid, Rotate 90°, grid undo and redo, keyboard shortcuts, and the database "+" popup and "×".
  - `PieceEditorWindow.Open()` and the Color Block Jam > Piece Editor menu item.
  - The `ShapeLibraryAuthoring.Commit` transaction semantics: validation, rollback, stale-disk refusal, the enum import only for new identities, and undo recording.
  - The existing BoardKitTests, ShapeLibraryJsonTests, ShapePrefabTests and DatabaseControlsTests.

## Ownership

- Owner/writer role: Piece Editor owner session "Database oluşturma popup" (board-kit area), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/BoardKit/PieceEditorWindow.cs
  - Assets/ColorBlockJam/Editor/BoardKit/LevelPieceRequest.cs
  - Assets/ColorBlockJam/Editor/BoardKit/LevelPieceRequest.cs.meta
  - Assets/ColorBlockJam/Editor/BoardKit/ShapeLibraryAuthoring.cs
  - Assets/ColorBlockJam/Tests/EditMode/PieceEditorLinkTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/PieceEditorLinkTests.cs.meta
  - docs/art/board-kit/README.md
  - .agents/skills/board-piece-authoring/SKILL.md
  - production/tasks/TASK-20260925-145158848-5858b722-piece-editor-level-link/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Editor/Levels/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Editor/Controls/**
  - Assets/ColorBlockJam/Editor/BoardKit/ShapePrefabAuthoring.cs
  - Assets/ColorBlockJam/Editor/BoardKit/ShapeLibraryTransaction.cs
  - Assets/ColorBlockJam/Editor/BoardKit/ShapeLibraryUndoState.cs
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Scenes/**
  - Clicking or invoking Save piece, Delete selected piece, Save as template or any library write in the live project during implementation or verification (end-to-end library writes run only in an isolated project copy)
  - Changing `ShapeLibraryAuthoring.cs` beyond declaring and raising `LibraryCommitted`
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked:
  - `TASK-20260925-002022060-4c9dac46-block-runtime-objects` (session "Ruled tile level sistem") holds `ShapeLibraryAuthoring.cs` and `ShapePrefabAuthoring.cs`, released to it at 2026-09-25T09:21Z.
  - The Level Editor sessions hold `Editor/Levels/**`. The custom-pieces task consumes this API.
  - `PieceEditorWindow.cs`, `FieldActionsRow.cs` and `ActionIcons.cs` were last changed between 14:07 and 14:13 local by a session with no task contract (icon buttons). They have been quiet since.
  - `TASK-20260924-191842745-88e64a0d-board-kit` is waiting for a data cleanup and the block-runtime-objects handoff.
- Execution dependency: TASK-20260925-002022060-4c9dac46-block-runtime-objects hands off `ShapeLibraryAuthoring.cs` (lifecycle handoff recorded, or its session "Ruled tile level sistem" sends an explicit release for that file)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a saved library piece, when `ShowLibraryPiece` is called with its `TypeName`, then the Piece Editor is open and focused with that piece loaded. With an unsaved draft the unsaved-draft question appears first, and "Keep editing" changes nothing. An unknown `TypeName` keeps the draft and shows a message.
2. Given `ShowLevelPiece` with an empty request, then level-piece mode starts from a one-cell draft. With cells, those cells are loaded. The status names the level and the piece, the button reads "Apply to level", "Save as template" is visible and Delete selected piece is disabled. After a domain reload the mode, the request and the draft are unchanged.
3. Given level-piece mode with a drawn piece, when Apply to level is pressed, then `LevelPieceApplied` is raised exactly once with the request's level id and piece id and the normalized drawn cells. The draft stays. The piece JSON, `BlockShapeLibrary.asset`, `BlockShapeType.cs` and the shape prefabs are byte-identical before and after. An empty grid is refused with a message and raises nothing.
4. Given any successful library commit (Save, Delete, Import/Merge, Reload, SyncPrefabs, database selection, library undo or redo), then `LibraryCommitted` is raised exactly once. A commit that fails and rolls back raises nothing.
5. Given an open Piece Editor whose database was changed by a commit from another window, when the designer saves in the Piece Editor, then the save succeeds instead of being refused as changed on disk.
6. Given level-piece mode in an isolated project copy, when Save as template runs with a valid unique name, then the active piece database gains the record through the existing transaction, `LibraryCommitted` is raised, and the window shows the new library piece. With an invalid or taken name nothing is written and the mode and draft stay.
7. Given the change, then the library-mode behavior listed under behavior that must remain unchanged still works, and the existing BoardKitTests, ShapeLibraryJsonTests, ShapePrefabTests and DatabaseControlsTests pass.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - production/tasks/TASK-20260925-082521498-8e54a900-level-editor-custom-pieces/contract.md (consumer; decisions D1-A to D7-A)
  - DEC-102 in PROJECT.md (editor-only static event precedent)
  - docs/art/board-kit/README.md (Piece Editor section)
  - .agents/skills/board-piece-authoring/SKILL.md
  - production/tasks/TASK-20260924-191842745-88e64a0d-board-kit/contract.md (parent area; D-005 Delete behavior)
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - UI Toolkit, in the window's existing inline style.
  - `internal` API inside `ColorBlockJam.Editor`, with no new assembly and no package.
  - Static events follow the DEC-102 pattern: every subscriber names its stop point and unsubscribes in `OnDisable`.
  - No Level Editor types are referenced from `Editor/BoardKit`.
- Explicit out of scope:
  - Everything on the Level Editor side: the "+" tile, the Custom palette section, `Custom_` ids, 5x5 and 25-cell validation, conflict warnings, Re-stamp and relabelling.
  - A level schema change.
  - Runtime shape learning.
  - Changes to `ShapeLibraryTransaction` or `ShapePrefabAuthoring`.

## Verification and risk

- Focused test path/command: Assets/ColorBlockJam/Tests/EditMode/PieceEditorLinkTests.cs through Unity 2022.3.62f2 `-batchmode -runTests -testPlatform EditMode -testFilter "PieceEditorLinkTests|BoardKitTests|ShapeLibraryJsonTests|ShapePrefabTests|DatabaseControlsTests"` in an isolated project copy
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: an out-of-Editor compile of `ColorBlockJam.Editor` and its EditMode tests, then a clean live Editor compile with 0 Console errors
- EditMode:
  - The new PieceEditorLinkTests cover ShowLibraryPiece, ShowLevelPiece, the domain-reload field round trip, Apply to level (event count and cells), LibraryCommitted (raised once on success, not on failure), and the stale-cache Save after an external commit.
  - Save as template is tested end to end only in the isolated copy.
  - The existing board and database-control suites stay green.
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - In the live Editor, open level-piece mode through `ShowLevelPiece`, check the labels, capture the window, and close it without any Save.
  - User acceptance of the level-piece look.
- Risk: medium (new cross-window editor API and static events). Each Save as template run writes a prefab, `BlockShapeLibrary.asset`, the piece JSON and `BlockShapeType.cs`. It runs only on the designer's own click.
- Approval already granted (exact target only): none. One user approval of this API plan is required before the first production write (medium risk).
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /game-studio-orchestration TASK-20260925-002022060-4c9dac46-block-runtime-objects TASK-20260925-145158848-5858b722-piece-editor-level-link after the user's plan approval, then an independent verifier. At handoff, send the final API to the session "Parça template boş kareler" for the custom-pieces task.

## Stop conditions

- The user has not approved the API plan: make no production write.
- `ShapeLibraryAuthoring.cs` is still held by block-runtime-objects: do not edit it; wait for its release.
- `PieceEditorWindow.cs` or `ShapeLibraryAuthoring.cs` changed since it was read: re-read it and reapply only this task's change.
- Any step would need `Editor/Levels/**`, `Data/**`, `Prefabs/**`, `ShapeLibraryTransaction.cs`, `ShapePrefabAuthoring.cs` or a library write in the live project: stop and request a contract change.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:ebfc171932747265788487a804eaa27492533697bb570adf26903fa0daf6285f
- Evidence fingerprint: sha256:bc9cec639d42e325df223a414fcca81144c19ee8ea824cda77e36a1e72d15c1e
- Reviewed revision: vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a
- Attempt count: 1
- Last feedback: User approved the API plan with "devam et" (2026-09-25).
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a
- Closed at: 2026-09-28T08:47:26Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 ShowLibraryPiece opens and loads a library piece | PASS | EditMode LibraryPieceRequestLoadsThatPiece, UnknownLibraryPieceKeepsTheDraftAndSaysWhy (isolated copy 502/502) | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a |
| AC-2 ShowLevelPiece level-piece mode survives a domain reload | PASS | EditMode LevelPieceRequestOpensLevelPieceMode, LevelPieceModeSurvivesAGuiRebuildAndIsSerialized, ChoosingNewPieceLeavesLevelPieceMode; live capture level-piece-mode.png | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a |
| AC-3 Apply to level raises LevelPieceApplied once and writes nothing | PASS | EditMode ApplyToLevelRaisesTheEventOnceAndWritesNoLibraryData (SHA-256 of piece JSON, BlockShapeLibrary.asset and BlockShapeType.cs unchanged) | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a |
| AC-4 LibraryCommitted once per successful commit, never on failure | PASS | EditMode LibraryCommittedIsRaisedOncePerSuccessfulCommitIncludingUndo; independent verifier traced every Commit caller | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a |
| AC-5 Piece Editor Save accepted after an external commit | PASS | EditMode CommitNoticeRefreshesTheCachedLibraryJsonAndKeepsTheDraft; isolated-copy end-to-end AC-5 PASS (external delete 11 -> 10, Piece Editor Save accepted) | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a |
| AC-6 Save as template through the existing transaction (isolated copy) | PASS | Isolated-copy end-to-end AC-6 PASS (shapes 11 -> 12, one commit notice, enum LinkTemplateTest, library mode) and AC-6a PASS (blank name refused, mode kept) | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a |
| AC-7 Library-mode behavior and existing suites preserved | PASS | Isolated copy: ColorBlockJam.Editor.EditModeTests + LevelPreview.Tests 502/502, BoardKit/ShapeLibraryJson/ShapePrefab/DatabaseControls included; live Editor compile 0 errors | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af047fcdc17cd326ba4acfe777bee6b8ee84a02454347e11b11a57ac7b4b032a |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-25T15:40Z — READY_TO_CLOSE — the API, level-piece mode, Apply to level, Save as template and the LibraryCommitted cache refresh are implemented. Isolated copy: 502/502 Editor EditMode (8 new PieceEditorLinkTests); copy-only end-to-end AC-5 and AC-6 PASS; the live Editor loads the API with 0 compile errors; live capture done. Independent verifier: VERIFY: PASS, with three non-blocking notes (a dropdown re-select is a no-op, a pre-GUI oversize message is dropped, and reserved ID 30 comes from the user's deleted test piece). Digest: sha256 over "path sha256(bytes)
" for the allowed paths, with the managed block excluded from contract.md.
- 2026-09-25T14:58Z — in_progress — the user's "devam et" approved the API plan and authorized execution. "Ruled tile level sistem" released ShapeLibraryAuthoring.cs, which met the dependency. The direct lane runs in this conversation as the only lifecycle writer under the Plastic exception (no Git lock service).
- Created — planning — contract authored from the user's "+" level-piece request relayed by the Level Editor session; waits on the block-runtime-objects release of ShapeLibraryAuthoring.cs and one user approval of the API plan
- 2026-09-28T08:47:26Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
