# UI Sprite Atlases: draw counts and capture comparison

Final verification of `TASK-20260927-001142693-43e35c51-ui-atlases-portrait-lock`
(DEC-142), run on 2026-09-28 by an independent verifier in the shared live Editor
(Unity 2022.3.62f2, active build target Android, MCP for Unity HTTP transport).
Nothing under `Assets`, `ProjectSettings`, `Packages` or `production/tasks` was
changed by this pass. The sprite packer mode was switched only for the measurement
and capture runs and left at Sprite Atlas V2 - Enabled (5). `ProjectSettings/EditorSettings.asset`
is byte-identical to its state before the pass.

## Settings readback

| Item | Read back |
| --- | --- |
| `PlayerSettings.defaultInterfaceOrientation` | `Portrait` (`defaultScreenOrientation: 0`). The four `allowedAutorotateTo*` flags stay on; Unity reads them only under Auto Rotation. |
| `EditorSettings.spritePackerMode` | `SpriteAtlasV2` (5), Sprite Atlas V2 - Enabled |
| `HomeSettings.spriteatlasv2` | Allow Rotation off, Tight Packing off, Padding 4, mipmaps off, sRGB on, Bilinear, Include in Build on; Android and iOS override ASTC 6x6, Max 2048; 25 packables, one 2048x2048 page |
| `HudPopups.spriteatlasv2` | same settings; 19 packables, one 2048x2048 page plus one 2048x1024 page |
| Membership | 47 textures under `Art/UI`. 44 sit in exactly one atlas (listed one by one, no folder packables). The three full-screen backgrounds (`bg_home_screen`, `bg_menu`, `bg_block_texture`) are in none. `Icons/Gameplay/target.png` does not exist in this workspace yet; it arrives with the booster gameplay task. |
| Source compression | 42 packed sprites import with Compression None and no platform override. `Panels/bg_gameplay_time_holder` and `Buttons/Btn_booster_normal` are still Compressed, because the booster task owns them. The backgrounds keep their own compression. |
| Packing warnings | The latest pack in the Console warns "Source Texture ... is using compressed format" for `Btn_booster_normal` and `bg_gameplay_time_holder` only. A new `SpriteAtlasUtility.PackAtlases` call on the Android target found the cache up to date and logged nothing. |

## Method

- Play Mode entered from `Init` (Play Mode start scene set to `Init` for the entry
  only, then reset to none) and routed to Hub. Enter Play Mode Options skip the
  domain and scene reload.
- A read-only probe (MCP `execute_code`) swapped the Hub `UIDocument` onto a runtime
  copy of its PanelSettings that targets a 1080 x 1920 RenderTexture. This is the
  same technique as the capture tests, so layout matches the captures. It turned
  off the UI input module, then opened each screen through the Hub container:
  - Settings: `IHubNavigator.OpenSettings`
  - Pre-level: `IHubNavigator.OpenPreLevel`
  - Purchase: `BoosterPurchaseOverlay.Open(Rocket, PreLevelPresenter)`, over the pre-level popup
- For each screen, the Editor was paused and the Frame Debugger
  (`FrameDebuggerWindow.EnableFrameDebugger`, read through
  `FrameDebuggerUtility`) listed the frame's events. UI Toolkit draws are the
  `UGUI.Rendering.RenderOverlays/UIR.DrawChain/UIR.DrawRanges` events on `verify-rt`.
  The camera's URP events (clear, SSAO, final blit) are not UI and are excluded.
- Draw calls = the sum of each UIR event's `m_DrawCallCount`, read after setting the
  event limit and letting the window repaint. UIR's own `UIRenderDevice.GatherDrawStatistics`
  agrees with it on every screen (`drawRangeCount + 1` draws in `drawRangeCallCount` DrawRanges batches).
- Distinct textures = the textures behind the resolved `background-image` of every
  displayed element. This shows whether the packer is active: with the packer ON, a
  sprite's texture is the atlas page (`sactx-...`).
- Captures: the existing explicit capture tests `HubCaptureTests.CaptureHome`,
  `HubCaptureTests.CaptureSettings` and `PreLevelCaptureTests.CapturePreLevelAndPurchase`
  ran with `CBJ_HUB_CAPTURE=1` set in the Editor process for those runs only.
  Each run's 20 PNGs were copied out, and the stored PNGs under `docs/verification/hub-*`
  were restored byte-identically (md5 checked). The variable was cleared afterwards.
  The Editor renders ASTC atlas pages by decoding them on the CPU, so the ON set
  shows the real ASTC 6x6 result.

