---
title: Color Block Jam Case: Product Requirements Document
created: 2026-09-24
modified: 2026-09-24
type: prd
status: final v8
tags: [rollic, color-block-jam, game-developer-case, prd]
---
# Color Block Jam Case: Product Requirements Document

## 1. Summary
- **What:** A complete Color Block Jam case vertical slice in Unity `2022.3.62f2`: Home, Settings, five playable levels with timer, coins and result flows, a visual level editor, level validation, functional boosters, Ice and Arrow. Additional mechanics use the catalog when the user selects them.
- **For whom:** Case evaluators, non-technical level designers and players on Android, with iOS as a target, in portrait.
- **Why:** The Rollic Game Developer Case 2026. The brief evaluates "code quality, architecture, and the usability of the editor" and not art.
- **Success metric:** the brief's evaluation order. When two options compete, the higher one wins.
  1. Architecture: data, logic, and view are separate, coupling is low, the code is easy to extend.
  2. Editor usability: a designer uses the editor without help.
  3. Code quality: clear names, no dead code, consistent style.
  4. Correctness: every brief section 4 acceptance criterion passes.
  5. Performance: no allocation in the update loop, a stable frame rate on a mid-range device.
  6. Scope control: a small, complete result beats a large, incomplete one.
- **Scope:** One complete case vertical slice. The five-level game, Home, Settings, visual level editor, level validation, functional boosters, Ice and Arrow are completion requirements. The mechanics catalog supplies detailed rules for additional mechanics when the user selects them. The user chooses implementation tasks and their order; each selected task is complete only after its applicable acceptance and verification rows pass. There is no preset mechanic sequence or reduced completion target.
- **Source documents:**
  - Case brief: `docs/Game-Developer-Case-2026.md` (the authority for requirements).
  - `PROJECT.md`: game states, the lifecycle rule "nothing runs unattended", Architecture Rules, Performance Rules 1 to 7, Reference Material, and the decision log.
  - `AGENTS.md`: engine, Onity, PrimeTween, Input System, asset locations, version control, Editor tooling.
  - Appendix: [Mechanics Catalog](Color-Block-Jam-PRD-Mechanics-Catalog.md), with booster details and specifications for user-selected mechanics.
  - User references: [Visual References](Color-Block-Jam-Visual-References.md), with the supplied screenshots, per-screen explanations, and their requirement interpretation.
  - [UI Asset Mapping](UI-Asset-Mapping.md): 45 verified supplied sprite paths/GUIDs mapped to screens and controls.
  - [Android Player Settings](Android-Player-Settings.md): applied IL2CPP/ARM64 configuration, screenshot comparison, technical reasons and verification limits.
  - [Rendering Specification](Rendering-Reference-Review.md): shared materials, cached property blocks, instanced color, separate ice and freeze shaders, clipping passes, and device verification (DEC-96).
  - Full-game reference: `docs/Color-Block-Jam-Mechanics-Guide.md` (English mechanics guide) and Rollic's official help pages.
- **Precedence:** brief > `PROJECT.md` > `AGENTS.md` > this PRD > the appendix > the mechanics guide. This PRD decides only where the brief is silent (section 12).
- **User correction, 2026-09-24:** all tools belong to one **Booster** category and shared system. There are four in-game choices (Time Freeze, Rocket, Hammer, Vacuum) and two pre-level choices (Extra Time, Rocket). Rocket is the same booster in both contexts; only its target selection differs. These six UI placements represent five booster definitions, not separate skill and booster systems. User clarifications govern the case interpretation of the reference images; prices, ads, unlock levels, and other full-game content in a screenshot are not automatically requirements.
- **Confirmed booster clarification:** Time Freeze grants 10 extra seconds of play by freezing the countdown for 10 seconds; it does not also add 10 to the countdown. Rocket removes one 1x1 cell while preserving the block. Hammer removes one exposed block layer/piece or one obstacle, not a fixed grid cell. Buy spends coins and grants persistent booster stock. This replaces the provisional additive Freeze, cell-splitting Hammer and placeholder-only purchase rules from v3.
- **Review resolutions:** the user confirms Newtonsoft and UI Toolkit, and specifies a separate 10-second Time Freeze countdown while the main clock is held. Delegated decisions use durable stock commits before effects (7.6, DEC-92) and a bounded final-action presentation before teardown (7.4, DEC-93). These decisions replace the conflicting v4 serializer, save-boundary and terminal-effect wording.

## 2. Goals and Non-Goals
### 2.1 Goals
- G-1: Every brief section 4 acceptance criterion passes and traces to a verification row (sections 4.6 and 16).
- G-2: A non-technical designer creates, saves, and plays a new level in the editor with no code, no hand-edited file, and no help.
- G-3: Levels 1 to 5 are authored as ordered records in the `Main` level database (its `manifest.json` and packs, DEC-196 in `PROJECT.md`), meet the targets in 11.11, and are loaded by the game from those same files through the project scope installer's Addressables reference (DEC-194).
- G-4: The architecture shows a clean data, logic, and view split. The domain is plain C# in its own assembly, views are thin, and one session state owns gameplay.
- G-5: Gameplay allocates nothing per frame and domain gameplay runs only in `Playing`. Owned saves and the bounded visual-only Won Finish follow the explicit lifecycle exceptions in `PROJECT.md`.
- G-6: The full case flow, validation, functional boosters, Ice and Arrow pass their acceptance and verification rows before the vertical slice is declared finished. The user chooses the order of implementation tasks.
- G-7: The deliverables are complete: the project, a short README, and an Android `.apk` tested on a real phone.
- G-8: A selected catalog mechanic can be added through registration, with its rules, editor support, visuals and tests (section 8.3).

### 2.2 Non-Goals
- NG-2: Working tab sections (Shop, Leaderboard, Collection, Support). They are placeholders; Shop and Collection remain visibly locked throughout the case.
- NG-3: Audio playback and haptics. Settings toggles store and show state only. Booster gameplay effects are specified in 7.10. The life economy left this non-goal on the user's order of 2026-09-28 (PROJECT.md DEC-174: 5 lives, one lost per failed, retried or abandoned attempt, one refilled per 30 minutes, all refilled for 1 coin).
- NG-4: Unselected catalog mechanics in a build. Their specifications remain available for the user's next implementation choice (section 8).
- NG-5: Content after level 5, monetization, analytics, backend, localization.
- NG-6: An engine upgrade or any Unity 6-only feature.
- NG-7: Unrelated framework, art, or package imports. Rendering follows CON-9 and DEC-96.

## 3. Users and Personas
| Persona | Goal | What they judge | Key needs |
| --- | --- | --- | --- |
| Player | Clear 5 short color-matching levels on a phone | Responsiveness, clarity, fair timer | Direct drag. Clear fail and win. Pause, restart, and home always reachable. |
| Non-technical level designer | Build and tune levels quickly | Whether the editor can be learned without help | Visual grid, click to place, rotate, errors in designer language, Save, one-click Play that never touches real progress. No code, no file editing. |
| Rollic evaluator | Score in the brief's order | Architecture, editor, code quality, correctness, performance, scope | Traceable ACs, a short README with decisions, enforced assembly boundaries, honest known problems. |

## 4. Scope
### 4.1 One Completion Scope
The PRD defines one complete case vertical slice. The completion gate covers the playable five-level flow, Home, Settings, visual level editor, level validation, functional boosters with coin purchase and persistent stock, Ice, Arrow, performance, verification, README and Android APK. Every delivered mechanic must be authorable, saved, loaded, played and verified through the same systems. No feature is treated as a disposable placeholder when the PRD specifies its full behavior.

The mechanics catalog is part of this product specification. The user names the next area or mechanic to implement and sets its order. A selected catalog mechanic joins the implementation checklist with its dependencies, acceptance criteria and verification. Until selected, it has a complete specification but no claimed implementation status. No catalog row sets priority or implies that it is already implemented.

### 4.2 Game and Editor Requirements
- Home: tab bar (Shop and Collection visibly locked), level button opening the pre-level popup with two booster selectors and Play, top UI, Settings, button feedback, layout at 1080 x 1920 and on 20:9.
- Settings: vibration, sound and music toggles with distinct states; placeholder secondary controls with feedback; close to Home.
- Gameplay: five playable levels, countdown timer, coins, pause, restart, home, fail and win flows, functional boosters and the life display as a placeholder.
- Editor: visual board width and height, door and block placement, timer, effect authoring, level validation, Save and Play from the editor. All five shipped levels are records in the same `Main` level database saved by this editor.
- Board: pooled visuals composed from supplied modular FBX pieces. Ice and Arrow appear in at least one shipped level each.
- Delivery: project (`Assets`, `Packages`, `ProjectSettings`), short README, Android APK and the verification evidence in section 16.

### 4.3 Selected Mechanics and Features
| Feature | REQ | Specification |
| --- | --- | --- |
| Level validation | REQ-O2 | 7.9 |
| Functional boosters, coin purchase and persistent stock | REQ-O3 | 7.6, 7.10 |
| Ice blocker | REQ-O4 | 7.11 |
| Arrow block | REQ-O5 | 7.12 |

These four features are required for the finished vertical slice. The catalog defines further selectable block, gate and board mechanics. When the user selects one, apply its catalog entry, shared architecture rules and verification to the same completion standard. The user sets the work order; dependencies are checked for each selected task.

### 4.4 Mechanics Catalog
Section 8 and the appendix define the rules, data, editor behavior, visuals, solver support, acceptance criteria and interactions for the remaining named mechanics. Their presence in the PRD is a design contract, not evidence that a feature is already in the build. A build registers only implemented, verified effect types. Unknown types fail clearly on load.

### 4.5 Out of Scope
- Working Block Collection, Canyon Chase, Block Heist, Rainbow Fest, daily quests, Pilot's Drop, levels after 5, a life economy, active Shop or Leaderboard, audio, haptics, real-money purchases, analytics, backend and localization. The booster purchase popup uses earned in-game coins.
- Runtime level editing, level sharing and remote level download.
- A mechanic the user has not selected for implementation; its catalog specification remains available for a later task.

### 4.6 Requirement Traceability
| REQ | Source | Summary | PRD section / AC |
| --- | --- | --- | --- |
| REQ-C1 | Brief §2 | Unity 2022.3.62f2 | CON-1 |
| REQ-C2 | Brief §2 | 1080 x 1920 portrait | CON-2, NFR-4, AC-1.4, AC-1.4a |
| REQ-C3 | Brief §2 | Supplied assets, unless absolutely necessary | 5.2, DEC-24, DEC-39 |
| REQ-C4 | Brief §2 | LLM tools in the README | 13.2 R-5, DEC-20 |
| REQ-C5 | Brief §2 | Asset source link | 13.2 R-7 |
| REQ-D1 to REQ-D4 | Brief §3 | One link; project folders; README; `.apk` or recording | 13.1 D-1 to D-4, DEC-19, DEC-27, DEC-54, V-M19 |
| REQ-H1 to REQ-H5 | Brief §4.1 | Tabs, level button, top UI, settings, feedback, layout | 7.1 AC-1.1 to AC-1.8 |
| REQ-S1 to REQ-S3 | Brief §4.2 | Placeholder feedback, toggle states, close to Home | 7.2 AC-2.1 to AC-2.6 |
| REQ-G1 | Brief §4.3 | 5 levels from supplied assets | AC-5.1, 11.11, DEC-38 |
| REQ-G2 | Brief §4.3 | Timer works and stops the level at zero | AC-3.5 to AC-3.8, AC-3.12, RULE-18 to RULE-22 |
| REQ-G3 | Brief §4.3 | Coin inventory | 7.6 |
| REQ-G4 | Brief §4.3 | Fail popup with Restart and Home | AC-4.1 to AC-4.3 |
| REQ-G5 | Brief §4.3 | Pause, restart, home | AC-4.4 to AC-4.8, AC-5.3, AC-5.3a |
| REQ-G6 | Brief §4.3 | Booster controls show feedback; this PRD also requires functional effects | AC-3.10, RULE-27, 7.10 |
| REQ-G7 | Brief §4.3 | Life placeholder | AC-1.7, DEC-12 |
| REQ-G8 | Brief §4.3 | Complete a level and continue | AC-4.9, AC-5.2 |
| REQ-E1 | Brief §4.4 | New, save, play without code or hand edits | AC-7.1, AC-7.8, AC-6.6 |
| REQ-E2 to REQ-E5 | Brief §4.4 | Width and height, doors, blocks, timer | AC-7.2 to AC-7.5 |
| REQ-E6 | Brief §4.4 | Visual form | AC-7.6, AC-7.12 |
| REQ-E7 | Brief §4.4 | One format, read by the game | 7.8, AC-8.1, AC-8.2, AC-8.7, AC-8.9, DEC-1 |
| REQ-E8 | Brief §4.4 | The 5 levels come from the editor | AC-8.4, AC-8.4a, AC-7.11 |
| REQ-O1 | Brief §5 | Case requirement traceability | 4.1, 16.7 |
| REQ-O2 | Brief §5 | Level validation | 7.9, DEC-68 to DEC-70 |
| REQ-O3 | Brief §5 | Functional boosters | 7.10, DEC-74 to DEC-80 |
| REQ-O4 | Brief §5 | Ice blocker | 7.11, DEC-81 |
| REQ-O5 | Brief §5 | Arrow block | 7.12, DEC-82 |
| REQ-EV1 | Brief §6 | Architecture | 10, 8.3, NFR-7, NFR-11 |
| REQ-EV2 | Brief §6 | Editor usability | 11, AC-7.1, NFR-9, EU-1 to EU-3 |
| REQ-EV3 | Brief §6 | Code quality | NFR-6, NFR-X1 to NFR-X7, 16.4 |
| REQ-EV4 | Brief §6 | Correctness | 7, 16 |
| REQ-EV5 | Brief §6 | Performance | 9.2, NFR-P1 to NFR-P10 |
| REQ-EV6 | Brief §6 | Scope control | 4, 14 |
| REQ-R1 to REQ-R6 | Brief §7 | Short README content | 13.2 |
| REQ-N1 | Brief §8 | Industry best practice | 9, 10 |
| REQ-N2 | Brief §8 | Asset feedback | 5.2 (AF-1 to AF-15), R-7 |
| REQ-N3 | Brief §8 | Decide unclear requirements and document | 12, R-3 |
| REQ-N4 | Brief §8 | 7 days | 14 |
| REQ-P1 | `PROJECT.md` | Performance Rules 1 to 7 | 9.2 to 9.5 |
| REQ-P2 | `PROJECT.md` | Lifecycle: nothing runs unattended | 9.3, 10, 16 |
| REQ-U1 | User | Modular pieces may be duplicated, disabled, changed, reordered, and made into prefabs | AC-3.14, AC-3.15, DEC-57 |
| REQ-U2 | User | One Booster system; four in-game and two pre-level choices, shared Rocket effect, immediate Time Freeze feedback | 7.10, DEC-74 to DEC-80, DEC-84 |
| REQ-U3 | User, screenshots 1 to 13 | Zero-stock popup, targeting banners, freeze presentation, completion screen, locked tabs, pre-level selection, opening sequence, and Vacuum outline/shrinking suction | 7.1, 7.3 to 7.5, 7.10; [Visual References](Color-Block-Jam-Visual-References.md); DEC-85 to DEC-88 |
| REQ-U4 | User, screenshots 14 to 16 and later clarification | White outline while held, fitting-door cube fragments, and a 3D Hammer removing one exposed layer/piece or obstacle | AC-3.17, AC-3.18, AC-BST-2.1 to AC-BST-2.7; DEC-75, DEC-89, DEC-90 |
| REQ-U5 | User, screenshot 17 | Presses on a gate, border, wall, block or empty surface inside the board area select the nearest block | RULE-9, AC-3.19 to AC-3.21; DEC-91 |
| REQ-U6 | User clarification | 10 s countdown freeze; Rocket one cell without destroying the block; Hammer one layer/piece/obstacle; real coin purchase | 7.6, 7.10; DEC-74 to DEC-78, DEC-85; OQ-15 to OQ-20 resolved |
| REQ-MECH | User-directed mechanics | Each selected catalog mechanic uses its entry and applicable verification | 8, appendix |

## 5. Product Constraints
### 5.1 Engine, Platform, Stack
| ID | Constraint | Source |
| --- | --- | --- |
| CON-1 | Unity `2022.3.62f2`. No upgrade and no Unity 6-only feature. | Brief §2, AGENTS.md |
| CON-2 | Android and iOS targets, portrait only, reference resolution 1080 x 1920. | Brief §2, AGENTS.md |
| CON-3 | Layout stays correct on a 20:9 device (for example 1080 x 2400) and respects the safe area. | Brief §4.1, PROJECT.md |
| CON-4 | Onity `v0.3.13` for DI, reactive state, events, async, pools, and the tick. No Zenject, VContainer, UniRx, R3, MessagePipe, or UniTask. | AGENTS.md |
| CON-5 | PrimeTween `1.4.11` for tweens. No DOTween or LeanTween. | AGENTS.md |
| CON-6 | Input System for touch. No legacy `Input.*` code. | AGENTS.md |
| CON-7 | UI Toolkit for all runtime screens, projected counter labels and the editor window. No new UGUI canvases or TextMeshPro UI components; the UGUI package stays only for the Input System UI module's `EventSystem`. URP 14. Newtonsoft Json.NET through Unity's `com.unity.nuget.newtonsoft-json` handles separate player saves and the canonical level JSON. | PROJECT.md; user confirmed |
| CON-8 | The lifecycle rules in `PROJECT.md` bind every feature (sections 9.3, 10). | PROJECT.md |
| CON-9 | Use the supplied assets and current Unity/URP stack. Block rendering uses shared materials, direct instanced color, one cached property-block owner with atomic property composition and reset on pool return. Ice and Time Freeze use separate shader graphs. All passes preserve depth, normals, shadows and clipping; device evidence selects the batching path (DEC-96). | PROJECT.md, DEC-96 |
| CON-10 | An Android phone is connected over USB. The `.apk` is the primary deliverable, and device checks run on the real phone. A recording is optional. | User fixed input |

### 5.2 Supplied Assets
- Location: `Assets/ColorBlockJam/Art`. File names stay unchanged. Supplied UI sprites import as Single, no mipmaps, Clamp.
- The [Art Asset to PRD Mapping](Art-Asset-PRD-Mapping.md) covers all 65 imported files and separates supplied HUD sprites from the authored 3D models and gameplay effect prefabs. The approved Vacuum cast uses a separately baked Multiple-sprite atlas. Import readiness does not mark gameplay criteria complete.
- `BlockParts.fbx`: `ModularBlock_Center`, `_Edge`, `_InnerCorner`, `_OuterCorner`; no Base exists in the imported kit (OQ-11 resolved). One gameplay cell spans 2×2 original subtiles/four studs. Vertex-neighborhood composition uses only these supplied meshes (AC-3.14).
- `WallAndDoor.fbx`: `Wall`, `Base_Side`, `Corner_1`, `corner_3`, `corner_3.002` (defective), `corner_4`, `corner_5`, `Door_1x1`, `Door_Arrow`; materials `Gate_Orange`, `Arrow_Gate_Orange`; an unattached `Key 1` geometry.
- `GroundGrid.fbx`: `Grid`, the board floor.
- `Arrows.fbx`: `Arrow_1` to `Arrow_3`, double-headed, measured at 1, 2 and 3 cells long (Arrow, OQ-10).
- `Textures/icetexture.jpg`: Ice overlay through one Shader Graph material (DEC-81).

| Screen | Supplied assets |
| --- | --- |
| Home | `bg_home_screen`, `bg_home_line`, `bg_home_coin_holder`, `bg_home_resource_holder`, `bg_profile_box_blue`, `btn_settings_normal`, `btn_level_bg_normal`, `btn_level_green`, Navigation icons, `bg_active_tab`, `Heart`, `ic_coin_single`, `ic_plus` |
| Settings | `bg_menu`, `Blockjam_Settings_{Music,Sound,Vibration}`, `btn_toggle_on`, `Btn_circle_{blue,green}_small`, `btn_popup_close`, `ic_support` |
| HUD | `bg_gameplay_time_holder`, `ic_time`, `ic_pause`, `ic_coin_single`, `btn_booster_normal`, `ic_clock_freeze`, `Hammer`, `ic_rocket_purple`, `Vacuum` |
| Pre-level and booster overlays | `bg_popup`, `bg_popup_header`, `bg_popup_info_holder`, `btn_booster_normal`, `Btn_circle_{blue,green}_small`, `btn_popup_green`, `btn_popup_close`, `ic_rocket_purple`; Extra Time uses `Clock` with an explicit label until dedicated supplied art is identified (AF-16) |
| Popups | `bg_popup`, `bg_popup_header`, `bg_popup_info_holder(_2)`, `Btn_popup_{green,blue}`, `btn_square_retry`, `ic_restart`, `btn_popup_close`, `ic_home` |
| Board | `GroundGrid.fbx`, `BlockParts.fbx`, `WallAndDoor.fbx` |
| Ice and Arrow | `Arrows.fbx`, `icetexture`; navigation `ic_lock.png` also serves locked Shop and Collection tabs |

Asset feedback list. Each row goes into README R-7.

| ID | Gap or issue | Product response |
| --- | --- | --- |
| AF-1 | Only `btn_toggle_on.png`; no OFF sprite. | DEC-24: grey tint, knob moved, "OFF" label. |
| AF-2 | No dedicated completion title or coin-pile sprite. | Reference 05's hierarchy with styled text, supplied panels/buttons and `ic_coin_single`; no screenshot cropping into runtime art (DEC-7). |
| AF-3 | No resume or play icon. | `btn_popup_green` with a "Resume" label. |
| AF-4 | No font. | DEC-39. |
| AF-5 | No tab label art or digit sprites. | Text labels. |
| AF-6 | One block texture, no per-color art. | Palette tint (DEC-23, DEC-58). |
| AF-7 | `corner_3.002` has a self-intersecting polygon that Unity discards. | Other corner meshes, visual check, RISK-3. |
| AF-8 | Mixed file naming (`Btn_`, `btn_`, `ic_`, `bg_`, bare names). | Reported. Not renamed. |
| AF-9 | `Clock`, `ic_time`, and `ic_clock_freeze` look alike. | DEC-47. |
| AF-10 | No sprite has 9-slice borders. | DEC-40. |
| AF-11 | `ic_home` shows toy bricks, not a house. | DEC-41: text label carries the meaning. |
| AF-12 | No background fits the screen: `bg_home_screen` is 2048 x 2048, `bg_menu` is 1118 x 436. | DEC-42: cover scaling. |
| AF-13 | No audio. | Toggles store state only (NG-3). |
| AF-14 | Undocumented model parts: arrow lengths, `ModularBlock_Base`, the unattached `Key 1`, unused `Door_Arrow`. | OQ-10, OQ-11, OQ-14. |
| AF-15 | No dedicated booster count, plus, selected-check, or targeting-banner set. | Supplied circle buttons, `ic_plus`, panel sprites, text/check glyph and USS state classes (7.10); selection differs by shape/text as well as color. |
| AF-16 | The supplied `Assets/ColorBlockJam/Art` inventory has no dedicated hourglass or red/orange pre-level rocket sprite matching references 09 to 12. | Use existing `Clock` with an "Extra Time" label and `ic_rocket_purple` for the shared Rocket; keep the screenshot as visual reference. No new art import is authorized by this doc change. |

