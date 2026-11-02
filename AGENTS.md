# ColorBlockJamCase2026 Agent Notes

Shared project rules for Claude and Codex. Global user instructions still apply;
this file records the project-specific decisions.

## Project

- Read `PROJECT.md` before you change any code. It defines the game states,
  the runtime lifecycle rule (nothing runs unattended), and the decision log.
- Case brief: `docs/Game-Developer-Case-2026.md`.
- Unity `2022.3.62f2` is a case constraint. Do not upgrade the Editor.
- Targets: Android and iOS, portrait, reference resolution 1080 x 1920.
- Branding: Rabbyte Games is reserved for PC games. This is a mobile game;
  do not use the Rabbyte Games name, logo or opening intro in this project.
- Version control: Unity Version Control (Plastic SCM) workspace, not git.

## Art Assets

- The supplied case assets live under `Assets/ColorBlockJam/Art`. A supplied
  file keeps its original name unless that name breaks the naming standard.
  UI sprites follow the standard: lowercase snake_case with a type prefix
  (`bg_` backgrounds and panels, `btn_` buttons, `ic_` icons, `mask_` masks).
  Rename only a sprite that does not follow it. Use `cm mv` with its `.meta`
  so the GUID survives, update every USS/UXML/C#/doc path, and record the
  original name next to the new one in `docs/UI-Asset-Import.md`. Models keep
  their supplied names. Supplied UI art and
  models sit in their own `Supplied` folders, apart from project-authored art
  (UI `Atlases`, `Models/BoardKit`, `Models/Boosters`):
  - `UI/Supplied/Backgrounds`, `UI/Supplied/Panels`, `UI/Supplied/Buttons`, `UI/Supplied/Masks`
  - `UI/Supplied/Icons/{Navigation, Settings, Currency, Gameplay, Boosters}`
  - `Models/Supplied` (FBX: `Arrows`, `BlockParts`, `GroundGrid`, `WallAndDoor`)
  - `Textures` (`icetexture` for the optional ice blocker)
- Everything under `UI` imports as a Single sprite with mipmaps off, alpha is
  transparency on, and wrap mode Clamp. Use the same settings for new UI art.
- Every UI sprite has one of two classes. Record the class, the border, and
  the import settings per sprite in `docs/UI-Asset-Import.md` (created with
  the first UI task; measure borders from the art, do not guess):
  - `nine-slice`: panels and button backgrounds that stretch (`bg_popup`,
    `bg_popup_header`, `bg_popup_info_holder`, `bg_popup_info_holder_2`,
    `bg_home_coin_holder`, `bg_home_resource_holder`,
    `bg_gameplay_time_holder`, `bg_profile_box_blue`, `bg_active_tab`,
    `btn_level_bg_normal`, `btn_level_green`, `btn_popup_blue`,
    `btn_popup_green`, `btn_booster_normal`, `btn_toggle_on`). Set the
    border in the Sprite Editor, Mesh Type Full Rect, and stretch them in UI
    Toolkit with `-unity-slice-left/top/right/bottom`. Never scale them
    non-uniformly without a border.
  - `fixed`: icons, masks, and full-screen backgrounds that keep their pixel
    aspect (`Icons/*`, `Masks/*`, `Backgrounds/*`, `btn_popup_close`,
    `btn_circle_blue_small`, `btn_circle_green_small`, `btn_square_retry`,
    `btn_settings_normal`). Backgrounds use
    `-unity-background-scale-mode: scale-and-crop` so 20:9 crops instead of
    stretches.
  - Common settings: Texture Type Sprite (2D and UI), Sprite Mode Single,
    Pixels Per Unit 100, Pivot Center, sRGB on, Filter Bilinear, Max Size at
    the source size. Sprites packed in a Sprite Atlas import uncompressed
    (Compression None, no platform override); the Sprite Atlases under
    `Art/UI/Atlases` carry the Android and iOS ASTC 6x6 overrides. The
    full-screen backgrounds (`bg_home_screen`, `bg_menu`,
    `bg_block_texture`) stay out of the atlases and keep their own importer
    compression (no ASTC 6x6 override yet; see `docs/UI-Asset-Import.md`). Sprite Atlas packing: Allow Rotation off, Tight
    Packing off, Padding 4, one atlas per screen group (PROJECT.md
    Performance Rule 2).
