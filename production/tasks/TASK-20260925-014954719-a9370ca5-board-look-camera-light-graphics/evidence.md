# Evidence — TASK-20260925-014954719-a9370ca5-board-look-camera-light-graphics

Revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity plus the owned changes below (2026-09-25).

## Transport and ownership

- The owner of TASK-20260924-224137662-d526d5ad-board-camera-framing ("Ruled tile level sistem") sent an explicit RELEASE at about 06:15. It covered `BoardCamera*.cs`, `BoardCameraFramingTests.cs`, `Game.unity`, `LevelPreviewHost.cs`, the DEC-103 row and the camera section of Environment-Layout.
- The live Editor's MCP for Unity bridge (HTTP 127.0.0.1:8082) answered "ping not answered" from 05:29 onward. The instance was listed, but no tool call or resource read reached the Editor. The live Editor itself stayed responsive: its log kept recording asset refreshes. Its window title showed `Game` with no `*`.
- So serialized edits used one transport only: an isolated batchmode copy (`%TEMP%\cbj-boardlook-c27b5b`, robocopy of the workspace including Library).
  - There, Unity 2022.3.62f2 ran `-executeMethod BoardLookApply.Apply`, a temporary copy-only script that edits through Unity APIs.
  - Afterwards only the five owned serialized files were copied back; each copy was byte-verified.
  - Before the copy-back, the workspace originals were unchanged since the copy was taken (Game.unity sha256 `56789580…` both sides) and the live Editor title had no `*`.
  - Originals are backed up in the session scratchpad (`backup/*.orig`).
- C# edits were made directly in the workspace; the copy compiled and tested the same files. The only later change was comment-only: the DEC number.
- Temporary files, which exist only in the copy and were never copied back:
  - `Assets/BoardLookBatch/Editor/*` (apply script and asmdef)
  - `Tests/Environment/PlayMode/BoardLookGameCaptureTests.cs` (removed before the final PlayMode run)
  - `Tests/LevelPreview/BoardLookPreviewCaptureTests.cs`

## Changed files (sha256 prefix after the change)

| File | sha256 | Change |
|---|---|---|
| Assets/ColorBlockJam/Environment/BoardCameraFraming.cs | 0ee48a65e8292f02 | Margins 4/12 → 1.5/5; summary gives the source of each value (DEC-103, DEC-118) |
| Assets/ColorBlockJam/Tests/Environment/EditMode/BoardCameraFramingTests.cs | c3561b77c46d965d | Distance test uses the constants; cases 9x5 = 16.166 and 5x11 = 13.856 |
| Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs | 92175ca2037d52c5 | Named preview light/camera constants mirroring Game |
| Assets/ColorBlockJam/Scenes/Game.unity | 9086dcd318a03e3b | See the Game list below |
| Assets/ColorBlockJam/Scenes/Hub.unity | d030ad3e4a3561c4 | Solid clear #D6E4FC, post off, Global Volume deleted |
| Assets/ColorBlockJam/Scenes/LevelEditor.unity | 729984d8618e6dc1 | RenderSettings only: Flat ambient (0.63, 0.64, 0.67) |
| Assets/ColorBlockJam/Settings/URP-Balanced.asset | cdd5ab294db5f706 | MSAA 1 → 2, HDR 1 → 0, shadow distance 50 → 30, plus Unity 2022.3 asset-version upgrade (9 → 11; default fields added) |
| ProjectSettings/QualitySettings.asset | fbf6f34664524ce8 | m_CurrentQuality 2 → 1 (Balanced), plus the Unity 2022.3 v3 serialization upgrade (defaults only) |
| PROJECT.md | — | DEC-103 margins updated; DEC-118 row appended |
| docs/Environment-Layout.md | — | Camera section values |
| docs/Color-Block-Jam-Visual-References.md, docs/Color-Block-Jam-PRD.md, docs/Art-Asset-PRD-Mapping.md | — | "orthographic" → "perspective" (DEC-103); PRD DEC-17 marked decided |

Game.unity changes:

- Main Camera: clear Solid #D6E4FC, far plane 50, post-processing off, `BoardCamera` margins 1.5/5, preset pose (0, 10.0264, -2.5901).
- Global Volume deleted.
- Directional Light: Euler (65, 120, 0), color (1, 0.9915804, 0.9198113), color temperature off, intensity 0.45, shadow strength 0.39.
- Ambient: Flat (0.63, 0.64, 0.67).

Unchanged, byte-identical:

- `ProjectSettings/EditorBuildSettings.asset` (sha256 `5a117d613f069dae…`)
- `ProjectSettings/ProjectSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `URP-Balanced-Renderer.asset` (SSAO kept)
- High Fidelity and Performant assets
- `SampleSceneProfile.asset`
- All `.meta` files

Android and iPhone still default to Balanced (`m_PerPlatformDefaultQuality` Android 1, iPhone 1).

The Game and Hub diffs contain only the listed hunks (checked with `diff` against the originals).

## Tests (isolated copy, Unity 2022.3.62f2 batchmode, graphics device on)

| Run | Result |
|---|---|
| EditMode `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests` (includes the copy-only preview capture) | 70/70 passed (`editmode2.xml`) |
| PlayMode `ColorBlockJam.Presentation.PlayModeTests` + `ColorBlockJam.Environment.PlayModeTests` (includes `SceneFlowSmokeTests`, 6/6) | 27/27 passed (`playmode2.xml`) |
| Batch logs (apply2, editmode2, playmode2) | 0 errors, 0 warnings |

An earlier PlayMode run failed 4 `EnvironmentViewLifecycleTests` (56 expected, 132 found). The cause was the copy-only capture test leaving `Game.unity` loaded: 76 is the Game board's piece count. With that test removed, all 27 pass.

## Captures (`captures/`)

- `before-game-level-01-1080x1920.png`: the camera task's capture (margins 4/12, skybox, vignette, intensity 2, shadow strength 1).
- `run1-intensity-0.6-game-level-01-1080x1920.png`: the plan's starting values.
- `game-level-01-1080x1920.png`, `game-level-01-1080x2400.png`: final values.
- `leveleditor-preview-level-01-1080x1920.png`: final LevelEditor preview. Foreign lights from the test runner's untitled scene were disabled for this capture.
- `*-report.txt`: pose, viewport, settings read back and per-point pixel samples.

Measured at 1080x1920 (final):

- Camera pose (0, 10.0264, -2.5901), Euler (75, 0, 0).
- Board viewport x 0.067–0.933, y 0.227–0.748; top/bottom width ratio 0.851. At 1080x2400: x 0.077–0.923, y 0.286–0.700, ratio 0.880.
- Background corners and top centre are #D6E4FC; there is no vignette.
- `volumes 0`; `renderPostProcessing False`; quality Balanced (MSAA 2, HDR off, shadow distance 30); ambient probe L0 non-black (0.355, 0.367, 0.406).

Pixel scoring (averages over the sampled points):

| Metric | Run 1 (I 0.6, A 0.68/0.69/0.72) | Final (I 0.45, A 0.63/0.64/0.67) | Target |
|---|---|---|---|
| Lit floor | #8D9AB6 (+33, +31, +30) | #7D89A4 (+17, +14, +12) | #6C7B98 ±15 |
| Shadowed/lit floor luminance (left wall) | 0.88 | 0.88 | 0.70–0.85 |
| Side wall tops | #70777F | #626971 | #3C4667 ±15 |
| LevelEditor preview vs Game | — | identical within 1/255 (floor #7C89A4, walls #626971, ratio 0.88) | same as Game |

Tuning stopped at the contract's bounds (intensity 0.45 is the lower limit; ambient is −0.05).

The residual lightening and greying of flat tops matches the Blinn-Phong specular of `MAT_Board` (`_Specular` 0.25, `_Smoothness` 0.35, NdotH ≈ 0.98 under this light and camera). The palette albedos are floor #687CA7 and wall #394961. Without the sheen, the same light predicts a floor of about #677AA1, inside the target. `MAT_Board` is forbidden here and belongs to the DEC-96 rendering owner (stop condition).

## Acceptance

| AC | Result | Evidence |
|---|---|---|
| AC-1 | PASS | `BoardCameraFramingTests` 11/11: 16.166 and 13.856; all frustum-corner cases at 0.5625 and 0.45; pose independent of the previous position |
| AC-2 | FAIL (marginal) | Pose exact. x 0.067–0.933 and the 20:9 x 0.077–0.923 match. y 0.227–0.748 is 0.022–0.023 below the plan's 0.25–0.77 (tolerance ±0.02). The board sits about 2% of screen height lower than predicted; the real game's y is 0.26–0.79. A HUD safe-area fit is out of scope. User acceptance possible. |
| AC-3 | PASS | Background #D6E4FC in all corners and top centre; `volumes 0`; `renderPostProcessing False` on the Game and Hub cameras (Hub read from the scene file). Frame Debugger pass list not run: the live Editor MCP was down. |
| AC-4 | FAIL | Lit floor red channel +17 (limit ±15); wall tops far from target; shadow ratio 0.88 (above 0.85). Highlights sit upper-left and shadows fall right and toward the camera, short (about 0.34 of the wall height). The gap is MAT_Board specular; follow-up for the DEC-96 owner. |
| AC-5 | PASS | Read back in the copy and in the workspace files: MSAA 2, HDR off, shadow distance 30, current quality Balanced, Android/iPhone → Balanced, SSAO renderer unchanged, EditorBuildSettings byte-identical |
| AC-6 | PASS | Preview capture equals Game within 1/255; `LevelEditor.unity` root count unchanged and scene not dirty after dispose (asserted); LevelPreview EditMode tests pass |
| AC-7 | PASS | EditMode 70/70, PlayMode 27/27, 0 errors or warnings in the batch logs; live-Editor Console not readable (MCP down) |
| AC-8 | PASS | DEC-103 updated, DEC-118 row added; wording fixed in 3 docs; Environment-Layout camera values. DEC-104 was already cited by the gameplay contract, so DEC-118 is used. |

## Open items

- **Live Editor sync:**
  - Reconnect MCP for Unity in the Editor window (HTTPLocal, 8082), or restart the Editor, then confirm `QualitySettings.GetQualityLevel()` is Balanced.
  - A running Editor may keep the old in-memory quality level until restart and could write it back on its next project-settings save.
  - `Game.unity` (open, not dirty) and the other assets reload on the next refresh.
- **MAT_Board specular:** follow-up for the DEC-96 owner to reach the AC-4 pixel targets.
- **Device checks:** NFR-P5 cost of 2x MSAA with SSAO on the Android phone. Not claimed.