## UI draw counts at 1080 x 1920

| Screen | Packer OFF: draw calls (UIR batches) | Packer ON: draw calls (UIR batches) | Textures OFF | Textures ON |
| --- | --- | --- | --- | --- |
| Home | 3 (3) | **1 (1)** | 16 | 2 (`bg_home_screen`, HomeSettings page) |
| Settings over Home | 5 (5) | **1 (1)** | 26 | 4 (`bg_home_screen`, HomeSettings, both HudPopups pages) |
| Pre-level over Home | 5 (4) | **2 (1)** | 23 | 4 |
| Purchase over Home (Rocket) | 6 (4) | **3 (1)** | 22 | 4 |

- NFR-P6 (Home at 6 UI draws or fewer): met. Home draws 1 call with the packer on,
  and 3 with it off.
- UI Toolkit in 2022.3 binds several textures per batch (texture slots), so even
  unpacked sprites share batches. Packing cuts Home from 16 textures to 2 and every
  screen to one DrawRanges batch.
- Pre-level and purchase stay at 2 and 3 GPU draws inside that one batch: UIR splits
  the batch into several draw ranges. This pass did not isolate why UIR splits it.
  Only 4 textures are bound, so texture slots are not the cause.
- Game-scene HUD screens are not measured: no gameplay HUD screen uses these atlases yet.

## Capture comparison (packer OFF vs ON)

Same 20 captures, same seeds, compared per pixel on RGB (0 to 255):

| Capture set | Max channel delta | Mean channel delta | Pixels with delta > 16 | Pixels with delta > 32 |
| --- | --- | --- | --- | --- |
| Home (4 images) | 96 to 99 | 0.17 to 0.21 | 3,630 to 3,883 (0.14 to 0.19 %) | 488 to 553 (0.02 to 0.03 %) |
| Settings (4) | 97 to 116 | 0.35 to 0.44 | 3,761 to 4,006 (0.15 to 0.19 %) | 428 to 691 (0.02 to 0.03 %) |
| Pre-level (6) | 101 to 114 | 0.23 to 0.29 | 2,197 to 3,020 (0.08 to 0.14 %) | 288 to 500 (0.01 to 0.02 %) |
| Purchase (6) | 83 to 87 | 0.20 to 0.25 | 1,334 to 1,355 (0.05 to 0.06 %) | 160 (0.01 %) |

Where the differences sit (see `*-off-on-diff.png` and `max-delta-crops.png`):

- Only on packed sprites: along anti-aliased silhouettes (lock icons, the Rocket icon,
  the popup shell rim, the close button, the selector circles) and in smooth gradients
  of the popup shell. The maxima sit at the inner rim of `bg_popup` and on the
  `lock` icon edge. Both show ASTC 6x6 block artifacts and bilinear edge sampling
  of an atlas region instead of a standalone texture.
- None on `bg_home_screen`, which is unpacked. It is the control area.
- Faint 1-pixel differences on label text outlines, from sub-pixel filtering next
  to the changed sprites. No layout, position, size or color shift, and no missing
  or extra element.

Verdict: the ON captures match the OFF captures within compression tolerance.

## Residuals and not run

- `Btn_booster_normal` and `bg_gameplay_time_holder` still import compressed and
  warn on packing. The booster gameplay task brings them uncompressed in its own
  `.meta` versions.
- Device checks were not run: portrait lock on the Android phone, ASTC memory on
  device (estimate: 4.46 MiB for the three pages), and device draw or frame cost.
- Draw counts come from the Editor (Direct3D 11). UI Toolkit's texture slots
  per batch can differ on a mobile graphics API, so device batch counts may be
  higher. The device Frame Debugger check is still open.

## Tests run with the packer on (Sprite Atlas V2 = 5)

| Suite | Result |
| --- | --- |
| PlayMode `ColorBlockJam.Hub.PlayModeTests` | 17 passed, 0 failed, 3 skipped (the explicit capture tests, ignored without `CBJ_HUB_CAPTURE`) |
| PlayMode `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests` | 6 of 6 passed |
| EditMode `ColorBlockJam.Hub.EditModeTests` | 81 of 81 passed |
| Capture tests, packer ON and packer OFF (explicit, by name) | 3 of 3 passed in each state |
