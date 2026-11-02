# Evidence — TASK-20260925-185129336-2df1d5a7-piece-editor-live-level-sync

## Implementer (2026-09-25, session "Database oluşturma popup")

Preflight: READY WITH WARNINGS. Base cs:9, and the owned files were unchanged against it. The Unity CLI sees only
BRN-Game-2 (the 2022.3 version gate). MCP for Unity HTTP 8082 serves two projects, so every call pins
`ColorBlockJamCase2026@99077e4df1ecfaa4`. At start the Console held stale Editor/Levels compile errors from
another session's in-flight edit; the out-of-Editor harness compiled the current sources cleanly.
Domain workflow: unity-game-dev.

### Changes

- `Editor/BoardKit/LevelPieceRequest.cs`: an optional `name` constructor parameter and a serialized `Name`
  (empty when not given).
- `Editor/BoardKit/PieceEditorWindow.cs`:
  - The Apply to level path is removed (`SaveOrApply`, `ApplyToLevel`, `k_applyTooltip`, and the "Apply to level"
    text and status). `save-piece` is hidden in level-piece mode.
  - `LevelPieceChanged` is sent from `SendLevelPiece` after a changed stroke end, Rotate, Clear (empty grid: not
    sent, with a status message), grid undo/redo, and a committed Name edit (Enter or FocusOut). A blank name is
    not sent. The event is raised before `Undo.IncrementCurrentGroup`, followed by `Undo.FlushUndoRecordObjects`,
    so a listener's `RecordObject` joins the stroke's group. Each listener runs in its own try/catch.
  - `LoadLevelPiece` shows the request name ("New Piece" when empty). An equal request (ids, normalized cells,
    name) is a no-op. `CanDiscardDraft` never asks in level-piece mode.
  - `ShowLevelPiece(request, bool focus)` passes the flag to `GetWindow`; the one-argument form forwards with true.
  - `LevelPieceApplied` stays declared and never raised (CS0067 suppressed with a comment) until the Level Editor
    switches (AC-10).
- `Editor/BoardKit/ShapeLibraryAuthoring.cs`:
  - `TryAddPieces` checks Play Mode and compilation first, then commits every new record in one `Commit` and turns
    authoring exceptions into `false` plus an issue.
  - `AddRecords` is the pure builder. It checks the TypeName (`IsTypeName`), the display name, duplicates in the
    batch, cells and the composer; an equal existing record is skipped; a compiled unused name keeps its ID; other
    new names get consecutive IDs.
  - The composer check is extracted to `ValidateFootprint`, which `Validate` also uses.
- `Tests/EditMode/PieceEditorLinkTests.cs`: the level-piece tests are updated for the new behavior, and tests are
  added for grid and name sync, the undo group, a throwing listener, the echo guard, the no-prompt rule, the focus
  overload, and `TryAddPieces`/`AddRecords`.
- `docs/art/board-kit/README.md` (Level Editor link) and `.agents/skills/board-piece-authoring/SKILL.md`.

### Checks

- Out-of-Editor harness (`scratchpad/harness/build.sh all`): `ColorBlockJam.Editor` and the EditMode tests build.
  A full rebuild shows no warning from the owned files. The remaining warnings are pre-existing CS0649 warnings in
  `Editor/Boosters/VacuumMotionAssets.cs`.
- Live Editor refresh and compile, about 22:24: 0 Console errors or warnings.
- Live `PieceEditorLinkTests`, job `232f4ba7a35c44e89b9d6ad7bd9a9f85`: 17/18 passed. `CommittedNameEditSendsTheNewName`
  failed because a synthetic `KeyDownEvent` goes to the panel's focused element, not to the event target (probe:
  trickle=0 unfocused, 1 after `Focus()`). Test fix: focus the field first, as a designer would; the production
  wiring is unchanged.
