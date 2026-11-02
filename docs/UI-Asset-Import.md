---
title: UI Asset Import
created: 2026-09-27
modified: 2026-09-29
type: reference
status: active
tags: [color-block-jam, ui-toolkit, sprites, nine-slice, game-developer-case]
---

# UI Asset Import

Import settings and nine-slice border data for the 47 supplied PNGs under
`Assets/ColorBlockJam/Art/UI`. Written for the Hub Home screen task
(`production/tasks/TASK-20260926-235915697-461b4f9c-hub-home-screen/contract.md`,
scenario 12) against `docs/Hub-And-Player-Data-Design.md` sections 6.6-9. Source
classification and border list: `AGENTS.md` "Art Assets".

## Purpose

Every UI sprite is one of two import classes (`nine-slice` or `fixed`). This
doc records, per sprite, which class it is, its measured nine-slice border
(where applicable), and whether it is actually wired into a UXML/USS screen
yet ("applied") or still pending. Designers and the UI Toolkit layout author
use this table instead of re-measuring borders by eye each time a new screen
needs one of these sprites.

## The two classes

- **`nine-slice`**: panels and button backgrounds that stretch. Border is set
  in the Sprite Editor, Mesh Type Full Rect, and consumed in UI Toolkit USS
  with `-unity-slice-left/top/right/bottom`. Never scale these non-uniformly
  without a border. 15 sprites carry this class (9 `Panels/*`, 6 `Buttons/*`).
- **`fixed`**: icons, masks, and full-screen backgrounds that keep their pixel
  aspect. Backgrounds additionally use
  `-unity-background-scale-mode: scale-and-crop` so a 20:9 device crops
  instead of stretching. 32 sprites carry this class.

## Common import settings (from `AGENTS.md`)

Applies to all 47 sprites unless a future exception is recorded:

- Texture Type: Sprite (2D and UI); Sprite Mode: Single; Sprite Mesh Type: Full Rect
- Pixels Per Unit: 100; Pivot: Center; sRGB: on; Filter: Bilinear
- Max Size: source size
- Compression (DEC-142, 2026-09-28): a sprite packed in a Sprite Atlas imports
  **uncompressed** (default Compression None, no Android or iOS override); the
  atlas owns the device format (Android and iOS ASTC 6x6). The three full-screen
  backgrounds stay out of the atlases and keep their own importer compression;
  `AGENTS.md` asks for an Android and iOS ASTC 6x6 override on them (see
  [Sprite Atlases](#sprite-atlases-dec-142))
- Mipmaps: off; Alpha Is Transparency: on; Wrap Mode: Clamp
- Sprite Atlas packing: Allow Rotation off, Tight Packing off, Padding 4, one
  atlas per screen group (`PROJECT.md` Performance Rule 2)

Checked 2026-09-27 before the Hub Home task wrote anything: all 47 `.meta`
files had `spriteBorder` `{0,0,0,0}`, `spriteMeshType: 1` (Tight) and no
platform override turned on (`overridden: 0`). So the Mesh Type (Full Rect in
`AGENTS.md`) was **not** applied; the device format now comes from the Sprite
Atlases (DEC-142). The Hub Home task changed only
`spriteBorder` on the three sprites marked applied below; UI Toolkit slices a
bordered sprite from its rect, and the Home captures show the three stretched
sprites without seams at Tight mesh type.

## Border measurement method (2026-09-27)

For each of the 15 `nine-slice` sprites: loaded the PNG as premultiplied RGBA
(`Pillow` + `NumPy`), and for each edge walked outward from the image's own
center row/column, comparing every line against that center reference line
(mean absolute channel difference, tolerance ≈6/255). The border is the
distance from the true canvas edge to the first line that still matches the
center reference — i.e. where the rounded corner, decorative frame, or any
transparent padding baked into the canvas ends and the flat, genuinely
repeatable strip begins. Border is measured against the full canvas (matches
how Unity's `spriteBorder` slices the whole texture rect, including baked-in
transparent padding).

Sanity check: rendered every sprite through a from-scratch nine-slice
compositor (corners copied verbatim, edges resized along one axis, center
resized both axes) at 3x width / 0.7x height and 3x width / 1.5x height, plus
a "wide pill" render (800px wide) for `bg_home_resource_holder` and
`btn_level_green`, and inspected every result image directly. Where a
rendered stretch showed a visible seam, warped decal, or corner distortion,
the confidence/verdict below reflects that evidence rather than the border
numbers alone. Independently cross-checked against an earlier automated pass
(a first survey script): 13 of 15 sprites matched to the pixel: 1
sprite (`btn_toggle_on`) differed by 1-2px (both passes agree it is a poor
9-slice candidate); 1 sprite (`btn_level_bg_normal`) differs substantially on
the left border (this doc's value is preferred — see its note below).

The stretch-test renders and the machine-readable border list were working
files of the measuring pass and are not kept in the repository; the table below
is the record.

## Original source verification

On 2026-09-30, the current assets were compared directly with the original
`drive-download-20260924T081806Z-1-001.zip` delivery archive.

- Archive SHA-256: `861df8ed9fc4765df3db7cdaabcb38ca2d3bda6f3efd71baf65cd5dad9e03664`.
- All **47 UI PNGs, four FBXs and one ice texture** are byte-identical to the
  original entries. PNGs were matched by SHA-256 to account for renamed paths.
- All 47 supplied PNGs retain their GUIDs from the project's initial
  [import audit](art/asset-import-audit.json). The ZIP itself contains no Unity
  `.meta` files; importer settings and nine-slice borders are project setup.
- **31 original UI filenames were renamed; 16 stayed unchanged.** The 32-row
  table below also includes `target.png` → `ic_target.png`, which was added
  separately and is absent from the delivery ZIP. Its placement in the current
  `Supplied` folder does not make it part of the original supplier set.

## Renamed sprites (DEC-191)

The supplied names mixed casing styles (`Btn_`, `btn_`, `ic_`, `bg_`, PascalCase
and bare names). On 2026-09-29, on the user's order, every sprite that did not
follow the standard was renamed to lowercase snake_case with a type prefix:
`bg_` backgrounds and panels, `btn_` buttons, `ic_` icons, `mask_` masks
(`AGENTS.md` "Art Assets"). Each file moved with `cm mv` together with its
`.meta`, so its GUID, importer settings, borders and atlas membership are
unchanged. The 16 sprites that already followed it kept their names. Use this
table to translate the original names in older task contracts, verification
records, DEC rows and the supplied asset set. Paths are relative to
`Assets/ColorBlockJam/Art/UI/Supplied`.

| Original name | New name |
| --- | --- |
| `Buttons/Btn_booster_normal.png` | `Buttons/btn_booster_normal.png` |
| `Buttons/Btn_circle_blue_small.png` | `Buttons/btn_circle_blue_small.png` |
| `Buttons/Btn_circle_green_small.png` | `Buttons/btn_circle_green_small.png` |
| `Buttons/Btn_levelBg_normal.png` | `Buttons/btn_level_bg_normal.png` |
| `Buttons/Btn_level_green.png` | `Buttons/btn_level_green.png` |
| `Buttons/Btn_popup_blue.png` | `Buttons/btn_popup_blue.png` |
| `Buttons/Btn_popup_green.png` | `Buttons/btn_popup_green.png` |
| `Buttons/Btn_settings_normal.png` | `Buttons/btn_settings_normal.png` |
| `Buttons/Btn_square_retry.png` | `Buttons/btn_square_retry.png` |
| `Buttons/Popup_Close_Button.png` | `Buttons/btn_popup_close.png` |
| `Icons/Boosters/Clock_Freeze.png` | `Icons/Boosters/ic_clock_freeze.png` |
| `Icons/Boosters/Hammer.png` | `Icons/Boosters/ic_hammer.png` |
| `Icons/Boosters/Rocket_Purple_Icon.png` | `Icons/Boosters/ic_rocket_purple.png` |
| `Icons/Boosters/Vacuum.png` | `Icons/Boosters/ic_vacuum.png` |
| `Icons/Currency/Coin_Single.png` | `Icons/Currency/ic_coin_single.png` |
| `Icons/Currency/Heart.png` | `Icons/Currency/ic_heart.png` |
| `Icons/Gameplay/Clock.png` | `Icons/Gameplay/ic_clock.png` |
| `Icons/Gameplay/target.png` | `Icons/Gameplay/ic_target.png` |
| `Icons/Navigation/Lego_Brickv1.png` | `Icons/Navigation/ic_lego_brick_v1.png` |
| `Icons/Navigation/Settings_Icon.png` | `Icons/Navigation/ic_settings.png` |
| `Icons/Navigation/Support_icon.png` | `Icons/Navigation/ic_support.png` |
| `Icons/Navigation/block_collection.png` | `Icons/Navigation/ic_block_collection.png` |
| `Icons/Navigation/home_icon.png` | `Icons/Navigation/ic_home.png` |
| `Icons/Navigation/leaderboard.png` | `Icons/Navigation/ic_leaderboard.png` |
| `Icons/Navigation/lock.png` | `Icons/Navigation/ic_lock.png` |
| `Icons/Navigation/shop.png` | `Icons/Navigation/ic_shop.png` |
| `Icons/Settings/Blockjam_Settings_Music.png` | `Icons/Settings/ic_settings_music.png` |
| `Icons/Settings/Blockjam_Settings_Sound.png` | `Icons/Settings/ic_settings_sound.png` |
| `Icons/Settings/Blockjam_Settings_Vibration.png` | `Icons/Settings/ic_settings_vibration.png` |
| `Masks/SquareRoundedMask.png` | `Masks/mask_square_rounded.png` |
| `Panels/ActiveTab.png` | `Panels/bg_active_tab.png` |
| `Panels/profile_box_blue.png` | `Panels/bg_profile_box_blue.png` |

## All 47 sprites

Unity border order is **L, B, R, T** (as in `AGENTS.md`/`Sprite.border`). USS
slice order is **L, T, R, B** (as in `-unity-slice-*`). "Sprite Editor border"
says whether the measured border is set on the importer (`TextureImporter.spriteBorder`).
The three sprites the Home screen stretches carry one (applied 2026-09-27 by the
Hub Home task through `spriteBorder` and `SaveAndReimport`, no other importer field
changed); their USS `-unity-slice-*` values in `Hub/UI/Home.uss` equal these borders.
The Settings task applied `bg_popup`, `bg_popup_header`, `bg_popup_info_holder`,
`btn_popup_blue` and `btn_popup_green` the same way (2026-09-27, read back through
`TextureImporter.spriteBorder` and `Sprite.border`); `UI/Settings/SettingsPopup.uss`
repeats them and `SettingsPopupAssetTests` checks both. The pre-level task reuses them. "Used by" names the
screen that uses the sprite now; "planned" rows are not wired yet.

| Path | Size (px) | Class | Border L,B,R,T (Unity) | Slice L,T,R,B (USS) | Confidence | Sprite Editor border | Used by |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Panels/bg_popup.png | 976×976 | nine-slice | 106, 104, 107, 74 | 106, 74, 107, 104 | High | **applied 2026-09-27** | shared Settings popup shell (sliced, `UI/Settings/SettingsPopup.uss`); pre-level popup (`Hub/UI/PreLevel.uss`) and shared purchase popup (`UI/Purchase/BoosterPurchasePopup.uss`) shells (sliced) |
| Panels/bg_popup_header.png | 612×204 | nine-slice | 79, 90, 79, 90 | 79, 90, 79, 90 | High | **applied 2026-09-27** | Settings, pre-level and purchase popup title tabs (sliced) |
| Panels/bg_popup_info_holder.png | 522×335 | nine-slice | 43, 35, 42, 26 | 43, 26, 42, 35 | High | **applied 2026-09-27** | Settings popup inset holding the rows and buttons; pre-level selector inset; purchase icon and description inset (sliced) |
| Panels/bg_popup_info_holder_2.png | 62×68 | nine-slice | 23, 23, 23, 23 | 23, 23, 23, 23 | Medium | measured, not applied | — |
| Panels/bg_home_coin_holder.png | 101×84 | nine-slice | 24, 24, 25, 24 | 24, 24, 25, 24 | Medium | **applied 2026-09-28** (Game HUD task, DEC-153): border and Full Rect only, GUID unchanged | Game HUD coin holder pill (sliced, `UI/Game/TopBar/GameTopBar.uss`) |
| Panels/bg_home_resource_holder.png | 155×192 | nine-slice | 40, 52, 40, 45 | 40, 45, 40, 52 | High | **applied 2026-09-27** | Home currency holder pill (sliced) |
| Panels/bg_gameplay_time_holder.png | 151×115 | nine-slice | 50, 50, 50, 50 | 50, 50, 50, 50 | Medium | **set by the booster task** (B-27): Full Rect, Max Size 256, Uncompressed, no Android/iOS override (the Sprite Atlas compresses); replaces the measured 37, 40, 37, 39 | Game HUD timer field inside the top bar's Time panel (sliced with `-unity-slice-scale: 0.6px`, `UI/Game/TopBar/GameTopBar.uss`, DEC-153) |
| Panels/bg_profile_box_blue.png | 512×512 | nine-slice | 139, 149, 138, 140 | 139, 140, 138, 149 | Medium | measured, not applied | Home profile button (fixed square, not sliced) |
| Panels/bg_active_tab.png | 298×474 | nine-slice | 33, 146, 31, 144 | 33, 144, 31, 146 | Medium | **applied 2026-09-27** | Home raised active tab (sliced) |
| Buttons/btn_level_bg_normal.png | 883×325 | nine-slice | 323, 131, 75, 119 | 323, 119, 75, 131 | Medium | measured, not applied | Game HUD Level badge and Time panel, drawn whole at a uniform scale (680×250, scale-to-fit, never sliced; DEC-153); not used on Home (reference 06 shows one green pill) |
| Buttons/btn_level_green.png | 420×418 | nine-slice | 97, 162, 97, 164 | 97, 164, 97, 162 | High | **applied 2026-09-27** | Home Level button (sliced); current route node (fixed square) |
| Buttons/btn_popup_blue.png | 244×211 | nine-slice | 46, 65, 47, 69 | 46, 69, 47, 65 | Medium | **applied 2026-09-27** | Settings placeholders Legal Terms, Restore Purchases, Support, Language (sliced, 494×199) |
| Buttons/btn_popup_green.png | 425×316 | nine-slice | 69, 128, 70, 129 | 69, 129, 70, 128 | Medium | **applied 2026-09-27** | Settings popup HOME button, hidden on Hub (sliced, `-unity-slice-scale: 0.63px`); pre-level Play (sliced, 404×169, 0.63); purchase Buy (sliced, 360×164, 0.58) |
| Buttons/btn_booster_normal.png | 187×187 | nine-slice | 93, 93, 93, 93 | 93, 93, 93, 93 | Medium | **set by the booster task** (B-27, handoff measurement): Full Rect, Max Size 256, Uncompressed, no Android/iOS override (the Sprite Atlas compresses); replaces the measured 78, 83, 78, 77 | Game HUD booster buttons, always drawn as a uniform 180-unit square (scale-to-fit), so the border is never stretched |
| Buttons/btn_toggle_on.png | 136×140 | nine-slice | 67, 68, 66, 68 | 67, 68, 66, 68 | **Low** | not applied (used fixed) | Settings toggle ON knob, fixed 113×116, not sliced (DEC-134) |
| Buttons/btn_circle_blue_small.png | 128×132 | fixed | — | — | — | none (fixed) | pre-level selector, unselected and zero stock (fixed, 204×210) |
| Buttons/btn_circle_green_small.png | 139×143 | fixed | — | — | — | none (fixed) | Home plus badges; pre-level selected selector (204×210), check and plus badges (fixed) |
| Buttons/btn_settings_normal.png | 134×139 | fixed | — | — | — | none (fixed) | Home locked route nodes (fixed) |
| Buttons/btn_square_retry.png | 512×531 | fixed | — | — | — | none (fixed) | — |
| Buttons/btn_popup_close.png | 150×155 | fixed | — | — | — | none (fixed) | Settings, pre-level and purchase popup close (117×121) |
| Icons/Boosters/ic_clock_freeze.png | 512×512 | fixed | — | — | — | none (fixed) | — |
| Icons/Boosters/ic_hammer.png | 1024×1024 | fixed | — | — | — | none (fixed) | — |
| Icons/Boosters/ic_rocket_purple.png | 1024×1024 | fixed | — | — | — | none (fixed) | Rocket selector and purchase icon (from Rocket.asset) |
| Icons/Boosters/ic_vacuum.png | 1024×1024 | fixed | — | — | — | none (fixed) | — |
| Icons/Currency/ic_coin_single.png | 512×512 | fixed | — | — | — | none (fixed) | Home coin holder icon; purchase Buy price icon (from Coin.asset) |
| Icons/Currency/ic_heart.png | 512×512 | fixed | — | — | — | none (fixed) | Home life holder icon (from Life.asset) |
| Icons/Currency/ic_plus.png | 152×152 | fixed | — | — | — | none (fixed) | Home plus badge glyph; pre-level zero-stock badge glyph (fixed) |
| Icons/Gameplay/ic_clock.png | 512×512 | fixed | — | — | — | none (fixed) | Extra Time selector and purchase icon (from ExtraTime.asset, DEC-47) |
| Icons/Gameplay/ic_pause.png | 128×132 | fixed | — | — | — | none (fixed) | — |
| Icons/Gameplay/ic_restart.png | 350×350 | fixed | — | — | — | none (fixed) | — |
| Icons/Gameplay/ic_time.png | 512×527 | fixed | — | — | — | none (fixed) | — |
| Icons/Gameplay/ic_target.png | 512×512 | fixed | — | — | — | none (fixed); Full Rect, Read/Write on, RGBA32, Max Size 256; not packed into an atlas (read at runtime) | Game HUD Rocket crosshair. Added by the booster task (48th file). Read/Write and an uncompressed format are needed because the reticle layer reads the black alpha silhouette once on Home to make its white copy (B-24); without it only the black outline shows |
| Icons/Navigation/ic_lego_brick_v1.png | 512×512 | fixed | — | — | — | none (fixed) | — |
| Icons/Navigation/ic_settings.png | 128×133 | fixed | — | — | — | none (fixed) | Home settings button (fixed) |
| Icons/Navigation/ic_support.png | 388×273 | fixed | — | — | — | none (fixed) | Home Support tab; Settings Support button (110×77) |
| Icons/Navigation/ic_block_collection.png | 130×154 | fixed | — | — | — | none (fixed) | Home Collection tab (dimmed) |
| Icons/Navigation/ic_home.png | 153×167 | fixed | — | — | — | none (fixed) | Home Home tab; Settings HOME button (96×105, hidden on Hub) |
| Icons/Navigation/ic_leaderboard.png | 135×168 | fixed | — | — | — | none (fixed) | Home Leaderboard tab |
| Icons/Navigation/ic_lock.png | 181×217 | fixed | — | — | — | none (fixed) | Home locked tabs and route nodes |
| Icons/Navigation/ic_shop.png | 130×169 | fixed | — | — | — | none (fixed) | Home Shop tab (dimmed) |
| Icons/Settings/ic_settings_music.png | 108×108 | fixed | — | — | — | none (fixed) | Settings Music row (128×128) |
| Icons/Settings/ic_settings_sound.png | 108×108 | fixed | — | — | — | none (fixed) | Settings Sound row (128×128) |
| Icons/Settings/ic_settings_vibration.png | 108×108 | fixed | — | — | — | none (fixed) | Settings Vibration row (128×128) |
| Masks/mask_square_rounded.png | 256×256 | fixed | — | — | — | none (fixed) | — |
| Backgrounds/bg_block_texture.png | 1547×3357 | fixed | — | — | — | none (fixed) | — |
| Backgrounds/bg_home_line.png | 6×237 | fixed | — | — | — | none (fixed) | Home route rails (tinted gold) |
| Backgrounds/bg_home_screen.png | 2048×2048 | fixed | — | — | — | none (fixed) | Home background (scale-and-crop) |
| Backgrounds/bg_menu.png | 1118×436 | fixed | — | — | — | none (fixed) | — |

All 15 nine-slice border values above were measured on 2026-09-27 with the
method described in the previous section.

## Elements smaller than their borders (`-unity-slice-scale`)

Two of the three applied sprites are drawn smaller than the sum of their own borders,
so their USS also sets `-unity-slice-scale`, which scales the border (corner) size
without changing the slice values:

| Element (Home.uss) | Size (panel px) | Sprite | Top + bottom border | `-unity-slice-scale` | Scaled top + bottom |
| --- | --- | --- | --- | --- | --- |
| `.holder-pill` | 300 x 84 | `bg_home_resource_holder` | 45 + 52 = 97 | 0.5 | 48.5 (fits 84) |
| `.level-button` | 490 x 185 | `btn_level_green` | 164 + 162 = 326 | 0.5 | 163 (fits 185) |
| `.tab--active` | about 250 x 264 | `bg_active_tab` | 144 + 146 = 290 | 0.8 | 232 (fits 264) |

`-unity-slice-scale` exists in Unity 2022.3 (`IStyle.unitySliceScale`, checked in the
live 2022.3.62f2 Editor on 2026-09-27). Its USS parser expects a length, so the value is
written with a unit (`-unity-slice-scale: 0.5px;`); a bare `0.5` logs "Property expects a
unit" and is dropped. The imported `Home.uss` stores all three as dimensions, and the Home
captures in
`docs/verification/hub-home/` render all three without seams or clipped corners. The
PanelSettings/sprite pixels-per-unit relation was not changed.

## Notes on sprites whose AGENTS.md class does not fit their geometry

- **`btn_level_bg_normal`** is not a plain rounded rectangle: it is a large
  circular avatar badge fused to the left end of a stretchable bar with a
  faint repeating diamond pattern baked into the fill (confirmed by direct
  visual inspection of the full sprite). The measured left border (323px,
  covering the whole badge) is correct given that geometry — a 9-slice
  cannot stretch a circle without turning it into an oval, so the badge must
  stay inside the non-stretch cap. Consequences to flag for whoever wires
  this sprite: (1) only horizontal stretching is safe for normal use
  (widening for a longer level number); the stretch-test render confirms
  clean corners and an undistorted badge at 3x width. (2) Vertical stretching
  slightly elongates the badge (visible in the 1.5x-height render) because
  the badge's curvature is only partly enclosed by the top/bottom border —
  keep vertical resizing modest or avoid it. (3) The diamond decoration in
  the fill stretches rather than tiles, which is fine at moderate widening
  but becomes visibly distorted at extreme widths. This value (323, 119, 75,
  131) replaces the 111, 119, 75, 131 recorded in the earlier automated pass
  in the first survey; that pass's left-border scan stopped early inside the
  circle's own curvature (a false match between two adjacent columns), which
  this doc's center-reference method avoids.
- **`bg_popup_info_holder_2`**, **`btn_booster_normal`**: an earlier
  automated pass flagged both as "near-circular, use fixed size" purely from
  their border-to-half-size ratio (border is 68-86% of the half-dimension on
  both). The stretch-test renders for both sprites (3x width at 0.7x and
  1.5x height) show **no visible corner distortion or seams** — both hold up
  as ordinary nine-slice sprites with a thin but usable stretch margin. This
  doc keeps them classed `nine-slice` at Medium confidence: they work, but
  have little headroom for a large one-axis stretch, so avoid stretching
  either past roughly 2x its native size on any one axis without a fresh
  visual check.
- **`btn_toggle_on`** genuinely does not fit the `nine-slice` class. The
  border scan never found a stable repeatable band (clamped near half-size
  on every edge), and the 1.5x-height stretch-test render shows a hard seam
  and a warped knob shape. Visual inspection of the full sprite explains why:
  this is a single baked "toggle is ON" graphic with an off-center glossy
  knob/tick mark, not a resizable capsule track. Recommend one of: (a) treat
  it as `fixed` (no slicing) at its native 136×140 for a toggle drawn at one
  size everywhere, or (b) if the toggle control needs to resize, ask for or
  compose a separate flat track sprite plus a round knob sprite rather than
  slicing this one image. Out of scope for this task to decide; flagged here
  for the Hub Home UI owner.
- **`bg_popup_header`**: AGENTS.md describes this as a "trapezoid/rounded
  header tab" with a thin vertical stretch band. Direct visual inspection
  shows a plain rounded rectangle bar, not a trapezoid, and the measured
  border (79, 90, 79, 90 on a 612×204 canvas) is symmetric and the
  stretch-test render is clean in both axes — no note beyond the corrected
  shape description is needed.
- **`btn_popup_blue`**, **`btn_popup_green`**: both carry a small diagonal
  gloss/highlight decal inside the stretchable middle area. It stretches
  smeared rather than tiling, which is a minor cosmetic effect visible in
  the stretch-test renders and common for 9-sliced buttons with a baked-in
  highlight; not a blocker, just worth knowing before a very wide button
  variant is requested.
- **`bg_profile_box_blue`**: canvas carries a large asymmetric transparent
  margin (~75px per side) around the actual square art, already folded into
  the measured border above, plus a faint diagonal crest watermark in the
  middle that stretches under vertical resize (cosmetic only; corners stay
  clean).

## Sprite Atlases (DEC-142)

Written by `TASK-20260927-001142693-43e35c51-ui-atlases-portrait-lock` on
2026-09-27. `EditorSettings` sprite packer mode is **Sprite Atlas V2 - Enabled**
(Editor and builds). Two Sprite Atlas V2 assets pack the UI sprites
(`PROJECT.md` Performance Rule 2):

| Atlas | Sprites | Used by |
| --- | --- | --- |
| `Art/UI/Atlases/HomeSettings.spriteatlasv2` | 25 | Home (top bar, level route, tab bar) and the Settings popup content (rows, toggle, icons) |
| `Art/UI/Atlases/HudPopups.spriteatlasv2` | 19 | the shared popup shell (Settings/pause, pre-level, purchase, win, fail), the pre-level selector circles, booster and HUD icons, the HUD timer, pause and restart controls |

Both atlases: Include in Build on, Allow Rotation off, Tight Packing off,
Padding 4, mipmaps off, sRGB on, Read/Write off, Filter Bilinear, Max Size 2048,
Android and iOS overrides ASTC 6x6.

**Compression rule (user decision 2026-09-28, "Sprite'lar sıkıştırılmasın").**
Packed sprites are uncompressed sources: their importers use default Compression
None and no Android or iOS override, so the atlas encodes ASTC 6x6 once from the
original pixels instead of from already-compressed pixels. Only the two atlases
compress. The sprite `.png` files and every other importer field (border, Max
Size, sprite mode, pixels per unit, filter, sRGB, mipmaps, wrap, alpha) are
unchanged. Two packed sprites (`Panels/bg_gameplay_time_holder`,
`Buttons/Btn_booster_normal`) belong to the booster gameplay task while it runs;
that owner brings them with Compression None and no overrides in its own `.meta`
versions (agreed 2026-09-28). `Panels/bg_popup_info_holder` is not booster-owned
and was set to None on 2026-09-28 (only its `textureCompression` line changed; its
43/35/42/26 border stays). The three full-screen backgrounds
keep their own importer compression. Packed size and memory: see the table after
the membership list.

**Background exception.** The three full-screen backgrounds
`Backgrounds/bg_home_screen` (2048×2048), `Backgrounds/bg_menu` (1118×436,
unused) and `Backgrounds/bg_block_texture` (1547×3357, unused) are in no atlas.
Each is drawn once, full screen, so an atlas gains no batching, and
`bg_home_screen` alone fills a 2048 page (`bg_block_texture` is taller than
2048), so packing them would force a 4096 atlas. `Backgrounds/bg_home_line`
(6×237) is not a full-screen background: it is the Home route rail and is packed
in HomeSettings.

**Shared popup shell.** `bg_popup`, `bg_popup_header`, `bg_popup_info_holder`,
`bg_popup_info_holder_2`, `btn_popup_blue`, `btn_popup_green` and
`btn_popup_close` go to HudPopups. Five popups draw them (Settings, which is
also the in-game pause menu per DEC-135; pre-level; purchase, on Home and in game;
win; fail). Four of the five are HUD or popup screens, and the Game scene draws
them in four of its states (pause, purchase, win, fail) against three Home
overlays, so one texture serves every popup shell. The Settings popup therefore
draws from both atlases: its shell from HudPopups, its rows, toggle and icons
from HomeSettings. Home itself draws no shell sprite, so Home's own draw count
(NFR-P6) uses only HomeSettings plus the background.

**Other boundary choices.** A sprite that Home shows goes to HomeSettings even
when a popup also uses it (`ic_coin_single`, `ic_plus`, `btn_circle_green_small`,
`btn_settings_normal`, `ic_home`, `ic_support`): Home is always on screen
behind its overlays. `btn_circle_blue_small` is drawn only by the pre-level
selector, a popup, so it goes to HudPopups. Reserved, unused sprites follow their
mapped screen (`ic_lego_brick_v1`, `btn_level_bg_normal`, `mask_square_rounded`,
`bg_home_coin_holder` to Home; `bg_popup_info_holder_2` to the popup shell).

"Source compression" is the sprite importer's default-platform compression;
no sprite below has an Android or iOS override turned on.

| Sprite | Atlas | Source compression |
| --- | --- | --- |
| Backgrounds/bg_home_line | HomeSettings | None |
| Buttons/Btn_circle_green_small | HomeSettings | None |
| Buttons/Btn_levelBg_normal | HomeSettings | None |
| Buttons/Btn_level_green | HomeSettings | None |
| Buttons/Btn_settings_normal | HomeSettings | None |
| Buttons/btn_toggle_on | HomeSettings | None |
| Icons/Currency/Coin_Single | HomeSettings | None |
| Icons/Currency/Heart | HomeSettings | None |
| Icons/Currency/ic_plus | HomeSettings | None |
| Icons/Navigation/Lego_Brickv1 | HomeSettings | None |
| Icons/Navigation/Settings_Icon | HomeSettings | None |
| Icons/Navigation/Support_icon | HomeSettings | None |
| Icons/Navigation/block_collection | HomeSettings | None |
| Icons/Navigation/home_icon | HomeSettings | None |
| Icons/Navigation/leaderboard | HomeSettings | None |
| Icons/Navigation/lock | HomeSettings | None |
| Icons/Navigation/shop | HomeSettings | None |
| Icons/Settings/Blockjam_Settings_Music | HomeSettings | None |
| Icons/Settings/Blockjam_Settings_Sound | HomeSettings | None |
| Icons/Settings/Blockjam_Settings_Vibration | HomeSettings | None |
| Masks/SquareRoundedMask | HomeSettings | None |
| Panels/ActiveTab | HomeSettings | None |
| Panels/bg_home_coin_holder | HomeSettings | None |
| Panels/bg_home_resource_holder | HomeSettings | None |
| Panels/profile_box_blue | HomeSettings | None |
| Panels/bg_popup | HudPopups | None |
| Panels/bg_popup_header | HudPopups | None |
| Panels/bg_popup_info_holder | HudPopups | None |
| Panels/bg_popup_info_holder_2 | HudPopups | None |
| Buttons/Btn_popup_blue | HudPopups | None |
| Buttons/Btn_popup_green | HudPopups | None |
| Buttons/Popup_Close_Button | HudPopups | None |
| Buttons/Btn_booster_normal | HudPopups | Normal Quality until the booster owner releases it, then None |
| Buttons/Btn_circle_blue_small | HudPopups | None |
| Buttons/Btn_square_retry | HudPopups | None |
| Icons/Boosters/Clock_Freeze | HudPopups | None |
| Icons/Boosters/Hammer | HudPopups | None |
| Icons/Boosters/Rocket_Purple_Icon | HudPopups | None |
| Icons/Boosters/Vacuum | HudPopups | None |
| Icons/Gameplay/Clock | HudPopups | None |
| Icons/Gameplay/ic_pause | HudPopups | None |
| Icons/Gameplay/ic_restart | HudPopups | None |
| Icons/Gameplay/ic_time | HudPopups | None |
| Panels/bg_gameplay_time_holder | HudPopups | Normal Quality until the booster owner releases it, then None |
| Backgrounds/bg_home_screen | none (background exception) | Normal Quality, own importer (unchanged) |
| Backgrounds/bg_menu | none (background exception) | Normal Quality, own importer (unchanged) |
| Backgrounds/bg_block_texture | none (background exception) | Normal Quality, own importer (unchanged) |

Packed once on 2026-09-28 with `SpriteAtlasUtility.PackAtlases` for the active
Android target. Page sizes come from the atlas preview textures. The device memory
is estimated from the ASTC 6x6 block size (16 bytes per 6x6 block, no mipmaps);
device memory was not profiled.

| Atlas | Pages (ASTC 6x6) | Estimated device memory |
| --- | --- | --- |
| HomeSettings | 1: 2048×2048 | 1,871,424 B (1.78 MiB) |
| HudPopups | 2: 2048×2048 and 2048×1024 | 1,871,424 + 935,712 = 2,807,136 B (2.68 MiB) |
| Total | 3 pages | 4,678,560 B (4.46 MiB) |

HudPopups needs a second page because its three 1024×1024 booster icons
(`Hammer`, `ic_rocket_purple`, `Vacuum`) and the 976×976 `bg_popup` do not fit
one 2048 page with the other sprites; Max Size stays 2048 (the OpenGL ES 3.0
minimum), so the atlas splits into pages instead of growing to 4096. A screen that
draws sprites from both pages binds two textures.

**Packing warning.** The first pack logged, for each of the 44 packed sprites,
"Source Texture ... is using compressed format. To ensure no loss in source pixel
details when packing to SpriteAtlas, please use uncompressed format in
TextureImporter.": the importers used Compression Normal Quality, so the atlas was
encoded from already-compressed pixels. On 2026-09-28 the 41 sprites this task
may write were set to Compression None (`TextureImporter.textureCompression`,
`SaveAndReimport`; each `.meta` changed only in its default-platform
`textureCompression` line, 1 to 0, and no Android or iOS override was on). A
forced reimport and repack of both atlases then logged 3 warnings, for
`bg_popup_info_holder`, `bg_gameplay_time_holder` and `btn_booster_normal`.
`bg_popup_info_holder` was then set to None as well; the other two warnings
disappear when the booster task lands its own `.meta` versions with the same rule. Page sizes and formats are unchanged
(HomeSettings 1 page 2048×2048; HudPopups 2048×2048 and 2048×1024; all ASTC 6x6).
The final capture comparison shows whether the result stays within compression
tolerance.

## Open follow-ups

- Mesh Type Full Rect was not changed (UI Toolkit slices from the sprite rect;
  the atlases use Tight Packing off). The Android and iOS ASTC 6x6 format now
  comes from the two atlases.
- The pre-level popup (task 4) reuses the five popup borders the Settings task
  applied; it writes no border itself.
- `btn_level_bg_normal` and `bg_popup_info_holder_2` are measured but have no
  border applied; the Game HUD draws `bg_popup_info_holder_2` unsliced as its
  count badge (scale-to-fit) and `btn_level_bg_normal` whole as its Level badge
  and Time panel (DEC-153). Home uses `btn_level_green`: reference 06 shows one
  green Level pill. `bg_home_coin_holder` got its measured border and Full Rect
  on 2026-09-28 for the Game HUD coin holder (DEC-153).
- `bg_popup_info_holder` keeps Hub Settings' 43, 35, 42, 26 (L, B, R, T); the
  booster task writes no border on it.
- `btn_toggle_on` is used as a fixed ON knob (DEC-134); it is never sliced.
- `bg_gameplay_time_holder` and `btn_booster_normal` arrive with Compression None
  in the booster gameplay task's own `.meta` versions; after they land, repack
  both atlases and expect no "compressed format" warning (DEC-142).
- Deliberate atlas exclusion: `Icons/Gameplay/ic_target.png`, added by the booster
  gameplay task, stays out of both atlases. Its HUD reads its pixels once on Home
  (Read/Write on, RGBA32, 256 px). The atlases list sprites one by one, with no
  folder entries, so new files are never packed by accident.
- The three full-screen backgrounds still import with default Compression Normal
  Quality and no Android or iOS override; the ASTC 6x6 override `AGENTS.md` asks
  for on them is not applied yet (outside this task's allowed paths).
