# Evidence: board camera framing

- Date: 2026-09-25. Implementer run (sole writer).
- Transport: MCP for Unity over HTTP at 127.0.0.1:8082, instance
  `ColorBlockJamCase2026@99077e4df1ecfaa4`. Unity 2022.3.62f2.
- Dependency: `TASK-20260924-222616741-46522211-level-editor-scene-preview` is
  closed (TASK_CLOSED), so editing `Editor/LevelPreview/LevelPreviewHost.cs` was
  unblocked.
- Starting Editor setup: only `LevelEditor.unity` loaded, active and clean;
  Game view 1080 x 1920. Restored to that setup at the end.
- No VCS command ran.

## Changed files

| File | Change |
|---|---|
| `Assets/ColorBlockJam/Environment/BoardCameraFraming.cs` (+ .meta) | New. Plain C# pose calculation from bounds, FOV, aspect, pitch and margins; reference constants 75 / 4 / 12. |
| `Assets/ColorBlockJam/Environment/BoardCamera.cs` (+ .meta) | New. Thin component: serialized target camera, pitch, margins; `Frame(Bounds)`; no Update. |
| `Assets/ColorBlockJam/Environment/EnvironmentPreview.cs` | After `m_view.Build(layout)`, frames `Camera.main`'s `BoardCamera` once with the board's world bounds. |
| `Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs` | Creates a DontSave `Preview Camera` (Camera + BoardCamera + URP additional data) under the host root; frames it after every build. |
| `Assets/ColorBlockJam/Editor/LevelPreview/ColorBlockJam.Editor.LevelPreview.asmdef` | References `Unity.RenderPipelines.Universal.Runtime` so the host adds the URP camera data before the DontSave pass. |
| `Assets/ColorBlockJam/Scenes/Game.unity` | Main Camera only: added `BoardCamera` (m_camera = its Camera, 75 / 4 / 12); saved pose = level 1 framing, position (0, 15.417293, -4.03459), rotation (0.6087614, 0, 0, 0.7933533). |
| `Assets/ColorBlockJam/Tests/Environment/EditMode/BoardCameraFramingTests.cs` (+ .meta) | New EditMode tests. |
| `docs/Environment-Layout.md`, `docs/Level-Editor-Scene-Preview.md`, `PROJECT.md` (DEC-103) | Documentation. |

Game.unity was opened additively, was clean on open, was changed through Unity
APIs (`Undo.AddComponent`, `SerializedObject`, `BoardCamera.Frame`), saved with
`EditorSceneManager.SaveScene(game)` and closed with `CloseScene(game, true)`.
SHA-256 before `78fef9ae…238276c7`, after `56789580…c9a92818`. The only hunks
added by this task are the Main Camera component list, its transform and the
new BoardCamera MonoBehaviour block.

## Compile and Console

- `refresh_unity` (force, all, compile request): recovered after domain reload,
  editor ready. `read_console` error/warning: 0 entries.
- Final Console: one MCP for Unity transport warning
  (`[WebSocket] Unexpected receive error`) from the tooling, nothing from the
  project.

## Tests

| Run | Job | Result |
|---|---|---|
| EditMode `BoardCameraFramingTests` | `3bef1993b2cc4393b0882a09c33ef977` | 11/11 passed |
| EditMode `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests` | `ca74a98976e54a84912b31fdd7b03b32` | 69/69 passed |
| PlayMode `ColorBlockJam.Environment.PlayModeTests` | `efa0668c3ac44eaeb4d5bf11c7d7257e` | 7/7 passed |
| PlayMode `SceneFlowSmokeTests` (includes `GameRunsWhenOpenedDirectly`) | `f7c2ea18c1824746883c8c653ad6ef09` | 6/6 passed |

## Acceptance

