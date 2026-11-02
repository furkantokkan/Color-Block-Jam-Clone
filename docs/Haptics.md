# Haptics

On 2026-09-30, device haptics were extracted into the standalone `Haptics`
library at the user's request. The name has no studio or game prefix. The
self-contained `Assets/Haptics` folder includes runtime sources, native files,
tests and its [usage guide](../Assets/Haptics/README.md). It was published to a
separate GitHub repository on 2026-09-30.

- `Assets/Haptics/Runtime`: `Haptics` assembly and namespace, `HapticStyle`,
  `IHapticOutput`, `DeviceHapticOutput`, Android JNI and iOS UIKit output.
  There are no Onity, player-data, UI or game references.
- `Assets/ColorBlockJam/Runtime/Haptics`: game-only `IHaptics`, `HapticFeedback`,
  `HapticStyles`, `IVibrationToggle` and `HapticsService`. The existing setting
  subscription stops playback when disabled and confirms enabling with a tap.
- `PlayerDataInstaller`: binds the standalone output and game adapter as
  project-scope singletons. Construction and disposal stay on the main thread.
- The existing Android Light amplitude of 120 is preserved. Preset timings,
  other amplitudes and game-event mappings are unchanged. The standalone output
  rejects unknown styles and ignores calls after its idempotent disposal.
- Source and plugin files moved with their original `.meta` GUIDs. iOS retains
  its iOS-only importer; both managed and native sides call `Haptics_Play`.
  Android's permission library now uses the generic `com.haptics.permission`
  manifest package and still declares `android.permission.VIBRATE`.

## Verification

- Unity target: `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026`,
  `2022.3.62f2`; Unity CLI `1.0.0-beta.11`, live Editor through the approved
  pre-Unity-6 MCP transport. Preparation: READY.
- Unity compiled and loaded `Haptics`; the adapter constructor resolves its
  output parameter as `Haptics.IHapticOutput`.
- Roslyn: `COMPILE_OK`, all 15 affected/dependent assemblies passed, including
  both haptics assemblies and their tests.
- Focused EditMode: 24/24 passed, 0 failed, 0 skipped; job
  `31ba171b52e0472d88a6b0aa29955d48`. The standalone output contributed 14
  cases; the game adapter contributed 10. Coverage includes native enum values,
  framework independence, Editor-safe playback/disposal, setting gating and
  confirmation, unknown game moments, missing dependencies and UI button routing.
- Final source review: all 9 moved metadata files match their original content;
  managed/native `Haptics_Play` symbols match. Live importer readback confirmed
  the original iOS GUID, iOS enabled, Android/Editor disabled and no script
  compilation failure. Automatic asset refresh is enabled.
- Domain workflow: `unity-game-dev`, with `unity-cli`, `implement-task` and
  `clean-oop-architecture` for the bounded extraction.
- Android/iOS builds and device feel were not run for this extraction. Editor
  tests do not verify native linking or physical vibration.
- The extraction changed no package manifest, scene, prefab, settings or save
  format and made no commit or check-in. Unrelated workspace changes were preserved.

## GitHub Publishing

- Repository: [furkantokkan/Haptics](https://github.com/furkantokkan/Haptics),
  private, default branch `main`.
- Initial commit: `7fe9034cafe00b8a7746d02abe2ac6ad2fb67997`
  (`Add standalone Unity haptics library`).
- Local checkout: `C:/Users/e-fur/Documents/Repos/Haptics`. All 28 exported
  files matched `Assets/Haptics` byte for byte, including Unity metadata.
- Remote `main` matches the local commit and the standalone checkout is clean.
  No game files were published and no Plastic SCM check-in was made.
- Runtime and test sources remain at the revision verified above; no tests were
  rerun for the unchanged export. Native build/device validation remains pending.
