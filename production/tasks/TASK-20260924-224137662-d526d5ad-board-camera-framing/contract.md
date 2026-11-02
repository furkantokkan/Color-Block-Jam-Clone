# TASK-20260924-224137662-d526d5ad-board-camera-framing — Board camera uses the reference game angle and fits the level

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-224137662-d526d5ad-board-camera-framing
- Supersedes: none
- Title: Board camera uses the reference game angle and fits the level
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Environment presentation (board camera)
- Workflow: direct

## Objective and baseline

- Observable objective: The Game scene camera and the LevelEditor scene preview show the board from the reference game's angle, with the whole level framed for the portrait screen, so the Game view looks like the game.
- Current behavior/evidence:
  - User request 2026-09-25: "game de kamera açısı da doğru olmalı oyundaki gibi referans projedeki açıyı kullanabiliriz" ("the camera angle in Game should also be right, like in the game; we can use the reference project's angle"). Their screenshot shows the LevelEditor Game view with "Display 1 No cameras rendering".
  - `Game.unity` Main Camera: perspective, FOV 60, position (0,1,-10), no rotation, and nothing frames it.
  - Reference (`CBJ Template/Assets/Project Files/Game/Scenes/Game.unity` and `Scripts/Controllers/CameraController.cs`):
    - Camera: perspective, FOV 60, rotation X 75 degrees (quaternion x 0.6088, w 0.7934), near 0.3, far 50.
    - `CameraController` values: offset (0, 18.4, -4.75), sideOffset 4, yOffset 12.
    - `LevelController.RepositionCamera` calls `Reposition(bounds.center, bounds.size)` after spawning the level.
  - The reference's `Reposition` scales the offset by a frustum fit measured from the camera's current position, so its result depends on where the camera was before.
- Expected behavior:
  1. A plain C# framing calculation takes the board bounds, the camera's vertical FOV and aspect, the pitch, the side margin and the vertical margin, and returns the camera position and rotation.
     - It uses the reference values: pitch 75, FOV 60, side margin 4, vertical margin 12.
     - Distance = max((size.x + sideMargin) / (2 tan(FOV/2) aspect), (size.z + verticalMargin) / (2 tan(FOV/2))).
     - Position = bounds.center - forward * distance.
     - It does not depend on the previous camera position.
  2. A thin camera component applies it (serialized pitch, margins and the target camera). It is framed once per board build, never per frame, and has no Update loop.
  3. The Game scene frames its Main Camera after the environment builds.
  4. The LevelEditor preview host adds a DontSave preview camera with the same component, so the Game view renders the previewed level from the game angle.
- Behavior that must remain unchanged:
  - Environment composition.
  - The level JSON.
  - The Game scene flow (`EnvironmentPreview` still builds from its level index).
  - Scene objects other than the Main Camera.

## Ownership