| AC | Result | Evidence |
|---|---|---|
| AC-1 Deterministic framing math | PASS | `RotationIsThePitchAndTheCameraLooksAtTheBoundsCentre`; `WidthAndDepthFitInsideTheFrustum` (5 cases, all 8 bounds corners inside the frustum, aspects 1080/1920 and 1080/2400); `DistanceIsTheLargerOfTheWidthAndDepthFits` (width-limited 9x5 = 20.015, depth-limited 5x11 = 19.919); `BoardCameraPoseIgnoresThePreviousCameraPosition` (two start poses give the same result); `InvalidLensIsRejected`. |
| AC-2 Game scene framed at 75 degrees | PASS | `Game.unity` opened Single (LevelEditor was clean) and played directly. Before the environment build the camera was moved to (0, 1, -10) with no rotation. After the build (76 pieces, level-01, bounds centre (0, 0.36, 0), size (5, 0.72, 6)) the camera was at (0, 15.4173, -4.0346), euler (75, 0, 0), FOV 60, aspect 0.5625, 1080x1920 pixels. The board's viewport rect is x [0.234, 0.766], y [0.329, 0.662]: whole board visible. Capture: `captures/game-play-level-01.png` (Main Camera rendered to a 1080x1920 RenderTexture). Play exited; Game stayed clean. |
| AC-3 LevelEditor preview camera | PASS | Host root holds `Preview Camera` with Transform, Camera, BoardCamera and UniversalAdditionalCameraData, all `DontSave`; 364 host objects, 0 missing DontSave. Level 2: camera (0, 15.417, -4.035), euler (75, 0, 0), aspect 0.5625, viewport x [0.181, 0.819], y [0.329, 0.662]. Level 3 (6x7 bounds): (0, 16.254, -4.259), viewport x [0.196, 0.804], y [0.311, 0.677]; preview restored to level 2 afterwards. Captures: `captures/leveleditor-preview-level-02.png`, `captures/leveleditor-preview-level-03.png`. `SaveScene(levelEditor, "Temp/BoardCameraLevelEditorCopy.unity", saveAsCopy: true)` is byte-identical to `LevelEditor.unity` (sha256 `3e743628…502636902de9`, `cmp` identical); copy deleted. Scene never dirty. |
| AC-4 No camera tick loop | PASS | `BoardCameraNeverTicks` (no Update/LateUpdate/FixedUpdate, no IOnityTickable); existing `EnvironmentComponentsNeverTick` still passes. `Frame` is called only from `EnvironmentPreview.BuildPreview` and `LevelPreviewHost.Build`. |

Manual item still open: user acceptance of the angle.

## Notes

- The Editor was unfocused, so direct Play stalled at frame 2.
  `Application.runInBackground = true` was set to let frames run. In the Editor
  that setter also writes `PlayerSettings.runInBackground`, so it was set back
  to `false` right after the capture. Correction (review fix pass): the Editor
  did rewrite `ProjectSettings/ProjectSettings.asset` (mtime 04:18:03) and
  `ProjectSettings/EntitiesClientSettings.asset` (04:16:55) during that window,
  although the setting was restored. Both files are byte-identical to cs:5
  (read-only `cm cat` + `cmp`), and `runInBackground: 0` is on disk. Plastic
  may list them as pending changes with an empty diff; undoing them is safe.
  This touched the forbidden `ProjectSettings/**` area only by timestamp.
  Future captures use the RenderTexture path or a focused Game view instead
  of changing `Application.runInBackground`. The review fix pass did not touch
  `runInBackground`, yet its forced refresh and PlayMode run rewrote both files
  again (04:29:42 and 04:29:56), still byte-identical to cs:5. The Editor
  rewrites them on refresh/Play, so the timestamp change is not specific to the
  toggle.
- The saved Main Camera pose only presets the level 1 framing. In edit mode
  `Game.unity` has no board geometry (the Environment prefab and the scene
  hold no MeshRenderer; pieces come from pools built in Play), so the edit-mode
  Game view shows an empty scene from the framed angle. Judge the angle from
  Play Mode, `captures/game-play-level-01.png`, or the LevelEditor preview.
- Other scene hashes are unchanged: Hub `0d51c0e2…`, Init `6a4256d3…`,
  Loading `249b7d2a…`, LevelEditor `3e743628…`.

## Review fix pass (2026-09-25)