- `Textures/icetexture.jpg` is a 2048 x 2048 tiling texture with no alpha
  (imports at 2.4 MB as ASTC 6x6). Import as Default texture, sRGB, mipmaps
  on, Wrap Repeat, Max Size 1024, Android and iOS override ASTC 6x6. It
  feeds the ice Shader Graph material (PROJECT.md Architecture Rules).
- The FBX kits are modular. Node names read from the FBX files:
  - `BlockParts.fbx`: meshes `ModularBlock_Center`, `ModularBlock_Edge`,
    `ModularBlock_InnerCorner`, `ModularBlock_OuterCorner`.
    `ModularBlock_Base` is the FBX's material, not a mesh (its FBX node type
    is `Material`), so the kit misses no piece; materials import off.
  - `WallAndDoor.fbx`: meshes `Wall`, `Corner_1`, `corner_3`,
    `corner_3.002` (defective, unused), `corner_4` (unused), `corner_5`,
    `Door_1x1` (the door) and `Door_Arrow` (the door with an arrow marker).
    `Base_Side`, `Gate_Orange` and `Arrow_Gate_Orange` are its materials,
    not meshes; the kit has no three-cell wall run.
  - `Arrows.fbx`: meshes `Arrow_1`, `Arrow_2`, `Arrow_3`.
  - `GroundGrid.fbx`: mesh `Grid`.
  Import with materials off (the project's shared materials apply), no
  animation, Read/Write off. One piece spans one grid cell at the import
  scale (`docs/art/board-kit/README.md`). The door's source materials are
  orange; the game ignores them and tints walls and doors per colour
  through the shared material and `MaterialPropertyBlock`.
- Asset feedback for the README:
  - `WallAndDoor.fbx` mesh `corner_3.002` (747 faces) has two defects: a
    stray flat plane that cuts through the inside of its U-shaped body, and
    one self-intersecting 6-sided polygon, which Unity drops with an import
    warning. The rest of the mesh imports (it shows in the FBX's
    Inspector); no prefab, asset or code
    references it (the board prefabs use `Wall`, `Corner_1`, `Door_1x1` and
    `Door_Arrow`), so it never reaches the board.
    `corner_3`, `corner_4` and `corner_5` are also unused; their prefabs were removed on 2026-09-29.
  - The supplied UI file names mixed casing styles (`Btn_`, `btn_`, `ic_`,
    `bg_`, PascalCase, bare names). 32 sprites were renamed to the standard
    above on 2026-09-29 (DEC-191); `docs/UI-Asset-Import.md` lists each
    original name.

## Unity Editor Control

The Unity CLI Pipeline package (`com.unity.pipeline`) needs Unity 6.0+, so
`unity status`, `unity command`, `unity list`, and `unity mcp` cannot reach this
Editor. Split the work this way:

- Unity CLI: verification and tooling that does not need a live Editor
  (`unity --version`, `unity doctor`, `unity pipeline list`, `unity logs`,
  `unity test`, `unity build`). `test` and `build` need the Editor closed
  because Unity locks the project.
- MCP for Unity (`com.coplaydev.unity-mcp`, pinned `v10.2.0`): live Editor
  control. This project explicitly authorizes it as the live-Editor transport.
  - Server name: `unityMCP`.
  - Claude Code (`.mcp.json`): stdio transport. The client starts the server
    itself with `uvx --offline --from mcpforunityserver==10.2.0
    mcp-for-unity --transport stdio` (uvx at `C:\Users\e-fur\.local\bin`).
    In the MCP for Unity window set Transport to `stdio` while Claude is
    the client; a `Transport Mismatch` warning in the window means the
    dropdown and the client config disagree. Claude Code reads `.mcp.json`
    at session start, so restart the session after changing it.
  - Codex (`.codex/config.toml`): HTTP transport, `http://127.0.0.1:8082/mcp`.
    Set the window Transport to `HTTPLocal` with port `8082` and start the
    local server while Codex is the client.
  - MCP for Unity stores the transport and HTTP URL in machine-wide
    `EditorPrefs` (`MCPForUnity.HttpUrl`), not per project. Check the window
    each time you open this project after you use another project.
  - Verify with the `mcpforunity://instances` and editor-state resources
    before you use tools. If the resource lists the instance but tools
    report "No Unity instances are currently connected", the transport does
    not match: fix the window dropdown and reconnect the session.
- Never apply one mutation through two transports.
- MCP for Unity's runtime assembly is dropped from player builds by
  `Editor/Core/PlayerBuild/PlayerBuildAssemblyFilter` (DEC-185).

## Architecture Stack

- Constants: private k_camelCase; public and internal constants PascalCase;
  no mass renames (DEC-178).
- DI, reactive state, events, and async: Onity (`com.onity.framework`, pinned
  `v0.4.0`). Do not add Zenject/Extenject, VContainer, UniRx, R3,
  MessagePipe, or UniTask.
  - Upgrade verified on 2026-09-27: manifest and lock pin `v0.4.0` at
    `8929214072a7df464708cccb628fb52ab762dc7f`; dependency versions stayed unchanged.
    Unity 2022.3.62f2 compilation passed, `PlayerSaveServiceTests` passed 10/10
    EditMode tests, and `ProjectScopeShapeCatalogTests` passed 4/4 PlayMode tests.
    Android/iOS player builds were not run for this upgrade.
- Tweens: PrimeTween (`com.kyrylokuzyk.primetween` `1.4.11`, npm scoped
  registry). It allocates nothing at runtime, and its license allows the NPM
  install in a shared repository. Do not add DOTween or LeanTween.
- Onity brings Input System `1.19.0`, Entities `1.4.8`, Burst, Collections,
  and Mathematics. Use the Input System for touch input. Do not add legacy
  `Input.*` code: Active Input Handling is "Input System Package (New)" only
  (DEC-188), so legacy calls throw at runtime. The app's one EventSystem,
  with InputSystemUIInputModule, is a child of `ProjectContext.prefab`
  (DEC-199); no scene has one. Do not add one to a scene: UGUI parents a UI
  Toolkit panel's event handler under the EventSystem that saw the panel
  first, so an EventSystem in a scene that unloads (Init, or the Hub or Game
  scene a prepared level replaces) takes the other scenes' handlers with it
  and they stop receiving touches (seen on the phone, DEC-188). Keep the
  module's action fields empty: it then builds its own default actions (the
  shared DefaultInputActions asset breaks after an InputTestFixture test).