### 5.3 Delivery and Process
- Time box: 7 days, 2026-09-24 to 2026-09-30 (section 14).
- Version control: Unity Version Control (Plastic SCM). The delivery link is an archive exported from the workspace (DEC-27).
- Tooling: the Unity CLI runs headless tests and builds with the Editor closed. MCP for Unity controls the live Editor. One mutation never goes through two transports. Tooling never reaches the player build (DEC-63).
- Device: the team's Android phone over USB runs the IL2CPP builds, the Profiler, the Frame Debugger, and the final full-flow test. The Game view with 1080 x 1920 and 20:9 presets is the pre-check, not the final check.
- High-risk changes (packages, Player Settings, prefabs, the piece catalog asset) need explicit user approval before they are made (DEC-61 to DEC-64, OQ-12).

## 6. Game Rules
Every rule is plain C# domain logic with at least one EditMode test (section 16).

### 6.1 Board
- RULE-1: The board is a rectangular grid of square cells. Width 3 to 8, height 3 to 10 (DEC-4). Any cell may be authored as void (no floor); floor cells must stay four-connected and every door must open onto a floor cell (DEC-100).
- RULE-2: A cell holds at most one block cell. Blocks never overlap.
- RULE-3: Walls follow every boundary between floor and non-floor, including the outer edge and interior voids. A boundary edge segment is solid unless a door covers it; the level JSON sets each door's span (`start`, `length`) (DEC-100).

### 6.2 Blocks and Composition
- RULE-4: A block has one palette color (DEC-23) and a footprint of 1 or more edge-connected cells. Blocks do not rotate during play.
- RULE-5: Level data stores each block's resolved footprint and its source shape ID (DEC-35). A shape-library edit never changes a shipped level.
- RULE-6: Block, wall, and door views use original modular pieces and pooled tile prefabs. Each library shape also has a derived prefab with BlockPiece on its root. Canonical piece JSON owns type/cells; the SO holds JSON/prefab references only. Runtime copies cached definitions and obtains roots through an explicitly prepared Onity factory. The composer runs at attempt build and footprint changes only (AC-3.14 to AC-3.16, DEC-57).

### 6.3 Doors
- RULE-7: A door lies on one board side and covers 1 or more contiguous edge segments (its span). Perimeter cells stay playable (DEC-32). A base door has one color that never changes.
- RULE-8: Doors on one side never overlap, and a door never wraps a corner.

### 6.4 Movement
- RULE-9: While the state is `Playing` and ordinary board input is enabled, one pointer owns one selected block until release/cancel (DEC-26). Pointer-down anywhere in the board's interaction area (interior, gates, walls and outer border) selects the direct-hit block or the nearest on-board block footprint in screen space. HUD/popup/outside-area input never selects. Keep the initial grab offset and do not retarget during the hold; other touches are ignored. Selection does not bypass movement constraints (DEC-91).
- RULE-10: A dragged block moves cell by cell on both axes through free cells and stops at the first blocked cell (block, wall, or a door it cannot use). It never passes through anything.
- RULE-11: On release the block snaps to the nearest valid cell-aligned position on its path.

### 6.5 Exit
- RULE-12: A block exits when its color matches a door, it is pushed against that door's side, and its whole width along that side lies inside the span (DEC-6, DEC-50).
- RULE-13: A valid exit commits in logic at once and frees the cells. Presentation moves the block through the fitting same-color door and breaks it into small color-matched cube fragments (reference 15). Wrong-color, too-wide or invalid approaches never start it. A winning exit is visible in the bounded Won Finish before full teardown; particles never decide occupancy, reward or success (DEC-25, DEC-90, DEC-93).
- RULE-14: A door that does not match the block's color, or whose span is too narrow, blocks it like a wall.

### 6.6 Win and Fail
- RULE-15: Won: no required block remains on the board while the state is `Playing` (RULE-29).
- RULE-16: Failed: the timer reaches zero while a required block remains.
- RULE-17: Terminal latch: the first terminal transition wins. Later requests in the same attempt are ignored.
- RULE-18: Fixed tick order: queued gameplay commands, then the timer, then view sync. A last exit in the frame the timer reaches zero gives Won (DEC-29). A command that enters the saving pause returns from the tick immediately; remaining commands, timer and view phases wait. A durably committed pending booster is the first command on Resume (DEC-92).