- Four minor reviewer findings, all confirmed; documentation-only fixes, no
  code or scene change.
  - Edit-mode Game view claim: `docs/Environment-Layout.md` Board camera now
    says the saved pose presets the level 1 framing and the board appears only
    in Play or the LevelEditor preview; note added above.
  - ProjectSettings rewrite: evidence corrected above.
  - Assembly listing: `BoardCameraFraming` and `BoardCamera` added to the
    `ColorBlockJam.Environment` row and intro of `docs/Environment-Layout.md`;
    `PROJECT.md` Environment-assembly bullet names the DEC-103 camera framing.
- `refresh_unity` (force, compile request): editor ready; Console error/warning
  holds only the MCP for Unity `[WebSocket]` tooling warning.
- EditMode `ColorBlockJam.Environment.EditModeTests` +
  `ColorBlockJam.Editor.LevelPreview.Tests` (includes `BoardCameraFramingTests`):
  job `4dba7062a70040b2beb061bbf31b4703`, 69/69 passed.
- PlayMode `ColorBlockJam.Environment.PlayModeTests`: job
  `0b6f92437324431f9aa55774b9afbace`, 7/7 passed.
- Editor state after: only `LevelEditor.unity` loaded, clean, not playing,
  `PlayerSettings.runInBackground` false. Game.unity sha256 `56789580…` and
  LevelEditor `3e743628…` unchanged.

## Independent verification (2026-09-25)

- Verifier: independent session, MCP for Unity HTTP
  (`ColorBlockJamCase2026@99077e4df1ecfaa4`). No production code, test, or scene
  edits and no VCS writes. Plastic was read with `cm status` and `cm cat` only.
- Starting state: only `LevelEditor.unity` loaded, clean, not playing, Game view
  1080 x 1920, `PlayerSettings.runInBackground` false.

### Tests (MCP `run_tests`)

| Run | Job | Result |
|---|---|---|
| EditMode `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests` | `88de6389081541f8907be23a8ecdeca2` | 69/69 passed |
| EditMode `ColorBlockJam.Tests.Environment.BoardCameraFramingTests` | `980097fb53904d0c84bf1f56663401ea` | 11/11 passed |
| PlayMode `ColorBlockJam.Environment.PlayModeTests` | `8195055da2c04ce7985aba0829e77d5d` | 7/7 passed |
| PlayMode `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests` | `e497458f108a4d7f9860538a33e494c0` | 6/6 passed |

Console before the runs: only MCP for Unity `[WebSocket]` tooling warnings; no
project errors or warnings.

### AC-2 live check (direct Play of Game, level index 0)

- Opened `Game.unity` Single (LevelEditor was clean). Saved Main Camera pose:
  (0, 15.4173, -4.0346), rotation (0.6088, 0, 0, 0.7934), `BoardCamera` present.
- Entered Play paused, frame 1, board not built: moved the camera to
  (0, 1, -10), identity rotation. Advanced frames with `EditorApplication.Step`
  (no `runInBackground` change).
- Frames 1-9: camera stayed at (0, 1, -10). Frame 10: board built (76 pieces)
  and the camera moved to (0, 15.4173, -4.0346), euler (75, 0, 0), FOV 60, aspect
  0.5625, 1080 x 1920. This matches the hand calculation for bounds 5 x 6:
  distance = max(9 / (1.1547 x 0.5625), 18 / 1.1547) = 15.588.
- Board bounds centre (0, 0.36, 0), size (5, 0.72, 6); viewport x [0.234, 0.766],
  y [0.329, 0.662]. The whole board is visible. Capture:
  `captures/verify-game-play-level-01.png` (Main Camera to a 1080 x 1920
  RenderTexture). The picture shows the whole play area.
- AC-4 live: after the build, the camera was moved to (3, 2, -20), euler
  (10, 5, 0). Stepping frames 11-16 left it there, so nothing re-frames it each
  frame.

### AC-3

- Automated: `LevelPreviewTests` (in the 69/69 run) call `host.TryShow` for two
  levels and assert DontSave on every GameObject and component under the host
  root. The root now includes `Preview Camera` (Camera, BoardCamera,
  UniversalAdditionalCameraData), so the preview camera's DontSave is covered.
