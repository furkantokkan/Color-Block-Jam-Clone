# Art Asset to PRD Mapping

Verified in Unity 2022.3.62f2 on 2026-09-24. This maps all **67 files** under `Assets/ColorBlockJam/Art` (excluding `.meta` files) to their intended use in the [PRD](Color-Block-Jam-PRD.md). The [import audit](art/asset-import-audit.json) records their paths before migration, Unity GUIDs, import state and prefab dependencies at verification time. That historical audit covered 65 files and six booster prefabs; the board material/shader add two art files. Current board, typed-data and 32-frame verification is recorded in the reports below.

Mapping identifies requirement ownership, not completed gameplay acceptance. The three booster effect prefabs are imported and independently tested; the game-session hookup, complete UI and device performance checks remain integration work. The modular board composer, reusable pools, Piece Editor and shared palette are now implemented. Original filenames and GUIDs are preserved. User approval of the corrected Vacuum appearance is recorded on 2026-09-24.

## Booster cast assets

Use these effect prefabs for gameplay presentation. HUD sprites remain in `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters`.

| Booster | Runtime prefab | Unity GUID | PRD ownership and use |
| --- | --- | --- | --- |
| Hammer / BST-2 | `Assets/ColorBlockJam/Prefabs/Boosters/Effects/HammerEffect.prefab` | `c90939859f03c2b4f9ced8b66e41ca49` | AC-BST-2.6, AC-4.12; PrimeTween wind-up, contact and recoil. The domain removes one exposed semantic piece/layer/obstacle under AC-BST-2.1; the view applies no damage. Reference 16. |
| Rocket / BST-3 | `Assets/ColorBlockJam/Prefabs/Boosters/Effects/RocketPurpleEffect.prefab` | `433299897f5eb974dbf32428f52d0bfe` | AC-BST-3.1, AC-BST-3.7, AC-5.7; PrimeTween flight to the chosen unit cell. Starting and in-level use share this view. One cell is removed only when a connected nonempty remainder survives (AC-BST-3.2, AC-BST-3.3). |
| Vacuum / BST-4 | `Assets/ColorBlockJam/Prefabs/Boosters/Effects/VacuumEffect.prefab` | `472b8660c1f7f1340832017200d5cf6e` | AC-BST-4.6, AC-BST-4.7, AC-4.12; approved upright camera-facing cast, lower canister, deforming hose and rigid upper nozzle. Matching block visuals shrink toward the view's `IntakePosition`. References 13 and 18. |

The Vacuum cast uses `Assets/ColorBlockJam/Art/Textures/Atlases/VacuumSuctionAtlas.png` (GUID `cf02aebc233a6b6418080f07aedf7c74`) and `Assets/ColorBlockJam/Art/Materials/Effects/MAT_VacuumSprite.mat` (GUID `93aa866c0b6d73741beb5be6d87ef47c`). Its 32 Blender-baked frames use one SpriteRenderer and a 768 x 1536 atlas, Android/iOS ASTC 6x6, with mipmaps and Read/Write off. There is no runtime skinning, Animator or render camera. Entry/exit combines baked turning with PrimeTween scaling; the upper intake remains fixed while the hose swallows. IsIntakeReady identifies the full-size front-facing interval. Both mobile formats have a 512 KiB compressed payload. The static icon-shaped Vacuum model below is not the active cast.

All three views last 0.75 s, start only through `Play`, and expose explicit pause/resume/stop. Session integration must own their placement, warm pools and cancellation; animation callbacks must never spend stock or mutate board occupancy. The white block outline and pulled block views in AC-BST-4.5/4.6 are separate presentation integration work. [Motion report and current PlayMode tests](art/boosters/motion/README.md) cover the standalone views, not all booster/domain criteria.

## Authored model sources and shared material