- Live `PieceEditorLinkTests`, job `85bac6d22f8a4a80b20cd22bd4ca9c76`: 17 passed and 1 skipped
  (`ShowLevelPieceWithoutFocusUpdatesTheOpenWindow` ignores itself because the user's own Piece Editor is open).
- Isolated copy `C:\Users\e-fur\AppData\Local\Temp\cbj-live-sync-455a9888` (robocopy of the live project at about 22:32,
  batchmode, graphics device on), `-runTests -testPlatform EditMode -testFilter
  "PieceEditorLinkTests|BoardKitTests|ShapeLibraryJsonTests|ShapePrefabTests"`: 74/74 passed. The counts were
  BoardKitTests 21, PieceEditorLinkTests 18 (including the focus-overload test), ShapeLibraryJsonTests 18 and
  ShapePrefabTests 17.
- Saved-library check in the same copy (copy-only `TryAddPiecesCopyCheck.Run` through `-executeMethod`, exit 0).
  Output, verbatim:

```text
before: records=11 compiling=False
changed-on-disk: returned=False issue=Piece data changed on disk. Reload the piece data before saving: Assets/ColorBlockJam/Data/Board/PieceDatabases/BlockShapeLibrary.json jsonKept=True enumKept=True prefabs=0 commits=0
read-only: returned=False issue=The authoring file is read-only: Assets/ColorBlockJam/Data/Board/PieceDatabases/BlockShapeLibrary.json jsonKept=True enumKept=True prefabs=0 commits=0
success: returned=True issue= commits=1 records 11->13 enumChanged=True
  record id=31 type=CopyCheckHook name=Copy Check Hook cells=(0,0)(1,0)(2,0)(2,1) prefab=Assets/ColorBlockJam/Prefabs/Board/Shapes/Piece_031.prefab enumLine=True
  record id=32 type=CopyCheckStep name=Copy Check Step cells=(0,0)(1,0)(1,1)(2,1)(2,2) prefab=Assets/ColorBlockJam/Prefabs/Board/Shapes/Piece_032.prefab enumLine=True
retry: returned=False issue=Wait for compilation before adding pieces to the library. commits=1 jsonKept=True compiling=True
undo: jsonRecords=11 libraryRecords=11 commits=2
```

  The retry ran while the enum import compiled, so it was refused as specified. The skip-on-retry path is covered
  by the in-memory test. After the check, that copy's enum reserves IDs 31 and 32 and has `Piece_031` and
  `Piece_032`, so it is no longer a clean test baseline.
- Live project: `BlockShapeLibrary.json` sha256 `c4661086…a439` and `BlockShapeType.cs` sha256 `6ac62f9b…6521`
  match the copy's pre-check hashes. There is no `Piece_031` or `Piece_032` in the live project.

### Acceptance status (implementer view)

- AC-1 to AC-6, AC-8, AC-9 and AC-11: automated evidence above. Play Mode and compilation refusals are checked in
  code before `Load`. The compilation refusal was observed in the copy's retry; the Play Mode refusal is by code
  review only.
- AC-7: in the copy, the routing test confirms the same instance and no second window. The one-argument form still
  converts to `Action<LevelPieceRequest>`. The flag passes straight to `GetWindow(title, focus)`. The visible focus
  behavior still needs a live check while no user Piece Editor is open, or user acceptance.
- AC-10: open. `LevelPieceApplied` is declared and never raised; it is deleted after the Level Editor session drops
  its references (LevelDatabaseWindow.cs 191/244, LevelPlusTileTests.cs 706/714).
- AC-12: README and skill updated. Neither mentions Apply to level.

## Verifier (2026-09-25, independent session)

Preflight equivalent: read the contract, evidence.md, and got a fresh `cm status` of the workspace before touching
anything. Baseline `cs:9` copies of the four owned files were pulled with `cm cat ...#cs:9 --file=`.

### Containment (diff vs cs:9)

Diffed the four owned files and both docs sections against `cs:9`; every change matches the implementer's "Changes"
list and the contract's Expected behavior text almost verbatim (Apply-to-level path removed, `LevelPieceChanged`
added and wired from `EndPaint`/`ClearGrid`/`RotateGrid`/`OnUndoRedo`/`CommitName`, `IsShownLevelPiece`/
`IsSameFootprint` echo guard, `ShowLevelPiece(request, bool focus)` overload, `TryAddPieces`/`AddRecords`/
`GetNextUnusedId` added to `ShapeLibraryAuthoring.cs`, `Validate` refactored to call the extracted
`ValidateFootprint` with unchanged logic, `LevelPieceRequest.Name`). No `System.Linq` in the four files; all new
members are `internal`.