- Code: `LevelPreviewHost.Build` calls `m_camera.Frame` once per build with the
  board world bounds. The camera uses the same `BoardCamera` component and
  default reference values as the Game scene.
- `LevelEditor.unity` sha256 is still `3e743628…` (unchanged on disk).
- NOT independently re-run live: the LevelEditor preview capture and the
  save-as-copy byte comparison. The MCP session stopped answering (see below)
  before the check could run. The implementer's `leveleditor-preview-level-02/03.png`
  captures were inspected and show the whole board from the same angle.

### Changed files and path check

- In allowed paths: `Environment/BoardCameraFraming.cs`(+meta),
  `Environment/BoardCamera.cs`(+meta), `Environment/EnvironmentPreview.cs`,
  `Editor/LevelPreview/LevelPreviewHost.cs`,
  `Editor/LevelPreview/ColorBlockJam.Editor.LevelPreview.asmdef`,
  `Scenes/Game.unity`, `Tests/Environment/EditMode/BoardCameraFramingTests.cs`(+meta),
  `docs/Environment-Layout.md`, `docs/Level-Editor-Scene-Preview.md`,
  `PROJECT.md` (DEC-103 row, Environment bullet), and this task folder.
- `Game.unity` versus cs:5: the camera hunks are the Main Camera component list
  (+`330585548`), its transform, and the `BoardCamera` block (m_camera
  `330585545`, 75/4/12). The other hunks (installers, Environment prefab
  instance, Environment Composition) belong to the play-area environment task.
- Outside the allowed paths: `ProjectSettings/ProjectSettings.asset` and
  `ProjectSettings/EntitiesClientSettings.asset` are listed as Plastic `CH`, but
  both are byte-identical to cs:5 (checked again). The Editor rewrote them on
  refresh or Play, so this is a timestamp-only no-op. The other pending files in
  the workspace (for example `EditorBuildSettings.asset`, ProBuilder
  `Settings.json`) predate this task's scene work and belong to other tasks.
- Other scene hashes unchanged: Hub `0d51c0e2…`, Init `6a4256d3…`,
  Loading `249b7d2a…`.

### Editor state left behind (needs the user)

- Leaving Play failed: the `isPlaying = false` request got no reply. From then on
  the MCP for Unity session reconnected every 15-30 s, but it never answered a
  ping ("Unity session not ready ... ping not answered"). The Editor log shows
  repeated `[WebSocket] Keep-alive failed` / `Connection closed`. Focusing the
  Unity window did not bring MCP back. Sending Ctrl+P to the window and further
  polling were blocked by the session's permission classifier.
- The Unity Editor is still in paused Play Mode with `Game.unity` loaded
  (`Temp/__Backupscenes/0.backup` exists). No scene file was written:
  `Game.unity` is still `56789580…` and `LevelEditor.unity` is still `3e743628…`.
- To restore: exit Play Mode (Ctrl+P), then reopen
  `Assets/ColorBlockJam/Scenes/LevelEditor.unity`. Do not save `Game.unity` if
  Unity prompts; the play-mode camera moves are discarded when Play exits.

## Implementer fix pass after verification (2026-09-25)

Verifier result: AC-1, AC-2, AC-4 PASS; AC-3 UNPROVEN only because the live
re-run could not happen. No FAIL. No production code, scene, test or doc change
was needed; this pass makes AC-3 repeatable and re-runs it plus the tests.

### Live Editor

- MCP HTTP 8082 still lists the instance, but every tool call returns
  `success:false` and `mcpforunity://editor/state` reports "ping not answered"
  (checked 05:12 and 05:28). The server log shows the plugin registering and
  dropping (1005) every ~45 s. The Editor process responds; its title shows the
  `Game` scene, so it is still in the verifier's paused Play Mode. This pass did
  not touch the Editor window. It still needs the user: Ctrl+P, then reopen
  `LevelEditor.unity`, and do not save `Game.unity`.
