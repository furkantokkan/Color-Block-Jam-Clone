# TASK-20260929-173012400-f8ec453e-android-block-touch-haptics — Android block-touch haptics on Vivo Y36

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260929-173012400-f8ec453e-android-block-touch-haptics
- Supersedes: none
- Title: Android block-touch haptics on Vivo Y36
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:38 plus pending workspace changes; planning snapshot 2026-09-29T17:30Z
- Task kind: bug
- Area: Android device haptics, block touch feedback
- Workflow: direct

## Objective and baseline

- Observable objective: Make the block-touch haptic perceptible on the reported Vivo Y36 when the game's Vibration setting is enabled.
- Current behavior/evidence: DragInputRouter calls BlockTouch after a block is successfully picked. HapticsService gates output on the saved Vibration setting, which defaults on. HapticStyles maps BlockTouch to Light. AndroidVibratorHaptics creates a 40 ms waveform at amplitude 41/255 on API 26+, and uses the duration-only Vibrator API on older supported versions. Android's official API reference says amplitude control is hardware-dependent. The Vivo's Android version, amplitude capability, system haptic settings, and device log are not available here; adb is not installed and no Vivo device is connected. The reported build is therefore not reproduced in this workspace.
- Expected behavior: A valid block press produces a clearly perceptible haptic on a Vivo Y36 with the game's Vibration setting on and the device able to vibrate. The fix uses the existing Android haptics service and evidence-supported runtime behavior; it does not add a compile-time define for one phone model.
- Behavior that must remain unchanged: Turning the game's Vibration setting off suppresses all haptics; other haptic moments retain their intended feedback; the iOS UIKit path and existing Android support for older API levels remain intact; no plugin, package, scene, or serialized setting is added.

## Ownership