| Imported files | Role / PRD ownership |
| --- | --- |
| `Assets/ColorBlockJam/Art/Models/Boosters/Hammer.fbx`; `Assets/ColorBlockJam/Prefabs/Boosters/Models/Hammer.prefab` | Rigid mallet used by HammerEffect; AC-BST-2.6. LOD0/LOD1: 1,900 / 940 triangles. |
| `Assets/ColorBlockJam/Art/Models/Boosters/RocketPurple.fbx`; `Assets/ColorBlockJam/Prefabs/Boosters/Models/RocketPurple.prefab` | Rigid projectile used by RocketPurpleEffect; AC-BST-3.1, AC-BST-3.7. LOD0/LOD1: 2,972 / 1,228 triangles. |
| `Assets/ColorBlockJam/Prefabs/Boosters/Models/Vacuum.prefab` | The Vacuum's baked sprite visual (SpriteRenderer plus its `Intake` point), nested in `VacuumEffect.prefab`. The earlier 3D `Vacuum.fbx` model and its mesh prefab were unused and deleted on the user's 2026-09-28 order; the motion source stays `ArtSource/Boosters/VacuumMotion.blend`. |
| `Assets/ColorBlockJam/Art/Materials/Shared/MAT_Gameplay.mat`; `Assets/ColorBlockJam/Art/Textures/Atlases/T_GameplayPalette.png` | One shared opaque Board Lit material and 128 x 32 atlas for board, environment and booster model LODs. Eight booster swatches plus white for dynamic board tint. The unused `MAT_BoosterPalette.mat` was removed after a dependency scan. [Current shared-art evidence](art/shared-art/README.md). Device NFR-P4/P5 compliance remains pending. |

Blender sources and regeneration scripts live in `ArtSource/Boosters/`; they are not runtime dependencies. `Boosters.blend` owns the three rigid models; `VacuumMotion.blend` owns the approved upright bake. The [model report](art/boosters/README.md) records mesh/import checks. No reference-project art or screenshot crop is used as runtime art.

## Supplied board kit

The 11 base-shape library entries reference derived
`Prefabs/Board/Shapes/Piece_<id>.prefab` files (IDs 0, 1, 2, 4, 6, 10, 18, 22,
23, 27, 29); the 23 unused identities were removed on 2026-09-25.
Each has a `BlockPiece` root and uses only original kit meshes. `BlockShapeLibrary`
loads type/footprint data from canonical JSON and binds existing prefabs;
safe Save preserves prefab identity/custom root content.
Runtime checkout/return uses `BlockPieceFactory` backed by Onity factories/pools;
shape changes affect copied attempt state. These outputs map to AC-3.15, AC-3.16
and AC-8.8 and do not add files under `Art`. See the [board report](art/board-kit/README.md).

| Imported file | PRD ownership | Integration status |
| --- | --- | --- |
| `Assets/ColorBlockJam/Art/Models/Supplied/BlockParts.fbx` | AC-3.14, AC-3.15, AC-3.16 | Four original-mesh tile prefabs and typed catalog drive all 33 reference shapes. Vertex-based composition, half-cell calibration and Onity pools are verified. No Base is present; OQ-11 resolved. |
| `Assets/ColorBlockJam/Art/Models/Supplied/WallAndDoor.fbx` | AC-3.4, AC-3.14, AC-3.15 | Colored Wall/corner/door/marker prefabs are calibrated; their closed -Y source top faces upward. Defective `corner_3.002` is excluded (AF-7). Full board-edge placement remains session integration work. |
| `Assets/ColorBlockJam/Art/Models/Supplied/GroundGrid.fbx` | AC-3.1, AC-3.11; PRD 5.2 | Reusable one-unit Grid prefab for the perspective board (DEC-103). Camera/layout integration remains pending. |
| `Assets/ColorBlockJam/Art/Models/Supplied/Arrows.fbx` | AC-BLK-01.4, AC-BST-3.5 | Three original-mesh marker prefabs with shared white ArrowColor, centered pivots and stud clearance; [visual verification](art/board-kit/arrows.md). Ready for later block Effect Data references. Selecting/placing the proper axis marker after gameplay footprint changes remains integration work. |
| `Assets/ColorBlockJam/Art/Textures/icetexture.jpg` | AC-BLK-03.3, AC-BLK-03.6, AC-BLK-03.7 | T1 ice overlay source. Shared Shader Graph material and lifecycle remain pending; this is not the Time Freeze HUD frost effect. |

