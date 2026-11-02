# Measured baselines

Newest first. Compare a new measurement only with one of the same kind (same level, device, quality level, build
type, input handling). Record the date, the build or Editor state and the exact scenario.

## 2026-09-29: drag stutter report, level 5

Scenario: level 5 (Test Level), idle 2-3 s, orange 3x1 block dragged back and forth for about 5 s. Quality
`Balanced` (MSAA 2x, SSAO, soft shadows), Active Input Handling `Both`, Highlight Plus outline at `Highest`.

### Editor, live capture (Windows, 24 threads, Game view 1080 x 1920)

User-visible: Stats showed 36.9 FPS (27.1 ms), CPU main 23.7 ms, render thread 5.1 ms, SetPass 39, batches 76,
181.7k triangles, 152 shadow casters while the block was held.

Capture 1 (`profileEditor=false`, 1256 frames):

| Group | Frames | Avg ms | > 50 ms | PlayerLoop | EditorLoop self |
| --- | --- | --- | --- | --- | --- |
| Free | 861 | 24.3 | 48 | ~3 ms | 20.0 ms |
| Held | 395 | 26.9 | 50 | ~3.5 ms | 22.4 ms |

- Held adds about 0.6 ms of rendering: `Inl_Unnamed_ScriptableRenderPass` +0.44 ms and `Highlight Plus` +0.09 ms.
- While held, a spike of 80-135 ms every 4-5 frames; PlayerLoop 3-5 ms inside those frames.
- `GC Used Memory` flat at 1154 MB across spikes (not GC). Mesh, texture, render texture and material counts flat.
- `Profiler Used Memory` 160 MB to 1.57 GB over the capture (the Profiler's own buffer).

Capture 2 (`profileEditor=true`, 502 frames):

| Frames | Avg frame | `Application.Message` total | Its self time |
| --- | --- | --- | --- |
| 44 spikes | 99.7 ms | 63.1 ms | almost all (children `MouseMove` ~1 ms per spike) |
| 458 normal | 14.5 ms | 2.5 ms | |

- Every spike chain was `EditorLoop > Application.Message`: the Windows message pump. Mouse and input-hook software
  running at the time: Razer Synapse (RzSynapse, 10 RazerAppEngine processes), Logitech Options+, MSI Afterburner.
  Seven Unity.exe processes were running (other sessions).
- `OnGUI` self 3.15 ms per frame (visible Editor windows).
- PlayerLoop allocations: ~950 B per frame from `GUI.Repaint` / `IMGUISendQueuedEvents > GUIUtility.BeginGUI`
  (`UIDocument.OnGUI`, Editor-only: the Android IL2CPP UIElementsModule has no `UIDocument.OnGUI`), plus ~73 B per
  frame in the Highlight Plus pass and ~36 B in `ScriptableRenderer.AddRenderPasses` (both also on device).

### Editor, isolated copy (level 1, vsync off, windowed batch copy)

| Scenario | Main thread | GPU | SetPass | Batches |
| --- | --- | --- | --- | --- |
| Idle | 6.3 ms | 1.5 ms | 10 | 56 |
| Drag with outline | 7.7-7.9 ms | 2.1-2.3 ms | 50 | 96 |
| Drag, outline off | 6.5-8.1 ms | 1.5-1.7 ms | 10 | 41-56 |

### Device: Samsung Galaxy A16 (SM-A165F), Android 16, Mali-G57 MC2, Vulkan, 1080 x 2340, 60 Hz

Development IL2CPP ARM64 build, Game first (level 5), `FrameRatePolicy` 60, Input System only, DevPerfProbe.
`device_session.py` drag plan; windows grouped per phase.

| Group | Frames | FPS | dtAvg | Frames > 20 ms | Main | GPU | GC B/frame | SetPass | Batches |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Not touching | 31-151 | 38.2-41.2 | 24.3-26.2 | about 55 % | 24.4-26.1 | 24.4-26.1 | 15-98 | 10-11 | 39-40 |
| Lead drag, outline on | 230 | 34.9 | 28.63 | 165 | 28.57 | 28.55 | 943 | 38 | 67 |
| Lead drag, outline off | 175 | 38.9 | 25.72 | 95 | 25.84 | 25.78 | 32 | 10 | 39 |
| Step drag, outline on | 100 | 33.5 | 29.84 | 78 | 29.86 | 29.79 | 209 | 46 | 75 |
| Step drag, outline off | 117 | 39.0 | 25.65 | 63 | 25.64 | 25.68 | 32 | 10 | 39 |

- GPU-bound at about 25 ms even when idle: 60 fps (16.7 ms) is not reached on this phone at `Balanced`.
  Main equals GPU because the main thread waits for the GPU.
- The Highlight Plus outline costs about 3-4 ms of GPU per frame (+28 SetPass) and about 0.9 KB of GC per frame
  while a block is held.
- Touch drag, a HUD button (Pause opened the Settings popup with HOME) and Android Back (closed it) all worked with
  Active Input Handling set to the Input System only.
- The user reported the dev build "sometimes shows no outline": that is the probe's A/B on even presses.
- `check_16kb.py`: `liblofelt_sdk.so` (Nice Vibrations 4.1.2) has 4 KB LOAD alignment; Android 16 showed
  "Android App Compatibility" at the first launch. Unity's own libraries are 16 KB aligned. Nice Vibrations was
  then replaced by the project's own haptics (DEC-186).

GPU breakdown (`-GpuBreakdown`, `--mode breakdown`, level 5 idle, nobody touching; 3 s per configuration):

| Configuration | FPS | dtAvg ms | Frames > 20 ms | GPU ms | vs baseline |
| --- | --- | --- | --- | --- | --- |
| Baseline (MSAA 2x, SSAO, shadows 30, scale 1) | 38.3 | 26.09 | 64 | 26.08 | 0 |
| SSAO off | 60.0 | 16.67 | 0 | 16.45 | -9.42 (vsync-capped) |
| MSAA 1x | 38.0 | 26.32 | 66 | 26.36 | +0.23 |
| Shadows off (distance 0) | 40.3 | 24.79 | 59 | 24.72 | -1.30 |
| Render scale 0.75 | 55.0 | 18.18 | 15 | 18.16 | -7.91 |
| HUD hidden | 41.0 | 24.39 | 57 | 24.34 | -1.70 |
| SSAO + MSAA + shadows off | 60.0 | 16.67 | 0 | 16.20 | -9.42 (vsync-capped) |
| Baseline again | 37.8 | 26.46 | 67 | 26.37 | +0.37 (no thermal drift) |

- SSAO alone decides 60 fps on this phone: with it the GPU needs about 26 ms, without it the frame is vsync-bound.
  MSAA 2x costs nothing measurable (Mali resolves on tile). SSAO was then turned off (DEC-187).

After DEC-186/187/188 (repo state, `-FromInit`, `--taps` Level then Play, level 1, no outline A/B):

| Group | FPS | dtAvg ms | GPU ms | SetPass | Batches | GC B/frame |
| --- | --- | --- | --- | --- | --- | --- |
| Idle | 58.5-58.8 | 17.0-17.1 | 17.0 | 7-8 | 42 | 14-16 |
| Held, outline on | 46.7 | 21.4 | 21.3 | 46 | 81 | ~190 |

- `outline_check.py`: all 6 press screenshots showed the outline (+12.5k white pixels, idle baseline 0).
- `dumpsys vibrator_manager`: block touch (~50 ms, amplitude 0.16), gate exit and win vibrations `finished`; no UI
  button vibrated (ClickEvent never reached self-closing buttons; `ButtonHaptics` now uses PointerUpEvent). Read the
  `played:` amplitudes to tell the styles apart before claiming which moment vibrated.
- The outline is now the largest remaining GPU cost (~4.3 ms while held); its quality is a look decision
  (DEC-145).