`cm status` shows pending changes in the forbidden `BoardKitAssets.cs` and `BoardKitTests.cs` (and other README
sections outside "Level Editor link", e.g. Arrow visuals / Palette), but diffing them against `cs:9` shows only a
`MAT_Board` -> `MAT_Gameplay` rename and a wall/corner seam-calibration change (play-area-environment D-001) —
unrelated to this task and consistent with the contract's own note that another session (the DI task) holds a
pending `BoardKitAssets.cs` edit and that other README sections may be edited elsewhere. `BlockShapeLibrary.asset`
shows Plastic status "Checked-out (unchanged)" (no byte difference). `BlockShapeType.cs` and the piece JSON have
zero pending changes. `BlockShapeLibraryJson.cs` (source of `Equal`, used by `AddRecords`) has zero pending changes,
confirming `Equal` is a pre-existing method this task only calls. No file outside the allowed list was written by
this task.

### Independent test run

Fresh isolated copy via `robocopy` (excluding Temp/Logs/obj/.plastic/Library\Bee/.vs/tmp), NOT the implementer's
already-mutated `cbj-live-sync-455a9888`: `C:\Users\e-fur\AppData\Local\Temp\cbj-verify-1cadb2f8` (54,860 files,
2.322 GB, robocopy exit 1 = success). Deleted `Library\EditorInstance.json`, then ran:

`Unity.exe -batchmode -runTests -testPlatform EditMode -testFilter "PieceEditorLinkTests|BoardKitTests|ShapeLibraryJsonTests|ShapePrefabTests" -testResults <copy>\results.xml -projectPath <copy> -logFile <copy>\tests.log`

Exit code 0. Parsed `C:\Users\e-fur\AppData\Local\Temp\cbj-verify-1cadb2f8\results.xml`: **74/74 passed, 0 failed, 0
skipped, 0 inconclusive** — `BoardKitTests` 21/21, `PieceEditorLinkTests` 18/18 (including
`ShowLevelPieceWithoutFocusUpdatesTheOpenWindow`, which ran to completion rather than skipping, since this fresh
copy had zero Piece Editor windows open), `ShapeLibraryJsonTests` 18/18, `ShapePrefabTests` 17/17. Matches the
implementer's reported counts exactly. `tests.log` shows only the same pre-existing `CS0618` warning in
`Editor/Boosters/VacuumMotionAssets.cs` — no error or new warning from the owned files.

### AC-8/AC-9 saved-library path (independent reproduction)

In the same copy (project already warm, so no re-robocopy needed), copied `TryAddPiecesCopyCheck.cs` into
`Assets/ColorBlockJam/Editor/BoardKit/`, deleted `Library\EditorInstance.json` again, and ran
`Unity.exe -batchmode -projectPath <copy> -executeMethod ColorBlockJam.Editor.BoardKit.TryAddPiecesCopyCheck.Run -logFile <copy>\copycheck.log`
(exit 0, fast incremental compile, same single pre-existing warning). `TryAddPiecesCopyCheck.txt` output is
byte-identical to the implementer's, reproduced independently in a separately-created copy:

```text
before: records=11 compiling=False
changed-on-disk: returned=False issue=Piece data changed on disk. Reload the piece data before saving: ... jsonKept=True enumKept=True prefabs=0 commits=0
read-only: returned=False issue=The authoring file is read-only: ... jsonKept=True enumKept=True prefabs=0 commits=0
success: returned=True issue= commits=1 records 11->13 enumChanged=True
  record id=31 type=CopyCheckHook ... prefab=.../Piece_031.prefab enumLine=True
  record id=32 type=CopyCheckStep ... prefab=.../Piece_032.prefab enumLine=True
retry: returned=False issue=Wait for compilation before adding pieces to the library. commits=1 jsonKept=True compiling=True
undo: jsonRecords=11 libraryRecords=11 commits=2
```