- UI: UI Toolkit for every runtime screen (UXML, USS, `UIDocument`,
  `PanelSettings` scaled to 1080 x 1920) and for the level editor
  (`EditorWindow` with UI Toolkit). Do not add UGUI canvases or TextMeshPro
  UI; `com.unity.ugui` stays installed only for the `EventSystem` that the
  Input System UI module needs.
  UI Builder's canvas size is a per-document preview setting stored separately
  from UXML and USS. Set every screen document's canvas to 1080 x 1920 to match
  the runtime `PanelSettings`; use the Canvas header's width and height fields
  to set it. Save the active document through UI Builder. Do not edit its
  `Library/UIBuilder` JSON while the Editor is open: the in-memory
  `BuilderDocument` can overwrite that change. After correcting screens, reopen
  each UXML and verify its own saved canvas dimensions. **Fit Canvas only
  changes the preview zoom.** If elements overlap in UI Builder but render
  correctly in-game, check each UXML's canvas size before changing its runtime
  layout or styles.
- UI authoring: humans must be able to open and edit reusable screens/popups
  directly in UI Builder without running gameplay or toggling visibility and
  reverting it before saving. Keep layout, dimensions, colors, typography and
  static content in modular UXML/USS templates and shared styles. Load the
  required common styles/font in the standalone document and supply clearly
  identified representative text/icons for runtime-bound fields. Runtime owns
  actual offer/player/state data and changes only the fields that must be
  dynamic; samples are never gameplay/economy defaults. Keep the reusable
  document visible and initialize embedded overlays closed before the first
  rendered frame, including delayed/lazy binding and re-enable paths. Scoped
  host-instance visibility rules are valid when needed for pre-bind safety;
  they must not hide the standalone authoring root. Verify standalone and host
  previews plus runtime replacement and open/close behavior. The shared
  purchase popup follows this contract; see `PROJECT.md` UI Authoring.