- Owner/writer role: environment/play-area session (this conversation), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Environment/BoardCamera*.cs
  - Assets/ColorBlockJam/Environment/BoardCamera*.cs.meta
  - Assets/ColorBlockJam/Environment/EnvironmentPreview.cs
  - Assets/ColorBlockJam/Scenes/Game.unity
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Tests/Environment/EditMode/BoardCameraFramingTests.cs
  - Assets/ColorBlockJam/Tests/Environment/EditMode/BoardCameraFramingTests.cs.meta
  - docs/Environment-Layout.md
  - docs/Level-Editor-Scene-Preview.md
  - PROJECT.md
  - production/tasks/TASK-20260924-224137662-d526d5ad-board-camera-framing/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/LevelEditor.unity
  - Assets/ColorBlockJam/Scenes/Hub.unity
  - Assets/ColorBlockJam/Scenes/Init.unity
  - Assets/ColorBlockJam/Scenes/Loading.unity
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Editor/Levels/**
  - Cinemachine or any new package
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Scenes/Game.unity
    Sole writer: environment/play-area session through the live Editor (MCP HTTP); saved only when the scene has no unrelated unsaved changes
    Matching `.meta` owner: environment/play-area session (unchanged)
- Other active writers/worktrees checked: Game.unity is already owned by `TASK-20260924-185655569-dea430a7-play-area-environment` (same session). `Editor/LevelPreview/**` belongs to `TASK-20260924-222616741-46522211-level-editor-scene-preview`, which runs first; this task edits its host only after that task hands off.
- Execution dependency: TASK-20260924-222616741-46522211-level-editor-scene-preview reaches a stable verifier handoff
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given board bounds, FOV 60, aspect 1080/1920, pitch 75 and margins 4/12, when the framing is calculated, then the rotation is Euler(75,0,0), the camera looks at the bounds centre, and both the width and depth fit inside the frustum at that distance. The result is identical for any previous camera position (EditMode).
2. Given the Game scene is played directly with level 1, when the environment builds, then the Main Camera is framed once from the 75-degree angle and the whole board is visible in the 1080x1920 Game view.
3. Given the LevelEditor scene preview shows a level, then the Game view shows it from the same angle, framed; the preview camera is DontSave, and `LevelEditor.unity` stays byte-identical after a save.
4. Given play or preview, then no Update/tick loop exists for the camera: framing runs only on board builds.

## References and constraints

- Story/GDD/ADR/design/log/image: user message and screenshot 2026-09-25; reference files named above; PROJECT.md portrait 1080x1920 and 20:9 layout rule; Performance Rule 4 (one tick owner).
- Unity version/platform: 2022.3.62f2; Android/iOS portrait
- Package/architecture constraints: no Cinemachine; plain C# math plus a thin MonoBehaviour; `System.Linq` not used.
- Explicit out of scope: runtime re-framing on resolution change, camera shake or zoom effects, and the HUD layout.

## Verification and risk

- Focused test path/command: `BoardCameraFramingTests` (EditMode)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1
- PlayMode: acceptance 2 (a direct Game scene play, or the existing Game scene smoke test, plus a camera transform read)
- Manual/built-player/profiler/device evidence:
  - Game view captures of the Game scene and of the LevelEditor preview at 1080x1920.
  - User acceptance of the angle.
- Risk: high (Game.unity scene write)
- Approval already granted (exact target only): user request 2026-09-25 for the Game camera angle plus "her şeyi sen yap"; target `Assets/ColorBlockJam/Scenes/Game.unity` Main Camera only
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-224137662-d526d5ad-board-camera-framing, then an independent verifier

## Stop conditions

- Game.unity has unrelated unsaved changes in the live Editor: do not save it; ask the user.
- The fit needs a change in Runtime or Presentation code.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:44b433e47098658e4f59065f57f9256addb901c3422672a0088094e752c3af12
- Evidence fingerprint: sha256:1c98d8b110b0c3bf97f34c71d28292793afa9af8122e3996afac93e2809efe3b
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b2be38aaac44031c6a189d970a4f0ff6eb5a47f415ffd62f8b369a14fb449c81
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b2be38aaac44031c6a189d970a4f0ff6eb5a47f415ffd62f8b369a14fb449c81
- Closed at: 2026-09-25T03:15Z
- Closure mode: automatic
- Closed by: implement-task (direct, empty ledger; independent verifier PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Deterministic framing math | PASS | EditMode run in a fresh isolated copy (Unity 2022.3.62f2 batchmode -runTests), because the live Editor's MCP was not answering. Also read the source against the (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b2be38aaac44031c6a189d970a4f0ff6eb5a47f415ffd62f8b369a14fb449c81 |
| AC-2 Game scene camera framed at 75 degrees | PASS | A copy-only PlayMode test (verify/GameCameraPlayCheck.cs) loads Game directly and moves the camera to (0,1,-10) before the board builds. It uses a 1080x1920 tar (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b2be38aaac44031c6a189d970a4f0ff6eb5a47f415ffd62f8b369a14fb449c81 |
| AC-3 LevelEditor preview camera | PASS | Re-ran verify/BoardCameraPreviewCheck.cs in batchmode (-executeMethod, exit 0) in the verifier's own isolated copy. It publishes every level through the real Le (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b2be38aaac44031c6a189d970a4f0ff6eb5a47f415ffd62f8b369a14fb449c81 |
| AC-4 No camera tick loop | PASS | The BoardCameraNeverTicks EditMode test, a grep of the sources and call sites, and a live PlayMode check that moves the camera after the build. (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b2be38aaac44031c6a189d970a4f0ff6eb5a47f415ffd62f8b369a14fb449c81 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's camera-angle request with the reference project's values
- 2026-09-25T03:15Z — in_progress — task-chain workflow: implement, 3 reviewers (4 findings), fix, independent verify
- 2026-09-25T03:15Z — TASK_CLOSED — all criteria PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
