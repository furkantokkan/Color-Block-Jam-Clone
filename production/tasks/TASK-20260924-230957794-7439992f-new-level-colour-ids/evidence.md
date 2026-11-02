# Evidence: TASK-20260924-230957794-7439992f-new-level-colour-ids

Date: 2026-09-25. Implementer: single writer (workflow subagent). No checkin or shelve.

## Preconditions

- `LevelDatabaseWindow.cs` was last modified at 02:24 (other writer idle); it still held
  the two `"red"` literals at lines 683 and 687, so the stop condition "other writer already
  changed the literals" did not apply.
- Pre-edit sha256: `47f66084cab75d0166a0b8aa5ca6fe88401c45894dc7f244f8ea77867d431311`.

## Change

- `Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs`
  - The starter level literal moved out of `CreateLevel` into
    `internal static LevelData CreateStarterLevel(string id, string name)` (the "extracted
    seed" AC-1 names), so an EditMode test can reach it without writing a level JSON.
  - Door `d1` and block `b1` colour ids: `"red"` -> `"Red"` (the `PaletteColor.Red` name).
  - Ids, name, 3 x 3 size, 60 s timer, `Single` shape, origin (1,1), selection, dialogs and
    save flow are unchanged; `CreateLevel` passes the same id and `Level {n}` name.
  - Post-edit sha256: `ab7570f2cd3fb2bd78d0a99d5d6c1e57f62345c86639dcc9f4f1dddf7527d6ad`.
- `Assets/ColorBlockJam/Tests/EditMode/LevelCreateColourTests.cs` (+ `.meta`, guid
  `adbbb3882bba49c39365e4dc7008d230`), in `ColorBlockJam.Editor.EditModeTests`:
  - `StarterLevelDoorAndBlockColoursParseAsRed`: both ids parse with
    `BoardLayout.TryParseDoorColor` to `PaletteColor.Red`.
  - `StarterLevelBuildsABoardLayoutAndValidates`: `BoardLayout.TryCreate` succeeds and
    `LevelData.Validate` passes.
  - `StarterLevelKeepsItsIdNameSizeTimerAndStarterShape`: preservation of id, name, size,
    timer, `d1`, `b1`, `Single`.
  - sha256: `3db4cf33ef9c5760fe2469e430e77b28a8d5d67d9ed1ff97d61d898e4830c1b6`.

## Live Editor (MCP for Unity, HTTP 8082)

- 06:2x and 06:55: `mcpforunity://instances` lists `ColorBlockJamCase2026@99077e4df1ecfaa4`,
  but `execute_code` / `read_console` return
  `Unity session not ready for 'read_console' (ping not answered)`; once the instance was
  briefly missing. No live compile, Console read or test run was possible.

## Isolated-copy verification (fallback)

- Copy: `%TEMP%\CBJcolourids-38e209` (robocopy of the repo incl. Library, excluding
  Temp/Logs/obj/.plastic/Library/Bee; 6 locked `.vs` index files skipped; copy's
  `Library/EditorInstance.json` deleted). Nothing copied back except this evidence.
- Command:
  `Unity.exe -batchmode -projectPath <copy> -runTests -testPlatform EditMode
  -assemblyNames "ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests;ColorBlockJam.Environment.EditModeTests"
  -testResults <copy>\results.xml -logFile <copy>\test.log` (06:56-06:59, exit 0).
- Compile: 0 `error CS`; the only warning is the pre-existing
  `VacuumMotionAssets.cs(54,13) CS0618 TextureImporter.spritesheet` (unrelated).
- Results: 172 total, 172 passed, 0 failed, 0 skipped
  (Editor.EditModeTests 103/103, Editor.LevelPreview.Tests 9/9,
  Environment.EditModeTests 60/60). The three `LevelCreateColourTests` cases passed.
  Copy of the report: `editmode-results-isolated.xml` in this folder.

## Acceptance status

- AC-1 New level colours parse: PASS (EditMode, isolated copy, current file hashes above).
- AC-2 New level previews without warning: UNPROVEN-pending-live-check. Steps once the live
  Editor answers MCP (or for the user):
  1. Recompile the live Editor; Console shows no new errors.
  2. Open the LevelEditor scene additively (or the Level Editor window), create a scratch
     database with Create Database, click "+" (Create Level).
  3. Expect the new level in the dropdown, the preview board showing a red door on the top
     edge and a red single block at (1,1), and no colour/`BoardLayout` warning in the Console.
  4. Delete the scratch database JSON and its Addressables entry afterwards, and reselect the
     previous active database.

## Independent verification (verifier, 2026-09-25 07:02-07:10)

Verifier did not edit production code or tests. It added only this section and
`editmode-results-verifier.xml` (sha256 `24f0f6e0...c1cc68c8`).

### Live Editor

- 07:02: `mcpforunity://instances` listed `ColorBlockJamCase2026@99077e4df1ecfaa4`
  (connected_at 04:02:08Z). `execute_code` returned `Instance ... not found. Available: none`;
  three `read_console` retries (07:03-07:04) returned
  `Unity session not ready for 'read_console' (ping not answered)` or `not found`.
- The live `Editor.log` shows MCP WebSocket receive errors after the last domain reload.
  The live `Library/ScriptAssemblies/ColorBlockJam.Editor.dll` is dated 04:34:54 and
  `ColorBlockJam.Editor.EditModeTests.dll` 04:35:27, both before this task's edits (06:19).
  The live Editor has **not** compiled this change yet. The verifier did not touch the live
  Editor, its scenes, or Play Mode.

### Files and scope

- Current sha256 values match the implementer's record:
  - `LevelDatabaseWindow.cs` `ab7570f2...27d6ad` (mtime 06:19:06).
  - `LevelCreateColourTests.cs` `3db4cf33...30c1b6` (mtime 06:19:25); `.meta` guid
    `adbbb3882bba49c39365e4dc7008d230` (06:19:37).
- `grep` of `LevelDatabaseWindow.cs`: no `"red"` literal remains. `"Red"` appears only at
  lines 702 (door `d1`) and 706 (block `b1`) inside `internal static CreateStarterLevel`.
  `CreateLevel` (line 681) calls `CreateStarterLevel(id, $"Level {number - 1}")`. The
  extracted body keeps id, name, 3 x 3 size, 60 s timer, `d1` Top 1..1, `b1` `Single` at
  (1,1) with footprint `(0,0)`.
- The `CreateLevel` hunk matches the earlier `LevelDatabaseWindow.before.cs` scratch copy
  (01:33) apart from the two literals and the extraction. The window's other hunks in that
  diff are the other writer's (01:55-02:24) preview-publish and stepper work, not this task.
- Repo files modified since 06:00 that this task can own: only the three allowed Assets files
  and this task folder. The other files modified in that window belong to other live
  sessions' tasks: board camera, board look, drag gate, the Gameplay/Domain split,
  `ProjectSettings/QualitySettings.asset`, and the scenes. No file outside the allowed
  paths is attributable to this task.

### Isolated-copy EditMode run (fresh copy, independent of the implementer's)

- Copy: `%TEMP%\CBJverify-f747c1` (robocopy of the repo including Library, excluding
  Temp/Logs/obj/.plastic/Library/Bee/.vs; `Library/EditorInstance.json` deleted). Its two
  source hashes match the repo. Nothing was copied back except the results XML.
- Command: `Unity.exe -batchmode -projectPath <copy> -runTests -testPlatform EditMode
  -assemblyNames "ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests;ColorBlockJam.Environment.EditModeTests"
  -testResults <copy>\results.xml -logFile <copy>\test.log`. Exit code 0 at 07:09.
- Compile: 0 `error CS`. The only warning is the pre-existing
  `VacuumMotionAssets.cs(54,13) CS0618`.
- Result: 172/172 passed (Editor.EditModeTests 103/103, Editor.LevelPreview.Tests 9/9,
  Environment.EditModeTests 60/60). All three `LevelCreateColourTests` passed:
  - `StarterLevelDoorAndBlockColoursParseAsRed`
  - `StarterLevelBuildsABoardLayoutAndValidates`
  - `StarterLevelKeepsItsIdNameSizeTimerAndStarterShape`

### Verdict

- AC-1: **PASS**. Verified by the EditMode run in the independent isolated copy, on the
  seed that `CreateLevel` calls. MCP `run_tests` was not possible because the live Editor
  did not answer.
- AC-2: **UNPROVEN, pending the live check**. It needs the live Editor or the user's
  acceptance. Run steps 1-4 above once MCP answers or the Editor is restarted. Also confirm
  that the live Console is clean after the live recompile.

## Implementer follow-up after verification (2026-09-25 07:10-07:19)

- No code or test change. The verifier reported no FAIL and no out-of-scope change; AC-1 is
  PASS. AC-2 is left UNPROVEN because only the live Editor or the user can close it.
- File hashes are unchanged since verification: `LevelDatabaseWindow.cs` `ab7570f2...27d6ad`,
  `LevelCreateColourTests.cs` `3db4cf33...30c1b6`.
- Live Editor retry: at 07:10 `mcpforunity://instances` listed the instance with a new
  `connected_at` 04:10:24Z. Every call from 07:11 to 07:18 (11 calls: `execute_code`,
  `read_console`) returned `success: false` or `Instance ... not found. Available: none`.
  The live `Editor.log` ends with `WebSocketTransportClient.SendJsonAsync` send exceptions.
  The live `ColorBlockJam.Editor.dll` is still dated 04:34:54, so the Editor has not compiled
  this change. Nothing in the live Editor was touched.
- How AC-2 is covered without the live check: `LevelPreviewHost.TryShow`
  (`LevelPreviewHost.cs:80-89`) rejects a level only through `LevelData.Validate`,
  `BoardLayout.TryCreate`, and `LevelPreviewBlockMapper.TryMap`. `TryMap` rejects on an unknown
  shape, a missing prefab, or a colour that fails `BoardLayout.TryParseDoorColor`
  (`LevelPreviewBlockMapper.cs:62`). The AC-1 tests prove the starter level passes
  `Validate`, `TryCreate` and `TryParseDoorColor` for both ids, so no colour warning can
  come from the preview for it. The `Single` shape and prefab gates did not change in this
  task. A headless `TryShow` test would have to go in `ColorBlockJam.Editor.LevelPreview.Tests`,
  which is outside this contract's allowed paths, so none was added.
- To close AC-2: run steps 1-4 of the implementer's AC-2 list once MCP answers, or have the
  user accept it. To do it by hand: open Color Block Jam > Level Editor, click "+" on a scratch
  database, and check that the red door and block show with no Console warning.

## Independent verification, second pass (verifier, 2026-09-25 07:19-07:28)

The verifier edited no production code or tests. It added only this section and three
evidence files in this folder:
- `editmode-results-verifier2.xml` (sha256 `f8b52cef...cdcd37c`)
- `preview-probe-results-verifier2.xml` (sha256 `24f344f3...6ed6ef2ff5`)
- `VerifierStarterLevelPreviewProbe.cs.txt` (sha256 `74b79081...24b3f5d0`): the probe source,
  kept as text so Unity does not compile it

### Live Editor

- 07:19: `mcpforunity://instances` listed `ColorBlockJamCase2026@99077e4df1ecfaa4`
  (connected_at 04:19:09Z). `execute_code` returned `success:false`, and `read_console`
  returned `Unity session not ready for 'read_console' (ping not answered)`.
- 07:27: one retry after a reconnect (connected_at 04:26:54Z) failed the same way.
- The live `Editor.log` still ends with `WebSocketTransportClient.SendJsonAsync` exceptions.
- The live `Library/ScriptAssemblies/ColorBlockJam.Editor.dll` is still dated 04:34:54, so the
  live Editor has not compiled this change.
- The verifier did not touch the live Editor, its scenes, or Play Mode.

### Files and scope

- sha256 values are unchanged:
  - `LevelDatabaseWindow.cs`: `ab7570f2...27d6ad` (mtime 06:19:06)
  - `LevelCreateColourTests.cs`: `3db4cf33...30c1b6`
  - `.meta`: `f6322616...36db5d`, guid `adbbb388...d230`
- `grep`: no `"red"` remains. `"Red"` appears only at lines 702 and 706 in `CreateStarterLevel`.
- Diff of `CreateLevel` against the pre-task scratch copy (`LevelDatabaseWindow.before.cs`
  629-663): only the two literals changed, and the unchanged `new LevelData(...)` body moved
  into `CreateStarterLevel(id, $"Level {number - 1}")`. The id loop, save, selection and status
  are identical.
- Task files:
  - `LevelDatabaseWindow.cs`, `LevelCreateColourTests.cs` and its `.meta`
  - this folder: `evidence.md`, the four XML reports, and the probe `.txt`
  All are inside the allowed paths.
- Since 06:00, nothing under `Assets/ColorBlockJam/Data`, `Assets/ColorBlockJam/Runtime`,
  `Packages` or `ProjectSettings` changed, except `ProjectSettings/QualitySettings.asset`
  (06:39). That file belongs to `TASK-20260925-014954719-a9370ca5-board-look-camera-light-graphics`,
  not to this task.

### AC-1: EditMode run in a fresh isolated copy

- Copy: `%TEMP%\CBJcolourverify2-ff0896`, made at 07:21 after checking that no Assets,
  Packages or ProjectSettings file had changed since 07:05. It is a robocopy with Library and the
  standard excludes, and its `EditorInstance.json` was deleted. The hashes of both source files
  match the repo.
- Command: `Unity.exe -batchmode -runTests -testPlatform EditMode -assemblyNames
  "ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests;ColorBlockJam.Environment.EditModeTests"`.
  It exited with 0 at 07:25.
- Result: 172/172 passed. Editor.EditModeTests 103/103, LevelPreview.Tests 9/9,
  Environment.EditModeTests 60/60.
- All three `LevelCreateColourTests` cases passed.
- Compile: 0 `error CS`. The only warning is the pre-existing `VacuumMotionAssets.cs(54,13) CS0618`.

### AC-2: headless stand-in for the live check (copy only, not in the repo)

- A verifier-only test was added to the copy's `Tests/LevelPreview` folder.
- The test builds the real `LevelPreviewHost` with the project's `Environment.prefab`,
  `EnvironmentDatabase` and `BlockShapeLibrary` in the runner's untitled scene.
- It calls `LevelDatabaseWindow.CreateStarterLevel` through reflection and passes the result to
  `TryShow`. It then repeats the call with the old lowercase `"red"` seed as a negative control.
- Result: 10/10 passed, exit 0 at 07:26.
  - Log: `VERIFIER-PROBE seed TryShow=True error='' blocks=1 levelId=level-001`.
  - Log: `VERIFIER-PROBE old-lowercase TryShow=False error='Door 0 needs a gate colour name.'`
- The window's only preview warning is `LevelPreviewOwner.WarnOnce` (`LevelPreviewOwner.cs:144-147`,
  `:209`), and it runs only when `TryShow` returns false. The probe therefore shows that the
  new seed publishes without the colour warning, and that the old seed would have raised it.