- Level data: level databases under `Assets/ColorBlockJam/Data/Json/Levels`
  are the sole authored sources and store ordered level records. A database
  is a folder: `manifest.json` lists its level IDs in play order, grouped into
  `pack-NNN.json` files of at most 100 levels (DEC-196). Every manifest and
  pack is a TextAsset entry in the local `Level Database` Addressables group
  at the derived address `LevelDatabase/<name>/<file>`, with the `level-json`
  label. The Level Editor dropdown lists the manifests; selecting one sets the
  Addressables reference `m_levelJson` (`LevelJsonReference`) of
  `ProjectScopeInstaller` on `ProjectContext.prefab` and saves the prefab; no
  ScriptableObject wraps it (DEC-194). Create Database adds a uniquely named
  folder with an empty manifest and selects it. The editor validates and
  writes the selected database directly (only the changed packs, then the
  manifest); it has no Import/Merge/Export workflow. Project scope loads and
  validates the active manifest asynchronously into a plain C# `LevelCatalog`,
  which it binds for Hub and Game, before Init routes to Hub; the Game scene
  loads its level's pack on Home, and the Level Editor keeps its own catalog
  with every level (DEC-192). Attempts copy
  validated values into plain C# state and never mutate authored data. The
  persistent player save is a separate JSON file. Use Newtonsoft Json.NET
  (`com.unity.nuget.newtonsoft-json` `3.2.2`) for both boundaries. If effects
  enter the level schema, register converters explicitly; never enable
  `TypeNameHandling`. Preserve runtime DTOs for IL2CPP with `link.xml`.
  No JSON work runs in the `Playing` tick path.
- Non-allocating helpers (installed on 2026-09-24 on the user's order; any
  further package change is a High-risk step that needs the user's go):
  - ZLinq and NuGetForUnity were removed on 2026-09-29 (DEC-185); gameplay
    code uses plain loops and no `System.Linq`.
  - ZString `2.6.0` from the UPM git URL
    `https://github.com/Cysharp/ZString.git?path=src/ZString.Unity/Assets/Scripts/ZString#2.6.0`.
    Use `ZString.Format` and `ZString.Concat` for runtime HUD and popup
    text and for editor text rebuilt per frame, repaint or pointer move;
    one-shot editor messages, tooltips, dialogs and validation sentences
    may use interpolation instead (DEC-179). The timer still uses a table
    of prebuilt `m:ss` strings built once at level load, so the tick
    allocates nothing.
    ZString needs `System.Runtime.CompilerServices.Unsafe`, which Unity
    2022.3 does not ship and the git path does not include. The project's
    single copy is `Assets/Plugins/Roslyn/System.Runtime.CompilerServices.Unsafe.dll`
    (6.0.0, placed there by MCP for Unity's Roslyn installer); its importer
    is set to Any Platform and auto-referenced so ZString compiles in the
    Editor and in players. Do not add a second copy through NuGet. If the
    Roslyn folder is ever removed, add the `System.Runtime.CompilerServices.Unsafe`
    6.0.0 DLL (Microsoft, MIT) under `Assets/Plugins` by hand.
  - Newtonsoft: `com.unity.nuget.newtonsoft-json` `3.2.2` from the Unity
    registry (see the level-data bullet above).
- Haptics: the standalone `Haptics` assembly and namespace under
  `Assets/Haptics/Runtime` own the Android Vibrator (JNI), iOS UIKit feedback
  (`iOS/Haptics.mm`) and `Android/VibratePermission.androidlib`. This library
  has no game or Onity dependency and can be copied into another project.
  `ColorBlockJam.Haptics` (`Runtime/Haptics`) retains game-event mapping and
  the committed player-setting subscription; gameplay reaches that adapter
  through `ColorBlockJam.Haptics.IHaptics`. See `docs/Haptics.md`. Nice
  Vibrations was removed on 2026-09-29 on the user's order: its
  `liblofelt_sdk.so` is not 16 KB page aligned, so Android 16 showed an
  "Android App Compatibility" dialog and Google Play would reject it for
  Android 15+ targets. Do not add a haptics plugin; check any new native
  library with `.agents/skills/cbj-performance-profiling/scripts/check_16kb.py`.
- IL2CPP: Onity uses source-generated activators with a reflection fallback on
  AOT. Run an Android IL2CPP smoke build before you rely on DI in a device
  build.
