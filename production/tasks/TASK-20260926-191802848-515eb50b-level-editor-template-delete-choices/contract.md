# TASK-20260926-191802848-515eb50b-level-editor-template-delete-choices — Template delete asks Make Custom, Delete Blocks or Cancel

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-191802848-515eb50b-level-editor-template-delete-choices
- Supersedes: none
- Title: Template delete asks Make Custom, Delete Blocks or Cancel
- Repository (absolute path): C:\Users\e-fur\Documents\Repos\ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: Plastic workspace /main@ColorBlockJamCase2026 (single shared workspace, no worktree)
- Base revision: cs:10 plus pending local changes of several Level Editor sessions (planning provenance only)
- Task kind: feature
- Area: Level Editor and Piece Editor template (piece library) delete
- Workflow: trio (implementer, independent verifier, bugfixer on findings), sequential

## Objective and baseline

- Observable objective: deleting a piece-library template that blocks of the active level database use asks the designer to make those blocks Custom, delete them, or cancel, from both delete buttons, and applies the answer to every level of the active database before the template is deleted.
- Current behavior/evidence:
  - The Level Editor palette tile × (`LevelShapePalette.ShapeDeleteRequested` to `LevelDatabaseWindow.DeleteShape`) counts the active database's blocks with `CountBlocksUsing`. It asks one Delete/Cancel question through the `ConfirmPieceDelete` seam, then calls the undoable `ShapeLibraryAuthoring.Delete`. Blocks keep the deleted TypeName.
  - The Piece Editor "Delete selected piece" (`PieceEditorWindow.DeletePiece`) shows a hard-coded Delete/Cancel dialog with no usage count and no pending-template guard. It commits a shorter record list through `SaveRecords`.
  - After either delete, each block that used the piece gets a `LibraryDeleted` warning ("uses X, which is no longer in the piece library"). The AC-9 banner counts these blocks but offers no action (evidence: workflow `wf_f4d9479e-60f`, reader "level-delete").
  - A session applied record whose id vanished resurfaces as a raw-TypeName Custom tile.
  - Validation facts:
    - An emptied level fails `Validate` ("Level {L} needs at least one block."), and a database with no level fails ("Level database has no levels.").
    - A removed block that another block's effect names breaks that effect's reference rule.
    - In the shipped database, deleting Square would empty level-01, and deleting TripleHorizontal would empty level-02.
- Expected behavior: see Acceptance scenarios. In short:
  - **Make Custom** relabels each using block to its library-family or `Custom_` id, keeping cells, colour, origin and effects.
  - **Delete Blocks** removes the using blocks and deletes every level left with no block. The question names those levels beforehand.
  - **Cancel** changes nothing.
  - The choice applies to every level of the active database: the open level's saved record and its undoable draft, plus the other levels, in one atomic database write. The template is deleted only after that write succeeds.
- Behavior that must remain unchanged:
  - A delete that no block uses (or an unreadable database) keeps today's two-button question and texts.
  - The pending-template-edit refusal (DEC-125) stays.
  - The Custom tile × (`DeleteCustomPiece`) and its tests stay.
  - Library deletes keep enum identities and prefabs reserved.
  - Piece Editor library changes other than a delete keep D2-A (a warning and a manual Re-stamp).
  - A clean open draft stays clean; unsaved draft edits and their undo history survive a Make Custom.
  - Level JSON uses no `TypeNameHandling`.
  - No live project data file is written by tests.

## Ownership