- Owner/writer role: One implementer, then independent verification when device access is available
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Haptics/AndroidVibratorHaptics.cs
  - Assets/ColorBlockJam/Runtime/Haptics/HapticStyles.cs
  - Assets/ColorBlockJam/Runtime/Haptics/DeviceHapticOutput.cs
  - Assets/ColorBlockJam/Tests/UI/EditMode/HapticsTests.cs
  - production/tasks/TASK-20260929-173012400-f8ec453e-android-block-touch-haptics/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Gameplay/**
  - Assets/ColorBlockJam/Runtime/Player/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Data/**
  - Per-manufacturer compile defines or model-name branches without device evidence
  - Plastic checkin, shelve, player build, or publishing
- Serialized assets: none
- Other active writers/worktrees checked: The haptics source and HapticsTests.cs have no pending workspace edits. TASK-20260927-004448215-54655902-booster-gameplay-flow owns Gameplay and Player paths but not Runtime/Haptics or Tests/UI/EditMode/HapticsTests.cs. The current UI Builder task has no path overlap. No matching task contract owns the block-touch haptic behavior.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given an Android build on a Vivo Y36 with the game's Vibration setting on and system vibration available, when the player presses a movable block, then one clearly perceptible block-touch haptic is felt.
2. Given the game's Vibration setting off, when the player presses a movable block or triggers another haptic moment, then the device produces no game haptic.
3. Given an Android device using the supported pre-API-26 path, when a block-touch haptic plays, then the existing duration-based fallback still works without a compile-time phone-specific define.
4. Given the Haptics EditMode tests, when the BlockTouch mapping and vibration gate are exercised, then the selected style is non-None and the setting still gates output as before.

## References and constraints

- Story/GDD/ADR/design/log/image: User report in this conversation; PROJECT.md DEC-156 and DEC-186; Android Vibrator API reference at https://developer.android.com/reference/android/os/Vibrator and VibrationEffect API reference at https://developer.android.com/reference/android/os/VibrationEffect.
- Unity version/platform: Unity 2022.3.62f2; Android device target Vivo Y36; iOS behavior is preservation scope only.
- Package/architecture constraints: Keep ColorBlockJam.Haptics, its current JNI boundary, and the Player Vibration toggle. Use runtime API or hardware capability checks only when evidence supports them. Do not add dependencies or change packages, ProjectSettings, or serialized assets.
- Explicit out of scope: Haptics redesign across all moments, changing the gameplay press/pick flow, changing app or system settings, a manufacturer-specific build, adding a haptics plugin, and Android player builds without exact target approval.

## Verification and risk

- Focused test path/command: Assets/ColorBlockJam/Tests/UI/EditMode/HapticsTests.cs; run its focused HapticsTests fixture once at the final revision.
- Required execution preflight: unity-cli
- Required Unity domain workflow: unity-game-dev
- Unity compilation/Console evidence: Run unity_compile_check.py against every changed C# file; if it is COMPILE_UNVERIFIED, use Unity's compile check. Preserve unrelated Console evidence.
- EditMode: HapticsTests focused fixture, once after implementation is complete.
- PlayMode: not applicable to the service and style mapping.
- Manual/built-player/profiler/device evidence: Required on a Vivo Y36: confirm the app toggle is on, system vibration is available, and a block press is felt. A built Android player is required for this check; request exact player-build approval before building. No device is attached to this workspace, so no device result is claimed at planning time.
- Risk: low
- Approval already granted (exact target only): none; the user's report authorizes task intake only. Android player build requires separate exact target approval.
- Commit/checkin constraint: forbidden
- Current invocation permission: none; this invocation creates the task contract only.
- Handoff destination: $implement-task TASK-20260929-173012400-f8ec453e-android-block-touch-haptics

## Stop conditions

- Stop before a player build until the user approves the exact Android build target.
- If reproduction shows the game's Vibration setting or Android system vibration is off, report that evidence before changing haptic intensity.
- If the fix requires edits outside the allowed paths or adds a package/serialized asset, update the task through its approved change process first.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: in_progress
- Current phase: verification
- Last cycle verdict: none
- Contract fingerprint: sha256:aec2a5bb3acf71d987aa2cabd4886669269f2f6a61fe2b78fdec39d461d8c4e1
- Evidence fingerprint: sha256:c77e72565369b6d0b51c5624ddd910b4468cccdc2be29c586cccc34a692519a6
- Reviewed revision: vcs:cs:38+owned-sha256:10f8bdfdfbd41183569e13d34def827ecfd350d2c7fa7742214138a8d1d7699a
- Attempt count: 1
- Last feedback: none
- Blocker: Vivo Y36 device verification needs an Android player build and attached device; exact build target approval is outstanding. AC-3 device check also awaits compatible hardware.
- Next action: Approve the exact Android player-build target, verify AC-1 on Vivo Y36 and AC-3 on pre-API-26 hardware if available, then continue $implement-task TASK-20260929-173012400-f8ec453e-android-block-touch-haptics
- Closed revision: none
- Closed at: none
- Closure mode: none
- Closed by: none
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | UNPROVEN | manual device verification | vcs:cs:38+owned-sha256:10f8bdfdfbd41183569e13d34def827ecfd350d2c7fa7742214138a8d1d7699a |
| AC-2 | PASS | focused HapticsTests | vcs:cs:38+owned-sha256:10f8bdfdfbd41183569e13d34def827ecfd350d2c7fa7742214138a8d1d7699a |
| AC-3 | UNPROVEN | focused HapticsTests and Android device verification where available | vcs:cs:38+owned-sha256:10f8bdfdfbd41183569e13d34def827ecfd350d2c7fa7742214138a8d1d7699a |
| AC-4 | PASS | focused HapticsTests | vcs:cs:38+owned-sha256:10f8bdfdfbd41183569e13d34def827ecfd350d2c7fa7742214138a8d1d7699a |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored
- Started — implementation — direct implement-task under the lifecycle lock in Library/ccgs/task-lifecycle-locks; Unity preflight READY WITH WARNINGS; exact Unity 2022.3.62f2 project instance resolved through MCP for focused EditMode verification
- Verification — Android Light touch amplitude raised; Roslyn compile passed and HapticsTests passed 7/7; Vivo Y36 and pre-API-26 device checks remain unproven
<!-- TASK-LIFECYCLE:END -->
