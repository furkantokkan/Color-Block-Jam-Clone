---
name: cbj-performance-profiling
description: Measure and fix frame-rate, stutter, GC and draw-call problems in ColorBlockJamCase2026 (Unity 2022.3, Android). Use it whenever someone reports FPS drops, jank, hitches, slow or choppy block drags, "kasıyor", "fps düşüyor", "takılıyor", heavy GC, too many SetPass calls or batches, device or Editor performance, or asks to optimize or profile the game, even without the word "profile". It separates Editor-only cost from game cost with a live Profiler capture over the MCP 8082 bridge, measures on the USB phone with a copy-only development build and adb-driven drags, and maps the numbers to PROJECT.md Performance Rules and NFR budgets before anything is changed.
---

# CBJ performance profiling

The Editor's Stats panel and FPS counter add Editor work (window repaints, Windows message handling, IMGUI) to the
game's own frame. On 2026-09-29 a "drag stutters" report looked like a 37 FPS game problem; the capture showed the
game's PlayerLoop at 3-5 ms and 100 ms Editor spikes in `EditorLoop > Application.Message`. So: split Editor from
game first, confirm on the phone, and only then change code or settings. Every change needs before/after numbers
from the same level, device and quality level.

Read `PROJECT.md` (Performance Rules 1-8, DEC rows) before proposing a change; `references/baselines.md` holds the
last measured numbers and the signatures seen so far.

## Budgets and rules to measure against

| Item | Budget | Source |
| --- | --- | --- |
| Frame rate | 60 fps (16.7 ms) on the Android phone; `FrameRatePolicy` sets 60 before the first scene | NFR-2, DEC-28, DEC-183 |
| Draw cost | Level 5 Playing with HUD: SetPass ≤ 20, batches ≤ 150, triangles ≤ 200k | NFR-P4 |
| GC | 0 B per Playing frame in the game's own code | Performance Rule 5 |
| Ticking | One `IOnityTickable` (GameplayTickOwner); nothing runs outside Playing | Rule 4, lifecycle rules |
| Rendering | One shared material with MPB colour; UI in Sprite Atlases | Rules 1, 2 |

## Working in the shared Editor

Several sessions drive the same live Editor. Read-only queries are fine at any time. Do not enter Play Mode, open
scenes or write assets in it yourself: ask the user to reproduce, and let the hook record. Never use
`Undo.RecordObject` there. Always finish with `restore.cs`: the Profiler buffer grows about 1 MB per recorded frame.
Settings, scenes, prefabs and packages are high-risk targets that need the user's explicit approval.

## Choose the path

| Symptom | Path |
| --- | --- |
| Slow or stuttering in the Editor Game view | A (live capture), then B to confirm on the phone |
| Slow on the phone, or a release/perf check | B |
| "Did this code change regress?" without the phone | C, or B when the phone is connected |

## Path A: live Editor capture

All commands run from this skill folder. `live_editor.py` pins the ColorBlockJamCase2026 instance on the 8082 bridge
(BRN-Game-2 is often connected too).

1. Identity and state: `python scripts/live_editor.py code scripts/csharp/state.cs`. The path must be this repo. If
   the project is "not connected", ask the user to set the MCP for Unity window to HTTPLocal on port 8082.
2. Install the hook. First capture, game versus Editor split:
   `python scripts/live_editor.py code scripts/csharp/hook_install.cs --set FRAMES=1200 --set PROFILE_EDITOR=false`
   To see inside `EditorLoop` (second capture): `--set FRAMES=600 --set PROFILE_EDITOR=true`.
   Recording starts on the next EnteredPlayMode and stops when that Play session ends. A script recompile
   removes the hook, so check `state.cs` shows `hook=True` before asking for the repro.
3. Ask the user: open the level with Test Level (or Init), stay idle 2-3 s, drag the block about 5 s, stop Play.
   A plain Play on the LevelEditor scene is refused by its guard; the hook ignores that.