- Owner/writer role: Level Editor session "Parça template boş kareler" (implementer agent, then an independent verifier), sequential
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelPieceDeleteRoute.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelPieceDeleteRoute.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelPieceDeletePlan.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelPieceDeletePlan.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelPieceSession.cs
  - Assets/ColorBlockJam/Editor/BoardKit/PieceEditorWindow.cs (the `DeletePiece` delegate seam only)
  - Assets/ColorBlockJam/Tests/EditMode/LevelPieceDelete*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelPieceDelete*Tests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasBlockToolsTests.cs (seam guards on the existing unused-piece delete test only)
  - docs/Level-Editor-Design.md (template delete section and the D2-A note only)
  - PROJECT.md (one DEC-126 decision row only)
  - production/tasks/TASK-20260926-191802848-515eb50b-level-editor-template-delete-choices/**
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
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Editor/BoardKit/ShapeLibraryAuthoring.cs
  - Assets/ColorBlockJam/Editor/BoardKit/ShapeLibraryTransaction.cs
  - Assets/ColorBlockJam/Editor/BoardKit/ShapeLibraryUndoState.cs
  - Any other file under Assets/ColorBlockJam/Editor/BoardKit/** and any `PieceEditorWindow.cs` change beyond the `DeletePiece` seam
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditCommands.cs (call `TryRemoveBlock` only; owned by staircase task 224a7aef while its lock is held)
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasModel.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasElement.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelBoardResize.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCornerSteps.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasCornerVoidsTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCornerStepsTests.cs
  - Writing the live `Data/Levels/*.json`, `Data/Board/PieceDatabases/*.json`, `BlockShapeLibrary.asset`, shape prefabs or `BlockShapeType.cs` from a test; tests use in-memory databases and fake seams, guarded by a SHA-256 check of the live level and piece JSON
  - Newtonsoft `TypeNameHandling`
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked:
  - The staircase task `TASK-20260926-181022947-224a7aef-level-canvas-staircase-corners` (session "Rotate tool rotation behavior") holds its lifecycle lock. Its files are listed as forbidden above. `docs/Level-Editor-Design.md` is shared, so edit it only after that session's "LEVEL EDITOR PATHS FREE", or in a disjoint section after announcing it.
  - `PieceEditorWindow.cs` belongs to the board-kit session "Database oluşturma popup" (offline). Its task `TASK-20260925-185129336-2df1d5a7-piece-editor-live-level-sync` is in_progress with no lifecycle lock.
    - The user's order "İkisi birden şimdi" (2026-09-26) authorizes the one `DeletePiece` seam now.
    - That session is notified before the write. If it turns live and is writing `PieceEditorWindow.cs`, wait for its release.
  - Session "Edit dropdown ve colors kareleri" sent "PATHS FREE" for the Level Editor on 2026-09-26.
  - Announce TAKE of `LevelDatabaseWindow.cs` to live Level Editor sessions before the first write, and send "LEVEL EDITOR PATHS FREE" after.
- Execution dependency: none (the paths are disjoint from the lock holder 224a7aef; the shared design doc waits as noted above)
- Queue policy: none

## Acceptance scenarios

1. Given a template no block of the active database uses (or an unreadable database), when its × is clicked in the Level Editor, then today's two-button question and texts appear through `ConfirmPieceDelete`, and the existing undoable delete runs. Given the same in the Piece Editor, then its current question and delete run.
2. Given a template that blocks of the active database use, counted in the open draft and the saved levels by exact `shapeId`, when its × is clicked, then one three-choice question appears through a new `ChoosePieceDelete` seam. The default is `EditorUtility.DisplayDialogComplex("Delete Piece", message, "Make Custom", "Cancel", "Delete Blocks")`: Enter picks Make Custom, and Esc or the close box picks Cancel. The message names:
   - the piece and TypeName;
   - the block count per level, the open level marked;
   - under the heading "These levels will be deleted because no block would remain:", every level Delete Blocks would delete, with an added line when the open level has unsaved changes that would be lost;
   - the runtime caveat that exists today;
   - each other level database's count, marked unchanged.
3. Given option limits, then only possible options are offered and the message says why the other is missing:
   - Make Custom is impossible when a using block's cells get no id (over 5 x 5 or 25 cells, or disconnected).
   - Delete Blocks is impossible when:
     - a remaining block's effect names a removed block;
     - the database would be left with no level;
     - a level to be written would fail `Validate` for another reason.
   - With one option the question has two buttons: that option and Cancel. With none, the status explains it and nothing changes.
4. Given Make Custom, then each using block gets `LevelCustomPiece.TryGetShapeId` against the library without the deleted template and without any TypeName that has a pending template edit (a library family's TypeName, else its `Custom_` id). Its cells, colour, origin and effects stay. This happens in:
   - every level of the active database;
   - the open level's saved record, in one atomic database write through the `WriteLevels`/`SaveLevels` path, whose backup keeps the pre-delete JSON;
   - the open draft, as one undo step "Make Blocks Custom". The dirty baseline moves with it, so a clean draft stays clean, and unsaved edits and their undo history survive.
5. Given Delete Blocks, then every using block is removed from every level of the active database, and a level whose written record has no block left is deleted from the database, in one atomic database write.
   - If the open level stays, its draft loses the same blocks as one undo step, "Delete Piece Blocks".
   - If the open level is deleted, the window selects the nearest remaining level, as Delete Level does.
6. Given any accepted choice:
   - The order is: pre-checks before the question, then the level write, then the draft step, then the library delete through the undoable `ShapeLibraryAuthoring.Delete`, then the catalog, palette, tool and level-choice refresh.
   - The pre-checks are Play Mode, library loaded and current, writable files, and no stale prefab issue for the remaining records.
   - A failed level write changes nothing and names the error.
   - A failed library delete leaves the written levels valid, and the status says to delete again.
   - Cancel changes nothing.
   - The status names what changed, including the deleted levels.
7. Given the Piece Editor's "Delete selected piece" for a library piece that blocks of the active database use, then the same question and behavior apply through one `internal static Func<string, bool>` delegate seam on `PieceEditorWindow`. The seam is registered by an `[InitializeOnLoad]` class in `Editor/Levels`, `LevelPieceDeleteRoute`, and adds no new BoardKit reference to Level Editor types.
   - With a Level Editor window open, its draft-aware flow runs.
   - With none open, the same plan runs on the active level database's saved JSON.
   - The Piece Editor then shows no deleted piece selected.
   - The pending-template-edit refusal applies on both paths.
8. Given a library commit that removes a TypeName, from any path including undo/redo, then session applied records and named pieces whose id vanished take `TryGetShapeId` of their cells against the new catalog. No raw-TypeName Custom tile appears, and the Piece Editor level-piece link follows the new id.
9. Given the change, then the tests cover:
   - the pure plan (every option, limits, pending-template twin, emptied levels, database left empty, effect references);
   - the window flow with an in-memory database and fake seams (Cancel, both options, clean and dirty drafts, open level deleted, failed write, failed library delete, two Undos);
   - the Piece Editor seam, both with and without a Level Editor window.

   The existing unused-piece test keeps passing, with `Assert.Fail` guards on `WriteLevels` and `ChoosePieceDelete`, and live level and piece JSON hashes are unchanged after the tests.
10. Given the documentation, then:
    - `docs/Level-Editor-Design.md` describes template delete;
    - `PROJECT.md` has one DEC-126 row. It records that this flow supersedes D2-A/AC-9 of task 8e54a900 for template deletes in the active database; changed-footprint warnings and the manual Re-stamp stay.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - The user's request, 2026-09-26, session "Parça template boş kareler": "bir template'i sildiğimde leveldeki parçalar customa düşebilir ve template silmeye tıkladığımızda diğer levelerden sil veya custom yap olanlarda veya iptal gibi bir seçenek çıkabilir eğer varsa".
  - The user's answers, 2026-09-26:
    - On emptied levels: "leveli de sil ve uyarı olarak da bu leveler silinecektir block kalmayacağından dolayı diye belirt".
    - On the Piece Editor delete: "İkisi birden şimdi".
  - Read-only design workflow `wf_f4d9479e-60f`: five evidence readers, two designs, and an adversarial critique whose synthesis this contract adopts. The one exception: the user chose to delete emptied levels, where the critique recommended refusing.
  - Tasks `TASK-20260925-082521498-8e54a900-level-editor-custom-pieces` (D2-A, AC-2, AC-5, AC-9) and `TASK-20260926-140520462-2196c1d1-level-editor-pen-edit-templates` (DEC-125 atomic database write and pending template edits).
  - Decision rows DEC-119, DEC-124 and DEC-125.
- Unity version/platform: 2022.3.62f2, Editor authoring only
- Package/architecture constraints:
  - A plain C# `LevelPieceDeletePlan` in `Editor/Levels/Canvas` with EditMode tests.
  - Dialogs through `EditorUtility` seams only.
  - One undo group per designer action on the draft.
  - Dependency direction: Levels to BoardKit. The BoardKit seam is a plain delegate and references no Level type.
  - No new package or asmdef.
  - ZString or plain concatenation is fine in editor text; no `System.Linq` in new code.
- Explicit out of scope:
  - Rewriting level databases other than the active one. They are counted and named as unchanged.
  - A banner action for blocks that already name a deleted TypeName.
  - A custom UI Toolkit modal instead of `DisplayDialogComplex`.
  - Changing the Custom tile ×.
  - Changing `ShapeLibraryAuthoring`, `ShapeLibraryTransaction` or `ShapeLibraryUndoState`.
  - The runtime shape catalog wiring (DEC-124, still open).

## Verification and risk

- Focused test path/command: new `LevelPieceDeletePlanTests` and `LevelPieceDeleteTests` (EditMode, `ColorBlockJam.Tests.EditMode`), run through the live Editor's MCP `run_tests`, pinned to the instance, with the whole `ColorBlockJam.Tests.EditMode.Level` group plus `PieceEditorLink` and `BoardKit` tests
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and no new Console errors in the live Editor (MCP for Unity HTTP 127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`)
- EditMode: the focused tests above plus every Level, PieceEditorLink and BoardKit test green; live level and piece JSON SHA-256 unchanged
- PlayMode: not applicable (editor tooling)
- Manual/built-player/profiler/device evidence:
  - V-1: the user deletes a used template from the Level Editor × and from the Piece Editor, tries Make Custom, Delete Blocks with an emptied level, and Cancel, then accepts the dialog texts and results.
  - The Windows Enter, Esc and close-box behavior of the three-button dialog is part of V-1.
- Risk: medium (database-wide writes that can delete levels; a new cross-window seam)
- Approval already granted (exact target only): the user's 2026-09-26 request and answers, for the three-choice flow on both delete buttons, deleting levels left with no block, and the `PieceEditorWindow.DeletePiece` seam
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: `$implement-task TASK-20260926-191802848-515eb50b-level-editor-template-delete-choices`, then an independent verifier

## Stop conditions

- A needed change falls outside the allowed paths, including any BoardKit edit beyond the `DeletePiece` seam, or `LevelEditCommands.cs` while 224a7aef holds its lock: stop and record it.
- A live session is writing `LevelDatabaseWindow.cs` or `PieceEditorWindow.cs` (edited within the last 10 minutes by someone else, or a TAKE message): wait for its release.
- A test would write live project data: stop.
- The library delete cannot run after a successful level write in a way that leaves a level invalid: stop and report.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:3cff3c63d505a0a246bcfbb33019f5364e888f319af829f6f34839040fcc6af6
- Evidence fingerprint: sha256:eec5b2357a3673306ab5afb996ddab9bdcbfafc4e4224f4a357ae1136b8c5e54
- Reviewed revision: vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Unused template keeps today's delete on both buttons | PASS | automated: LevelPieceDeleteTests.AnUnusedPieceKeepsTheTwoButtonDelete, route unused/unreadable cases, LevelCanvasBlockToolsTests.TileDeleteAsksFirstThenDeletesThroughTheLibraryTransaction (in-memory database, Assert.Fail seam guards); Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-2 Used template asks the three-choice question with counts, emptied-level warning, caveat and other databases | PASS | automated: LevelPieceDeletePlanTests.TheQuestionNamesPieceCountsCaveatAndOtherDatabases, DeletingTheOpenLevelWithUnsavedChangesSaysTheyWillBeLost; DisplayDialogComplex("Delete Piece", msg, "Make Custom", "Cancel", "Delete Blocks") through the ChoosePieceDelete seam; Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-3 Only possible options offered, with the reason for a missing one | PASS | automated: LevelPieceDeletePlanTests (learning limits, open draft id, no level would remain, effect names a removed block, neither option refusal status); Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-4 Make Custom relabels in every level, saved open record and draft as one undo step, baseline kept | PASS | automated: LevelPieceDeletePlanTests (own-cell ids, effects kept, prefab twin preferred, pending template twin falls back to Custom_), LevelPieceDeleteTests (one write, clean draft stays clean, unsaved edits stay unsaved); Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-5 Delete Blocks removes blocks and deletes emptied levels in one database write | PASS | automated: LevelPieceDeletePlanTests.DeleteBlocksRemovesTheBlocksAndDeletesEveryLevelLeftEmpty, LevelPieceDeleteTests (emptied level deleted, open level deleted opens the nearest remaining level); Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-6 Pre-checks, write-then-draft-then-library order, failure and Cancel handling | PASS | automated: LevelPieceDeleteTests (failed write changes nothing, written-but-not-reloaded status, failed library delete keeps written levels, Cancel changes nothing, two Undos); Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-7 Piece Editor delete runs the same flow through the delegate seam, with and without a Level Editor | PASS | automated: LevelPieceDeleteTests (route with and without a Level Editor, every outcome's status, pending refusal on both paths, Piece Editor HelpBox shows the returned status); deviation within intent: the seam is Func<string, string> (null = not handled, else the status to show) instead of Func<string, bool>, because a bool cannot tell Cancel from a refusal; still one plain delegate, no Level type in BoardKit; Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-8 Session records of a removed TypeName follow the new id on any library commit | PASS | automated: LevelPieceDeletePlanTests.SessionPiecesOfADroppedTypeNameTakeTheIdOfTheirOwnCells (RenameVanished on every library commit); Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-9 Plan, window and seam tests; live data hashes unchanged | PASS | automated: 30 LevelPieceDelete* tests; live Data/Levels and PieceDatabases JSON, BlockShapeLibrary.asset and BlockShapeType.cs SHA-256 unchanged across both verifier runs; Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| AC-10 Design doc and DEC-126 row | PASS | manual review by the verifier: docs/Level-Editor-Design.md 'Template delete (DEC-126)' and the PROJECT.md DEC-126 row match the code; Level EditMode group 2026-09-26 (live Editor, MCP HTTP, ColorBlockJamCase2026@99077e4df1ecfaa4): 689/689 (job 44bc8385); PieceEditorLink|BoardKit 38 passed + 1 self-ignored (job 46122314); clean compile; independent verifier round 1: VERIFY: PASS (Level 687/687, PieceEditorLink+BoardKit 38/39 + 1 self-ignored, live hashes unchanged) with 4 minor findings; fix round and independent re-verify: VERIFY: PASS (4/4 findings fixed; Level 689/689, PieceEditorLink+BoardKit 38/39 + 1 self-ignored; clean compile; live data hashes unchanged) | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |
| V-1 User checks both delete buttons and all three choices in the Editor | UNPROVEN | manual: the user deletes a used template from the Level Editor tile x and from the Piece Editor, tries Make Custom, Delete Blocks with an emptied level (e.g. Square empties level-01) and Cancel, and checks Enter/Esc/close on the dialog; pending | vcs:cs:7+owned-sha256:9901964555331e54b6e5a24e94110a3f2ea46970220e770f9bab6599c7713c8e |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's 2026-09-26 template delete request and answers (emptied levels deleted with a warning; Piece Editor delete included now), design workflow wf_f4d9479e-60f
- Updated — implementation — 2026-09-26T19:21Z: execution authorized by the user's "İkisi birden şimdi"; unity-preflight READY WITH WARNINGS (live Editor idle over MCP HTTP, Unity CLI cannot reach 2022.3); lifecycle lock held; queued because LevelDatabaseWindow.cs was written by another session at 19:19:17 UTC
- Verification — 2026-09-26T20:32Z: implemented through workflow wf_27a98ce6-354 and fix workflow wf_8c3f97e4-3cc; VERIFY: PASS twice; recorded deviations: the Piece Editor seam is Func<string, string>, the Cancel status reads 'Deleted nothing: the delete was cancelled.', a use only in the open unsaved draft changes the draft without a level write, and the window-less path reports refusals through a dialog and the Piece Editor status; V-1 awaits the user
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