- Following the isolated-copy rule (MCP unreachable > 5 min), the checks below
  ran in `%TEMP%\CBJCameraVerify-7c41e2a9`, a robocopy of the repo at 05:17
  (Library included; `Library/EditorInstance.json` deleted in the copy only).
  The task files are byte-identical between repo and copy (sha256 prefixes:
  BoardCameraFraming `7b59a61d`, BoardCamera `b42775be`, EnvironmentPreview
  `eaef66e3`, LevelPreviewHost `eb8022e9`, LevelPreviewOwner `0d99f152`,
  BoardCameraFramingTests `2e25ca1e`, Game.unity `56789580`, LevelEditor.unity
  `3e743628`). Nothing was copied back except captures.

### AC-3 repeatable check (new)

- Script: `verify/BoardCameraPreviewCheck.cs` + `verify/CameraVerify.Editor.asmdef`
  in this task folder (outside `Assets`, so it never compiles into the project).
  Copy both into `Assets/CameraVerify/Editor/` of an isolated copy and run
  `Unity.exe -batchmode -projectPath <copy> -executeMethod CameraVerify.BoardCameraPreviewCheck.Run -logFile <copy>\camera-verify.log -checkOut <folder>`.
  Exit code 0 means pass. It opens `LevelEditor.unity` Single and publishes
  every level through the real `LevelPreviewOwner.OnLevelPublished` path (the
  call the Level Editor window makes). A 1080x1920 target gives the preview
  camera the Game view's aspect, which batchmode lacks. For each level it
  checks: the pose equals `BoardCameraFraming.Calculate` for the host bounds;
  the rotation is Euler(75,0,0); all 8 bounds corners are inside the viewport;
  every object and component under the host root is DontSave. It also renders
  a PNG. With a preview shown and after disposal, it saves the scene as a copy
  and compares the bytes with `LevelEditor.unity`.
- Result (`captures/ac3-check.txt`): RESULT PASS, all five levels.
  - level-01 (5x6): cam (0, 15.4173, -4.0346), euler (75,0,0), aspect 0.5625,
    viewport x [0.234, 0.766] y [0.329, 0.662]. This is the same pose the
    verifier measured for the Game scene.
  - level-02 (6x6): viewport x [0.181, 0.819] y [0.329, 0.662].
  - level-03/04/05 (6x7): cam (0, 16.2538, -4.2587), viewport x [0.196, 0.804]
    y [0.311, 0.677].
  - `missingDontSave=0` and `poseMatchesFraming=True` for every level.
  - Save-as-copy with level-03 shown and again after dispose: sha256
    `3e743628…502636902de9`, identical to `LevelEditor.unity`, scene not dirty,
    the file on disk unchanged. After the null publish the host is gone and the
    scene is back to 1 root.
- Captures: `captures/ac3-leveleditor-preview-level-01.png`,
  `captures/ac3-leveleditor-preview-level-03.png`. They show the whole board,
  blocks and doors from the 75-degree game angle, centred in portrait.
- The first batchmode run aborted with `Failed to create CoreCLR, HRESULT
  0x80004005` from the compiler host. This was a machine resource fault, not a
  C# error. The unchanged rerun compiled cleanly and passed.

### Tests re-run (isolated copy, Unity 2022.3.62f2 batchmode `-runTests`)

| Run | Result |
|---|---|
| EditMode `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests` (includes `BoardCameraFramingTests` 11/11) | 69/69 passed |
| PlayMode `ColorBlockJam.Environment.PlayModeTests` | 7/7 passed |
| PlayMode `SceneFlowSmokeTests` (includes `GameRunsWhenOpenedDirectly`) | 6/6 passed |

### Out-of-scope files

- `ProjectSettings/ProjectSettings.asset` and
  `ProjectSettings/EntitiesClientSettings.asset` were compared again with `cmp`
  against their cs:5 copies: byte-identical. Plastic lists them only because
  the live Editor rewrote them with the same content on refresh or Play. This
  pass did not revert them. A `cm undo` would be a VCS write in a workspace
  shared by other live sessions, and the open Editor rewrites the files anyway.
  With no content change, there is nothing to revert; the user can undo them
  safely at any time.

### Acceptance after this pass