4. Analyze (each is a C# method body run through `execute_code`):
   - `split_held_free.cs`: held versus free frames, histogram, top self markers, held-minus-free deltas.
     Needs `PROFILE_EDITOR=false`.
   - `spike_chains.cs --set SPIKE_MS=34.5`: the dominant call chain of every spike and the spacing between spikes.
   - `marker_breakdown.cs --set MARKER=Application.Message --set SPIKE_MS=34.5` (or `OnGUI`, `PlayerLoop`).
   - `allocations.cs --set SCOPE=PlayerLoop`, then `--set SCOPE=All` to prove the scan finds Editor allocations.
   - `memory.cs`: GC collections versus spikes, leak counters versus the Profiler's own growth.
   - `editor_windows.cs`: visible windows that repaint during Play.
5. `python scripts/live_editor.py code scripts/csharp/restore.cs`.

### Reading a live capture

| Signature | Meaning | Next step |
| --- | --- | --- |
| PlayerLoop small, EditorLoop large | Editor cost, not game cost | Confirm with path B before touching game code |
| Spikes chain `EditorLoop > Application.Message`, self time far above its `MouseMove` children | Windows message pump: mouse input volume or input-hook software (Razer Synapse, Logitech Options+) | Editor-only; try a lower mouse polling rate or closing the utility; check the phone |
| `OnGUI`, `SceneView.Paint`, `GameView.Paint` high | Visible Editor windows repaint every frame | Play Maximized, hide the Scene view |
| `PlayerLoop > GUI.Repaint` or `IMGUISendQueuedEvents > GUIUtility.BeginGUI` allocates | `UIDocument.OnGUI`, which exists only in the Editor (the Android player's UIDocument has none) | Ignore for device budgets |
| `Inl_Unnamed_ScriptableRenderPass` + `Highlight Plus`, SetPass +30-40 while a block is held | Highlight Plus outline (DEC-145, Highest quality) | A look decision; measure on the phone with the probe's outline A/B |
| `GC Used Memory` flat across spikes | Spikes are not GC | Look elsewhere |
| `Profiler Used Memory` grows ~1 MB/frame | The Profiler's own buffer | Not a leak; restore afterwards |
| `PreUpdate.SendMouseEvents` present | Legacy Input Manager still active | Active Input Handling should be the Input System only |

## Path B: the Android phone

The phone is the target (NFR-2). A development build adds some overhead; compare builds of the same kind.

1. Check the phone: `adb devices -l` (adb lives in the Unity Android module:
   `%ProgramFiles%\Unity\Hub\Editor\2022.3.62f2\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools`).
2. Build: `powershell -ExecutionPolicy Bypass -File scripts/device_build.ps1 -Level 5`. It mirrors the repo into
   `%USERPROFILE%\.agent-context\cbj-perf\devbuild` (not %TEMP%, which was wiped mid-run on 2026-09-29),
   adds the copy-only probe and build script from `assets/device/`, sets the Game scene's level index and
   `enableFrameTimingStats` in the copy only, clears `Library/ScriptAssemblies`, `EditorInstance.json` and `ilpp.pid`
   (the copied pid would let the batch Editor kill the live Editor's IL post-processor), and runs a batch build with an
   isolated LOCALAPPDATA. Game is the first scene, so the APK opens straight into that level.
   The first IL2CPP ARM64 build took about 10 minutes; later builds in the same copy reuse its Bee cache.
   Before touching the phone, check it is not in use: `adb shell dumpsys telecom` must show no `state=ACTIVE` call,
   and `dumpsys window` no keyguard. Installing can raise a Play Protect prompt and launching takes the screen.
3. Run: `python scripts/device_session.py --apk <copy>\Builds\cbj-dev.apk --out <file>`. It launches the game,
   reads the block positions the probe logs, idles, drags the largest block half a cell back and forth (outline on
   and off on alternate presses), drags it across cells, idles again, and prints a table per group.
   Tell the user before they play the test build: DevPerfProbe hides the white outline on every second touch for
   the A/B, which looks like a bug (on 2026-09-29 the user reported it as one). The real game is unaffected.
   To test the real flow build with `-FromInit` and pass `--taps "x,y;x,y"` (Hub Level, then Play; take the
   coordinates from `adb exec-out screencap -p` screenshots) and `--shots <dir>` for a screenshot per press. If a
   button does nothing while the probe logs `held=True` windows, the touch reaches the Input System but not
   UI Toolkit: an EventSystem or panel-handler problem, as with an EventSystem in Init (DEC-188).
   For a GPU cost breakdown build with `-GpuBreakdown` and run `--mode breakdown`: 8 s after launch the probe
   measures baseline, SSAO off, MSAA 1x, shadows off, render scale 0.75, HUD hidden, the three combined, and
   baseline again (thermal drift check), 3 s each, and nobody should touch the phone meanwhile.
4. Read the table:
   - `dtAvg` against 16.7 ms and the `>20ms` / `>34ms` counts (hitches matter more than the average).
   - `gpu` near or above `dtAvg` while `main` is low: GPU-bound (fill rate, MSAA, SSAO, shadows, outline passes).
     `main` high: CPU-bound (scripts, culling, batching).
   - `gcB` should be about 0; each window carries the probe's own log line once.
   - `setpass` and `batches` against NFR-P4; `outline on` versus `outline off` is the outline's real cost.
5. Check the APK's native libraries: `python scripts/check_16kb.py <apk>`. A library with less than 16 KB LOAD
   alignment makes Android 16 show "Android App Compatibility" at launch and fails Google Play's 16 KB rule for
   Android 15+ targets. On 2026-09-29 only Nice Vibrations' `liblofelt_sdk.so` failed.
6. Nothing from the copy goes back to the repo unless it is a change the user approved (for example a scene edit
   made and verified there); copy those files back only when the live Editor has the target scene closed.

## Path C: isolated copy harness (no phone)

For an A/B of a code change: mirror the repo, add a copy-only PlayMode test that loads Game additively, drives an
`InputTestFixture` mouse along a block and records `ProfilerRecorder` values (Main Thread, GC Allocated In Frame,
SetPass, Batches) per scenario. Run it windowed (not `-batchmode`: allocation fixtures and UI Toolkit need a
graphics device) with `-testPlatform PlayMode -testFilter <name>`. Editor numbers include Editor overhead; use them
only to compare two versions against each other.

## Known levers in this project

| Lever | Where | Decision owner |
| --- | --- | --- |
| Frame rate 60 on mobile | `Runtime/Core/Startup/FrameRatePolicy.cs` | Done (DEC-183) |
| Input System only (no legacy Input Manager) | Player Settings, Active Input Handling | Done (DEC-188); never add an EventSystem to Init: it takes Hub's UI Toolkit handler with it when Init unloads |
| Outline quality, downsampling | `HeldBlockHighlight.Configure` (Highlight Plus, DEC-145) | Look decision by the user |
| SSAO | `Settings/URP-*-Renderer.asset`, SSAO feature `m_Active` | Off (DEC-187): it cost 9.4 ms of GPU on the Galaxy A16 |
| MSAA, shadow distance, render scale | `Settings/URP-Balanced.asset` (DEC-118) | Look decision by the user |
| Android Optimized Frame Pacing | Player Settings `androidUseSwappy` | User approval |
| Allocations in Playing | ZString/prebuilt strings, ZLinq, pools (Rules 3, 5, 8) | Code change with before/after numbers |

## Report

Give the user, and the DEC row when a decision is made:
- the bottleneck class (Editor-only, CPU script, CPU render, GPU, GC, loading);
- the numbers before (scenario, level, device, quality, build type);
- what changed and which work it removes;
- the numbers after, same scenario;
- what is still unmeasured (for example release build, thermal throttling after minutes of play).

## Files

- `scripts/live_editor.py`: 8082 client (`code`, `tool`, `resource`, `instances`).
- `scripts/csharp/*.cs`: `state`, `hook_install`, `restore`, `split_held_free`, `spike_chains`, `marker_breakdown`,
  `allocations`, `memory`, `editor_windows`.
- `scripts/device_build.ps1`, `scripts/device_session.py`: phone build, scripted drag session, GPU breakdown.
- `scripts/check_16kb.py`: 16 KB page alignment of every native library in an APK or AAB.
- `scripts/outline_check.py`: proves the held-block outline in every `--shots` screenshot against an idle baseline.
- `assets/device/DevPerfProbe.cs`, `assets/device/DevGpuBreakdown.cs`, `assets/device/DevBuild.cs`: copy-only
  probes and build script.
- `references/baselines.md`: measured numbers and history.