### 6.7 Timer
- RULE-19: Each level has an authored duration in whole seconds, 10 to 300, default 60 (DEC-11).
- RULE-20: The countdown starts when the attempt enters `Playing`, advances only in `Playing` outside the opening hold or active Time Freeze, pauses on `Paused`, and stops on teardown.
- RULE-21: The display is `mm:ss` ("02:55", as in the reference top bar; changed from `m:ss` on the user's request of 2026-09-28), rounded up to the whole second. The text changes only when the displayed second changes.
- RULE-22: `Time.timeScale` is never used for pause.

### 6.8 Coins, Lives, Boosters
- RULE-23: Coins are persistent and owned by the save service. A Won attempt grants 10 coins exactly once. A successful booster purchase subtracts its displayed coin cost and grants its displayed quantity in one transaction. Start balance 0; no negative balance (DEC-9, DEC-85).
- RULE-24: The zero-stock booster popup is the coin-spending surface. Shop and coin `ic_plus` buttons remain locked/placeholders; no real-money payment flow is added.
- RULE-25: Editor test play uses an in-memory save store for coins, booster stock, level index and toggles. Purchases and uses in test play never alter real saved data (DEC-30).
- RULE-26: Lives are a static heart with the count "5" (DEC-12).
- RULE-27: Four in-game booster controls and two pre-level selectors show feedback and apply their specified effects through one shared Booster system. Selection alone never spends stock; valid application does (7.10).

### 6.9 Effect and Board Event Rules
- RULE-28: In the command phase the core publishes `BlockExited` (door exit commit), `BlockRemoved` (booster removal), `MoveCommitted` (a release that changed the board), and `FootprintChanged`. Registered effects subscribe to the events they require (DEC-72).
- RULE-29: Every block is required unless an implemented effect says otherwise. Won is checked once after the command phase: no required block remains, whether it left by exit or by booster.
- RULE-30: Effect state. An effect's state lives in the attempt, is rebuilt from level data on Restart and Next Level, is notified only in the command phase while `Playing`, and holds no static state.
- RULE-31: Booster application is a session command inside `Playing`, not a session state. Pre-level selection is an overlay on `Home` and spends nothing. The opening commands apply once after the attempt is built; countdown and ordinary board input are held during their short presentation. Targeting itself does not pause the countdown; an active Time Freeze still suppresses it. A zero-stock purchase popup suspends the attempt through `Paused` (DEC-80, DEC-85).
- RULE-32: Rocket removes one 1x1 footprint cell only if a nonempty edge-connected block remains. Removing the last cell or splitting the block is refused. Hammer removes one semantic layer/piece/obstacle, not a 1x1 cell and not a generic connected-component splitter. In the current slice a plain/Arrow block is one layer; active Ice is one obstacle layer over that block. Superseded (booster-gameplay-flow, DEC-146, DEC-147): a split is allowed, and each remaining 4-connected part becomes an independent block with a new runtime ID; only the last/only cell is refused. Hammer removes the tapped block whole, active Ice included. Any surviving authored layer/footprint uses the same composer. Attempt mutations never write the authored level JSON.
- RULE-33: Ice. A frozen block cannot start a drag and cannot exit. Each `BlockExited` of another block lowers its counter by 1. At 0 it is a normal block from the next input (7.11).
- RULE-34: Arrow. The block moves only along its axis, both ways, and exits only through doors on the two sides its axis points to (7.12).
- RULE-35: Validation runs only in the editor, never in Play Mode or the player build (7.9, DEC-69).

## 7. Functional Requirements
AC numbering: AC-1.x Home, AC-2.x Settings, AC-3.x HUD and board, AC-4.x popups, AC-5.x level flow, AC-6.x coins, AC-7.x editor and AC-8.x data. AC-VAL, AC-BST, AC-BLK-03 and AC-BLK-01 cover validation, boosters, Ice and Arrow. A criterion marked '(brief)' quotes the brief; PRD additions have a separate suffixed row.

### 7.1 Home
- Stories: as a player I see my level, coins, and lives, tap the level to choose optional starting boosters, then press Play. I open Settings. Every button responds to touch.
- Content: `bg_home_screen`; top UI with the life holder (Heart, count, `ic_plus`), the coin holder (ic_coin_single, balance, `ic_plus`), and `btn_settings_normal`; the level button (`btn_level_bg_normal`, `btn_level_green`, "Level N"); a tab bar with Shop, Collection, Home (active, `bg_active_tab`), Leaderboard, Support, each with an icon and a text label.
- Settings and the pre-level popup are overlays on `Home`, not session states. No gameplay object runs on Home. [References 06 to 10](Color-Block-Jam-Visual-References.md#ref-06) guide the layout; Shop and Collection content stays locked.

| AC | Criterion |
| --- | --- |
| AC-1.1 (brief) | "The level button starts the current level." |
| AC-1.1a | The approved interpretation of AC-1.1 is Level button > pre-level popup > Play > current level. The popup shows Level N, Extra Time and Rocket selectors, Play and close. Closing or Android Back returns to Home without starting an attempt or spending a charge (references 09 and 10). |
| AC-1.2 | The level button shows the current level from the save service. After the last level is won, the number keeps counting and the levels play again from the first (DEC-44, DEC-173). |
| AC-1.3 (brief) | "The settings button opens the settings screen." |
| AC-1.4 (brief) | "The layout is correct at 1080 x 1920 and stays correct on a 20:9 device." |
| AC-1.4a | At 1080 x 1920 and 1080 x 2400 nothing is clipped, overlaps, or sits under the notch or the home indicator; UI stays inside `Screen.safeArea`; backgrounds fill the screen with cover scaling (DEC-18, DEC-42). |
| AC-1.5 | A placeholder tab gives press feedback, does not navigate, and Home stays active. Shop and Collection show `ic_lock.png` and a locked/disabled content state throughout the five-level case. The store products and collection content from references 07 and 08 are not implemented. |
| AC-1.6 | Every other Home button (both `ic_plus`, the profile box) gives feedback on press and release (DEC-14). |
| AC-1.7 | The life holder shows the static count. No action changes it. |
| AC-1.8 | The coin holder shows the persisted balance. |
| AC-1.9 | Pre-level selectors are independent: neither, either, or both can be selected. Unselected is blue with a count badge; selected is green with a check badge. A second tap deselects without consuming a charge. Play applies the selected boosters as specified in 7.10. |

### 7.2 Settings
- Content (reference 19, PROJECT.md DEC-134): a popup over the dimmed Home with `bg_popup`, a `bg_popup_header` "SETTINGS" title, `btn_popup_close`, and a `bg_popup_info_holder` inset holding the Vibration, Sound and Music toggle rows with their `Blockjam_Settings_*` icons, then placeholder buttons Legal Terms, Restore Purchases, Support and Language. Toggles store and show state only (NG-3). The same popup component serves the in-game pause menu (reference 20, DEC-135).

| AC | Criterion |
| --- | --- |
| AC-2.1 (brief) | "Each toggle shows a different visual state for on and for off." |
| AC-2.2 | A tap flips state and visual at once. ON: the `btn_toggle_on` knob (baked "On") at the right of a dark green track. OFF: a plain light grey knob at the left of a dark navy track. Both states differ in a greyscale screenshot (reference 19; PROJECT.md DEC-134 supersedes DEC-24). |
| AC-2.3 (brief) | "The player can close the screen and return to the home screen." |
| AC-2.3a | The Android Back button also closes Settings to Home. |
| AC-2.4 | Every placeholder button gives press feedback (DEC-14). |
| AC-2.5 | Toggle states persist across app restarts through the save service (DEC-15). |
| AC-2.6 | While Settings is open, Home controls under it receive no input. |

### 7.3 Gameplay HUD and Board
- Content: HUD top with pause (`ic_pause`), "Level N", the timer (`ic_time` on `bg_gameplay_time_holder`), and the coin balance. HUD bottom, left to right: **Time Freeze, Rocket, Hammer, Vacuum**, matching references 01 to 04. The board is a 3D perspective view (pitch 75, FOV 60; PROJECT.md DEC-103) of the composed floor, walls, doors, and blocks, fitted between the HUD bars (DEC-17).
- Drag input comes from the Input System gameplay action map, enabled only in `Playing`. Gameplay tweens target gameplay objects.

| AC | Criterion |
| --- | --- |
| AC-3.1 | The attempt builds from the selected `LevelData` record in `LevelDatabase`: size, walls, doors, blocks, colors, and timer match what the editor saved. |
| AC-3.2 | A block moves only in `Playing`, never overlaps another block, and never crosses a wall (RULE-2, RULE-10). |
| AC-3.3 | On release a block rests on a cell-aligned position (RULE-11). |
| AC-3.4 | A block exits only through a door of its color whose span covers its width. Other doors block it (RULE-12, RULE-14). |
| AC-3.5 (brief) | "The timer counts down and stops the level at zero." |
| AC-3.6 | The timer starts at the authored value in `m:ss`, plus an applied required Extra Time bonus. Text changes on an integer display change, including an immediate bonus, never every frame. |
| AC-3.7 | At zero with a required block left, the state becomes `Failed`; the timer stays at `0:00`; input and blocks stay frozen. |
| AC-3.8 | In `Paused` the timer value does not change. After Resume it continues from the same value. |
| AC-3.9 | The HUD coin balance equals the save service balance. |
| AC-3.10 | The four in-game booster controls show press, available, empty-stock, targeting and application feedback; a valid use applies the effect in 7.10. |
| AC-3.11 | At 1080 x 1920 and 1080 x 2400 the largest board (8 x 10) fits the board rectangle without touching the HUD. |
| AC-3.12 | Backgrounding the app during `Playing` enters `Paused` through Suspend. On return the pause popup shows and no time was lost (DEC-31). |
| AC-3.13 | A pointer that starts on a HUD or popup element never starts a board drag. |
| AC-3.14 | The composer samples the four occupied quadrants at each footprint vertex. Full occupancy uses four supplied half-cell Centers; a straight boundary uses two Edges; a convex quadrant uses one OuterCorner; three occupied quadrants use one InnerCorner; opposite quadrants use two opposing OuterCorners. A gameplay cell has 2×2 studs. No new board geometry or overlapping additive tops. All reference footprints in four rotations have exact coverage; single-cell placements and both L4-minus-one-cell cases are checked. Empty, disconnected and duplicate input is rejected before replacing output. A reused composition buffer allocates 0 B. Walls/doors use calibrated original mesh prefabs, with placement driven by every floor/void boundary (RULE-3) through the RuleTile-authored environment rules (DEC-100, [Environment Layout](Environment-Layout.md)). The wall exceptions are the Blender-authored `Brick_End`, `Corner_End`, `Corner_Join` and `Door_End` (DEC-139). Built from the supplied Wall and door profiles inside Wall's, Corner_1's and Door_1x1's mesh bounds, `Brick_End` caps both ends of each straight outer-boundary wall run, `Corner_End` forms an outer-boundary corner with a large quarter-round outer arc and both arm ends rounded, `Corner_Join` merges a board corner beside a one-cell void into its shorter wall so no unit there is shorter than one cell, and `Door_End` closes both ends of each gate, so walls and gates read as separate rounded units; interior holes keep one continuous frame. |
| AC-3.15 | One reusable tile prefab exists per piece type, built from supplied FBX children; its children may be duplicated, disabled, re-pivoted and reordered without changing the composer. A catalog maps tiles to prefabs/calibration. Each shape library entry also references an identity-preserving derived prefab with a BlockPiece root and composed preview. No new mesh is modeled; `corner_3.002` is excluded and FBX names stay unchanged. |
| AC-3.16 | Board pieces come from Onity pools warmed at level load to the level's counts plus booster headroom (DEC-60). BlockPieceFactory implements Onity IFactory with PooledFactory/PrefabComponentPool per source type; Create/Return enforce ownership and refuse capacity growth. Hooks reset mutable state while retaining warm sub-pools. No Instantiate/Destroy inside an attempt; owner disposal returns outstanding roots and disposes all pools. |
| AC-3.17 | Selecting a block on pointer-down shows a white outline around its outer silhouette while that pointer stays held, including during dragging. Pointer-up, pointer cancel/loss, Pause, Home, Restart or terminal teardown clears it. Outline does not persist as a toggle or leak to a reused pooled block. It remains legible without internal modular-piece seams (reference 14). Frozen selection can show the outline/blocked feedback but cannot move. |
| AC-3.18 | A legal fitting same-color exit plays the color-matched block-to-cube disintegration near that door (reference 15); refused exits emit none. Pooled/session-owned fragments pause with the attempt and return once. The last exit latches Won and stops gameplay immediately, then finishes its visual within the 0.8 s bound before teardown (AC-4.12); interruption uses forced cleanup. |
| AC-3.19 | In normal gameplay, a direct block hit selects that block; otherwise a press inside the board area on a gate, wall, outer border or empty cell selects the nearest on-board block by distance to its projected occupied footprint. Use no center-distance shortcut or small-radius cutoff. Exclude removed/mid-exit blocks; ties use stable ID order; an empty board selects nothing (reference 17). |
| AC-3.20 | Nearest selection runs once per pointer-down. Preserve the chosen block and grab offset until release/cancel; a border press does not snap/teleport it, and dragging across another block never switches selection. Ice, Arrow and occupancy rules still gate movement. The held outline follows the selected block and clears with the owning pointer. |
| AC-3.21 | HUD/popup/overlay hits and points outside the board area do not trigger nearest selection, including when a board is behind UI. Paused/Won/Failed and the opening hold reject ordinary selection. Booster target mode keeps its explicit target/cancel rules rather than applying nearest-block fallback to a destructive command. Behavior matches at 1080 x 1920 and 1080 x 2400 with no scene-wide or per-frame candidate scan. |

### 7.4 Pause, Fail, and Win Popups
| AC | Criterion |
| --- | --- |
| AC-4.1 (brief) | "The fail popup opens when the level fails. The popup gives a way to restart the level and a way to return to the home screen." |
| AC-4.2 | The fail popup opens only after teardown. No timer tick, block movement, gameplay input, or gameplay tween is active while it shows. |
| AC-4.3 | Fail popup buttons: Restart (`btn_square_retry` with `ic_restart`) and Home (`btn_popup_blue`, DEC-41). |
| AC-4.4 | The pause button moves `Playing` to `Paused` through Suspend, then opens the pause menu: the shared Settings popup plus a Home button (reference 20). Its close button resumes; Restart is the HUD restart button (PROJECT.md DEC-135 supersedes DEC-13). |
| AC-4.5 | In `Paused` the board ignores input, and gameplay tweens are paused, not stopped. |
| AC-4.6 | Resume returns to `Playing` with board, timer, and tweens exactly as they were. |
| AC-4.7 | Home from a popup runs teardown if needed and shows committed balances. Result navigation is disabled while a win-save transaction is pending or failed; Retry must commit it first (AC-6.14). |
| AC-4.8 | A second tap on a popup button during a transition has no effect. No double restart, no double reward. |
| AC-4.9 | After the bounded Won Finish and full teardown, open the completion overlay laid out as reference 05 without its ad button (DEC-173): dimmed board, prominent "Level Complete", supplied coin visual, a confetti burst, and one `btn_popup_blue` button reading Continue (Next Level) with the +10 reward. There is no separate Home button; Back acts as Home after the commit (AC-4.11). A pending/failed win save shows saving/Retry feedback and disables the button; committing enables it without another reward. Reference X2 ads and 40/80 rewards do not change the economy. |
| AC-4.10 | After the last database level, level numbers keep counting and the levels play again from the first: Level 6 of the five-level case plays level 1 and reads "Level 6" (DEC-173, superseding DEC-8). |
| AC-4.11 | Android Back acts as Pause in Playing, Resume on the ordinary pause popup, and Home on fail/win popups. Saving gates navigation; a pending/failed win save keeps Back on the Retry screen. Backgrounding never dismisses these gates or resumes play. |
| AC-4.12 | The final door exit, Hammer hit or Vacuum color clear latches Won once and disables gameplay input, domain ticks and both clocks immediately. Its visual-only Finish runs to completion within at most 0.8 foreground seconds before teardown; no callback changes gameplay/rewards. Missing visuals, timeout, backgrounding, scene unload or an allowed Home/Restart interruption force cleanup. Disk saving cannot extend this bound. |
| AC-4.13 | Before win/fail teardown, capture the board using a render target allocated at level load. The popup displays this static capture; no live board remains behind it. If capture is unavailable, use the supplied background. Release/reuse the capture through its UI owner on navigation; no per-attempt leak or capture-time texture allocation. |

**Won Finish (DEC-93):** `Won` is latched at the gameplay decision, not after an animation callback. The session stops the gameplay action map, main/freeze clocks, queued commands and domain tick; it starts only the accepted final visual intents and a bounded, cancellable presentation deadline. Other gameplay tweens stop. Existing pooled views may animate only for this finite render-only phase, under the session owner, using PrimeTween without an independent gameplay Update/tick. At visual completion or the 0.8 s deadline, capture the static board, run full Teardown and open the result popup. Failure captures and tears down immediately, with no Finish. Reward/progression saving is a separate owned transaction; slow/failed disk I/O never keeps the board alive, and Retry never replays Finish or awards again.

Popup animations are UI work owned by the popup view and stopped in `OnDisable` (DEC-45).

### 7.5 Level Flow and Progression
| AC | Criterion |
| --- | --- |
| AC-5.1 | Levels 1 to 5 come from `LevelDatabase` in order, meet 11.11, are solved by hand before submission, and have their solutions in `docs/LEVELS.md` (DEC-38). |
| AC-5.2 (brief) | "The player can complete a level and continue to the next level." |
| AC-5.3 (brief) | "Restart sets the level back to its start state. No data stays from the previous attempt." |
| AC-5.3a | Block positions and footprints, exited blocks, effect state, the timer, the active drag, targeting, tweens, and pooled objects are all rebuilt from level data. Persistent coin and stock transactions remain committed. |
| AC-5.4 | A latched win prepares one snapshot containing +10 coins and the advanced level index. Publish both only after durable commit. The result popup may show a saving/retry state, but Continue/Home cannot leave until commit succeeds. Retries reuse the intended snapshot instead of incrementing again. Clamp the saved index to the database count at load (DEC-44, DEC-92). |
| AC-5.5 | Killing the app mid-level and relaunching opens Home on the same level. Attempt state is not restored. Committed coin purchases and booster spending remain saved; relaunch grants no refund or refill. |
| AC-5.6 | Transitions follow the table below exactly. Any other request is rejected and changes nothing. |
| AC-5.7 | On Play while Home, validate/build the board, warm pools and resolve eligible starting effects before committing their stock debit. Failed preparation spends nothing. After a successful commit, enter Playing and apply Extra Time then Rocket once before any timer phase. Their opening icons briefly appear (reference 11); ordinary drag/HUD input/countdown begin after the sequence finishes (reference 12). Pause suspends presentation; teardown cancels it. Double Play cannot duplicate effects/charges. Backgrounding during the Home commit waits for explicit Play; it reuses the committed intent without a second debit/reroll, while closing discards it without refund. Restart, direct Next Level and editor test play start with no selections. |

| From | Event | To | Step |
| --- | --- | --- | --- |
| Home | Level button | Home | Open pre-level overlay; no gameplay or spending |
| Home | Pre-level close / Android Back | Home | Clear selection, close overlay |
| Home | Pre-level Play | Playing | Prepare/warm/validate; remain Home during any starting-stock commit; then apply committed opening effects once |
| Playing | Pause, Android Back, or app backgrounded | Paused | Suspend |
| Paused | Resume | Playing | Resume |
| Playing | Valid booster use needs stock commit | Paused | Capture intent, Suspend with Saving reason, return from accepting tick; no pause popup |
| Paused | Stock commit succeeds, app foreground | Playing | Publish committed stock; apply captured intent once before timer, then resume normal phases |
| Paused | Stock commit fails | Playing | No effect/debit; show error and resume previous clocks, unless backgrounded |
| Paused | App backgrounded during stock commit | Paused | Latch user-resume requirement; result never auto-resumes; retain committed intent for explicit Resume |
| Playing | Zero-stock booster tap | Paused | Suspend, cancel targeting, open purchase overlay |
| Paused | Purchase overlay close / Android Back | Playing | Close overlay, resume; app backgrounding returns to the normal pause overlay |
| Paused | Restart | Playing | Teardown, rebuild |
| Paused | Home | Home | Teardown |
| Playing | No required block left | Won | Latch result, stop gameplay/clocks/input; prepare one reward snapshot; bounded Finish, capture, teardown, popup |
| Playing | Timer reaches 0 | Failed | Teardown |
| Won | Next Level, reward commit complete | Playing | Finish/teardown already complete; build next attempt |
| Won | Home, reward commit complete | Home | Cancel any remaining Finish, teardown if needed |
| Failed | Home | Home | Already torn down |
| Failed | Restart | Playing | Rebuild |

### 7.6 Coin Inventory and Persistence
The save service is the only owner of persistent data (DEC-10, `PROJECT.md` rule 8).

| AC | Criterion |
| --- | --- |
| AC-6.1 (brief) | "The coin count stays correct after the player leaves the level and returns to it." |
| AC-6.1a | Without a win or purchase, the balance is unchanged after Playing/Pause/Home/Level, Playing/Fail/Home/Level, Playing/Pause/Restart, and app kill/relaunch. A committed purchase remains reflected after those paths; navigation never refunds it. |
| AC-6.2 | A Won attempt commits exactly +10 coins once, even after save retries or several Next Level taps. The same intended coin/progression snapshot is retried; failed writes publish neither change. |
| AC-6.3 | Failed, Restart, Pause, Resume, and Home leave the balance unchanged. |
| AC-6.4 | Home, HUD, and the win popup read one reactive coin value from the save service. No other stored copy exists. |
| AC-6.5 | Balance, booster stock, level index and toggles survive an app restart. The save service writes on change only; coin debit and purchased stock grant are one coherent persisted change. |
| AC-6.6 | Editor test play uses an in-memory save store and never changes real saved coins, booster stock, level index or toggles, including after a purchase/use (DEC-30). |
| AC-6.7 | Buy displays a positive configured price and quantity. With enough coins, it subtracts the price and grants the quantity to that booster exactly once. Default case tuning is 10 coins for 1 charge, with 1 initial charge seeded once per available definition; these are design defaults, not screenshot prices or user-specified amounts. |
| AC-6.8 | Insufficient coins, invalid offer, inventory overflow or persistence failure leave both balance and stock unchanged and show a clear message. Buy cannot double-submit while a transaction is pending; a duplicate completion cannot grant or debit twice. |
| AC-6.9 | A successful purchase refreshes balance/count without auto-cast or pre-level selection, then closes the purchase popup. In-game purchase resumes play; pre-level purchase returns to the pre-level popup with the new count and no selection. Restart/Next Level/relaunch do not reset or refund inventory. |
| AC-6.10 | A validated booster intent commits one available charge before applying once (AC-6.11); invalid or pre-commit cancelled uses spend none. Committed interrupted intents follow AC-6.12/13. No negative stock, duplicate spend or spending through stale UI counts. Both Rocket contexts read the same stock. |
| AC-6.11 | An in-game use validates first, captures its intent and enters Paused with the Saving reason before any persistent work. The accepting tick skips all remaining gameplay phases. Successful commit precedes one application on the first resumed command phase; failure changes no board, timer, coin or stock value. No JSON or file I/O occurs in a Playing tick. |
| AC-6.12 | Fault injection before/during temp write and replacement restores either the whole previous snapshot or the whole committed next snapshot, never mixed coin/stock fields. Reload sees the durable result. After a committed debit, killing the app before visual/application completion does not refund or replay the discarded attempt. Test the storage adapter on the target device; this is a requirement, not assumed crash-proof behavior. |
| AC-6.13 | Backgrounding during a write never auto-resumes on completion. A successful pending use waits for explicit Resume and applies once before the timer. Home/Restart from that later pause discard it without refund. While the write is unresolved, duplicate uses and navigation are gated. Old scene/attempt callbacks cannot apply an effect to a new attempt. |
| AC-6.14 | A failed win save shows Retry and blocks Continue/Home while preserving the latched result. Retrying commits the same reward/progression snapshot once. Final presentation and teardown still finish within their own bound; disk I/O cannot keep the board active. |

**Save boundary (DEC-92)**

- The save service owns one local Newtonsoft JSON player snapshot under `Application.persistentDataPath`: save version/revision, coins, booster stock, level index and toggles. The authored level database files and their format versions are separate from player saves. Replace the pending PlayerPrefs design; this documentation change does not migrate or write runtime saves.
- One owned transaction at a time writes a temporary snapshot, flushes it and atomically replaces the committed file through the platform storage adapter. Publish the reactive snapshot only after success. On failure retain the previous snapshot and report failure; retry the same prepared snapshot rather than applying its arithmetic twice. Verify replacement/recovery semantics with fault injection and on-device tests before claiming durability.
- A valid in-game booster request captures a non-allocating intent (attempt generation, booster ID and resolved target), clears stale pointer/drag commands, enters Paused with reason Saving and ends the accepting tick. The session coordinator awaits the save outside the gameplay tick; only then may the first resumed command apply the frozen intent. Both main and freeze clocks and existing gameplay tweens stay suspended during the write. No new session enum or manual pause popup is needed.
- A rejected target never starts a save. Pre-level Play prepares the board/pools and resolves Rocket while still Home, then commits all applicable selected charges together before entering Playing. No eligible Rocket target means no Rocket debit. The opening sequence applies committed effects once without rerolling or another spend. If backgrounded during this Home commit, remain Home with the committed intent; the next explicit Play starts that prepared attempt without another debit or reroll. Closing the pre-level popup after commit discards the intent without refund.
- All write completion paths are owned and awaited, and guard the originating scene/attempt generation. New use/navigation is gated while unresolved. Backgrounding an in-game write latches the need for an explicit Resume; a late completion cannot reactivate play. A committed pending use may then be discarded by Home/Restart without refund. A process kill after durable debit also keeps the debit: attempts are not recovered and effects are not replayed on launch.
- Purchases commit in Home or the existing Paused modal; toggles commit on Home, and the win snapshot commits in Won. JSON/allocation and file I/O are allowed at these owned save boundaries, never in Playing. Win saving runs independently of the bounded Finish presentation; after teardown the result popup can show Retry, with Continue/Home disabled until success. Editor test play substitutes an isolated in-memory adapter for the entire transaction path.

### 7.7 Level Editor
- Stories: as a designer I create a level, size the board, paint blocks and doors, set the timer, save, and press Play, without touching code or files. I open, duplicate, reorder, and fix levels.

| AC | Criterion |
| --- | --- |
| AC-7.1 (brief) | "A designer can make a new level, save it, and play it. The designer does not write code and does not edit a file by hand." |
| AC-7.2 | Width and height steppers resize the grid at once within 3 to 8 by 3 to 10. A shrink that removes content first previews it hatched, then asks in designer language ("Remove 2 blocks and 1 door?"). Cancel keeps the size. Confirm is one Undo step. Structural validation runs after every resize (DEC-33). |
| AC-7.3 | Blocks: pick a shape and a color, rotate with `R`, click to place, drag to move, right-click or Eraser to delete. An overlapping or out-of-bounds placement is refused and the ghost shows red. |
| AC-7.4 | Doors: pick a color, click boundary segments to create or extend a span, right-click to delete. Interior, corner-wrapping, and overlapping doors are refused. |
| AC-7.5 | The timer field accepts whole seconds 10 to 300 and also shows `m:ss`. |
| AC-7.6 (brief) | "The editor shows the level in a visual form. It does not show only numeric fields." |
| AC-7.7 | Save validates the active working level and writes it to the canonical level database: its pack, then the manifest (DEC-196). Unsaved changes are marked. Undo and Redo cover every edit. |
| AC-7.8 | Play saves the level and enters Play Mode straight into it, skipping Home, with an in-memory save store. Leaving Play Mode returns to the editor with the same level open. |
| AC-7.9 | Structural errors (no blocks, overlap, out of bounds, invalid door, unknown color or effect) block Save. They are listed in designer language with the cell, and a click highlights it. |
| AC-7.10 | Everything above works in the editor window alone. The Inspector is never needed. |
| AC-7.11 | Each Save writes an editor provenance stamp into the active level record in its level pack, alongside its format version (DEC-37). |
| AC-7.12 | The editor opens from the menu, by double-clicking the `LevelDatabase` asset in `Assets/ColorBlockJam/Data/Levels`, or from its "Open in Level Editor" button (DEC-34). |
| AC-7.13 | The level selection bar selects a `LevelDatabase` reference and target level, edits a working copy, and offers Level Name, Create, Delete, and Reload JSON; Save Level sits in the bottom bar (11.1, DEC-119). It shows the referenced JSON source; Source Image is absent. |

### 7.8 Level Data Format and Database
- Level authority: the active level database is the sole authored source, with ordered level records. It is a folder under `Assets/ColorBlockJam/Data/Json/Levels` (`Main` ships) holding `manifest.json` and `pack-NNN.json` files of at most 100 levels (DEC-196 in `PROJECT.md`). The editor validates and writes those files. The project scope installer on `ProjectContext.prefab` names the active manifest through an Addressables reference and stores no duplicate level list; no ScriptableObject wraps it (DEC-194 in `PROJECT.md`). Stable level IDs identify records across reordering. There are no per-level ScriptableObject assets, `Resources.Load`, `StreamingAssets`, or level path lookups (DEC-1, DEC-2).
- Runtime conversion: project scope parses and validates the referenced JSON once before play. On level load, the selected parsed `LevelData` is copied into a plain C# attempt model. Gameplay never mutates the source, reads Unity objects during a tick, or reparses level JSON in a Playing tick. The editor and runtime share validation and conversion rules.
- Editor source workflow: Create, Delete, and Save validate and write the canonical JSON; Reload reads it. A stale editor view cannot overwrite external changes. Invalid data leaves the previous file intact. There is no second level import/merge/export path. Player progress remains a separate JSON snapshot (7.6).
- JSON database envelope (format version 1): `formatVersion` and `levels[]`; each level has `id`, `name`, `board` (width, height), `timerSeconds`, `doors[]`, `blocks[]`, and `cellObjects[]` (empty until a cell object is selected). The planned provenance `editorStamp` (tool, editorVersion, savedAtUtc) and optional `validation` record (7.9) require schema support before they can be written. Format version belongs to the authored database contract.
- Door: `id` (for example `d1`), `side` (`Top`, `Bottom`, `Left`, `Right`), `start`, `length`, `colorId`, `effects[]`.
- Block: `id` (`b1`), `colorId`, `origin`, `cells[]` relative to the origin, `effects[]`. The block's piece is the footprint key of its cells; DEC-193 removed `shapeId` (level JSON version 3, [Level Data Format](Level-Data-Format.md)).
- Effect in level JSON: `{type, params}`. `type` is a stable lowercase string from the effect registry (`ice`, `arrow`). The planned format-1 extension keeps `params` as a list of `{key, value}` string pairs. A registered converter per effect validates keys and turns them into typed values when JSON loads; Save applies the reverse mapping (DEC-71).
- Conventions: x grows right, y grows up, both from 0. Door `start` counts from the left (Top, Bottom) or the bottom (Left, Right). Colors are palette string IDs, never indexes (DEC-23).
- Extension: version 1 JSON includes effects arrays and `cellObjects`; the current foundation requires them to be empty. Registering an implemented effect type need not change `formatVersion` if its data fits the existing envelope. A breaking envelope change requires a versioned, validated migration before the editor overwrites the file.
- Serializer contract for the canonical level JSON: `TypeNameHandling.None`; only registered effect IDs choose explicit converters. CLR type names or metadata in input never select a runtime type. Required fields/arrays, allowed keys, enum values, IDs and parameter ranges are validated; missing or malformed required values fail instead of silently defaulting. Keep `link.xml` preservation for runtime level DTOs/converters and player-save DTOs, and test JSON read/write round trips and invalid input. JSON work stays outside the Playing tick; player-save boundaries are in 7.6. DTO fields follow DEC-73.

AC-8.1's shared saved format is the level database (a manifest plus packs, DEC-196), authored by the editor and read by the game through the project scope installer's Addressables reference (DEC-194).

| AC | Criterion |
| --- | --- |
| AC-8.1 (brief) | "The saved data has one format. The game reads the same format." |
| AC-8.2 | One level JSON record holds every authored attribute: size, doors, blocks (color, resolved footprint, source shape), effects, and timer. Its parsed `LevelData` counterpart has the same gameplay values. No gameplay attribute exists only in the editor or only in the game. |
| AC-8.3 | The game reads levels only through `LevelDatabase`. Its record order is play order, and the editor controls it. |
| AC-8.4 (brief) | "The 5 levels from section 4.3 come from this editor." |
| AC-8.4a | Each shipped level carries the editor stamp (AC-7.11). |
| AC-8.5 | At load one converter turns the selected `LevelData` record into the plain C# attempt model. Board logic never reads Unity objects or JSON DTOs. |
| AC-8.6 | A level JSON record with a structural error or unknown format version does not load. Invalid editor changes never replace the existing file. The failure reports one clear error and builds no board. |
| AC-8.7 | Round trip: editor working model, canonical JSON Save, runtime load gives equal values and order for every authored field, including effects and params when supported. |
| AC-8.8 | DEC-193 supersedes the enum, ID and prefab parts of this criterion: the editor-only piece library stores `{name, cells}` templates keyed by their footprint, and no save writes an enum, a prefab or a script. Shape definitions live in canonical JSON keyed by stable BlockShapeType IDs/names; the library SO holds JSON/prefab references only. The palette remains a PaletteColor-keyed SO. Export copies saved JSON; Import replaces active records; Merge appends missing identities, skips identical records and rejects conflicts. Reload reconciles external edits; Save/Undo refuse stale disk baselines. The complete batch is validated before persistence, with new enum source imported once, last. New starts an unsaved draft without compilation. Save commits trimmed cells and an owned derived prefab, then appends a PascalCase enum member only for a new type. Existing IDs never renumber. Save preserves prefab GUID/custom root content and rejects unrelated/read-only or open Prefab Mode targets. Delete removes the entry with Undo while retaining its enum and prefab. Library Undo restores JSON and stale derived prefabs regardless of selection or an open editor window. Width/height sliders, centered cross, shared tint and wide New/Delete above Save/manipulation controls support authoring. Ctrl+Z/Ctrl+Shift+Z undo/redo strokes, Clear, Rotate and resize; visible help explains shortcuts. Draft Undo preserves name/color. Level JSON string IDs convert at the load boundary, never as array indexes. |
| AC-8.9 | The `Main` level database in `Assets/ColorBlockJam/Data/Json/Levels` (its manifest and packs) ships in the local `Level Database` Addressables group; the project scope installer's level JSON reference on `ProjectContext.prefab` names its manifest (DEC-165, DEC-194, DEC-196 in `PROJECT.md`). A code search finds no `Resources.Load` or `StreamingAssets` use for levels. |
| AC-8.10 | Every saved database JSON has `formatVersion` 1 and ordered `levels[]`; each level has `doors[].effects`, `blocks[].effects`, and `cellObjects`, even when empty. Save updates that JSON directly. |
| AC-8.11 | An unknown effect type, parameter key or value, missing required parameter, unknown color ID, or dangling ID fails JSON validation loudly (AC-8.6). Nothing is ignored, defaulted, or dropped. |
| AC-8.12 | The save service and canonical level JSON loader/writer use the approved Newtonsoft package. Registered converters preserve the format-1 string-pair params representation and use `TypeNameHandling.None`; unexpected type metadata is rejected. Runtime DTO preservation and a representative level load pass on Android IL2CPP. |
| AC-8.13 | Create, Delete, and Save write only validated records to the canonical JSON. Reload reads the same file. An external file change blocks a stale editor Save without partial changes or lost records. |

### 7.9 Level Validation (REQ-O2)
Brief: "Level validation. The editor gives a warning before it saves an unsolvable level." The v1 static color and span check (old DEC-21) missed blocked corridors and deadlocks, so it is replaced by a three-stage pipeline (DEC-68).

**Definitions**

- Solvable: a finite sequence of legal moves from the authored start makes every required block leave. A legal move is a one-cell slide of one block, or an exit through a fitting door with a clear path to it (the exit folds in the "pushed against the side" slides).
- The timer and boosters are not part of solvability. Rollic states every level is solvable without boosters (DEC-68).
- Results: Solvable (with a move list), Proven unsolvable, Unknown (budget ran out), Not checked (cancelled, over a hard limit, or an effect with no solver hook).

**Pipeline on Save, Play, and the Check button**

| Stage | What | Owner | On failure |
| --- | --- | --- | --- |
| 1 | Structural validation (base rules, AC-7.9) | Domain, shared with the runtime loader | Save is refused. Stages 2 and 3 do not run. |
| 2 | Static pre-checks. PRE-1: every block has a reachable door of its color wide enough. PRE-2: every ice counter is at most the number of other required blocks. PRE-3: effect-contributed checks. | Validation assembly | Proven unsolvable with a named reason. Stage 3 is skipped. Also runs live after each edit as yellow warnings. |
| 3 | Bounded search: guided breadth-first search over one-cell slides, with plain BFS as the test oracle. Default budget 250,000 states and 3.0 s, per-user override 10,000 to 5,000,000 states and 0.5 to 30 s. | Validation assembly, run by an editor service on a worker thread | Proven unsolvable or Unknown opens the warning dialog. |

- Rules are not re-implemented. The solver asks the Domain predicates once per block position and caches the answers as bit masks, and a parity test proves agreement with the runtime (AR-10).
- Effects plug in through one solver hook interface per effect, registered by effect type. The search core never branches on an effect type. Ice and arrow ship their hooks with their implementations.
- Hard limits: 8 x 10 board, 16 blocks, 64 effect state bits. Over a limit, the result is Not checked with a designer-language reason.
- Threading: the service snapshots the level on the main thread and runs the search on a worker thread with a progress counter and Cancel. It refuses to start in Play Mode and cancels on entering Play Mode (DEC-69). The Validation assembly is editor-only and never ships.
- Detail (state key, reductions, heuristic, test fixtures): appendix section C.

**Editor support**

- Validation panel: a status chip (green "Solvable, 23 moves", red "Cannot be solved", yellow "Not confirmed", grey "Not checked", stale after any edit), Check, Show solution, and the message list with click-to-highlight.
- Bottom bar progress during a check: position counter, time bar, Cancel check.
- Warning dialog (modal UI Toolkit window): Save anyway, Cancel, and Show best attempt. Focus starts on Cancel; Enter and Escape both mean Cancel. Proven unsolvable and Unknown have different titles and honest reasons.
- Solution and best-attempt replay on the grid (Play, Pause, Step, Stop). The replay never changes the level, the Undo stack, or the dirty flag.
- A validation badge per level in the database list, and a "Check all levels" command.

**Validation record** (planned optional editor-owned `validation` object in each canonical level JSON record; no gameplay code reads it)

| Field | Type | Meaning |
| --- | --- | --- |
| `status` | string | `Solvable`, `Unsolvable`, `Unknown`, or `NotChecked`. Unknown text reads as `NotChecked`. |
| `contentHash` | string | 16 hex digits over the rule-relevant content (timer excluded). Lets Save reuse an unchanged result. |
| `solverVersion` | int | Bumped when solver rules change. A mismatch invalidates the stored result. |
| `slideMoves` | int | Length of the found solution; 0 when not Solvable. |
| `savedAnyway` | bool | True when the designer chose Save anyway. |

**Acceptance criteria**

| AC | Criterion |
| --- | --- |
| AC-VAL-1 | Structural validation runs first. A structural error blocks Save, and the pre-checks and solver do not run. |
| AC-VAL-2 | PRE-1: a block with no reachable door of its color wide enough gives Proven unsolvable without a search. The message names the block, its cell, and the width it needs. |
| AC-VAL-3 | PRE-2: an ice counter above the number of other required blocks gives Proven unsolvable without a search, with both numbers. |
| AC-VAL-4 | Changing only the timer leaves the result and the content hash unchanged. Boosters are never simulated. |
| AC-VAL-5 | Slide and exit legality come from the Domain predicates. A randomized parity test shows solver and runtime agree on every move. |
| AC-VAL-6 | Every Solvable move list, replayed through the runtime Domain rules, is legal at every step and ends with every required block gone. |
| AC-VAL-7 | Proven unsolvable is returned only by a pre-check or an exhausted search. A budget hit always gives Unknown. |
| AC-VAL-8 | The default budget is 250,000 states and 3.0 s. The limit hit first is shown in the Unknown message. The override is clamped to its ranges. |
| AC-VAL-9 | The window keeps repainting during a check, the counter updates at least every 200 ms, and Cancel stops the run within 100 ms. |
| AC-VAL-10 | A check cannot start in Play Mode and is cancelled on entering it. The release APK does not contain the Validation assembly. |
| AC-VAL-11 | An edit or level switch during a check cancels it and any pending save, and marks the result stale. A stale result never shows as Solvable. |
| AC-VAL-12 | Proven unsolvable and Unknown open the blocking dialog with Save anyway, Cancel, and (for search results) Show best attempt. Focus starts on Cancel. |
| AC-VAL-13 | Cancel writes nothing (the file is byte-identical and the level stays dirty). Save anyway writes the matching status with `savedAnyway` true. |
| AC-VAL-14 | A Solvable result saves with no dialog and shows "Solvable in N moves" and Show solution. |
| AC-VAL-15 | Show solution replays the move list with Play, Pause, Step back, Step forward, and Stop, changes nothing in the level, and stops on any edit, level switch, window close, or Play Mode entry. |
| AC-VAL-16 | Show best attempt replays the path with the most exits and highlights the blocks that never exited, with a caption. |
| AC-VAL-17 | The same level and budget give the same status and move list on every run. |
| AC-VAL-18 | More than 16 blocks, more than 64 effect bits, or an effect with no solver hook gives Not checked with a designer-language reason. |
| AC-VAL-19 | Ice and arrow are solver hooks registered by effect type, with no ice- or arrow-specific branch in the search. A test-only hook needs no search change. |
| AC-VAL-20 | A hook that is not exit-monotone turns off eager exits, and the result then matches plain exhaustive BFS. |
| AC-VAL-21 | Save writes the validation record. An unchanged level with the same hash and solver version reuses the stored status; Check always searches. |
| AC-VAL-22 | Every shipped level 1 to 5 is Solvable in the default budget and stores Solvable with `savedAnyway` false. |
| AC-VAL-23 | After setup the solver allocates nothing per expanded state. |
| AC-VAL-24 | Every validation message uses designer terms (color, shape, cell) and no type names or IDs. A click highlights the named block. |

Verification: V-VAL-1 to V-VAL-13 (16.5). Structural checks, bounded cancellable search, honest warning, Save anyway, solution preview and Play Mode guard are all required.

### 7.10 Functional Boosters (REQ-O3)
All tools use one **Booster** category, definition registry and command/application system; there is no separate Skill hierarchy. The five effects are Time Freeze, Rocket, Hammer, Vacuum and Extra Time. Rocket has pre-level and in-game entry points with one shared stock and rule. Detail: [Mechanics Catalog section 2](Color-Block-Jam-PRD-Mechanics-Catalog.md#2-boosters). Visual states: [references 01 to 04 and 09 to 18](Color-Block-Jam-Visual-References.md). Exact prefab, sprite and requirement bindings: [Art Asset to PRD Mapping](Art-Asset-PRD-Mapping.md).

| Usage context | Available choices | Target selection |
| --- | --- | --- |
| Pre-level | Extra Time, Rocket | Selection in the Level popup; automatic application at opening. Rocket chooses a random valid cell. |
| In-game | Time Freeze, Rocket, Hammer, Vacuum | Immediate Time Freeze; player-selected cell/block/color for the others. |

Five definitions serve six UI placements. The two Rocket placements have the same ID, effect, validity checks and persistent stock. Different icon colors in the reference do not create different mechanics. Context is data describing availability and target acquisition, not a duplicate implementation of the effect.

| ID | Booster | Rollic rule | User intent | Slice rule | Decision |
| --- | --- | --- | --- | --- | --- |
| BST-1 | Time Freeze | Pauses the countdown for 10 s | Freeze the main clock and open a separate 10-second countdown | After its stock commit, activate without targeting: hold the main value, count a separate HUD timer from 10 to 0 while Playing, then resume the main clock. Blocks still move/exit; no numeric +10 is added to the main clock. | DEC-76 |
| BST-2 | Hammer | Earlier research describes a smash or counter decrement | Remove one layer/block-piece or one obstacle | Target a semantic piece/obstacle. A plain/Arrow block is one layer and is removed whole. On active Ice, remove that obstacle shell only, leaving the block. A future layered block loses only its exposed layer. Superseded (booster-gameplay-flow, DEC-147): Hammer removes the tapped block whole, whatever its footprint, including an active Ice shell and its block; it never splits. | DEC-75 |
| BST-3 | Rocket | Reference 01 says one unit and 11 to 12 shows a changed footprint | Only a 1x1 grid part, never whole-block destruction | Remove one valid cell and recompose the same block. Refuse splits and final-cell removal. Pre-level chooses a random valid cell; in-game uses the tapped cell. Superseded (booster-gameplay-flow, DEC-146): splits are allowed; each remaining 4-connected part becomes an independent block. Only the final/only cell is refused, and starting candidates include split-causing cells. | DEC-74, DEC-84 |
| BST-4 | Vacuum | Removes all blocks of one color | Show the dismissible targeting banner; pull the chosen color off the board | In-game target. The tapped block picks the color. Every block of that color is removed, including iced and arrow blocks. | DEC-77 |
| BST-5 | Extra Time | User-supplied pre-level hourglass reference | Select before Play; gain time on opening | Pre-level only. Add `extraTimeSeconds` once before the countdown starts. +10 s is the adopted adjustable case default, distinct from the in-game freeze. | DEC-84 |

**Shared rules**

- A booster is a session command applied in the command phase (RULE-18, RULE-31). Each rule is plain C# on the board model. A registry maps booster ID to supported contexts, target kind, rule, icon, description, and default charges. The pre-level popup and HUD read the same definitions; effect logic is independent of the UI entry point.
- Targeting: a stocked targeted booster shows its selected style and a dismissible top banner with icon, name, instruction and close button (references 02 and 03). Hammer: "Tap a block layer or obstacle"; Rocket: "Tap a block part"; Vacuum: "Tap a block to choose its color". Superseded (booster-gameplay-flow, DEC-150): the copy is Rocket "Tap and destroy one unit of a block!", Hammer "Tap any block to destroy it!", Vacuum "Tap and vacuum blocks with the same color!", and the banner sits under the timer instead of replacing it. The next board tap picks the target. Close, the same button, empty space or Android Back cancels. Pause, backgrounding and teardown cancel. Drag does not move blocks while targeting; the timer continues unless Time Freeze is active. Switching boosters spends nothing (DEC-80).
- Charges: persistent stock owned by the save service, keyed by the shared booster ID. Seed 1 once per available definition (configurable 0 to 9); never reseed on Restart/Next Level. Pre-level/HUD display the same stock. Selection is free. Validate first, then commit one stock debit through 7.6 before the effect applies once; the rule never debits again. Coins change on purchase, not use (DEC-78, DEC-85, DEC-92).
- Cancelled, switched or refused uses before commit spend nothing. A failed stock commit produces no effect. Once durable, the debit survives interruption/kill even if its pending effect is discarded with the attempt; there is no refund or cross-attempt replay.
- Whole-block removals raise `BlockRemoved`, never `BlockExited`. Rocket raises `FootprintChanged` and cannot remove a whole block. Removing only an obstacle/layer preserves the underlying required block and does not clear other Ice counters. Superseded (booster-gameplay-flow, DEC-146, DEC-147): a Rocket split raises one `FootprintChanged` per resulting part (parent first, then new IDs); Hammer removes no obstacle alone. Won is checked after the command phase (RULE-29, DEC-79).
- Views: effect visuals come from pools warmed at level load. Piece headroom covers every supported Rocket footprint reachable from the authored board, bounded by its cell count rather than initial charges; purchases may replenish charges mid-attempt. Hammer removes existing semantic pieces and does not create arbitrary split children. No effect grows a pool during play. Effect tweens target gameplay objects (DEC-60).
- Hammer application: resolve exactly one exposed semantic target. With active Ice, remove its shell completely, set its counter to 0 and preserve the underlying block. Otherwise a plain/Arrow block is one piece and is removed whole, regardless of footprint. If a layered mechanic is selected, remove only its exposed layer and retain authored lower content. Hammer never geometrically splits a footprint or causes a second effect in the same hit. Superseded (booster-gameplay-flow, DEC-147): there is no exposed-layer or shell-only target; the tapped block is removed whole, active Ice included.
- Hammer presentation: use a pooled **3D mallet** over the chosen piece/obstacle with a short strike and removal feedback (reference 16); a flat HUD icon is not the cast model. Its motion and return follow session-owned PrimeTween/Suspend/Teardown. The common composer renders surviving content. Use the imported Blender-authored `Assets/ColorBlockJam/Prefabs/Boosters/Effects/HammerEffect.prefab`; its mesh and rigid PrimeTween motion are ready for session/pool integration. Reference-project code/assets remain behavior examples only.
- HUD: four `btn_booster_normal` buttons in the order **Time Freeze, Rocket, Hammer, Vacuum**, each with a count badge at bottom-right. Zero count uses a plus badge and opens the purchase popup (reference 01); selected uses a distinct USS class and the targeting banner. Reference ad badges are omitted.
- Time Freeze presentation: activation holds the main level clock at its current value and opens a **separate visible countdown starting at 10 seconds**, with screen-edge Shader Graph frost and the iced timer style (reference 04). The second counter runs 10 to 0 only while Playing; at 0 its indicator/frost end and the main clock resumes from the held value. For an expiry inside a tick only leftover delta advances the main clock. Use prebuilt integer strings and update each display only when its shown value changes. Pause/saving/background suspends both clocks; Resume preserves both remaining values. Teardown clears the secondary timer. A second activation while active is refused without spending. No +10 is added to the main time; pre-level Extra Time remains a separate booster.
- Vacuum presentation (references 13 and 18): mark the selected block's outer silhouette with a white outline, then pull matching block/piece visuals toward the nozzle while shrinking them. Use `Assets/ColorBlockJam/Prefabs/Boosters/Effects/VacuumEffect.prefab` near the lower board edge; `ic_vacuum.png` is the HUD icon only. The approved cast has a lower canister, upright deforming hose, rigid upper grip/nozzle and fixed upper intake. Its 16 Blender-baked frames (superseded (booster-gameplay-flow, DEC-147): the imported cast bakes 32 frames, see the motion report) give a camera-facing flat 2D appearance while board simulation stays 3D under the perspective board camera; accepted entry/exit stays tween-driven. No outline seams around modular tiles. Domain removals commit together before the pull. Pooled PrimeTween presentation follows the session lifecycle; a winning cast completes within Won Finish rather than being cut immediately. Timeout/navigation/background forces cleanup (DEC-93). Callbacks never own occupancy, spending or reward logic. See the [motion report](art/boosters/motion/README.md) for imported presentation assets and verification limits.
- Pre-level: exactly two selectors, **Extra Time then Rocket**. Blue plus count means unselected; green plus check means selected (references 09 and 10). Either, both or neither may be selected. Zero stock cannot be selected and can open the same purchase presentation over Home. Closing/back clears selection without spending.
- Opening: while Home, prepare the board/pools, sample Rocket uniformly from a stable list of eligible cells and durably commit applicable selected charges together. Failed preparation/commit starts no attempt and spends nothing; no eligible Rocket target skips only Rocket. On the first Playing command phase apply Extra Time then Rocket once from the committed intents without another debit/reroll. The session-owned intro shows selected applicable effects and holds countdown/ordinary input until it finishes (references 11 and 12). Suspend preserves progress; teardown cancels it. With no selection there is no intro or stock transaction.
- Purchase: at zero stock show the selected booster's title, icon, effect description, configured bundle/coin price and Buy button. Buy atomically debits coins and grants persistent stock; default offer is 1 charge for 10 coins. Disable duplicate submissions while saving; insufficient funds or save failure changes neither value and shows feedback. Success refreshes counts/balance but does not auto-cast, auto-select or resume gameplay. Gameplay enters Paused before the popup and resumes on close/Back; if backgrounded, return to the ordinary pause popup. A Home purchase view remains on Home. Buy is unavailable for an unimplemented effect and cannot sell an unusable charge (DEC-85).
- Level data: booster stock is player JSON save data, never mutable `LevelDatabase` state. Per-level availability, if selected later, is a level rule in the effect catalog.
- Editor support: the booster settings panel (11.9).

| AC | Criterion |
| --- | --- |
| AC-BST-0.1 | Booster application commands in Paused/Won/Failed are rejected: board, timer and stock are unchanged. A purchase is a separate save-service transaction allowed only through the Home/Paused purchase overlay; it never applies the booster. |
| AC-BST-0.2 | Pause, backgrounding, or teardown while targeting cancels targeting and spends nothing. After Resume no booster is selected. |
| AC-BST-0.3 | One valid booster intent durably commits exactly one charge before applying once, never a second debit inside the rule. Selection/deselection/invalid or pre-commit cancelled uses are free. The pre-level and in-game Rocket keep separate stock (DEC-167). Restart/Next Level clear intents/choices but neither reset nor refund committed stock. Use leaves coins unchanged (AC-6.11 to AC-6.13). |
| AC-BST-0.4 | A booster that leaves no required block gives `Won` exactly once, and the win popup opens after teardown. A booster command and timer zero in one tick give `Won`. |
| AC-BST-0.5 | While targeting, a drag does not move a block. Empty space, the same button, or Android Back cancels. |
| AC-BST-0.6 | A button with no charge shows a plus/empty state and opens the matching purchase popup; it cannot enter targeting, cast, or become a selected pre-level choice. |
| AC-BST-0.7 | After the win popup, fail popup, Home, or Restart, `Tween.GetTweensCount(target)` is 0 for every booster target and every effect view is back in its pool. |
| AC-BST-0.8 | Playing ticks during targeting and a freeze allocate 0 B, and no `Instantiate` happens during any booster use (device Profiler). |
| AC-BST-0.9 | The purchase popup matches reference 01, blocks underlying input, suspends gameplay and costs no charge to dismiss. A successful Buy spends the displayed coins and grants the displayed stock once. Failures change neither value; success does not auto-cast/resume. Closing cannot resume an app-background pause. |
| AC-BST-0.10 | Hammer, Rocket and Vacuum show a reusable top targeting banner with the correct icon/name/instruction and close action. It clears on cancellation, application, pause and teardown. Time Freeze does not enter targeting. |
| AC-BST-0.11 | Only configured contexts are offered: exactly four HUD choices and two pre-level choices in the reference order. Both Rocket placements resolve the same definition and rule. |
| AC-BST-1.1 | One accepted tap activates after durable stock commit without target selection. Keep the main clock visible and unchanged; open a second visible HUD countdown at 10. It runs to 0 in 10 Playing seconds, then closes and the main clock resumes from the held value without a numeric +10 increment. |
| AC-BST-1.2 | With main time 3 s, activation shows main 0:03 plus separate 10; after 4 Playing seconds, main remains 0:03 and the second counter shows 6. After 6 more seconds, the second reaches 0/closes and main starts counting down, failing 3 seconds later without an exit. Fixed-step and mid-tick-expiry tests apply leftover delta once. |
| AC-BST-1.3 | Paused, saving and background time consume neither clock. Pausing at secondary 6 preserves main 0:03/secondary 6; Resume continues from both values without restarting or reapplying the booster. |
| AC-BST-1.4 | One accepted activation spends one charge. A second activation while its frost feedback is active is refused with no spend. |
| AC-BST-1.5 | Blocks move and exit during the freeze. Screen-edge Shader Graph frost, iced main timer and the separate 10-to-0 HUD countdown appear together on activation, remain distinct/readable within the safe area, update without allocations, and clear on expiry or terminal transition. |
| AC-BST-2.1 | One Hammer hit removes exactly one selected exposed block-piece/layer or obstacle. A plain/Arrow I3 is a single piece and is removed whole; it is not split into two cells. Other blocks remain unchanged. Superseded (booster-gameplay-flow, DEC-147): one hit removes the tapped block whole; there is no layer or obstacle target. |
| AC-BST-2.2 | On active Ice with any remaining count, Hammer removes that one obstacle shell, clears its counter and preserves the underlying block footprint/color/axis. The same hit cannot also remove that block. Superseded (booster-gameplay-flow, DEC-147): Hammer on an iced block removes the shell and the block together. |
| AC-BST-2.3 | Layer/obstacle removal does not advance other Ice counters or emit a door exit. Removing only an obstacle does not win while the underlying required block remains; removing the final plain block wins once. Superseded (booster-gameplay-flow, DEC-147): Hammer never removes an obstacle alone; removing the final block, iced or not, wins once. |
| AC-BST-2.4 | Hammer on a block already committed to exit is refused and keeps the charge. |
| AC-BST-2.5 | Hammer produces no geometric split children. Exposed-target selection is deterministic and affects only one layer or obstacle per hit. If a layered mechanic is selected, authored lower content remains intact. Superseded (booster-gameplay-flow, DEC-147): the target is the tapped block, removed whole with no split children. |
| AC-BST-2.6 | The cast shows a 3D mallet striking the chosen piece/obstacle. Pause suspends it; teardown leaves no mallet tween or active pooled cast view. Each applied hit spends one charge regardless of the target's footprint size. |
| AC-BST-2.7 | Restart restores the original authored block and obstacle state and clears cast/outline views; spent stock stays spent. No Instantiate/Destroy or pool growth occurs during the hit. |
| AC-BST-3.1 | Rocket on the foot cell of an L4 leaves an I3 of the same color on the same world cells minus that cell. Its rendered pieces equal a directly composed I3. |
| AC-BST-3.2 | Rocket on a cell whose removal would split the block is refused. The cell shows red while targeting and the charge is kept. Superseded (booster-gameplay-flow, DEC-146): such a cell is valid and shows a crosshair; removing it leaves independent blocks that move, exit and are picked separately. |
| AC-BST-3.3 | Rocket on a 1-cell block is refused with no spend; such cells are excluded from starting Rocket candidates. Any successful Rocket use leaves at least one cell in the same required block and cannot directly win by destroying it. |
| AC-BST-3.4 | A 2-wide block that did not fit a 1-wide door exits through it after Rocket narrows it to 1 on that side. |
| AC-BST-3.5 | Rocket on an iced or arrow block keeps the counter or axis, and the ice overlay and arrow marker are recomposed. |
| AC-BST-3.6 | Restart after a Rocket use rebuilds the authored shape. No `Instantiate` happens during the use, and every piece is back in its pool after teardown. |
| AC-BST-3.7 | A starting Rocket picks one eligible random cell and applies the same effect as an in-game use on that cell. With an injected seed, the chosen target and result repeat. A full 2x2 fixture loses one corner cell and becomes L3 as in references 11 to 12; exactly three original cells remain. No extra charge or second Rocket effect is created. |
| AC-BST-3.8 | Opening Rocket excludes removed/mid-exit targets, split-causing cells and final cells (superseded (booster-gameplay-flow, DEC-146): split-causing cells are valid candidates; only final cells are excluded). If all blocks are 1x1, no Rocket is applied or spent. Pause/Resume and double Play never reroll or reapply a committed use. |
| AC-BST-4.1 | Vacuum on a red block removes every red block in one tick, including iced and arrow blocks, and no other color. |
| AC-BST-4.2 | A red block already committed to exit is not removed twice. Its exit completes. |
| AC-BST-4.3 | Vacuum removals do not change other ice counters. |
| AC-BST-4.4 | If the chosen color was last, latch Won once and stop gameplay immediately. Show the final shrinking suction within the bounded Won Finish, then capture/teardown/open the popup. Reward saving/retries do not repeat the removal, debit or animation. |
| AC-BST-4.5 | Selecting a Vacuum target gives that block a visible white silhouette outline that preserves color/shape readability and has no internal modular-piece seams. It clears on cancel, completion, pool return or teardown. |
| AC-BST-4.6 | Matching block/piece visuals travel into the displayed vacuum nozzle while shrinking; other colors are unaffected. The flat camera-facing cast sits near the lower board edge, with reference 18's lower canister, upright deforming hose and rigid upper nozzle/intake; it preserves the accepted entry/exit and current authored colors without changing board simulation. Use the mapped VacuumEffect cast, not the HUD icon. Verify motion with a manual clip or ordered captures at both portrait aspect ratios. |
| AC-BST-4.7 | Ordinary Pause/Resume preserves suction position/scale. Home, Restart, Failed or forced cleanup stops the pull and returns every view once. Normal Won allows only its bounded Finish before full cleanup. Domain removal and durable charge debit happen once before animation, including a winning cast. |
| AC-BST-5.1 | A selected Extra Time adds exactly `extraTimeSeconds` to the authored countdown once at opening, before normal countdown. With no selection the authored timer is unchanged. |
| AC-BST-5.2 | The opening hold spends no countdown time. With both selected, Extra Time then Rocket apply once; normal drag and HUD booster use become available after the sequence. Pause preserves sequence progress and teardown removes all opening effects. |
| AC-BST-5.3 | Cancelling the pre-level popup spends nothing. Restart restores authored board/time and clears selections; direct Next Level and editor test play do not reuse a previous pre-level selection. Runtime bonus values can exceed the editor's 300 s authoring cap and still display correctly without per-tick allocation. |

Verification: V-BST-1 to V-BST-9 (16.5), V-M25 to V-M27 (16.2).

### 7.11 Ice Blocker (REQ-O4, BLK-03)
- Rollic: "An iced block is frozen until a specified number of other blocks are removed." Slice rule: RULE-33. Counter 1 to 20. Only door exits of other blocks count (DEC-79, DEC-81). A tap or drag on a frozen block plays a short shake.
- Hooks: drag gate, exit gate, board event listener, counter (section 8.3). Solver hook with PRE-2.
- Planned level JSON record: `{"type": "ice", "params": [{"key": "exitsToThaw", "value": "2"}]}` on a block. The parsed `LevelData` holds its typed effect after validation.
- Editor: Add effect > Ice in the Effects panel, a stepper 1 to 20, a snowflake badge with the number on the grid (11.7).
- View (`PROJECT.md`): ice is a shader, not new art. `icetexture.jpg` feeds one URP Shader Graph material that the frozen block shows as a second material on its own composed pieces; the shader expands each surface slightly outward (DEC-184, replacing the earlier pooled overlay copies). Tint, opacity, and a crack amount are written into each piece's `MaterialPropertyBlock`; the crack follows the remaining counter. The counter is a screen-space UI Toolkit label placed from the block's projected position, updated only on a counter or layout change.
- Demo: once ice merges, one shipped level gains an iced block, is re-validated, replayed, and noted in `docs/LEVELS.md`.

| AC | Criterion |
| --- | --- |
| AC-BLK-03.1 | A frozen block does not move on drag, blocks other blocks like a wall, and shakes. |
| AC-BLK-03.2 | Each exit of another block lowers the counter by exactly 1, and the label changes in the same frame. |
| AC-BLK-03.3 | At 0 the overlay plays a thaw tween and returns to its pool, and the block can be dragged on the next input. |
| AC-BLK-03.4 | A designer adds Ice, sets the counter, sees the badge, saves, reopens, and sees the same value in `LevelDatabase`. The canonical level JSON holds the record above. |
| AC-BLK-03.5 | Validation reports Proven unsolvable when a counter exceeds the other required blocks (PRE-2). |
| AC-BLK-03.6 | Restart restores the authored counter and overlay. Teardown leaves no ice tween and no active overlay or label. |
| AC-BLK-03.7 | One ice material serves every frozen block. No material is instantiated. The label allocates nothing on change. |

Verification: V-ICE-1 to V-ICE-3 (16.5).

### 7.12 Arrow Block (REQ-O5, BLK-01)
- Rollic: "Arrow blocks move only in their indicated direction." Slice rule: RULE-34. The axis is Horizontal or Vertical and moves both ways, because the supplied arrows are double-headed (DEC-82). A diagonal pointer is projected onto the axis.
- Hooks: axis constraint, exit gate (section 8.3). Solver hook narrows moves and exit sides; PRE-1 checks the axis sides.
- Planned level JSON record: `{"type": "arrow", "params": [{"key": "axis", "value": "Horizontal"}]}` on a block. The parsed `LevelData` holds its typed effect after validation.
- Editor: Add effect > Arrow, a two-button axis selector, a double-headed arrow badge on the grid.
- View (`PROJECT.md`): the arrow marker is composed along the axis from `Arrows.fbx` pieces by the composer's neighbor logic, one shared material with a property-block tint, pooled, parented to the block view. OQ-10 settles which mesh maps to which run.
- Demo: once arrow merges, one shipped level gains an arrow block, is re-validated, replayed, and noted in `docs/LEVELS.md`.

| AC | Criterion |
| --- | --- |
| AC-BLK-01.1 | A Horizontal arrow block changes only its column, a Vertical one only its row. |
| AC-BLK-01.2 | It exits only through a matching door on a side its axis points to. A matching door on a perpendicular side blocks it. |
| AC-BLK-01.3 | A designer sets the axis in at most two clicks, sees the badge, saves, reopens, and sees the same axis in `LevelDatabase`. The canonical level JSON holds the record above. |
| AC-BLK-01.4 | The marker follows the axis, follows drags and exit tweens, and returns to its pool on exit and teardown. |
| AC-BLK-01.5 | Validation reports an arrow block with no reachable door of its color on its axis sides (PRE-1). |
| AC-BLK-01.6 | EditMode tests cover axis-limited movement and axis-only exit for I3, L3, and 1x1 footprints. |

Verification: V-ARW-1 to V-ARW-3 (16.5).

## 8. Mechanics Catalog Summary
### 8.1 How to Use the Catalog
The catalog covers 18 block effects, 11 gate effects, 9 board objects and booster interactions. These are specifications the user can select as implementation tasks in any order. The appendix gives each mechanic its rule, dependencies, authored level data and canonical JSON shape, editor controls, visuals, solver support, acceptance criteria and edge cases. The build registers only implemented and verified types. S, M, L and XL are relative complexity estimates, not priorities or delivery waves.

### 8.2 Summary Tables
**Block effects**

| ID | Name | One-line rule | Cx |
| --- | --- | --- | --- |
| BLK-01 | Arrow | Moves only along its axis (7.12). | S |
| BLK-02 | Layer | A matching door peels the outer color; the block leaves after the last layer. | M |
| BLK-03 | Ice | Frozen until N other blocks exit (7.11). | S |
| BLK-04 | Star | Exits only through a star door of its color. | M |
| BLK-05 | Combined | Two blocks move as one until one member exits. | L |
| BLK-06 | Chained Blocks | Cannot move until N key blocks exit. | M |
| BLK-07 | Bomb | Fails the level if its countdown ends on the board. | M |
| BLK-08 | Ropes | Stuck until same-color scissors blocks exit. | M |
| BLK-09 | Moving Blocks | Movable obstacle that never exits and is not required. | S |
| BLK-10 | Combined Locked | A linked pair that separates after N other exits. | M |
| BLK-11 | Hidden Blocks | Concealed until a named block exits, then must also exit. | M |
| BLK-12 | Dynamite | Fails the level if not out within N moves. | M |
| BLK-13 | Curtain | Can exit only while open; every exit toggles it. | S |
| BLK-14 | Time Capsule | Its exit adds bonus seconds. | S |
| BLK-15 | Color-Switching | Two colors; other exits clear or swap them. | M |
| BLK-16 | Color Swapping | Its exit swaps two colors on the remaining blocks. | M |
| BLK-17 | Turn Based Arrow | Axis-limited until N exits, then free. | S |
| BLK-18 | Magnet Block | A same-color block moved next to it fuses into one block. | L |

**Gate effects** (plain color door is the starting rule)

| ID | Name | One-line rule | Cx |
| --- | --- | --- | --- |
| GTE-01 | Door | Opens and closes after each clear. | S |
| GTE-02 | Iced Door | Sealed until N blocks clear. | S |
| GTE-03 | Moving Door Lock | A lock moves to the next door after each clear. | M |
| GTE-04 | Colorful Door | Accepts one color, then shows the next. | S |
| GTE-05 | Locked Door | Opens after N key blocks exit. | M |
| GTE-06 | Size-Changing Door | Its span changes after each clear. | M |
| GTE-07 | Color-Switcher | Alternates between two colors on every exit. | S |
| GTE-08 | Jumping Single Door | A blocker moves along the span after each exit. | M |
| GTE-09 | Colorful Jumping Door | Jumps to the next door and recolors it after each clear. | M |
| GTE-10 | Button Doors | Open while a block of the button color rests on its button. | M |
| GTE-11 | Laser Door | Open while its beam line is free of blocks. | M |

**Board objects**

| ID | Name | One-line rule | Cx |
| --- | --- | --- | --- |
| OBJ-01 | Colorful Path | Only blocks of its color may enter. | S |
| OBJ-02 | Crate | A wall until N clears, then reveals its contents. | M |
| OBJ-03 | Moveable Crate | A draggable obstacle that opens after N exits. | L |
| OBJ-04 | Flower Crate | Opens when its linked leaf blocks are cleared. | L |
| OBJ-05 | Barrier | Opens and closes after each clear. | S |
| OBJ-06 | Colorful Crate | Clears when every block of its color has left. | S |
| OBJ-07 | Colorful Moving Path | Moves when a block slides onto it. | XL |
| OBJ-08 | Ivy | Loses one tile after each exit. | S |
| OBJ-09 | Elevator | Raises the next stacked block when its cells are free. | L |

**Booster and mechanic interactions:** Layered blocks, gate and object interactions and per-level booster availability use the same booster system. Before implementing a selected interaction, check it against Hammer's one-exposed-piece-or-obstacle rule. Rocket keeps its one-cell rule in both entry contexts. A mechanic requiring a group, merge or spawner hook gets a design check before code; the user chooses when it is implemented.

### 8.3 Extensibility Requirements
The core is closed: board, move resolver, exit resolver, session, timer, tick owner, the canonical JSON-to-`LevelData` loader, the `LevelData`-to-model loader, composers, piece pools, and editor window shell do not change when a mechanic is added. A mechanic is added by registration: one effect definition (type ID, target, parameter schema, validation, rule factory, solver support), one rule class, one overlay view entry, one editor badge entry, and its tests (DEC-72).

| Hook | Question the core asks | Used by |
| --- | --- | --- |
| XP-1 Drag gate | May this block start a drag? | Ice; chained, locked, crates |
| XP-2 Axis constraint | Which axes may this block move on? | Arrow; turn-based arrow |
| XP-3 Cell gate | May this block enter this cell? | Paths, barrier, ivy, crates |
| XP-4 Exit gate | May it exit through this door now, and with what outcome? | Arrow; star, curtain, layer, most gate effects |
| XP-5 Board event listener | None; receives `BlockExited`, `BlockRemoved`, `MoveCommitted`, `FootprintChanged` | Ice; every counter and toggle |
| XP-6 Timer modifier | Is the countdown suppressed this tick; bonus seconds? | Time Freeze, pre-level Extra Time and the opening hold; time capsule |
| XP-7 Lose condition | None; requests `Failed` through the latch | Bomb, dynamite |
| XP-8 Win participation | Does this block count as required? | Moving blocks, hidden blocks |
| XP-9 Block spawner | None; asks the board to place a pre-pooled block | Hidden, crates, elevator |
| XP-10 Counter | Current value; decrement on the declared event or clear when the obstacle is removed | Ice (door exits decrement; Hammer removes the Ice obstacle); catalog counters need their own rule |

- Hook arrays are built once at attempt build and iterated with plain loops: no LINQ, closures, boxing, or per-call delegates. Order is registry order, then entity order. Gates combine by AND, axis constraints by intersection.
- The registry is split by assembly: Domain holds definitions and rules, Presentation maps type ID to an overlay pool, Editor maps type ID to a badge. One catalog function builds an immutable registry for each composition root. No static registry.

| AC | Criterion |
| --- | --- |
| AC-EXT-1 | Adding ice and arrow changes no core file listed above. The implementation review lists the changed files and confirms it. |
| AC-EXT-2 | One catalog function builds the registry for the runtime root, the editor, the validator, and the tests. No static registry. A duplicate type ID throws when the registry is built. |
| AC-EXT-3 | A completeness test fails if a registered effect has no overlay entry, no badge entry, or neither a solver hook nor a declared "unsupported" status. |
| AC-EXT-4 | The editor opens a level with an unknown effect, shows a red badge and entry, keeps the raw effect unchanged, and blocks Save until it is removed. |
| AC-EXT-5 | The Effects panel offers only applicable types, generates fields from the parameter schema with clamping, and makes each add, remove, and change one Undo step. Badges update in the same frame and have tooltips. |
| AC-EXT-6 | With ice and arrow on the board, the `Playing` tick allocates 0 B, including hook dispatch and overlay updates. |
| AC-EXT-7 | Restart and Next Level rebuild attempt effects from level data: authored counters return, freeze ends and starting selections clear. Persistent booster stock and purchases are preserved without refund or reseeding. |
| AC-EXT-8 | The build registers only implemented and verified effect definitions. A level with an unregistered type fails clearly as unknown. |
| AC-EXT-9 | The README has "How to add an effect", the mechanics catalog link and the booster rule decisions. |
| AC-EXT-10 | The core emits the four board events from the board core with no listeners. The base cost of the design is limited to empty hook arrays and the unknown-type check. |

Verification: V-EXT-1 to V-EXT-5 (16.6).

## 9. Non-Functional Requirements
### 9.1 Product NFRs
| ID | Requirement | Measure | Detailed by |
| --- | --- | --- | --- |
| NFR-1 | Zero GC allocation per frame in `Playing`. | 0 B GC Alloc per frame in steady state. | NFR-P2 |
| NFR-2 | Stable frame rate on the team's Android phone. | 60 fps target (DEC-28). | NFR-P1, NFR-P4 to NFR-P6 |
| NFR-3 | No `Instantiate` or `Destroy` during an attempt. | No such Profiler sample in `Playing`. | NFR-P3, NFR-P10 |
| NFR-4 | Layout correct at 1080 x 1920 and 1080 x 2400 inside `Screen.safeArea`. | Game view pre-check and phone check. | AC-1.4a, AC-3.11 |
| NFR-5 | Nothing runs unattended outside `Playing`. | `PROJECT.md` verification. | NFR-L1 to NFR-L10 |
| NFR-6 | Code quality: English names, `m_`/`s_`/`k_` style, no dead code, no warnings, owner and stop point named. | Code-quality gate (16.4). | NFR-X1 to NFR-X7 |
| NFR-7 | Domain rules run in EditMode tests without a scene. Time and storage enter through interfaces. | EditMode suite green. | NFR-X5 |
| NFR-8 | The Android IL2CPP ARM64 build runs the full flow with DI working. | Smoke build on the phone. | NFR-P9 |
| NFR-9 | A first-time designer makes, saves, and plays a level in 10 minutes or less (EU-1). | Hallway test. | 11.10 |
| NFR-10 | Home to a playable board in 1 s or less on the phone. | Stopwatch and Profiler marker. | NFR-P7 |
| NFR-11 | Assemblies: Domain (no engine references), Presentation, Editor, Validation (editor-only). One-way references. | Assembly review and a test. | NFR-X5 |

### 9.2 Performance (NFR-P)
| ID | Requirement | Verification and tools |
| --- | --- | --- |
| NFR-P1 | `Application.targetFrameRate` is 60, set at boot. On level 5 over 60 s of play: median and 95th-percentile frame time 16.7 ms or less, no drag frame over 50 ms, median main-thread CPU 8 ms or less. After a 10-minute soak on levels 1 to 5 the median stays at 58 fps or more. The README names the phone. | Profiler attached over USB to an IL2CPP development build; capture saved under `docs/perf/`. |
| NFR-P2 | The steady-state `Playing` tick allocates 0 B: drag, move, timer, view sync, exit tweens, HUD. Tween callbacks use target overloads. No LINQ, string building, boxing, closures, or async in the tick. Load-time allocations are listed in the README. | EditMode `Is.Not.AllocatingGCMemory` on tick, move, timer, composer; PlayMode `ProfilerRecorder` "GC Allocated In Frame"; 10 s phone capture. |
| NFR-P3 | Every board object comes from a pool warmed at level load (Onity pools do not prewarm; project code does Get N then Release N). Pool maximum is at least the warm count. Teardown returns everything. | Onity pool diagnostics (count unchanged between load and teardown), Pool Monitor, Profiler. |
| NFR-P4 | Level 5 in `Playing` with the HUD: 20 SetPass calls or fewer, 150 batches or fewer, 200k triangles or fewer. Measured values go in the README. | Frame Debugger (Editor and development player), Profiler Rendering module. |
| NFR-P5 | Start with DEC-96: (A) one shared URP 14 material with explicitly instanced MPB color/clip fields, one cached property owner per renderer. Compare (B) prebuilt per-color materials without MPB on ordinary block renderers, using SRP Batcher. Compare the steady board and equivalent exit/effect sequences; include B's effect-time MPB exceptions and cost. Use identical level/camera/quality/passes; lower device frame cost within NFR-P4 budgets decides the single retained path. No material instantiation or per-renderer polling at runtime. | Frame Debugger instancing/break reasons, SetPass/batches, CPU/GPU time, GC/material count; code review of MPB ownership/reset and shader passes. Record numbers and device/API in the decision log. |
| NFR-P6 | UI sprites packed into two Sprite Atlases (Home and Settings; HUD and popups), dynamic atlas on, one runtime font. Home 6 UI draws or fewer, HUD 5 or fewer. | Frame Debugger, Sprite Atlas preview, UI Toolkit Debugger. |
| NFR-P7 | Play tap to first `Playing` frame 1.0 s or less; cold start to interactive Home 5 s or less; Restart to `Playing` 0.5 s or less. | Development-only stopwatch log and a "LevelLoad" Profiler marker; median of three runs. |
| NFR-P8 | Loops the project does not own (PrimeTween, Onity pumps, Input System, UI Toolkit, EventSystem, Entities if on) cost 1.0 ms or less per frame in `Playing`, and each is in the README loop table. | Phone Profiler, PlayerLoop hierarchy over 300 frames. |
| NFR-P9 | Release APK: IL2CPP, ARM64, portrait, Input System only, managed stripping Low or higher, no development flags, no MCP for Unity or unused packages, size 100 MB or less. Keep the required Newtonsoft runtime dependency and level/player-save DTO/converter preservation. | Build report, APK assembly inspection, IL2CPP level-load and player-save JSON smoke tests, `adb install`. |
| NFR-P10 | After 10 Restarts on level 5 and 5 Home round trips: pool totals unchanged, no active board object after teardown, 0 gameplay tweens, managed heap growth 1 MB or less between cycle 2 and 10. | PlayMode soak test, pool diagnostics, Profiler Memory module. |

### 9.3 Lifecycle (NFR-L)
| ID | Requirement | Verification and tools |
| --- | --- | --- |
| NFR-L1 | One session object per attempt holds the state, one `CompositeDisposable`, and one `CancellationTokenSource`. Everything gameplay starts registers with it. | EditMode test without a scene. |
| NFR-L2 | Suspend and Teardown run fixed steps; a normal Won performs only the bounded visual Finish/capture before full Teardown. Result popups open after Teardown; pause and in-game purchase popups open after Suspend. Disk saving cannot delay visual cleanup. | Fake-port order tests, 0.8 s deadline and forced-cleanup cases. |
| NFR-L3 | Exactly one project class implements `IOnityTickable` (the session tick owner); gameplay systems use a project tick interface and are ticked only in `Playing`. No gameplay `Update`. | EditMode reflection test; Profiler marker absent outside `Playing`. |
| NFR-L4 | Terminal transitions happen once; after Won/Failed, domain ticks, gameplay commands and main/freeze clocks change nothing. Only the bounded Won Finish may advance render-only presentation before teardown; no gameplay or effect tween remains when a result popup opens. | PROJECT.md lifecycle tests, same-frame case, final-cast and timeout tests. |
| NFR-L5 | Every `Subscribe` result is in the session disposable or bound with `AddTo(this)`. | Observable Tracker (tracking on in development builds), Onity analyzers. |
| NFR-L6 | No `async void`; every task gets a token; nothing is fire-and-forget. | Reflection test; Task Tracker after popup, Home, Restart. |
| NFR-L7 | Gameplay tweens target gameplay objects; pooled objects stop their tweens on release; `Time.timeScale` is never the pause. | `Tween.GetTweensCount(target)` = 0 after teardown; code search. |
| NFR-L8 | The README lists every runtime loop, startup hook, and hidden object with its owner, states, and stop point. | Review plus a Profiler capture with the win popup open. |
| NFR-L9 | Dev tooling and unused packages do not run in the player (DEC-62, DEC-63). | APK content check. |
| NFR-L10 | Restart rebuilds from the selected `LevelData` record in `LevelDatabase`. Pools are owned by a disposable scene-scoped object, so none survive the scene. | PlayMode test on pool diagnostics after scene unload. |

### 9.4 Explainability (NFR-X)
| ID | Requirement | Verification and tools |
| --- | --- | --- |
| NFR-X1 | Every project type has a one-sentence summary "This does X", with "It is here because Y" and a decision ID when the reason is not obvious. | Source scan, review. |
| NFR-X2 | Every non-obvious choice carries a decision ID that exists in this PRD or `PROJECT.md`. | Script check of all DEC references. |
| NFR-X3 | Naming follows 5.1 and the global rules: simple verbs, no Manager or Helper types. | `.editorconfig` naming rules, review. |
| NFR-X4 | 0 compiler warnings in project code, no unused code or `using`, no commented-out code, no unguarded `Debug.Log`. | Console after a clean compile, IDE inspection, grep. |
| NFR-X5 | Assembly references match NFR-11; Domain compiles with no engine references. | Assembly review, EditMode reflection test. |
| NFR-X6 | Every checkin passes the review checklist (appendix section D) and its comment states what, why, and how verified. | Checklist in the checkin comment. |
| NFR-X7 | Every `Subscribe`, async method, tween, pool, loop, and tick names "Owner" and "Stops" in a one-line comment that matches the loop table or teardown list. | grep, review. |

### 9.5 Performance Rules Map
| `PROJECT.md` Performance Rule | NFRs |
| --- | --- |
| 1. One block material, color by property block | NFR-P4, NFR-P5, DEC-58, DEC-96 |
| 2. Sprite Atlases for UI | NFR-P6 |
| 3. Onity pools own board objects | NFR-P3, NFR-P10, NFR-L10, DEC-60 |
| 4. Onity owns the tick | NFR-L3, DEC-59 |
| 5. Zero per-frame allocation | NFR-P2, NFR-P1 |
| 6. Nothing third-party runs unattended | NFR-P8, NFR-P9, NFR-L8, NFR-L9, DEC-61 to DEC-63 |
| 7. Explainable code | NFR-X1, NFR-X2, NFR-X7 |

## 10. Architecture Requirements
`PROJECT.md` holds the full rules. These are the product-level requirements.

- AR-1: Data, logic, and view are separate. Board, blocks, doors, moves, timer, session, composers, effects, and the level model are plain C# in the Domain assembly. Views are thin.
- AR-2: One session state (`Home`, `Playing`, `Paused`, `Won`, `Failed`) decides whether gameplay runs. No system keeps its own flag.
- AR-3: One session object per attempt owns every subscription, task, tween target and pooled object it starts; it runs Suspend, the optional bounded Won Finish and Teardown. Save transactions have their own awaited save-service owner and cannot reactivate a disposed attempt.
- AR-4: One tick owner implements `IOnityTickable` and forwards ticks, only in `Playing`, in the order commands, timer, view sync. The Onity container ticks every bound tickable in every state, so gameplay systems never implement it themselves (DEC-59).
- AR-5: The editor saves ordered levels directly to the level database's packs and manifest (DEC-196). The game loads those same files through the project scope installer's Addressables reference, which stores no duplicate records (7.8, DEC-194).
- AR-6: Persistent data goes only through the save service and its atomic snapshot adapter (7.6). Booster commands use a typed, non-allocating intent/inventory port; rules/views never read or write storage. The coordinator awaits commit outside Playing before applying a captured intent once. Publish only committed snapshots. Editor test play binds an isolated in-memory adapter, including inventory.
- AR-7: Composition uses Onity contexts and bindings. Services that start work are disposable and scene-scoped. Board pools are owned by one disposable scene-scoped object, not by instance bindings the container never disposes (DEC-60).
- AR-8: Extension without format churn: a color is a palette entry, a shape is a library entry, a mechanic is a registration (8.3).
- AR-9: Pause is state-driven. `Time.timeScale` is never the pause.
- AR-10: The editor, the validator, and the runtime use the same Domain rules and the same effect registry, so they cannot disagree on validity.
- AR-11: Board visuals come from the composers and the piece catalog. A prefab edit never needs a composer or rule change (AC-3.15).
- AR-12: The Validation assembly references Domain only, is editor-only, and never ships.

## 11. Level Editor UX Requirements
### 11.1 Entry and Layout
- Entry points: `Tools > Color Block Jam > Level Editor`, a double-click on the `LevelDatabase` in `Assets/ColorBlockJam/Data/Levels`, or "Open in Level Editor" on that asset (AC-7.12).
- One dockable UI Toolkit window, at least 960 x 640, split into a left tools column and a right pane (DEC-119, [Level Editor Design](Level-Editor-Design.md)):
  - Left (resizable, 280 px by default, the splitter position persists): tool groups for Colors (named swatches), Blocks (shape swatches as mini grids, Selected Block, Piece Type, Rotate, Flip), Doors, Board (read-only Level ID, width, height, timer with an `m:ss` readout), Mechanics (the Effects panel, 11.7), Edit (Select, Eraser, Undo, Redo, nudge), and a collapsed Raw data foldout with the bound Doors and Blocks lists.
  - Right, top: the level selection bar. Row 1: the Level Database dropdown with the green Add icon (new database; a popup asks for its name) and the red Delete icon (delete database), the read-only JSON Source reference, and icon buttons for Reload JSON and Restore Previous Save. Row 2: the Target Level popup with previous/next arrows, Add (new level) and Delete (delete level), then the Level Name field. Add and Delete are round icons with no button box (`Editor/Controls/ActionIcons`), shared with the Piece Editor.
  - Right, below the selection bar: the top-down grid with cells, walls, colored doors, colored blocks with outlines, and effect badges, drawn to scale, then the validation panel (11.8).
  - Bottom (full width): Save Level at the left, then the one-line status and tool hint. The window tab shows Unity's unsaved-changes marker, and closing a dirty window asks Save, Discard or Cancel.
  - Play (11.5) is the Test Level button beside Save Level at the bottom left (DEC-127, 2026-09-27).
  - Not yet in the window (their own tasks): the ordered level list with drag to reorder, Duplicate and per-level validation badges (11.4), and check progress (11.8).

### 11.2 Designer Workflow (10 minutes or less on the first try, NFR-9)
1. Select the `LevelDatabase` in **Database**, then click **Create**. A working `LevelData` record with the next free stable ID, a 6 x 8 board and 60 s opens; **Save** appends it to the database.
2. Set **Width** and **Height** with steppers. The grid redraws at once.
3. Set **Timer** in seconds; `m:ss` shows next to it.
4. Pick **Block**, a shape, and a color. Press `R` to rotate. The ghost is green when valid, red when not. Click to place.
5. Pick **Door** and a color. Click a boundary edge to add a 1-segment door; click or drag along the side to extend it.
6. Read the **Validation** list: red errors block Save, yellow warnings do not. Click an entry to highlight it.
7. Click **Save**, then **Play** to test in Play Mode.
8. Stop Play Mode. The editor still shows the level. Adjust and repeat.

### 11.3 Interactions
- Select: click a block to select, drag to move (snapped, overlap-checked), `R` to rotate in place if it fits, a color swatch to recolor, Delete to remove.
- Right-click removes a block or door; Eraser does the same with a left click.
- Every edit supports Undo and Redo (`Ctrl+Z`, `Ctrl+Y`) through Unity Undo.
- Colors show as named swatches (Red, Blue, Green, Yellow, Purple, Orange), never as numbers.
- Every control has a tooltip with its shortcut. With nothing open: "No level selected: click New".

### 11.4 Save, Load, New, Duplicate, Resize
- Selecting a Target Level clones its parsed record into an editor working buffer. Edits and Undo/Redo affect that buffer; Save validates it and writes the selected record to its level pack with an editor stamp when that schema feature is implemented. Switching levels or closing with unsaved changes asks Save, Discard, or Cancel.
- Duplicate opens a working copy with a new stable ID; Save inserts it immediately after the source. Delete asks first and removes the record from canonical JSON. Create and Delete are the green Add and red Delete icons beside Target Level in the level selection bar (11.1). Database order is play order; editor draft edits support Undo/Redo.
- Reload JSON validates and reads the same canonical file. If it changed outside the editor, Save refuses to overwrite the newer content and asks the designer to reload. Invalid changes make no partial file update. These actions do not alter player saves.
- A shrink that removes content shows the hatched preview and the confirm dialog (AC-7.2).
- The designer never renames or moves files by hand.

### 11.5 Play from the Editor
- Play saves first, stores the database asset GUID and selected stable level ID in editor session storage, and enters Play Mode straight into that level's `Playing` state. The bootstrap reads the override only in the Editor and clears it on exit (RISK-12).
- As built (DEC-127): the button is **Test Level**. It makes the Game scene the Play Mode start scene, so the LevelEditor scene never runs, and Unity reopens the LevelEditor scene on exit.
- Test play binds an in-memory save store (DEC-30). Leaving Play Mode returns to the same level.

### 11.6 Error Prevention
- Steppers are clamped. Timer entry is clamped with a short notice. Invalid placements are refused at hover time.
- Structural checks block Save: at least one block, no overlap, all cells in bounds, doors only on boundary edges, no corner wrapping, no overlapping doors, known colors and effects, effect parameters in range.
- Messages use designer terms and a place: "Blue block at (3,2) overlaps Red block". Never type names or IDs.

### 11.7 Effects Panel 
- Built from the effect registry, not a hand-written form per effect. With nothing selected: "Select a block or door to add an effect."
- Add effect lists only definitions that match the selection, are registered in this build, and are not already on it or incompatible with it.
- Each effect is a card whose fields come from the parameter schema: integer steppers with clamping, toggles, named color swatches, segmented buttons for an axis or enum, "Pick on grid" for references. Each card has Remove.
- Every add, remove, and change is one Undo step, marks the level dirty, and re-runs validation for that block.
- Grid badges: one per effect (ice: snowflake and number; arrow: double-headed arrow along the axis) with a hover tooltip. An unknown effect shows a red badge and a read-only red card with Remove only (AC-EXT-4, AC-EXT-5).

### 11.8 Validation Panel and Dialog 
- The status chip, Check, Show solution, the message list, the progress bar with Cancel check, the warning dialog, and the replay controls, as defined in 7.9.
- A "Validation" foldout holds the per-user search budget override.
- Tooltip: "Boards up to 6 x 8 with up to 8 blocks always get a definite answer. Larger or denser levels may get 'could not confirm'."

### 11.9 Booster Settings 
- A "Boosters" foldout edits the `BoosterDefinition` ScriptableObjects under `Assets/ColorBlockJam/Data/Boosters`: initial stock seeded once per available definition (0 to 9, default 1), positive integer purchase cost and quantity (adopted case defaults 10 coins and 1 charge), and `extraTimeSeconds` (1 to 60 s, default 10). Time Freeze duration is the confirmed 10 s (DEC-76). Rocket has one definition, inventory entry and offer shared across both contexts. Context availability follows the fixed four/two mapping, not a separate skill asset.
- Changing initial-stock tuning never overwrites existing saved stock. Invalid offers and values exceeding supported balance/stock bounds cannot be saved or purchased. The price, quantity and Extra Time default are design defaults for playtesting, not numbers specified by the user or inferred from screenshots.
- These are game-wide tuning values, not level data. Changes are Undo steps. The designer never needs the Inspector for them.

### 11.10 Success Criteria
- EU-1: A designer who has not seen the editor follows only the README and makes, saves, and plays a level with at least one door and three blocks in 10 minutes or less, with no help, code, or file edits.
- EU-2: That designer opens nothing outside the Level Editor window and the Project window.
- EU-3: Every invalid action is impossible or explained in the status bar.

### 11.11 Level Design Targets (levels 1 to 5)
| Level | Board | Blocks | Colors | Doors | Timer | Teaches |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 4 x 5 | 2 | 2 | 2 | 60 s | Drag a block to the matching door |
| 2 | 5 x 6 | 4 | 3 | 3 | 60 s | Door width must fit the block |
| 3 | 6 x 7 | 6 | 3 | 4 | 75 s | Clear one block to free another |
| 4 | 6 x 8 | 8 | 4 | 5 | 90 s | Plan the exit order |
| 5 | 7 x 9 | 10 | 5 | 6 | 90 s | Tight timer and mixed shapes |

Every level is authored in the editor, solved by hand through Play, and has its solution and solve time in `docs/LEVELS.md`.

## 12. Decisions
`PROJECT.md`'s decision log is the single record that the README draws from (REQ-N3, R-3). This table keeps the PRD reasoning. The Log column says where each row stands: "PROJECT.md" means recorded there; "Pending" means the daily step (14.2) copies it there that day; "Needs approval" means a high-risk change that waits for the user's explicit approval before it is made or recorded. No third copy (such as a separate decisions file) exists.

| ID | Question | Decision | Reason | Log |
| --- | --- | --- | --- | --- |
| DEC-1 | Level data format and location | `Assets/ColorBlockJam/Data/Levels/LevelDatabase.json` stores ordered level records and is the sole authored source. `LevelDatabase.asset` is a ScriptableObject wrapper referencing that JSON as a `TextAsset`; it holds no serialized level copy. Player saves use separate JSON. No per-level ScriptableObject, parallel runtime database or scene-baked board. | The latest user direction makes JSON the direct level database while preserving a Unity/Onity reference for scene composition. | PROJECT.md |
| DEC-2 | How the game finds levels | The game receives a direct `LevelDatabase` asset reference to its JSON `TextAsset`. Player progression selects by ordered position; editor test play selects by stable level ID. No `Resources.Load`, no `StreamingAssets`, no string paths. JSON record order is play order. | One direct reference is build-safe and survives renames. `StreamingAssets` sits inside the APK on Android and needs an async web request. | PROJECT.md |
| DEC-3 | Editor form | UI Toolkit EditorWindow with a visual grid and one-click Play. Confirmed. | Numeric-only editors are forbidden. One UI technology. | PROJECT.md |
| DEC-4 | Board size limits | Width 3 to 8, height 3 to 10, clamped in the editor and checked at load. | The largest board still fits 1080 x 1920 and 20:9 at a readable scale. | Pending |
| DEC-5 | Block shapes | Any polyomino in the data. The editor offers 1x1, 1x2, 1x3, 2x2, L3, L4, T4, S4, I4 plus Rotate. | The composer renders any footprint. Rotation keeps the library small. | Pending |
| DEC-6 | Door model and exit fit | Boundary-only doors with a color and a span. Exit needs color match, contact with the side, and full width inside the span. | Smallest model for the core loop. | Pending |
| DEC-7 | Win popup | Reference 05's completion hierarchy, +10 reward, Continue (Next Level action), Home; no X2 ad option or 40/80 reward change. | User visual reference, preserving the case's economy and `Won` exits. | PROJECT.md |
| DEC-8 | After the last level | "More levels coming soon" with Home only; the level button replays the last level. | Honest scope, no wrap-around logic. | Superseded by DEC-173 (2026-09-28): numbers keep counting and the levels play again from the first |
| DEC-9 | Coin economy | +10 coins per win, including replays; start at 0. Buy subtracts the displayed cost and grants the displayed booster quantity. No negative balance or navigation penalties. | Functional purchase confirmed by the user. Default offer: 10 coins for 1 charge, adjustable design tuning. | Purchase confirmed; tuning adopted |
| DEC-10 | Persistence | One versioned Newtonsoft JSON player snapshot behind the save service; temporary write/flush and atomic replacement via a tested platform adapter. Holds level index, coins, toggles and shared booster stock. Publish only after durable commit; no split coin/stock writes. | Replaces pending PlayerPrefs design. Local-only persistence, no IAP/backend; boundaries and failure outcomes in DEC-92. | PROJECT.md; user delegated |
| DEC-11 | Timer format and bounds | Integer seconds, 10 to 300 (default 60), shown as `m:ss` rounded up. | Readable, and text changes once per second. | Pending |
| DEC-12 | Life placeholder | A static heart with "5". | The brief allows a placeholder. | Pending |
| DEC-13 | Pause popup buttons | Superseded by PROJECT.md DEC-135 (reference 20): the Settings popup plus Home; close resumes; Restart on the HUD. | The exits of `Paused` in `PROJECT.md`. | Superseded 2026-09-27 |
| DEC-14 | Touch feedback | One shared USS style: scale to 0.92 and darken on press, restore on release. | No per-button code, no allocation. | Pending |
| DEC-15 | Toggle persistence | Through the save service. | Negligible cost. | Pending |
| DEC-16 | Runtime UI framework | UI Toolkit for all runtime screens, Panel Settings at 1080 x 1920. Confirmed. No UGUI fallback; a blocking problem goes to the user as a decision change. | One UI idiom for game and editor. | PROJECT.md |
| DEC-17 | Board presentation | Supplied 3D meshes under a perspective camera (pitch 75, FOV 60) that frames the whole board for the screen aspect; the earlier orthographic proposal is superseded because the real game's board top edge is narrower than its bottom edge. | Uses the supplied art; fits any aspect ratio. | Decided (PROJECT.md DEC-103) |
| DEC-18 | Safe area | One reusable safe-area root driven by `Screen.safeArea` under every screen. | Works on any device. | Pending |
| DEC-19 | Build deliverable | Build an Android IL2CPP ARM64 APK and test it on the available phone; provide a recording as supplementary evidence if useful. | Device evidence verifies the player build as well as the project files. | Pending |
| DEC-20 | LLM disclosure | A running log of date, part, tool, use, and hours in `docs/LLM-LOG.md`, summarized in the README. | The brief asks for a per-part breakdown; hours feed R-6. | Pending |
| DEC-21 | Validation (old) | Superseded by DEC-68. | The static check missed corridors and deadlocks. | Pending |
| DEC-22 | Timer start | When the attempt enters `Playing`. | Simple and deterministic. | Pending |
| DEC-23 | Palette | One GamePalette asset owns 11 reference block/gate colors, Wall/Ground colors and eight booster art swatches. Board APIs use stable PaletteColor enum values; Blender consumes a derived export. Existing authored level color strings remain explicit load-boundary IDs. | Shared authority prevents drift; typed authoring follows the user's 2026-09-24 request. | Board assets/editor implemented; level conversion integration remains separate |
| DEC-24 | Toggle OFF visual | Superseded by PROJECT.md DEC-134 (reference 19): ON = `btn_toggle_on` knob right on a dark green track; OFF = plain light grey knob left on a dark navy track. | No OFF sprite (AF-1); readable without color. | Superseded 2026-09-27 |
| DEC-25 | Exit timing versus win | The exit commits at contact. A final exit latches Won/stops gameplay, runs the bounded visual Finish, captures the board and then tears down before the popup (DEC-93). | Preserve the requested final fragments without allowing gameplay after success. | PROJECT.md; user delegated |
| DEC-26 | Multi-touch | One active drag. | No contention, simple tests. | Pending |
| DEC-27 | Delivery link | A zip exported from the UVCS workspace (13.1 D-2). | The brief allows an archive. | Pending |
| DEC-28 | Frame rate | 60 fps; `Application.targetFrameRate = 60` set once in the boot composition root. | Android defaults to 30. | Pending |
| DEC-29 | Same-frame win and fail | Tick order commands, timer, view sync; a last exit at timer zero gives Won. | Deterministic and testable. | Pending |
| DEC-30 | Test play and save data | In-memory save store; the level GUID override lives in editor session storage and is cleared on exit. | Testing never changes real progress. | Pending |
| DEC-31 | App backgrounding | Auto-pause through Suspend. | Protects the timer inside the state table. | Pending |
| DEC-32 | Door position unit | Doors sit on edge segments, not cells. | Perimeter cells stay playable. | Pending |
| DEC-33 | Resize with content | Hatched preview, a confirm dialog naming what is removed, one Undo step, validation after every resize. | No silent data loss or invalid data. | Pending |
| DEC-34 | Editor entry points | Menu, double-click on the `LevelDatabase` asset, its "Open in Level Editor" button. | Designers never edit level data files by hand. | Pending |
| DEC-35 | Block storage | Resolved footprint plus the source shape ID. | Shipped levels never change with the library. | Pending |
| DEC-36 | Assemblies | Domain (no engine references), Presentation, Editor, Validation (editor-only), one-way references. | Makes the split enforceable. | Pending |
| DEC-37 | Level provenance | When schema support is added, Save records `editorStamp` (tool, editor version, save time) in the selected canonical JSON level record alongside `formatVersion`. | Makes AC-8.4 verifiable from the authored database without a second file. | Pending |
| DEC-38 | "Implement the first 5 levels" | Five original tutorial-curve levels authored in the editor from the supplied assets, not copies of live levels. | Live levels change between versions; the brief evaluates the editor pipeline. | Pending |
| DEC-39 | Text font | LiberationSans from the installed TextMeshPro essentials, used through a UI Toolkit font asset. No imported font. | No font is supplied (AF-4); a package font is not a new import. | Pending |
| DEC-40 | 9-slice borders | Borders set in sprite import settings for panels and buttons that stretch; each sprite classed as nine-slice or fixed. | No sprite has borders (AF-10). | PROJECT.md |
| DEC-41 | Popup Home button | `btn_popup_blue` with a "Home" text label; `ic_home` only as a secondary glyph. | `ic_home` shows bricks (AF-11). | Pending |
| DEC-42 | Background framing | Full-screen backgrounds use cover scaling, centered, drawn outside the safe area; UI stays inside it. | No background fits the screen (AF-12). | Pending |
| DEC-43 | iOS | A valid target with shared portrait and safe-area code; not built or tested in the time box; stated in the README. | The brief asks for Android; an unbuilt claim would be unverified. | Pending |
| DEC-44 | Editor levels and the database | Save of a new working record appends it to the database. Test play loads by database GUID and stable level ID. Test records are removed before delivery (V-M18). The saved index is clamped to the database count. | Simple for designers, safe for the shipped game. | Pending |
| DEC-45 | UI animation (was OQ-1) | USS transitions for UI; PrimeTween for gameplay only. | UI animation stays owned by its view. | Pending |
| DEC-46 | Reference device (was OQ-2) | The team's Android phone over USB; its model, SoC, RAM, and Android version go in the README, with a note if it is not mid-range. | Device checks are real (CON-10). | Pending |
| DEC-47 | Timer and booster icons (was OQ-3) | `ic_time` on the HUD; `ic_clock_freeze` for the in-game booster and frost timer style; `Clock` with an Extra Time label for the pre-level choice until dedicated supplied art is identified. | Distinguish contexts using existing art (AF-9, AF-16). | PROJECT.md |
| DEC-48 | Dev configs in the archive (was OQ-5) | Ship `AGENTS.md`, `PROJECT.md`, `CLAUDE.md`. Leave out `.mcp.json`, `.codex/`, `.claude/`. | Shows the process without machine-local settings. | Pending |
| DEC-49 | Door span (was OQ-7) | Any span in data; shipped levels use 1 to 3. | Readable doors. | Pending |
| DEC-50 | Exit trigger (was OQ-8) | A held block exits on contact with a fitting door, with no push (user feel test 2026-09-28, PROJECT.md DEC-164; replaced "exit only when pushed against the side"). | Deterministic; a leaning block can no longer be pulled back. | Decided |
| DEC-51 | Hallway test (was OQ-9) | EU-1 as in 11.10. With no first-time person, a labeled self-walkthrough is recorded and EU-1 is NOT VERIFIED in the README. | An author cannot fail on discoverability. | Pending |
| DEC-52 | Talent Team contact (was OQ-4) | None. Decide, document, continue. | The brief allows it. | Pending |
| DEC-53 | Delivery verification | Every applicable acceptance criterion, lifecycle rule and focused test passes; the Android APK runs on the phone. Record usability and frame-rate results with their test conditions. | Completion is based on evidence from the current revision. | Pending |
| DEC-54 | Package resolution in the archive | Ship `manifest.json` and `packages-lock.json`. Onity stays on its pinned git URL, PrimeTween on its npm registry. R-1 lists git, network, and the measured first-import time. If V-M19 fails, embedding Onity needs user approval. | A project that does not open fails every axis. | Pending |
| DEC-55 | Gate evidence rule | An AC counts as passed at a gate only when every V-row that lists it has run by that gate (16.8). | Gates cannot go green without evidence. | Pending |
| DEC-56 | Rendering boundaries | Keep the case architecture and supplied assets; use the explicit rendering contract in DEC-96. | Avoid unrelated framework, art and package dependencies. | PROJECT.md; refined by DEC-96 |
| DEC-57 | Board visuals | Original FBX tiles, catalog and tested neighbor composer remain shared. The later user request adds derived prefabs per library shape, each with BlockPiece and a passive preview; JSON cells/identity are authority; the SO only binds JSON and prefabs. Runtime changes copy data and recompose through prepared pools. | Safe prefab authoring and runtime type changes without new board meshes; updated 2026-09-24. | PROJECT.md |
| DEC-58 | Materials and UI batching | One block material with explicitly instanced property-block color/clip values and one cached property owner (DEC-96), measured against a prebuilt per-color SRP palette without ordinary tint MPB; count any effect-time overrides (NFR-P5). Sprite Atlases for UI. | MPB bypasses SRP Batcher; shared material alone does not prove instancing. Device frame cost may overturn the initial choice. | PROJECT.md |
| DEC-59 | Tick owner | Only the session tick owner implements `IOnityTickable`; gameplay systems implement a project tick interface. | The Onity container ticks every bound tickable in every state. | PROJECT.md (rule 4 wording to correct: Pending) |
| DEC-60 | Pool ownership and warm-up | One disposable scene-scoped object warms piece, block-view and effect pools for the authored board, reachable Rocket footprints and exit/Vacuum/mallet effects. Bound recomposition headroom by authored cells, not initial stock: purchases can replenish stock. Hammer removes existing layers/pieces without split-child creation. `BindPooledFactory` is not used for board pools. Hooks are `OnPoolGet` and `OnPoolRelease`. No growth during an attempt. | Extra boundary pieces need measured/tested headroom; no runtime Instantiate. | PROJECT.md (pools); detail Pending |
| DEC-61 | Entities default World | Define `UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP` after a smoke test confirms Onity still works. | The slice uses no ECS; otherwise system groups run every frame unowned. | Needs approval |
| DEC-62 | Unused packages | Remove unused Cinemachine, ProBuilder, Timeline and Visual Scripting when package approval is granted. | Reduce unused runtime/editor dependencies. | Needs approval |
| DEC-63 | MCP for Unity in the APK | Keep MCP for development; exclude its runtime tooling from the release APK through an approved package/build change. Preserve the independently required Newtonsoft package and runtime assembly. | Tooling exclusion must not remove the game's approved JSON dependency. | Package/build mutation still needs approval |
| DEC-64 | Release Player Settings | Android IL2CPP/ARM64 applied under DEC-94. Retain the current mobile Balanced profile for DEC-96's shader adaptation and measurements; this supersedes the provisional Performant choice. Portrait and Input System only remain release requirements checked separately. | Use verified current settings and user-approved rendering values; do not claim unverified release settings or a successful device build. | Backend/architecture applied; remaining release checks pending |
| DEC-65 | Performance ceilings | Accept the NFR-P numbers now; lower them after the first measurement; raise one only with a decision row. | Targets must exist before code. | Pending |
| DEC-66 | Diagnostics in builds | Development builds: task and observable tracking on. Release: task tracking off at boot. | Evidence in development, no bookkeeping in release. | Pending |
| DEC-67 | Renderers per block | Pooled per-piece renderers first; combine a block's pieces into one mesh only if NFR-P4 fails after DEC-58. | Simpler to explain; matches the kit. | Pending |
| DEC-68 | Validation (replaces DEC-21) | Structural checks, then sound static pre-checks, then a bounded guided search. Timer and boosters excluded. Default budget 250,000 states and 3 s. | The brief says "unsolvable"; only a search finds corridors and deadlocks. | Pending |
| DEC-69 | Where the solver runs | Editor-only assembly, worker thread, never in Play Mode or the player. | No runtime work; the editor stays responsive. | Pending |
| DEC-70 | Validation result | A warning with Save anyway, never a hard block. Unknown gets the same dialog with an honest reason. An editor-owned `validation` record is stored on each canonical JSON level when schema support is added. | The brief asks for a warning; the record enables badges, caching, and audit. | Pending |
| DEC-71 | Effect record shape | Keep format 1 canonical level JSON `{type, params}` with stable lowercase IDs and `{key, value}` string pairs. Explicit registered Newtonsoft converters validate and turn the pairs into typed authored data at load and back at Save; never enable `TypeNameHandling`. | Preserve the documented level schema while using the approved serializer and explicit type registry. | PROJECT.md; schema preserved |
| DEC-72 | Effect registry and hooks | One catalog function builds an immutable registry split across Domain, Presentation, and Editor, joined by type ID with a completeness test. Effects plug in through XP-1 to XP-10. The core emits board events from the board core. | The core never switches on effect type; no static state. | Pending |
| DEC-73 | Runtime level JSON DTOs and player-save DTOs retain public camelCase fields matching the documented JSON keys (`params` via the verbatim identifier); Newtonsoft mappings and converters preserve those keys explicitly. | Stable serialized names; a documented DTO exception to private-field `m_` style. | Pending |
| DEC-74 | Rocket | One shared effect removes one 1x1 cell and recomposes the same block. Refuse disconnected or empty remainders, including any 1-cell block. In-game the player chooses; pre-level chooses a random eligible cell. Invalid targets spend nothing. | User confirms one cell and explicitly excludes whole-block destruction; context changes target acquisition only. | PROJECT.md; user confirmed |
| DEC-75 | Hammer | A 3D mallet removes exactly one exposed semantic layer/block-piece or obstacle. Plain/Arrow blocks are one piece and are removed whole. Active Ice is one obstacle: remove its shell completely, preserving the underlying block. No same-hit cascade or geometric splitting. Future Layer reveals only its authored lower content. | Latest user clarification supersedes the cell-splitting interpretation of reference 16. A semantic piece can occupy several grid cells. | PROJECT.md; user clarified |
| DEC-76 | Time Freeze | Hold the main timer and open a separate visible 10-to-0 countdown. The secondary clock advances only while Playing; at 0 it closes and main time resumes from its held value. Pause/saving preserves both; no numeric bonus to main time. Shader Graph frost follows the secondary timer. | Explicit user clarification: freeze the main clock and start a new 10-second counter. | PROJECT.md; user confirmed |
| DEC-77 | Vacuum | UI label "Vacuum"; the tapped block chooses its color, then all blocks of that color are removed, effects included. "Color Vacuum" remains a research alias. | User reference 03; no duplicate booster for the naming difference. | PROJECT.md |
| DEC-78 | Booster charges | Persistent stock keyed by the five definitions; seed 1 once per available definition as adjustable design tuning. Starting and HUD Rocket share stock (superseded 2026-09-28 by PROJECT.md DEC-167: separate stock). Selection, invalid targets and pre-commit cancel are free; a valid intent commits one charge before its single application (DEC-92), with no refund after commit. Restart/Next Level clear selections but preserve stock and purchases, with no refunds or refill. Test play uses isolated stock. | Makes the confirmed purchase meaningful across attempts and removes duplicate context inventories. | PROJECT.md; persistence adopted |
| DEC-79 | Booster removal versus exit | Whole-block booster removals raise `BlockRemoved`, never `BlockExited`, so other Ice counters count door exits only. Rocket changes the footprint without removing the block. Hammer may clear the targeted Ice obstacle entirely; the underlying block still counts for Won. | No blanket counter decrement or cascade. other mechanic interactions need a separate case rule review. | PROJECT.md |
| DEC-80 | Booster flow | Pre-level selection overlays Home. Application is a session command in Playing. Opening feedback holds countdown/ordinary input. Targeting does not pause the countdown; active Time Freeze still applies. In-level purchase uses Paused; pre-level purchase stays on Home. | One session state owner; no separate Skill state or unowned timer/effect. | PROJECT.md |
| DEC-81 | Ice | Counter 1 to 20, lowered by other blocks' door exits. Rendered by one Shader Graph overlay material with property-block tint, opacity, and cracks, and a UI Toolkit counter label. | Rollic's rule; `PROJECT.md` ice decision. | PROJECT.md (rendering); rule Pending |
| DEC-82 | Arrow | An axis (Horizontal or Vertical), both ways; exits only on the two axis sides. The marker is composed by the composer (OQ-10). | The supplied arrows are double-headed; one axis rule supports authoring and solving. | Pending |
| DEC-83 | Single delivery scope and user-directed order | The case flow and four selected features in 4.3 form one completion target. The user names each implementation task and its order. Every selected mechanic passes its applicable acceptance and verification rows. | Keeps the PRD as an implementation reference without a fixed work sequence. | User-directed |
| DEC-84 | Booster taxonomy and entry points | One Booster category/system; four HUD options (Time Freeze, Rocket, Hammer, Vacuum), two starting options (Extra Time, Rocket), five definitions. Extra Time adds +10 s on opening as the adopted adjustable case default, separate from countdown freeze. | Explicit user taxonomy correction; Extra Time amount is design tuning, not inferred from screenshots. | PROJECT.md |
| DEC-85 | Empty stock and purchase | Zero stock opens the purchase popup. Buy atomically spends coins and grants persistent stock; default offer 10 coins for 1 charge is adjustable. Failed persistence or insufficient balance changes neither value. Guard duplicate submissions, refresh UI after success and do not auto-cast or auto-select. | User confirms functional Buy. In-game purchase pauses gameplay; Shop remains locked and no real-money purchase is added. | User confirmed |
| DEC-86 | Home and locked tabs | Level opens the two-selector popup, then Play starts the attempt. Shop and Collection stay visibly locked; all other non-Home sections stay placeholders. | User references 06 to 10 and explicit locked-section instruction. | PROJECT.md |
| DEC-87 | Reference provenance and art | Keep all 23 screenshots unchanged in docs, explain each in Markdown, and link them from requirements. Reuse supplied sprites/text for UI fallbacks; hourglass/red Rocket/completion art remains absent. The separately authorized Blender booster models and approved Vacuum bake are imported and mapped in Art-Asset-PRD-Mapping.md; distinguish them from the original supplied kit. | User documentation and later art-production requests; screenshots remain visual evidence, not runtime textures or new economy rules. | PROJECT.md |
| DEC-88 | Vacuum cast feedback | White target outline, flat camera-facing vacuum near the lower board edge, and matching pieces shrinking into its nozzle. Domain removal follows durable stock commit; pooled presentation follows removal. A winning pull remains visible within the bounded Won Finish before teardown. | User reference 13; DEC-93 owns final presentation and forced cleanup. | PROJECT.md |
| DEC-89 | Held-block outline | Show a white outer silhouette while the active pointer holds a selected block; clear on release, cancellation, state change and pool return. A frozen selection still cannot move. Targeted boosters share the visual treatment. | User reference 14; the outline must follow the entire footprint. | PROJECT.md |
| DEC-90 | Gate-exit polish | A valid same-color, fitting exit disintegrates into small same-color cube fragments at the passage. No fragments for a refused exit. Session-owned pools/animation follow logic commit and terminal cleanup. | User reference 15; preserves exit correctness and no-unattended-work rules. | PROJECT.md |
| DEC-91 | Forgiving board picking | Pointer-down within the board interior/walls/gates/border selects a direct hit or nearest projected occupied footprint. Stable ties, one selection per hold, retained grab offset, and held white outline. Exclude UI/outside-area input and committed exits; do not change booster targeting or movement constraints. | User reference 17; makes near-block gate/border presses usable without teleporting or per-frame retargeting. | PROJECT.md |
| DEC-92 | Durable save boundary | One atomic Newtonsoft player snapshot, publish after commit only. Validate/capture a booster intent before the internal saving pause; skip the accepting tick, await outside Playing, then apply once before the timer. Opening commits while Home. Failure has no effect/debit; committed interrupted intents are not refunded or recovered. Win Retry reuses one intended reward snapshot and gates result navigation. | User delegates the persistence decision. Keeps durable stock and the allocation-free gameplay tick compatible; see 7.6 for background/crash handling and tests. | PROJECT.md |
| DEC-93 | Final-action presentation | Won latches immediately and gameplay stops. Permit only a session-owned visual Finish of at most 0.8 foreground seconds, independent of save I/O; then capture a static board, fully tear down and open the popup. Failure/timeout/interruption forces cleanup with a supplied-background fallback. | User delegates the polish decision. Final exit/Vacuum/Hammer remains visible without delaying the gameplay result or letting simulation continue behind UI. | PROJECT.md |
| DEC-94 | Android configuration | IL2CPP and ARM64 only; retain the target mobile rendering values including Linear, Vulkan before GLES3 and ASTC. Verify portrait and Input System before release. | Android settings were saved and read back on 2026-09-24; APK/device checks remain pending. | Applied |
| DEC-95 | Imported UI mapping | Use the exact 45 sprite paths/GUIDs in UI Asset Mapping, retaining source filenames. Supplied screenshot is preserved separately from the 17 gameplay references. | User confirms imported icons; mapping was checked against asset metadata. No runtime UI assignment or measured slice border is implied. | Documented |
| DEC-96 | Rendering specification | Use a small URP 14 renderer with direct instanced tint, one cached property-block owner and pool reset. The user's 2026-09-25 extension shares one opaque material/shader and 128 x 32 color atlas across booster models, blocks, walls, gates, ground and arrows; match the original game's saturated plastic look. Ice, Time Freeze and the transparent baked Vacuum remain separate. Match depth, normal and shadow clipping across passes; the held-block outline covers the whole footprint. | Preserve visual correctness and stable mobile performance. Compare instancing, palette and frame cost on the target phone (NFR-P5). | [Shared-art implementation and Editor evidence](art/shared-art/README.md); device verification pending |
| DEC-97 | Completed vertical slice target | Verify the five-level game, Home, Settings, editor, validation, functional boosters, Ice and Arrow before calling the vertical slice finished. The user selects further catalog mechanics and directs their implementation order. Each selected mechanic becomes part of the completion checklist. | One product scope and a user-directed sequence. | PROJECT.md |

## 13. Deliverables and README Checklist
### 13.1 Deliverables
- [ ] D-1: One link to the archive (DEC-27).
- [ ] D-2: The project with `Assets`, `Packages` (`manifest.json`, `packages-lock.json`), `ProjectSettings`, and the root docs. Left out: `Library`, `Temp`, `Build`, `Builds`, `Logs`, `UserSettings`, `obj`, `.vs`, `.vscode`, `.idea`, `.plastic`, `*.csproj`, `*.sln`, `*.user`, `.mcp.json`, `.codex/`, `.claude/`. Passes V-M19.
- [ ] D-3: `README.md` at the root.
- [ ] D-4: The Android `.apk`, tested on the phone (DEC-19). A recording is optional.
- [ ] D-5: `LevelDatabase` holds exactly levels 1 to 5 in order, authored in the editor, with editor stamps and Solvable validation records. No test level remains (V-M18).
- [ ] D-6: Linked docs: `docs/LEVELS.md` (solutions), `docs/LLM-LOG.md` (tools, parts, hours), `docs/perf/` (captures).

### 13.2 README Checklist (brief sections 7 and 8)
- [ ] R-0: Short: two pages or less. Details are one-line links.
- [ ] R-1: Open and run: Unity `2022.3.62f2` (Android module for builds), git on PATH, network on first open, measured first-import time (DEC-54), the scene to open, Play.
- [ ] R-2: Explain how to open the editor and make a level using the 11.2 workflow.
- [ ] R-3: Key decisions with reasons, drawn from the `PROJECT.md` decision log, including the unclear-requirement decisions (DEC-38, DEC-43, DEC-44, DEC-51, DEC-54, DEC-74 to DEC-79).
- [ ] R-4: Known problems and incomplete parts: placeholder lives, toggles with no audio, iOS not built (DEC-43), EU-1 status (DEC-51), any unmet acceptance criterion and the phone profile if not mid-range.
- [ ] R-5: LLM tools and the parts they were used for, summarized from `docs/LLM-LOG.md` (DEC-20).
- [ ] R-6: Approximate work time, summed from the hours in `docs/LLM-LOG.md`.
- [ ] R-7: Asset feedback AF-1 to AF-16, and the supplied asset folder link from the brief.
- [ ] R-8: One line on running the tests (Test Runner, EditMode and PlayMode).
- [ ] R-9: The runtime loop table (NFR-L8) and the measured performance numbers (NFR-P1 to NFR-P8).
- [ ] R-10: "How to add an effect", the mechanics catalog link and the booster rule decisions (AC-EXT-9).

## 14. Execution and Completion Control
### 14.1 User-Directed Work Sequence
The user names the next area or mechanic, for example the board. For that task, use the requirements, acceptance criteria, dependencies and verification rows in this PRD and appendix. The task plan states the exact deliverable and tests. The document does not assign a fixed order to mechanics.

### 14.2 Task Completion
For each selected task: check prerequisites; implement the applicable data, domain, editor and view behavior; run focused EditMode, PlayMode and device checks where specified; record remaining defects. A selected mechanic is complete only when its acceptance criteria pass. Existing completed behavior is preserved and rechecked when the task touches shared systems.

### 14.3 Delivery Gate
The final case delivery includes all requirements in 4.1 and 4.3, five editor-authored playable levels, README and APK. Section 16 defines the evidence. If a requirement cannot pass, record it as unfinished and continue the work; do not describe the vertical slice as complete. A selected catalog mechanic also joins the gate when the user adds it to the implementation work.

### 14.4 Completion Checklist
Verify the 14 case acceptance criteria in 16.7, the lifecycle and allocation rules, one authoritative level database (manifest plus packs) shared by editor and runtime through the project scope installer's reference, direct JSON Save/Reload, pooled board composition, test-play isolation, validation, all five functional booster definitions in six placements, Ice, Arrow, the five levels, clean-open check and device build. Record actual results rather than planned checks.

## 15. Risks and Mitigations
| ID | Risk | Likelihood / Impact | Mitigation |
| --- | --- | --- | --- |
| RISK-1 | Drag collision and snapping take longer or feel wrong. | Medium / High | Verify grid-step logic with tests; tune feel with device input checks. |
| RISK-2 | Onity DI fails under IL2CPP. | Low / High | Run an early Android smoke APK, retain required DTO/converter types and verify on the phone. |
| RISK-3 | `corner_3.002` leaves a gap at a wall corner. | High / Low | Other corner meshes; visual check; AF-7. |
| RISK-4 | Editor features spread past the task. | Medium / High | Keep the selected task's acceptance criteria and verification visible; add adjacent editor work only when required. |
| RISK-5 | The board does not fit 20:9 or overlaps the HUD. | Medium / Medium | Camera fitted to the safe board rectangle; 8 x 10 checked (AC-3.11). |
| RISK-6 | Teardown misses an owner and something keeps running. | Medium / High | Everything registers with the session; V-M1 to V-M4; owner comments (NFR-X7). |
| RISK-7 | UI Toolkit runtime blocks safe area, input routing or sprite scaling. | Medium / High | Verify a portrait runtime screen with Input System routing and measured nine-slice borders before broad UI work. |
| RISK-8 | An authored level cannot be solved. | Medium / Medium | Run level validation and play through each level before it is final. |
| RISK-9 | Missing OFF art is judged incomplete. | Low / Low | DEC-24, greyscale check, AF-1. |
| RISK-10 | The evaluator expects git. | Low / Medium | The brief allows an archive; the README states UVCS. |
| RISK-11 | Allocation from strings or LINQ in hot paths. | Medium / Medium | Cached per-second strings; zero-alloc tests (V-E23). |
| RISK-12 | Play from the editor leaves stale state across domain reload. | Medium / Medium | GUID in session storage, cleared on exit; V-M13 with reload on and off. |
| RISK-13 | UI pointer events leak into a board drag. | Medium / Medium | UI picking checked before a drag (V-M14). |
| RISK-14 | A test level ships as level 6. | Medium / Medium | V-M18; index clamp. |
| RISK-15 | No first-time person for the hallway test. | Medium / Medium | DEC-51. |
| RISK-16 | Backgrounds letterbox or stretch on 20:9. | High / Low | DEC-42; V-M11. |
| RISK-17 | Packages do not resolve on the evaluator's machine. | Medium / High | Lock dependencies and run V-M19 on a clean extracted project. |
| RISK-18 | A selected mechanic remains partially integrated. | High / Medium | Require domain, editor, view, persistence and verification before marking it complete. |
| RISK-19 | The solver returns Unknown too often on dense levels. | Medium / Low | Guided order, reductions, a measured envelope, a per-user budget; the dialog never claims a proof it lacks. |
| RISK-20 | Solver rules drift from runtime rules. | Low / High | Rules precomputed from Domain predicates; parity and replay tests (AC-VAL-5, AC-VAL-6). |
| RISK-21 | A Rocket use needs more pieces than the pools hold. | Medium / Medium | Headroom from the unit-tested per-removal bound (DEC-60); pool counts checked in V-BST-5. |
| RISK-22 | The arrow meshes do not match the marker composition. | Medium / Low | Inspect the import scale and geometry before implementing the marker; keep the composer interface stable (OQ-10). |
| RISK-23 | MPB bypasses SRP Batcher; uninstanced properties, missing passes or competing MPB writers can also defeat batching or corrupt effects. | High / Medium | DEC-96 owns all renderer properties and requires consistent passes. Verify instanced draws and compare the palette on device, counting effect-time MPB exceptions (NFR-P5); retain the measured winner (DEC-58). |
| RISK-24 | Package or Player Settings approval delays implementation. | Medium / Medium | Identify the exact required changes in the selected task and request target approval before modifying them. |
| RISK-25 | Canonical level JSON load silently defaults malformed fields, or IL2CPP stripping removes level/player-save DTOs and converters. | Medium / Medium | Explicit Newtonsoft converters, `TypeNameHandling.None`, required/unknown-field checks, DTO/converter preservation, JSON read/write round-trip and fail-loud tests (V-E17, V-E24, V-D2). |
| RISK-26 | Catalog specifications are mistaken for implemented behavior. | Low / Medium | Register only implemented and verified types; track actual build status separately. |
| RISK-27 | Group or merge mechanics need additional hooks. | Medium / Low | Review the selected mechanic's dependencies and design the needed hook before implementation. |

## 16. Verification Plan
### 16.1 EditMode Tests (Unity Test Framework)
| ID | Test | Covers |
| --- | --- | --- |
| V-E1 | After `Won` or `Failed`, a domain tick changes nothing. | NFR-5, NFR-L4, AC-4.1, AC-4.2 |
| V-E2 | After `Won` or `Failed`, the timer value stays the same. | AC-3.7, NFR-L4 |
| V-E3 | After `Won` or `Failed`, move, exit, and booster commands are rejected. | AC-3.2, NFR-L4 |
| V-E4 | A second terminal request is ignored. | RULE-17, NFR-L4 |
| V-E5 | Only the transitions in the 7.5 table are allowed. | AC-5.6 |
| V-E6 | Pause freezes the timer; Resume continues from the same value. | AC-3.8, AC-4.6 |
| V-E7 | Timer at zero with blocks left gives `Failed`; the display rounds up and changes once per second. | AC-3.5, AC-3.6, AC-3.7 |
| V-E8 | Movement: no overlap, no wall crossing, stops at the first blocked cell, cell-aligned rest. | AC-3.2, AC-3.3 |
| V-E9 | Exit: color match plus span fit passes; wrong color or too wide is blocked. | AC-3.4 |
| V-E10 | No required block left sets `Won`. | RULE-15, RULE-29, AC-5.2 |
| V-E11 | A restarted attempt deep-equals a fresh load of the level. | AC-5.3, AC-5.3a |
| V-E12 | Coins: a win adds 10 once; fail, restart, pause, home add nothing; purchase spending is separate and saved with its stock grant. Save/reload keep committed values (fake storage). | AC-6.1, AC-6.1a, AC-6.2, AC-6.3, AC-6.5 |
| V-E13 | Canonical JSON parses to `LevelData`, then to a plain C# attempt model without losing authored fields. Levels resolve only through the directly referenced `LevelDatabase` in order; a code search finds no `Resources.Load` or `StreamingAssets` for levels. | AC-8.1, AC-8.2, AC-8.3, AC-8.5, AC-8.9 |
| V-E14 | Structural validation rejects overlap, out-of-bounds blocks, bad doors, no blocks and unknown colors in a JSON level record; loading also rejects an unknown format version. | AC-7.9, AC-8.6 |
| V-E15 | Size and timer clamps; a shrink reports exactly the blocks and doors it would remove. | AC-7.2, AC-7.5 |
| V-E16 | Last exit plus timer zero in one frame gives `Won`. | RULE-18, DEC-29, NFR-L4 |
| V-E17 | Round trip: editor working model, canonical JSON Save, runtime load gives equal values and order for every authored field, effects included when supported; the attempt built from it matches. | AC-3.1, AC-8.1, AC-8.7 |
| V-E18 | Editor placement checks give the same verdict as the runtime for rotate, move, and resize. | AR-10, AC-7.2, AC-7.3 |
| V-E19 | The saved index is clamped to the database count; advancing past the last level stays on it. | AC-1.2, AC-4.10, AC-5.4, AC-8.3 |
| V-E20 | JSON roundtrip/import/merge, whole-batch conflict refusal, stale/read-only file protection and JSON/prefab Undo pass. Palette/shape enums are unique and stable. New drafts and draft editing do not write library/enum source; Save alone commits a new type. Piece names generate valid enum identifiers; geometry saves trim margins; existing edits and delete/restoration preserve enum source. Grid dimensions preserve painted cells; center cross is independent and cell tint matches the preview palette. Ctrl+Z/Ctrl+Shift+Z restore whole strokes, manipulation results and dimensions; no-op paint and rejected resize add no history; name/color and authored files remain unchanged. | AC-8.8 |
| V-E21 | Composer tests: exact lists for 1x1, I4, L4, T4, 2x2, plus, both L4-minus-one-cell cases; invariants over the library in four rotations; bad input rejected; 0 B on a reused buffer. | AC-3.14 |
| V-E22 | Architecture tests: exactly one `IOnityTickable`; no gameplay `Update`; no `async void`; Domain has no engine references; assembly references match. | NFR-7, NFR-11, NFR-L3, NFR-L6, NFR-X5 |
| V-E23 | Zero-allocation tests on the tick, move, timer, and composer. | NFR-1, NFR-P2 |
| V-E24 | Canonical database JSON format-1 read/write round trip retains level order, string-pair params, effects arrays and cellObjects when supported. Unknown type/key/value/color/reference, missing required fields and CLR type metadata fail before the file is replaced. Explicit converters use TypeNameHandling.None. | AC-8.6, AC-8.10 to AC-8.12 |
| V-E25 | The Back action and the app-pause handler drive Pause, Resume, Home, and Settings close; backgrounding freezes the timer. | AC-2.3a, AC-3.12, AC-4.11 |
| V-E26 | Suspend and Teardown call order with fake ports; the token is cancelled and the disposable disposed. | NFR-L1, NFR-L2, AR-3 |
| V-E27 | Pure selection fixtures: projected-footprint distance for direct/gate/wall/border/empty-area presses, long versus short blocks, concave footprints, stable ties, no candidates, excluded mid-exit blocks, retained grab offset and single-pointer lock; input routing rejects UI/outside-area/state-gated/booster-targeting cases. | AC-3.19 to AC-3.21 |
| V-E28 | Create, Delete, and Save write the single canonical JSON after validation, preserve the reference asset GUID, and leave the file unchanged on invalid input. An external edit blocks a stale Save; Reload reads the new records in order. | AC-8.13 |

### 16.2 PlayMode and Manual Checks
| ID | Check | Covers |
| --- | --- | --- |
| V-M1 | After the finish popup, Home, and Restart, `Tween.GetTweensCount(target)` is 0 for gameplay targets. | NFR-5, NFR-L7 |
| V-M2 | Onity Task Tracker shows no running task from the ended session. | NFR-5, NFR-L6 |
| V-M3 | Onity Observable Tracker (tracking on) shows no live subscription from the ended session. | NFR-5, NFR-L5 |
| V-M4 | The Profiler shows no gameplay script sample while a popup is open. | NFR-5, AC-4.2 |
| V-M5 | Phone Profiler: 0 B GC Alloc per frame over 10 s of drag and countdown; no `Instantiate` in `Playing`. | NFR-1, NFR-3, NFR-P2, AC-3.16 |
| V-M6 | Full flow: Home, Settings, levels 1 to 5, a fail with Restart and Home, a pause (Resume, Restart, Home), the last-level win. Lives stay the same throughout. | AC-1.1 to AC-1.3, AC-1.7, AC-3.5, AC-4.1, AC-4.3 to AC-4.10, AC-5.2, AC-5.3 |
| V-M7 | Coin paths from AC-6.1a including purchase and app kill; Home/HUD balances and stock equal the save service; relaunch opens Home on the same level with committed purchases and spending preserved. | AC-1.8, AC-3.9, AC-5.5, AC-6.1, AC-6.1a, AC-6.4, AC-6.5 |
| V-M8 | Settings: ON and OFF differ in a greyscale screenshot; close returns Home; state survives relaunch; Home ignores input under it. | AC-2.1 to AC-2.3, AC-2.5, AC-2.6 |
| V-M9 | Secondary placeholder controls show press feedback; functional booster controls show the states and effects in 7.10. | AC-1.5, AC-1.6, AC-2.4, AC-3.10 |
| V-M10 | Hallway test per EU-1 with time, errors, and stuck points; EU-2 and EU-3 observed. If no first-time person: NOT VERIFIED (DEC-51). | AC-7.1, NFR-9, EU-1 to EU-3 |
| V-M11 | Game view at 1080 x 1920 and 1080 x 2400 with the safe-area simulator: Home, Settings, HUD, popups, the 8 x 10 board; backgrounds fill without letterbox or stretch. | AC-1.4, AC-1.4a, AC-3.11, NFR-4 |
| V-M12 | Each level matches 11.11, carries the stamp, and is played to a win from the editor's Play. | AC-5.1, AC-7.8, AC-7.11, AC-8.4, AC-8.4a |
| V-M13 | Test play, including booster use and Buy, leaves saved coins, stock, index and toggles unchanged; the override clears with domain reload on and off. | AC-6.6, RISK-12 |
| V-M14 | A pointer that starts on HUD or popup never starts a drag. | AC-3.13 |
| V-M15 | Editor: resize preview, Cancel, Confirm, one Undo; rotate; door create, extend, delete, refusals; Save clears the dirty marker; Undo and Redo per edit type; Duplicate, reorder, Delete; entry points; click-to-highlight; the game follows the new order. | AC-7.2 to AC-7.4, AC-7.7, AC-7.9, AC-7.10, AC-7.12, AC-8.3 |
| V-M16 | The grid shows cells, walls, colored doors, and colored shapes to scale; no workflow step needs a numeric-only field. | AC-7.6 |
| V-M17 | Developer run: New, size, three blocks, one door, timer, Save, Play to a win, no code, no file edits. | AC-7.1, AC-7.8 |
| V-M18 | Pre-delivery: the `Main` level database under `Assets/ColorBlockJam/Data/Json/Levels` holds exactly the 5 shipped records in order; the project scope installer's level JSON reference names its manifest. | D-5, AC-8.9, DEC-44 |
| V-M19 | Clean open: unpack the final archive to a new path with no `Library`, open in `2022.3.62f2`, packages resolve to the lock file, 0 compile errors, EditMode green, level 1 to a win, no excluded path present, first-import time recorded. | D-2, R-1, DEC-54, RISK-17 |
| V-M20 | Pool diagnostics: totals unchanged from load end to teardown, no active object after teardown; 10 restarts and 5 Home trips keep counts flat and heap growth 1 MB or less; no pool survives scene unload. | AC-3.16, NFR-3, NFR-P3, NFR-P10, NFR-L10 |
| V-M21 | Frame Debugger and Profiler on level 5 (Editor and phone): SetPass, batches, triangles, instancing/break reasons, CPU/GPU cost, GC and material count for both variants at identical quality/passes. Verify color/depth/normal/shadow clipping, whole-footprint outline and MPB reset; include Android Vulkan/OpenGLES3. Home/HUD UI draws and retained-path numbers go in the README. | NFR-2, NFR-P4, NFR-P5, NFR-P6, DEC-96 |
| V-M22 | README loop table reviewed; a phone capture with the win popup open shows no unlisted PlayerLoop sample; third-party time 1 ms or less. | NFR-P8, NFR-L8 |
| V-M23 | Code-quality gate (16.4). | NFR-6, NFR-X1 to NFR-X4, NFR-X6, NFR-X7 |
| V-M24 | Piece kit: reusable catalog tile prefabs plus one owned BlockPiece prefab per library entry; original mesh references/names unchanged and corner_3.002 unused. Save/rename preserves GUID/custom root content, collisions/read-only targets preserve outputs, and library Undo restores derived geometry. Calibration on a tile test copy changes no composer file. | AC-3.15 |
| V-M25 | Manual at 1080 x 1920 and 1080 x 2400: pre-level popup, all four selection combinations, blue/count and green/check states, cancel/Back, locked Shop/Collection, and placeholder feedback against references 06 to 10. | AC-1.1a, AC-1.5, AC-1.9 |
| V-M26 | Manual: zero-stock popup variants and targeting banners, blocked underlying input, purchase pause/close/background behavior, valid debit/grant, insufficient-balance feedback and no auto-cast against references 01 to 03. | AC-6.7 to AC-6.10, AC-BST-0.6, AC-BST-0.9, AC-BST-0.10 |
| V-M27 | Manual/device clip: opening icons and post-effect board, Continue/reward hierarchy, Time Freeze frost, and Vacuum white outline plus shrinking suction into reference 18's upright flat cast with a deforming hose and fixed upper intake. Include both portrait aspect ratios, pause/resume and terminal cleanup. | AC-4.9, AC-5.7, AC-BST-1.5, AC-BST-4.5 to AC-BST-4.7, AC-BST-5.2 |
| V-M28 | Manual/device: held-pointer outline on down/move/up/cancel and pooled reuse; fitting-door cube fragments versus refused exits; a 3D Hammer removing one whole plain piece or one Ice shell without hitting its underlying block. Include pause/resume, Restart and last-block teardown. | AC-3.17, AC-3.18, AC-BST-2.1 to AC-BST-2.7 |
| V-M29 | Manual/device at both portrait aspect ratios: drag begins from the nearest block after gate/wall/border/empty-space presses, with no jump or retarget while held. Include UI overlap, outside-area touches, frozen/axis-limited blocks, pointer cancellation and booster targeting. | AC-3.17, AC-3.19 to AC-3.21 |
| V-M30 | Editor level selection bar and bottom-bar Save Level (11.1): choose database and target, edit a working copy, Save or Discard on switch, Create, Delete, Undo/Redo, and Reload JSON. Show the JSON source, block stale writes, and require no hand-edited file. | AC-7.13, AC-8.13 |

### 16.3 Device Checks (the team's Android phone over USB)
| ID | Check | Covers |
| --- | --- | --- |
| V-D1 | 60 s capture on level 5 and a 10-minute soak on levels 1 to 5 with the Profiler attached; the numbers and the phone profile go in the README. | NFR-2, NFR-P1 |
| V-D2 | IL2CPP ARM64 build: the full flow runs, DI works, no AOT errors in logcat; representative effects load from the canonical JSON through `LevelDatabase`, and a player-save snapshot round-trips through preserved Newtonsoft converters. | NFR-8, AC-8.12 |
| V-D3 | The phone's 20:9 screen: nothing under the cutout or the system bars. | AC-1.4, AC-1.4a, NFR-4 |
| V-D4 | Android Back on Settings, in `Playing`, and on each popup. | AC-2.3a, AC-4.11 |
| V-D5 | Play tap to `Playing`, cold start to Home, Restart to `Playing`: median of three runs. | NFR-10, NFR-P7 |
| V-D6 | Background and resume in `Playing` auto-pauses with no time lost. | AC-3.12 |
| V-D7 | Release APK: build report, IL2CPP ARM64 library present; exclude MCP for Unity, unused packages and the Validation assembly. Confirm the required Newtonsoft runtime is retained and its player-save JSON smoke test passes; record size. | NFR-P9, NFR-L9, AC-VAL-10, AC-8.12 |

### 16.4 Code-Quality Gate
Run the code-quality gate before completion and again after any shared-system change (V-M23). Pass conditions:

- 0 compiler warnings in project code after a clean import (V-M19 console).
- `.editorconfig` naming rules report 0 violations in project code.
- A grep over gameplay assemblies finds 0 `Update`, `FixedUpdate`, `LateUpdate` outside the tick owner, 0 `Invoke`, `StartCoroutine`, `async void`, static events, `Time.timeScale` writes, legacy `Input.`, and `.material` reads on block renderers.
- Every type has its one-sentence summary; every `Subscribe`, async method, tween, pool, and loop has its owner and stop comment; every DEC reference exists.
- Assembly references match NFR-11 (V-E22).

### 16.5 Feature Verification Rows

Mixed rows are verified when the relevant feature is implemented. Record the exact tested behavior and revision; do not claim a row passed for functionality that was not exercised.

| ID | Check | Covers |
| --- | --- | --- |
| V-VAL-1 | EditMode: solvable 3 x 3, blocked corridor, node and time budgets, cancellation, ice cases. | AC-VAL-2, AC-VAL-3, AC-VAL-6, AC-VAL-7, AC-VAL-8, AC-BLK-03.5 |
| V-VAL-2 | EditMode: arrow sides, non-monotone test hook, unsupported effect and block limit, hook registration. | AC-VAL-18, AC-VAL-19, AC-VAL-20, AC-BLK-01.5 |
| V-VAL-3 | EditMode: symmetry, determinism, timer independence, solver and runtime parity. | AC-VAL-4, AC-VAL-5, AC-VAL-17 |
| V-VAL-4 | EditMode: structural errors stop the pipeline; cache reuse and stale results. | AC-VAL-1, AC-VAL-11, AC-VAL-21 |
| V-VAL-5 | EditMode: allocation does not grow with the node count. | AC-VAL-23 |
| V-VAL-6 | EditMode: every database level is Solvable and its record matches. | AC-VAL-22 |
| V-VAL-7 | Manual: the corridor level's dialog, focus on Cancel, byte-identical Cancel, Save anyway record and badge. | AC-VAL-12, AC-VAL-13 |
| V-VAL-8 | Manual: a Solvable save, Show solution, stepping, stop on edit, no Undo or dirty change. | AC-VAL-14, AC-VAL-15 |
| V-VAL-9 | Manual and benchmark: the envelope fixtures, repaint, progress, Cancel within 100 ms; numbers in the README. | AC-VAL-9 |
| V-VAL-10 | Manual: entering Play Mode cancels a check, and a check cannot start in Play Mode. | AC-VAL-10 |
| V-VAL-11 | Build: the APK does not list the Validation assembly (run in V-D7). | AC-VAL-10 |
| V-VAL-12 | Manual: Show best attempt highlights never-exited blocks with the caption. | AC-VAL-16 |
| V-VAL-13 | Hallway or self-walkthrough: a first-time user names the problem block from the dialog. | AC-VAL-24, EU-3 |
| V-BST-1 | EditMode: booster gating, targeting, charges, Won from a booster, same-tick ordering (fake ports). | AC-BST-0.1 to AC-BST-0.6 |
| V-BST-2 | EditMode/PlayMode: main 0:03 stays fixed while a separate visible counter starts at 10 and shows 6 after four Playing seconds; at 0 main resumes. Check no main increment, partial-tick expiry, both clocks during pause/saving/background, duplicate activation rejection and teardown cleanup. | AC-BST-1.1 to AC-BST-1.5 |
| V-BST-3 | EditMode: Hammer removes plain/Arrow I3 as one whole piece; Ice shell removal preserves its block and clears only its own counter; no geometric children or same-hit cascade; Restart restores authored board but not stock. Rocket refuses split/final-cell targets; Vacuum selects color. | AC-BST-2.1 to AC-BST-2.5, AC-BST-2.7, AC-BST-3.1 to AC-BST-3.4, AC-BST-4.1 to AC-BST-4.3 |
| V-BST-4 | PlayMode and manual: HUD states, frost timer style, recomposed visuals, Vacuum outline/suction, tweens and pools after teardown, restart rebuild. | AC-BST-0.7, AC-BST-1.5, AC-BST-3.5, AC-BST-3.6, AC-BST-4.4 to AC-BST-4.7 |
| V-BST-5 | Phone Profiler during each booster and gate fragments: 0 B per tick, no Instantiate/Destroy or pool growth, totals within warmed piece/block/effect headroom even with replenished Rocket stock, mallet/obstacle cleanup. | AC-3.18, AC-BST-0.8, AC-BST-2.6, AC-BST-2.7, AC-BST-3.6, RISK-21 |
| V-BST-6 | EditMode: pre-level none/either/both choices, same Rocket definition/count, deterministic random valid target, no eligible target, shared one-cell outcome and no reroll/reapply after pause or double Play. | AC-BST-0.3, AC-BST-0.11, AC-BST-3.7, AC-BST-3.8 |
| V-BST-7 | EditMode and PlayMode: Extra Time bonus once, opening command order and countdown/input hold, pause/resume progress, teardown cancellation, selection reset without stock refill/refund on Restart/Next Level, isolated editor-test-play stock, timer above 300 s. | AC-5.7, AC-BST-5.1 to AC-BST-5.3 |
| V-BST-8 | Fake storage and device adapter: core win/toggle and purchase/use snapshots, publish-after-commit, failed writes before/during replacement, retry without duplicate arithmetic, immediate kill/reload, no refund/replay of committed interrupted use. Verify accepting-tick early return, effect before resumed timer, background/no-auto-resume, Home/Restart abandonment, opening commit before Playing, backgrounded Home commit followed by explicit Play without reroll/debit or close without refund, generation guard, seed-once/shared stock and test-play isolation. | AC-6.5 to AC-6.13, AC-BST-0.3, AC-BST-0.9 to AC-BST-0.11 |
| V-BST-9 | PlayMode/device clip: final door exit, Hammer and Vacuum complete their visible Finish within 0.8 foreground seconds while domain/input/both clocks remain stopped. Cover missing visual, timeout, background and scene unload; all pools/tweens clean, static capture/fallback behind popup, failed-result capture and capture reuse/release without leaks. Slow/failing win save cannot extend Finish; Retry commits +10/progression once and gates navigation. | AC-3.18, AC-4.9, AC-4.12, AC-4.13, AC-5.4, AC-6.14, AC-BST-4.4, NFR-L2, NFR-L4 |
| V-ICE-1 | EditMode: frozen drag rejected, counter per exit, thaw at 0, Hammer and booster interactions, restart restore. | AC-BLK-03.1 to AC-BLK-03.3, AC-BLK-03.6 |
| V-ICE-2 | Editor manual plus round trip: add, set, badge, save the canonical level JSON, reopen; the file retains the Ice record. | AC-BLK-03.4 |
| V-ICE-3 | PlayMode and Frame Debugger: one ice material, overlay follows the footprint, label changes only on change, pools and tweens clean after teardown. | AC-BLK-03.3, AC-BLK-03.6, AC-BLK-03.7 |
| V-ARW-1 | EditMode: axis-limited movement and exit sides for I3, L3, 1x1. | AC-BLK-01.1, AC-BLK-01.2, AC-BLK-01.6 |
| V-ARW-2 | Editor manual plus round trip: axis in two clicks, badge, save the canonical level JSON, reopen; the file retains the Arrow record. | AC-BLK-01.3 |
| V-ARW-3 | PlayMode and manual: the marker follows the axis, drags, and exits and returns to its pool. | AC-BLK-01.4 |

### 16.6 Extensibility Verification Rows
| ID | Check | Covers |
| --- | --- | --- |
| V-EXT-1 | Review of the changed-file list for the ice and arrow checkins against the core list. | AC-EXT-1 |
| V-EXT-2 | EditMode: registry build, duplicate ID, completeness, implemented-type registration and board events without listeners. | AC-EXT-2, AC-EXT-3, AC-EXT-8, AC-EXT-10 |
| V-EXT-3 | Editor manual: unknown effect badge and blocked Save; Effects panel fields, clamping, Undo, badges. | AC-EXT-4, AC-EXT-5 |
| V-EXT-4 | EditMode and phone Profiler: 0 B tick with ice and arrow; restart and next level rebuild effect state. | AC-EXT-6, AC-EXT-7 |
| V-EXT-5 | README review. | AC-EXT-9 |

### 16.7 Brief Acceptance Criteria Coverage (G-1)
| Brief AC | Verified by | Brief AC | Verified by |
| --- | --- | --- | --- |
| AC-1.1 | V-M6 | AC-4.1 | V-E1, V-M6 |
| AC-1.3 | V-M6 | AC-5.2 | V-E10, V-M6 |
| AC-1.4 | V-M11, V-D3 | AC-5.3 | V-E11, V-M6 |
| AC-2.1 | V-M8 | AC-6.1 | V-E12, V-M7 |
| AC-2.3 | V-M8 | AC-7.1 | V-M17, V-M10 |
| AC-3.5 | V-E7, V-M6 | AC-7.6 | V-M16 |
| AC-8.1 | V-E13, V-E17 | AC-8.4 | V-M12 |

### 16.8 Coverage and Gate Evidence Rules
- Coverage rule: every AC and NFR ID appears in the Covers column of at least one verification row in 16.1 to 16.6. Expand ranges and fail the document check on gaps.
- An acceptance criterion passes only when its applicable verification rows run green on the current revision. Record the exact tested subset of a mixed row. A latest failing result overrides an earlier pass.
- The finished vertical slice requires all 14 brief criteria, every requirement in 4.1 and 4.3, five editor-authored levels including Ice and Arrow, and the README/APK checks. Each mechanic the user selects for implementation adds its own acceptance and verification rows to that gate. An unfinished requirement is reported as unfinished.

## 17. Open Questions
Defaults in force are DEC rows. A resolved question keeps its ID and points to its decision.

| ID | Question | Status | Decision in force |
| --- | --- | --- | --- |
| OQ-1 | PrimeTween or USS transitions for UI animation? | Decided | DEC-45 |
| OQ-2 | Which device is the NFR-2 reference? | Record the tested model and conditions | DEC-46 |
| OQ-3 | `Clock` or `ic_time` for the HUD timer? | Decided | DEC-47 |
| OQ-4 | Ask the Talent Team anything? | Decided | DEC-52 |
| OQ-5 | Ship dev-tool configs in the archive? | Decided | DEC-48 |
| OQ-6 | Start the timer on the first touch instead? | Open; revisit after the phone playtest | DEC-22 |
| OQ-7 | Door spans longer than 3 in shipped levels? | Decided | DEC-49 |
| OQ-8 | Exit automatically on contact with a fitting door? | Closed 2026-09-28: yes, for the held block (PROJECT.md DEC-164) | DEC-50 |
| OQ-9 | Is a first-time person available for the hallway test? | Open until the usability check | DEC-51 |
| OQ-10 | Do `Arrow_1` to `Arrow_3` act as end and middle pieces or as whole arrows of 1 to 3 cells? | Inspect the import before marker implementation | DEC-82 |
| OQ-11 | Is `ModularBlock_Base` part of block composition? | Resolved: absent from the imported source. The four supplied tile meshes cover the footprint using vertex neighborhoods; no new Base is modeled. See docs/art/board-kit/README.md. | DEC-57 |
| OQ-12 | Remaining package/Player Settings targets in DEC-61 to DEC-64? | Android IL2CPP/ARM64 approved and applied under DEC-94; DEC-96 retains current mobile quality. Track only unresolved targets, without asking again for these completed choices. | DEC-61 to DEC-64, DEC-94, DEC-96 |
| OQ-13 | What are the starting stock and purchase tuning defaults? | Adopted for playtesting: seed 1 once per available definition; 10 coins buys 1 charge. Adjustable design defaults, not user-specified prices | DEC-78, DEC-85 |
| OQ-14 | Is the unattached `Key 1` geometry usable as a key visual? | Open; check when key work is selected | Appendix section B |
| OQ-15 | Does in-game Time Freeze add seconds, suppress countdown, or both? | Resolved explicitly: hold the main clock, open a separate 10-second countdown, then resume the main clock | DEC-76 |
| OQ-16 | Exact Extra Time bonus and Time Freeze duration? | Secondary Freeze timer is 10 Playing seconds, paused with the main timer; separate pre-level Extra Time +10 s remains adjustable case tuning | DEC-76, DEC-84 |
| OQ-17 | Does Rocket remove a cell or an entire multi-cell block? | Resolved: one 1x1 cell only; refuse the last cell so the block survives | DEC-74; same rule in both contexts |
| OQ-18 | Should the zero-stock Buy action spend coins and grant charges? | Resolved: yes; functional coin purchase with persistent stock | DEC-85; tuning in OQ-13 |
| OQ-19 | Can Hammer create multiple connected components? | Superseded: Hammer removes a semantic layer/piece or obstacle, not a grid cell. No geometric split or new child blocks | DEC-75; future layers reveal authored lower content only |
| OQ-20 | What does Hammer remove on active Ice? | Resolved under the one-obstacle rule: the complete Ice shell, clearing its counter and preserving the underlying block; no same-hit cascade | DEC-75 |

### 17.1 Revision note — 2026-09-24

- v7: one completion scope for the playable case, editor, validation, functional boosters, Ice and Arrow. The user selects additional mechanics and implementation order.
- v8: the previous asset-authoritative database and separate JSON interchange design. Superseded by v9.
- v9: `LevelDatabase.json` is the sole authored level source. `LevelDatabase.asset` remains a direct `TextAsset` reference wrapper for Onity and scene Play. The editor writes the JSON through Create/Delete/Save and reloads that file; player saves remain separate JSON. The current foundation covers ID, name, board, timer, doors and blocks. It writes empty `effects[]` and `cellObjects[]` and rejects nonempty values until typed mechanics data exists. Provenance, validation records, visual editing and Play-from-editor remain planned work, not claims of current implementation.
- The requirements and verification rows remain plans until implementation produces evidence. Rendering requirements state the target behavior directly (DEC-96).

## 18. Glossary
| Term | Meaning |
| --- | --- |
| Attempt | One run of a level, from build to teardown. Restart creates a new attempt. |
| Session | The object that owns one attempt's state, disposables, and cancellation. |
| Tick owner | The one class that implements `IOnityTickable` and ticks gameplay only in `Playing`. |
| Required block | A block that must leave for `Won`. Every block unless an implemented effect excludes it. |
| Footprint | The cells a block covers relative to its origin, stored in the canonical level JSON and represented by parsed `LevelData`. |
| Edge segment | One cell-long piece of the board boundary. Doors cover segments, not cells. |
| Board events | `BlockExited`, `BlockRemoved`, `MoveCommitted`, `FootprintChanged`, published in the command phase. |
| Level database | A folder under `Data/Json/Levels` holding `manifest.json` and `pack-NNN.json` files with the ordered records (DEC-196); the project scope installer references the active manifest for the game, with no ScriptableObject wrapper (DEC-194). |
| Editor stamp | The marker the editor writes on each Save, proving a level came from the editor. |
| Piece | One modular FBX part (Center, Edge, OuterCorner, InnerCorner, Wall, DoorSegment, and others) with one prefab. |
| Block-piece / layer | A semantic gameplay piece, potentially spanning many grid cells. Hammer removes one exposed piece or obstacle; this is different from a modular render piece and from Rocket's 1x1 cell. |
| Piece catalog | The data asset that maps a piece type to its prefab and calibration. |
| Composer | Plain C# that turns a footprint or a board edge into piece placements by neighbor rules. |
| Effect | A mechanic attached to a block, door, or cell object, stored as `{type, params}`. |
| Hook | One of the ten extension interfaces (XP-1 to XP-10) through which effects change core behavior. |
| Booster | Shared category/system for Time Freeze, Rocket, Hammer, Vacuum and Extra Time. Usage context determines availability and target acquisition, not a separate effect implementation. |
| In-game booster | One of four HUD placements: Time Freeze, Rocket, Hammer, Vacuum. The earlier conversational word "skill" refers to these; it is not another class/system. |
| Pre-level booster | Extra Time or Rocket selected in the Level popup and automatically applied at the opening of that attempt. |
| Solver | The editor-only bounded search that decides whether a level can be solved. |
| Test play | Playing a level from the editor through a GUID override and an in-memory save store. |
| Save service | The only owner of persistent data: coins, booster stock, current level, toggles. |