## Shared board data and authoring

- `Assets/ColorBlockJam/Data/Art/GamePalette.asset`: 11 reference block/gate colors,
  wall/ground colors; booster UV roles and Blender export reuse this same `Colours` mapping.
- `Assets/ColorBlockJam/Data/Json/Pieces/BlockShapeLibrary.json`: the editor-only piece
  library, 11 templates as `{name, cells}` records keyed by their footprint (DEC-193).
  No wrapper asset points at it (DEC-195); the game never loads it.
- `Assets/ColorBlockJam/Data/Board/BoardPieceCatalog.asset` and
  `Prefabs/Board/`: four block tiles, ten environment prefabs and the pooled view.
- `Assets/ColorBlockJam/Art/Shaders/Board/BoardLit.shader` and
  `Art/Materials/Shared/MAT_Gameplay.mat`: shared instanced tint/clip state across
  Forward, Shadow, Depth and DepthNormals passes (DEC-96; device comparison pending).
- **Color Block Jam > Piece Editor** and the project-local authoring skill
  implement name-to-enum generation, a centered 7×7 grid, matching grid/preview tint,
  add/delete/save, JSON export/import/merge and trim-on-save (AC-8.8).
  See the [board kit report](art/board-kit/README.md).

## Supplied UI sprites

These 47 files map to UI roles. Full original sprite GUIDs and screen context are also in [UI Asset Mapping](UI-Asset-Mapping.md). No runtime UIDocument binding is implied.

| Imported file(s) | Intended role | PRD ownership |
| --- | --- | --- |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_clock_freeze.png` | Time Freeze HUD icon; separate countdown/frost still need implementation | AC-BST-1.5; PRD 7.10 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_hammer.png` | Hammer HUD icon only | AC-BST-0.10; PRD 7.10 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_rocket_purple.png` | Shared starting/HUD Rocket icon | AC-1.9, AC-BST-0.3, AC-BST-0.11 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_vacuum.png` | Vacuum HUD icon only; active cast is VacuumEffect | AC-BST-0.10; PRD 7.10 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Gameplay/ic_clock.png` | Extra Time fallback with explicit text label | AC-1.1a, AC-BST-5.2; AF-16 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Gameplay/ic_time.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_gameplay_time_holder.png` | Gameplay timer glyph and holder | AC-3.6, AC-3.11 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Gameplay/ic_pause.png` | Pause control | AC-4.4 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Gameplay/ic_restart.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_square_retry.png` | Restart/retry control | AC-4.3 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_booster_normal.png` | Four HUD booster slots and pre-level selector backgrounds | AC-3.10, AC-1.9; PRD 7.10 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_circle_blue_small.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_circle_green_small.png` | Available/selected badge backgrounds; text supplies counts/check | AC-1.9; AF-15 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Backgrounds/bg_home_screen.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Backgrounds/bg_home_line.png` | Home background and route decoration | AC-1.4a; PRD 7.1 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Backgrounds/bg_menu.png` | Settings panel/background | AC-2.3, AC-2.6; PRD 7.2 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Backgrounds/bg_block_texture.png` | Optional tinted decorative block texture; not a substitute for 3D board pieces | AF-6, DEC-23; no dedicated runtime placement required |
| `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_home_coin_holder.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_home_resource_holder.png` | Home coin/life holders | AC-1.7, AC-1.8 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_profile_box_blue.png` | Home profile placeholder | AC-1.6 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_level_bg_normal.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_level_green.png` | Home Level button/backing | AC-1.1a, AC-1.2 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_settings_normal.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_settings.png` | Home Settings entry / optional glyph | AC-1.3 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_active_tab.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_home.png` | Active Home tab; secondary Home popup glyph with a label | AC-1.5, AC-4.3; DEC-41 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_shop.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_block_collection.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_lock.png` | Locked Shop/Collection tabs | AC-1.5 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_leaderboard.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_support.png` | Placeholder navigation and Settings Support | AC-1.5, AC-2.4 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Navigation/ic_lego_brick_v1.png` | Reserved optional brick decoration; no extra tab or feature | PRD 5.2 supplied navigation inventory; no dedicated AC |
| `Assets/ColorBlockJam/Art/UI/Supplied/Masks/mask_square_rounded.png` | Reserved fixed-aspect mask; use only if required by the final UI layout | PRD 5.2 / AGENTS.md import policy; no dedicated AC |
| `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_header.png` | Shared modal shell/header | AC-1.1a, AC-4.1, AC-4.9; PRD 7.10 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_info_holder.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_info_holder_2.png` | Modal detail/price panels | AC-1.1a, AC-4.9; PRD 7.10 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_popup_green.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_popup_blue.png` | Labeled primary/secondary modal actions | AC-1.1a, AC-4.3, AC-4.6, AC-4.9 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_popup_close.png` | Settings/pre-level/modal dismissal | AC-1.1a, AC-2.3 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Currency/ic_coin_single.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Currency/ic_plus.png` | Balance, reward, price and add/empty-stock glyph | AC-1.6, AC-1.8, AC-3.9, AC-4.9; PRD 7.10 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Currency/ic_heart.png` | Static Home life display | AC-1.7 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_toggle_on.png` | ON/OFF control via tint, knob position and labels | AC-2.1, AC-2.2 |
| `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Settings/ic_settings_music.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Settings/ic_settings_sound.png`; `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Settings/ic_settings_vibration.png` | Settings row icons | AC-2.1, AC-2.2, AC-2.5 |