- This does not replace the contract's live "+" click in the real window: live Create
  Database, Addressables entry, selection and the Scene-view render were not exercised.

### Verdict

- AC-1: **PASS**, from a fresh isolated EditMode run at the current file hashes.
  MCP `run_tests` was impossible because the live Editor did not answer the ping.
- AC-2: **UNPROVEN, pending the live check**. The headless preview probe supports it strongly.
  To close it, recompile the live Editor and run implementer steps 1-4 with a scratch database,
  or get the user's acceptance.
- Out-of-scope changes attributable to this task: none.
- `%TEMP%\CBJcolourverify2-ff0896` (about 2.2 GB) can be deleted.

## AC-2 live check (orchestrator, 2026-09-25 after the Editor restart; MCP healthy)

- In the live Editor (LevelEditor scene active), `LevelDatabaseWindow.CreateStarterLevel("level-live-check", "Live Check")` gave door `Red` and block `Red`.
- The level was passed to the real preview handler (`LevelPreviewOwner.OnLevelPublished`), the same path the window's "+" publish uses.
- Result: EnvironmentView built with layout 3x3, 1 active block, 0 warnings or errors logged during the call.
- The preview was then restored to the window's current draft (level-01).
- The "+" button itself was not clicked, so that no level would be written to the shared `LevelDatabase.json`. The code path after the click (seed → publish → preview) is the one exercised here.