| AC | Result | Basis |
|---|---|---|
| AC-1 | PASS | Verifier run; 11/11 re-run. |
| AC-2 | PASS | Verifier's live Play check; smoke 6/6 re-run. |
| AC-3 | PASS (implementer re-run; needs verifier confirmation) | `verify/BoardCameraPreviewCheck.cs` RESULT PASS; repeatable by any verifier. |
| AC-4 | PASS | Verifier run; re-run in the 69/69 EditMode run. |

Still open, and only the user can close these: accepting the angle (see
the captures) and the 20:9 device check. Separately, the live Editor must be
taken out of paused Play Mode.

## Independent verification, second pass (2026-09-25 05:29-06:15)

- Verifier: independent session. No production code, test, scene, doc or
  ProjectSettings edit; no VCS write (`cm status` and `cm cat` only). New files
  only in this task folder: `verify/GameCameraPlayCheck.cs`,
  `verify/CameraVerify.PlayMode.asmdef` and the `captures/verifier2-*` files.
- Live Editor: MCP 8082 lists `ColorBlockJamCase2026@99077e4df1ecfaa4`, but
  `mcpforunity://editor/state` returned "ping not answered" on every poll from
  05:29 to 06:13 (more than 40 minutes). The Editor was not touched. Under the
  isolated-copy rule, all checks ran in a fresh robocopy made by this verifier,
  `%TEMP%\CBJCameraVerifier-b81e4c07` (not the implementer's copy;
  `Library/EditorInstance.json` deleted in the copy only; only locked `.vs`
  index files failed to copy). Files in the copy were byte-identical to the
  repo: BoardCameraFraming `7b59a61d`, BoardCamera `b42775be`,
  EnvironmentPreview `eaef66e3`, LevelPreviewHost `eb8022e9`, LevelPreviewOwner
  `0d99f152`, LevelPreview asmdef `8dfb3486`, BoardCameraFramingTests `2e25ca1e`,
  Game.unity `56789580`, LevelEditor.unity `3e743628`, LevelDatabase.json
  `02c013a5`. Nothing was copied back into `Assets`.

### Results (Unity 2022.3.62f2 batchmode in the copy)

| Check | Result |
|---|---|
| EditMode `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests` | 69/69 passed; all 11 `BoardCameraFramingTests` cases passed |
| AC-3 `verify/BoardCameraPreviewCheck.cs` (`-executeMethod`, exit 0) | RESULT PASS, levels 01-05; `captures/verifier2-ac3-check.txt` |
| AC-2/AC-4 `verify/GameCameraPlayCheck.cs` (PlayMode, copy-only assembly) | Passed; `captures/verifier2-ac2-ac4-play-check.txt` |
| PlayMode `ColorBlockJam.Environment.PlayModeTests` (alone) | 7/7 passed |
| PlayMode `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests` | 6/6 passed |

- A first PlayMode run put `CameraVerify.PlayMode` and the Environment
  PlayMode tests in one run. Four `EnvironmentViewLifecycleTests` failed with
  counts off by 76, because the Game scene the new check loaded (76 pieces)
  stayed loaded for the next tests. This came from how the check was run, not
  from the product: run alone, the same assembly passed 7/7.
- Compile: the batchmode logs show no `error CS`.

### Acceptance