Afterward, independently hashed the **live** project files: `BlockShapeLibrary.json` sha256
`c4661086572b591374de7774b72b97960fb6dd75748f7bbcf3bfadb67f13a439` and `BlockShapeType.cs` sha256
`6ac62f9bc58d0a2e8ab5f2f2bb0c5a586a09279e523122bf131b3f8560d06521` — both match the implementer's reported hashes
exactly. Confirmed no `Piece_031`/`Piece_032` prefab, no `CopyCheckHook`/`CopyCheckStep` reference anywhere under
the live `Assets/`, and `TryAddPiecesCopyCheck.cs` itself is absent from the live repository. Neither copy folder
was deleted.

### AC-7 visible focus (live Editor)

Read-only `mcpforunity://editor/state` on `ColorBlockJamCase2026@99077e4df1ecfaa4` showed idle/not-compiling/no
tests running. Per the contract's gate, ran a non-mutating reflection check only (`Resources.FindObjectsOfTypeAll`
count, no window opened or changed):
`PieceEditorWindowCount=1`. Since a Piece Editor is open live, `ShowLevelPiece(request, false)` was correctly **not**
called against the live Editor (it would have changed the user's window). AC-7's visible-focus claim stays
UNPROVEN via the live channel, same conclusion the implementer reached; the automated, non-focus part of AC-7
(same instance, no second window, content updates) is proven by
`ShowLevelPieceWithoutFocusUpdatesTheOpenWindow`, which passed outright in the isolated copy.

### Per-AC verdicts

| AC | Verdict | Channel |
|---|---|---|
| AC-1 | PASS | Code review of `SyncFields`/`SetStatus` vs cs:9 diff + `LevelPieceRequestOpensLevelPieceModeWithoutApplyToLevel`, `LibraryPieceRequestLoadsThatPiece` (independent run) |
| AC-2 | PASS | Code review of `SendLevelPiece`/`EndPaint`/`ClearGrid`/`RotateGrid`/`OnUndoRedo` + `GridEditsRaiseLevelPieceChangedOnceAndWriteNoLibraryData`, `LibraryModeEditsRaiseNothing`, `ThrowingListenerDoesNotStopOtherListeners` (independent run) |
| AC-3 | PASS | Code review of undo-group ordering (`RecordGridChange` -> `SendLevelPiece` -> `Undo.IncrementCurrentGroup`) + `ListenerUndoJoinsTheStrokeUndoStep` (independent run) |
| AC-4 | PASS | Code review of `CommitName`/`OnNameKeyDown` + `CommittedNameEditSendsTheNewName` (independent run) |
| AC-5 | PASS | Code review of `IsShownLevelPiece`/`IsSameFootprint`/`LoadLevelPiece` + `LevelPieceModeSurvivesAGuiRebuildAndIsSerialized`, `EqualRequestKeepsTheDraftAndItsUndoHistory` (independent run) |
| AC-6 | PASS | Code review of `CanDiscardDraft` (unchanged non-level-piece branch) + `LevelPieceModeNeverAsksToDiscard` (independent run) |
| AC-7 | UNPROVEN (visible focus); automated part PASS | Live editor idle, but `PieceEditorWindowCount=1` (a user window is open), so per contract the live focus call was not made; `ShowLevelPieceWithoutFocusUpdatesTheOpenWindow` passed outright in the isolated copy |
| AC-8 | PASS | In-memory batch test (`TryAddPiecesAddsTheBatchInOneCommitAndOneUndoStep`, independent run) + independently reproduced saved-library `TryAddPiecesCopyCheck` output (byte-identical to implementer's, separate copy) |
| AC-9 | PASS | Refusal test (`TryAddPiecesRefusesAnInvalidBatchAndWritesNothing`, `AddRecordsKeepsCompiledIdsAndNumbersNewTypesInInputOrder`, independent run) + independently reproduced changed-on-disk/read-only/compiling refusals in the copy check |
| AC-10 | UNPROVEN/pending by design | `LevelPieceApplied` confirmed still referenced in `LevelDatabaseWindow.cs` and `LevelPlusTileTests.cs` (grep across `Assets/`); correctly left declared/unraised, not deleted |
| AC-11 | PASS | 74/74 independent EditMode run (see above); live piece JSON/enum byte-identical before and after all verification; containment diff above |
| AC-12 | PASS | Diff of README "Level Editor link" section and SKILL.md's level-only-piece paragraph vs cs:9; both describe live sync, name link, focus overload, `TryAddPieces`, and that only Save Level/Save as template writes the library; neither mentions "Apply to level" |

### Defects

None found. No FAIL verdicts.

**VERIFY: PASS (10/12)** — 10 of 12 acceptance criteria independently confirmed passing; AC-7 (visible-focus half)
and AC-10 remain UNPROVEN/pending by design, exactly as the contract anticipates (AC-10 waits on the Level Editor
session dropping its reference; AC-7's live focus check is blocked by a currently-open user Piece Editor window).
Result files: `C:\Users\e-fur\AppData\Local\Temp\cbj-verify-1cadb2f8\results.xml`,
`C:\Users\e-fur\AppData\Local\Temp\cbj-verify-1cadb2f8\TryAddPiecesCopyCheck.txt`,
`C:\Users\e-fur\AppData\Local\Temp\cbj-verify-1cadb2f8\tests.log`,
`C:\Users\e-fur\AppData\Local\Temp\cbj-verify-1cadb2f8\copycheck.log` (copy retained, not deleted).

## Implementer follow-up (2026-09-25, after the verifier)

### Foreign edit in owned files

- At 22:51:34 `PieceEditorWindow.cs` changed, and at 22:52:10 `PieceEditorLinkTests.cs` changed. This came after the
  verifier's copy was taken, so the verifier checked the handoff bytes.
- The writer was the session "Grid highlight ve tools menüsü", found through a transcript search for `SetLevelTool`.
  "Parça template boş kareler", "Revize seçim ve frame davranışı" and "Düzenle ve sil ikonları" each said no.
- The user asked that session directly, with no contract. The relayed words, verbatim: "buradan rotate 90 derece
  kaldır ve üstteki açıklamayı kısaltıp sadeleştir ve üzerinde olduğumuz grid highlight olabilir ve level editörde
  bulunan tollar buna da etki etsin sadece tools menüsü".
- Its files:
  - `PieceEditorWindow.cs`: `s_levelTool`/`SetLevelTool`, `TransformPiece` with `LevelPieceTools`, hover outlines,
    Rotate 90° removed, shorter status text.
  - `PieceEditorLinkTests.cs`: rotate clicks became `TransformPiece`; the info assertion became "Level piece" +
    "live".
  - `BoardKitTests.cs`.
  - `LevelDatabaseWindow.cs`: a `SetLevelTool` call.
- That session has stopped and routes further Piece Editor changes here. `TransformPiece` still ends in
  `SendLevelPiece`, so rotate and mirror keep raising `LevelPieceChanged`.
- Out-of-Editor build of the current sources: `ColorBlockJam.Editor` and the EditMode tests build.
- Live re-run on the combined bytes (my handoff plus the foreign edit), job `3f2e0e10605f418094eb5902a52c9d6b`,
  PieceEditorLink/BoardKit/ShapeLibraryJson/ShapePrefab: 74 total, 73 passed, 1 skipped
  (`ShowLevelPieceWithoutFocusUpdatesTheOpenWindow`, user Piece Editor open).
- Taking the edit into this contract (Rotate 90° no longer preserved) waits for the user's confirmation in this
  conversation.

### AC-10: LevelPieceApplied deleted

- "Parça template boş kareler" reported that the Level Editor no longer references `LevelPieceApplied`.
  `LevelDatabaseWindow.cs` subscribes to `LevelPieceChanged` at lines 234 and 286, and `LevelPlusTileTests.cs` uses
  `OnLevelPieceChanged`. That session reported 511/511 Level EditMode tests live.
- Grep of `Assets` before the deletion: the only hit was the declaration in `PieceEditorWindow.cs`.
- The declaration, its comment and the CS0067 pragma were deleted. A full out-of-Editor rebuild shows no error and no
  warning from the owned files.
- Live refresh and compile: no compiler error. Reflection on the live assembly: `LevelPieceChanged=True;
  LevelPieceApplied=False`.
- Live run, job `ae2bb3df167c4be9a753ea571c570faf`, PieceEditorLink/BoardKit/ShapeLibraryJson/ShapePrefab plus
  LevelPlusTileTests: status succeeded, 100/100 completed, 0 failures. The tool returned no per-test summary this
  time; the focus test self-skips while the user's Piece Editor is open.