## Readiness and remaining art work

- **Imported and verified:** 13 authored booster files, 47 supplied UI sprites, four supplied model kits and one ice texture. All six authored prefabs resolve their references; VacuumEffect has 16 non-null sprite frames. The active scene stayed clean during the read-only audit.
- **Presentation accepted:** user approved the corrected Vacuum design. Keep the supplied HUD icon separate from the upright animated cast. Hammer/Rocket use rigid 3D motion; Vacuum uses the approved offline bake.
- **Original-kit import work remains:** the supplied 47 UI sprites do not yet have explicit Android/iOS ASTC 6x6 overrides; nine-slice borders and the two UI atlases still require the UI implementation/import audit (AF-10, NFR-P6). The four original FBX importers still have animation import enabled. Ice texture downscaling/platform overrides also remain. This mapping does not silently change those existing assets or claim their final optimization.
- **Additional rendering remains:** Time Freeze Shader Graph frost, shared ice overlay, whole-block outline, gate-exit fragments and composed UI. The board catalog, derived shape prefabs and standalone Onity piece factory are implemented. Dedicated hourglass/red Rocket/coin-pile/title art remains absent; use the documented supplied-art/text fallbacks.
- **Verification limits:** standalone booster tests and import checks pass. Session/pool integration, both portrait gameplay captures, device GC/frame-time/draw-call budgets and final build inclusion remain pending; no PRD V-row is marked complete by this map.
- **Mapping check:** [validation evidence](art/asset-prd-validation.json) confirms 65/65 art paths, 47/47 UI paths and matching Unity GUIDs. The PRD's section 16 coverage check passes for all 226 criteria (188 ACs and 38 NFRs), with no uncovered or undefined IDs.

## Change note

- Linked every imported art file to a requirement or an explicit reserved/source-only role; retained exact paths/GUIDs in the live audit.
- Reconciled the PRD, UI map and visual references with the approved cast prefabs, including reference 18's upright Vacuum.
- Preserved the existing Astra modeling credit in the root README. No scene, package, source asset or gameplay code changed for this mapping.