| AC | Result | Evidence |
|---|---|---|
| AC-1 | PASS | EditMode: `RotationIsThePitchAndTheCameraLooksAtTheBoundsCentre`, `WidthAndDepthFitInsideTheFrustum` (5 cases, 8 corners, 1080/1920 and 1080/2400), `DistanceIsTheLargerOfTheWidthAndDepthFits` (2 cases), `BoardCameraPoseIgnoresThePreviousCameraPosition`, `InvalidLensIsRejected` all passed. The source matches the contract formula exactly. |
| AC-2 | PASS | The test played Game directly. After the scene loaded, the board was not yet built and the saved pose was (0, 15.4173, -4.0346) at (75, 0, 0). The check set a 1080x1920 target and moved the camera to (0, 1, -10) with no rotation. The board then built (76 pieces, level-01 bounds centre (0, 0.36, 0), size (5, 0.72, 6)). The camera moved to (0, 15.4173, -4.0346) at (75, 0, 0), FOV 60, aspect 0.5625. That equals `BoardCameraFraming` for those bounds. On screen the board bounds span x [0.234, 0.766] and y [0.329, 0.662]. The bounds of all 76 rendered pieces, walls and doors included, span x [0.233, 0.767] and y [0.329, 0.663]. Capture: `captures/verifier2-game-play-level-01.png`, which shows the whole play area. |
| AC-3 | PASS | Every level went through the real `LevelPreviewOwner.OnLevelPublished` path. For each one, the pose equals the framing calculation at (75, 0, 0), all 8 bounds corners are on screen, and `missingDontSave=0`. Levels 01/02 put the camera at (0, 15.4173, -4.0346); levels 03-05 at (0, 16.2538, -4.2587). Both save-as-copy files, one taken with level-03 shown and one after dispose, have sha256 `3e743628…502636902de9`, identical to `LevelEditor.unity`. The scene never became dirty, and after dispose it is back to one root. Captures: `captures/verifier2-ac3-leveleditor-preview-level-02.png`, `-05.png`. |
| AC-4 | PASS | `BoardCameraNeverTicks` passed. Grep finds no Update, LateUpdate or `IOnityTickable` in `BoardCamera*.cs`. The only `BoardCamera.Frame` callers are `EnvironmentPreview.FrameMainCamera`, which runs once after `m_view.Build`, and `LevelPreviewHost.Build`. Live PlayMode check: the camera was moved to (3, 2, -20) at (10, 5, 0) after the build and was still there 60 frames later. |

### Changed files and path check

- Task files, all inside the allowed paths:
  - `Assets/ColorBlockJam/Environment/BoardCameraFraming.cs` (+meta, Plastic PR)
  - `Assets/ColorBlockJam/Environment/BoardCamera.cs` (+meta, Plastic PR)
  - `Assets/ColorBlockJam/Environment/EnvironmentPreview.cs` (PR; also owned by the play-area task)
  - `Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs` and `ColorBlockJam.Editor.LevelPreview.asmdef` (the folder is AD from the preview task)
  - `Assets/ColorBlockJam/Scenes/Game.unity` (CO)
  - `Assets/ColorBlockJam/Tests/Environment/EditMode/BoardCameraFramingTests.cs` (+meta, AD)
  - `docs/Environment-Layout.md`, `docs/Level-Editor-Scene-Preview.md`, `PROJECT.md` (DEC-103 row, Environment bullet at line 311)
  - this task folder
- `Game.unity` compared with `cm cat …#cs:5`: this task's hunks are only the
  Main Camera. They add component `330585548` (BoardCamera, m_camera
  `330585545`, 75/4/12) and change its transform to (0, 15.417293, -4.03459),
  rotation (0.6087614, 0, 0, 0.7933533). The other hunks (installers,
  Environment prefab instance, Environment Composition) belong to the
  play-area environment task.
- Outside the allowed paths:
  - `ProjectSettings/ProjectSettings.asset` and
    `ProjectSettings/EntitiesClientSettings.asset` are Plastic `CH`, but both
    are byte-identical to `cm cat …#cs:5`, which this verifier re-read itself.
    The Editor rewrote them on refresh or Play, so they differ only by
    timestamp; `runInBackground: 0` is on disk.
  - The other pending files predate this task (mtimes before its 04:09 start)
    and belong to other tasks: `Packages/manifest.json` and
    `packages-lock.json` (02:48), `EditorBuildSettings.asset` (01:20),
    ProBuilder `Settings.json`, and the removed `Assets/Scenes`.
- Other scene hashes unchanged: Hub `0d51c0e2`, Init `6a4256d3`, Loading
  `249b7d2a`, LevelEditor `3e743628`.

### Still open

- Live Editor: still in the earlier verifier's paused Play Mode with MCP not
  answering. The user must press Ctrl+P, reopen
  `Assets/ColorBlockJam/Scenes/LevelEditor.unity`, and not save `Game.unity`.
- Manual items only the user can close: approving the angle (see
  `captures/verifier2-*.png`) and the check on the 20:9 Android device.
