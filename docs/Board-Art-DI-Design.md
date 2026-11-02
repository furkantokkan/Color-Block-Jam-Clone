# Board Art Dependency Injection: Design and Task Contract (revised)

Decision owner verdict for the user request of 2026-09-25 ("every block carries too many unnecessary ScriptableObject references; pass them from one main system through Onity DI"). Paths are relative to `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026/Assets/ColorBlockJam/` unless noted. Re-verified at 02:08 against `BlockPiece.cs`, `BlockPieceFactory.cs`, `ModularBlockView.cs`, `BoardPieceView.cs`, `BlockShapeLibrary.cs`, `BlockPieceState.cs`, `ShapePrefabAuthoring.cs`, `ShapeLibraryAuthoring.cs`, `ShapeLibraryTransaction.cs`, `BoardKitPreview.cs`, `EnvironmentView.cs`, `EnvironmentPiecePlacement.cs`, `LevelPreviewHost.cs`, `LevelPreviewBlockMapper.cs`, `LevelPreviewOwner.cs`, the four test asmdefs, `MAT_Board.mat`, `Game.unity`, Onity `OnityContainer.cs` / `PrefabComponentPool.cs`, and the six task contracts.

Historical: `BlockPiece.TryRemoveCell`, referenced below, was removed on 2026-09-28 (DEC-178); a Rocket split now reaches `BlockPiece.SetCells`/`TrySetCells`.

Superseded in part by DEC-193 (2026-09-29): `BoardArt` holds the palette and the piece catalog only, the piece library is editor-only, and every block root is the one `ModularBlock.prefab` bound as `BoardPieceCatalog.BlockRoot`; the per-shape prefabs, `BlockShapeType` and `SourceType` named below no longer exist.

## Critique disposition

| # | Point | Verdict | Change |
|---|---|---|---|
| 1 | Signature bump regenerates only the 11 active shapes, rewrites JSON/library/undo, and recreates `GeneratedShape` fileIDs | Valid (`SyncPrefabs` commits `library.Shapes`; `Save` does `DestroyImmediate` + `InstantiatePrefab`) | Migration is `AssetDatabase.ForceReserializeAssets` on an exact list (precedent `VacuumMotionAssets.cs:94`). No signature bump, no resave menu, no `Save` call. |
| 2 | `LevelPreviewBlockMapper.cs:53` compile break; LevelPreview is a live writer (02:03-02:05); `LevelPreviewHost` ctor + 4 test call sites | Valid | Hard `WAITING_FOR_OWNER` on level-editor-scene-preview; its files enter the allowed list narrowly. |
| 3 | environment-rule-set-grid overlaps composer/database; board-kit is `ready`/`verification`, last write 01:55 | Valid | Added as execution dependency; ownership section corrected. |
| 4 | `FloorBase.prefab` will carry stale keys | Valid | `Prefabs/Environment/FloorBase.prefab` joins the reserialize list; `Prefabs/Environment/**` is otherwise still forbidden. |
| 5 | Unlisted breaking tests; `SetShape` removal kills `ShapeType`; EditMode asmdef lacks `Onity.DI` | Valid | `SetShape(BlockShapeLibrary.Shape)` kept without a library field; all listed tests enumerated; container-symmetry test moves to the PlayMode asmdef, which already references `Onity.DI`. |
| 6 | `RenderEnvironment` tints only swatches | Valid | Per-name colour map added. |
| 7 | `OnityContext.m_installers` must list the installer | Valid (`Game.unity:154-155`) | Step added. |
| 8 | No `FromInstance` | Valid | `container.BindInstance<BoardArt>(art)`; `RegisterBuildCallback(Action<IResolver>)`. |
| 9 | `Awake` runs at `Instantiate`; never in edit mode | Valid | Renderer cache stays lazy in `ApplyState`; `Awake` claim dropped. |
| 10 | SourceType-only ownership check collides with `Shapes/PrefabTests_*` | Valid | Unbound branch scans the exact folder only (no subfolders); bound branch uses `library.GetPrefab(type) == candidate`. |
| 11 | Removing `m_environment` orphans Arrow_1..3 | Valid | `BoardPieceCatalog.m_environment` stays. Not in scope. |
| 12 | Untinted placement constructor becomes a stale-colour trap; `EnvironmentView.cs:80` comment | Valid | Untinted constructor and `IsTinted` removed; `EnvironmentView` always calls `SetColor`. `EnvironmentView.cs` and `EnvironmentPiecePlacement.cs` are allowed narrowly, after the two environment tasks land. |
| 13 | `Load()` clears the cache | Valid | Build callback reads `Library.Shapes` (calls `LoadIfNeeded`, `BlockShapeLibrary.cs:54-60`) through `BoardArt.Validate`. No Runtime edit. |

Dropped from the draft: signature version 2, `BoardKitMigration` resave menu, `m_environment` removal, the `Awake` ordering claim, the "level-editor-scene-preview is queued only if live" wording.

## Decision summary

1. **Views take resolved `Color`, never a palette.** `BoardPieceView`, `ModularBlockView` and `BlockPiece` lose every ScriptableObject field. The palette is read in exactly two owners: `BlockPieceFactory` (blocks) and `EnvironmentDatabase` through `EnvironmentComposer` (environment). One MPB writer per renderer (PROJECT.md Performance Rule 1, DEC-96).
2. **One provider, `BoardArt`**, an immutable plain C# class in `ColorBlockJam.Presentation` holding `GamePalette`, `BoardPieceCatalog`, `BlockShapeLibrary`. `BoardArtInstaller : MonoInstaller` on the Game scene `SceneScope` binds it with `BindInstance` and loads the shape library in a build callback. Runtime stays independent of Presentation.
3. **Pooled instances are fed once by their owner at warm-up** through method parameters. Onity v0.3.13 pools do not inject (`PrefabComponentPool.CreateInstance` is `Instantiate` + `SetActive(false)`), and per-Get `container.Inject` would allocate in `Playing`.
4. **Editor tools construct `BoardArt` by hand** from `AssetDatabase` loads (precedent: `LevelPreviewHost` `Construct(m_database)`, `new BlockPieceFactory(m_library, ...)`).
5. **Serialized migration is a force-reserialize of an exact asset list.** Unity drops the fields the classes no longer declare; GUIDs, fileIDs, signatures, `BlockShapeLibrary.asset`, the JSON and the undo stack are untouched.
6. **This task runs last in the current queue:** after board-kit releases and after level-editor-scene-preview, floor-base-per-tile and environment-rule-set-grid land.

## 1. Shared provider and the fields that disappear

### 1.1 `BoardArt` (new, `Presentation/Board/BoardArt.cs`)

```csharp
/// <summary>The one owner of shared board art data. Bound once per Game scene; editor tools build it by hand.</summary>
public sealed class BoardArt
{
    public BoardArt(GamePalette palette, BoardPieceCatalog catalog, BlockShapeLibrary library); // ArgumentNullException
    public GamePalette Palette { get; }
    public BoardPieceCatalog Catalog { get; }
    public BlockShapeLibrary Library { get; }
    public Color GetColor(PaletteColor color) => Palette.GetColor(color); // GamePalette throws on an unknown value
    public Color ArrowColor => Palette.ArrowColor;
    /// <summary>Reads Library.Shapes (LoadIfNeeded, cache preserved), checks 4 catalog kinds and 13 palette colours.</summary>
    public bool Validate(out string error);
}
```

No Onity reference; no mutable state.

### 1.2 `BoardArtInstaller` (new, `Presentation/Board/Composition/BoardArtInstaller.cs`)

```csharp
public sealed class BoardArtInstaller : MonoInstaller
{
    [SerializeField] private GamePalette m_palette;
    [SerializeField] private BoardPieceCatalog m_catalog;
    [SerializeField] private BlockShapeLibrary m_library;

    public override void InstallBindings(OnityContainer container)
    {
        // Missing assets throw InvalidOperationException, as ProjectScopeInstaller does.
        var art = new BoardArt(m_palette, m_catalog, m_library);
        container.BindInstance<BoardArt>(art);                       // OnityContainer.cs:208
        container.RegisterBuildCallback(_ =>                          // OnityContainer.cs:260
        {
            // JSON parsing happens here on scene build, never inside Get during Playing.
            if (!art.Validate(out string error)) throw new InvalidOperationException(error);
        });
    }
}
```

The installer's three serialized fields become the only place in `Prefabs/**` or `Scenes/**` that references these assets, besides `EnvironmentDatabase.asset` (palette). Placement: the `SceneScope` GameObject in `Game.unity`, added both as a component and as an entry in `OnityContext.m_installers` (`Game.unity:154-155`). `ColorBlockJam.Presentation.asmdef` gains `Onity.DI` and `Onity.Unity`.

Rejected: binding in `ProjectScopeInstaller` (Runtime cannot reference Presentation for the catalog); an installer in `ColorBlockJam.Environment` (wrong composition root); a new assembly (a folder split, forbidden by PROJECT.md).

### 1.3 Fields removed and kept

| Component | Removed | Replacement |
|---|---|---|
| `BlockPiece` | `m_library`, `Library`, `m_defaultColor`, `Prepare(int)` | `Prepare(BoardPieceCatalog, int, BlockShapeLibrary.Shape)` for warm-up and `Prepare(BoardPieceCatalog, int, IReadOnlyList<Vector2Int>, Color)` for shipped footprints; `SetShape(BlockShapeLibrary.Shape)`; `SetColor(Color)`; reset colour is `Color.white` |
| `ModularBlockView` | `m_catalog`, `m_palette`, `m_shapes`, `SetShape(BlockShapeType, PaletteColor)` | `Prepare(BoardPieceCatalog, int)`, `SetCells(IReadOnlyList<Vector2Int>, Color)`, `SetColor(Color)` |
| `BoardPieceView` | `m_palette`, `m_colorType`, `m_useArrowColor`, `[ExecuteAlways]`, `OnEnable`, `OnValidate`, `ResetState` | `SetColor(Color)` is the single MPB write; `OnPoolRelease` clears clip fields without a GPU write |
| `EnvironmentPiecePlacement` | untinted constructor, `IsTinted` | every placement carries `Tint` |
| `EnvironmentDatabase` | nothing | **adds** `m_floorColor = PaletteColor.Ground`, `m_wallColor = PaletteColor.Wall`, `GetFloorColor()`, `GetWallColor()` |

Kept: `BlockPiece.m_sourceType`, `m_view`, `m_authoredVisual`, `m_generatedSignature`, `ShapeType`; `BoardPieceView.m_filter`, `m_renderer`; `BoardPieceCatalog.m_environment` (the only asset reference to Arrow_1..3, reserved for the Arrow effect); `EnvironmentView` serialized roots.

Reference count after the change: `Prefabs/**` holds zero references to the palette (`e13fe037917f25e4e9b6f520b747555f`), catalog (`baefb98cfae26c648b5c2cd923368ba5`) or library (`15e437ff7d9c59d4daa9be1f9a882770`); today it is 136 + 14 + 3 + 1 (Environment FloorBase).

## 2. How instances receive what they need

### 2.1 `BoardPieceView`

```csharp
public sealed class BoardPieceView : MonoBehaviour, IPoolHooks
{
    [SerializeField] private MeshFilter m_filter;
    [SerializeField] private MeshRenderer m_renderer;
    private MaterialPropertyBlock m_properties; private MeshRenderer[] m_renderers;
    private Color m_color = Color.white; private Vector4 m_clipPlane; private bool m_clipEnabled;

    public void SetColor(Color color) { m_color = color; ApplyState(); }
    public void SetClipPlane(Vector4 plane, bool enabled) { m_clipPlane = plane; m_clipEnabled = enabled; ApplyState(); }
    public void OnPoolGet() { }
    public void OnPoolRelease() { m_clipPlane = Vector4.zero; m_clipEnabled = false; } // hidden object; no GPU write
    private void ApplyState()
    {
        if (m_renderer == null) return;
        if (m_properties == null) m_properties = new MaterialPropertyBlock();
        if (m_renderers == null) m_renderers = GetComponentsInChildren<MeshRenderer>(true); // first call is the warm-up Get on Home, or edit mode
        ...
    }
}
```

Contract: every owner calls `SetColor` in the same call that `Get`s the piece. Both owners do after this change (`ModularBlockView.SetCells`; `EnvironmentView.Build` with every placement tinted). Per tile Get: 1 MPB write, 0 palette scans (today 3 writes, 2 scans).

### 2.2 `ModularBlockView`

`Prepare(BoardPieceCatalog catalog, int cellCapacity)` reads the four tile prefabs once and builds the four sub-pools; the catalog is not retained. `SetCells(cells, Color)` and `SetColor(Color)` forward the resolved colour. Pool names stay as they are (Prepare runs on Home).

### 2.3 `BlockPiece`

- Warm-up: `Prepare(catalog, cellCapacity, shape)` → `state.SetShape(shape)`, `m_view.Prepare(catalog, cellCapacity)`, `m_view.SetCells(state.Cells, Color.white)`. `ShapeType` stays `shape.Type`.
- Shipped: `Prepare(catalog, cellCapacity, resolvedCells, color)` as today, with `Color`.
- `SetShape(BlockShapeLibrary.Shape shape)` keeps `ShapeType` semantics (`BlockPieceState.SetShape` sets it; `SetCells`/`TryRemoveCell` null it, `BlockPieceState.cs:39,105`).
- `ResetForPool`: `m_color = Color.white`. Nothing renders until the next `SetColor` + `SetCells`.

### 2.4 `BlockPieceSpawnData`

`Color` stays `PaletteColor`. The factory resolves it: `piece.SetColor(m_art.GetColor(data.Color))`. Domain and level JSON never see `UnityEngine.Color`.

## 3. How pools and factories obtain the provider

- **`BlockPieceFactory(BoardArt art, Transform parent)`.** `Prepare(sourceType, count, cellCapacity)`: `var shape = art.Library.Get(sourceType)`; validate `prefab.SourceType == sourceType && art.Library.GetPrefab(sourceType) == shape.Prefab`; warm loop calls `piece.Prepare(art.Catalog, cellCapacity, shape)`. `Create`: `SetColor(art.GetColor(data.Color))`, then `SetShape(shape)` when `ResolvedCells == null`, else `SetCells(data.ResolvedCells)`. The future level session receives `BoardArt` by constructor injection and news the factory with its board root (Onity has no `WithArguments`). No runtime call site exists yet.
- **`EnvironmentView`.** Wiring unchanged (`[Inject] Construct(EnvironmentDatabase)` under `SceneScope`). Its pools need no provider: `EnvironmentComposer` tints floor placements with `GetFloorColor()`, wall/corner outputs with `GetWallColor()`, doors/arrows as today, and the floor-base tile (after floor-base-per-tile) with `GetFloorBaseColor()`. `EnvironmentView.Build` drops the `if (placement.IsTinted)` branch and the stale comment at L80.
- **Why not inject pooled instances.** `PrefabComponentPool.CreateInstance` is a bare `Instantiate`; `IPoolHooks` carries no parameters; `OnityContext.InjectGameObject` uses `GetComponentsInChildren` and scans only its own hierarchy. Per-Get injection would allocate in `SetCells` during `Playing` (Rocket `TryRemoveCell`), breaking Performance Rules 3 and 5.

## 4. Editor paths without a container

| Tool | Change |
|---|---|
| `ShapePrefabAuthoring.Save` L74-81 | Remove the `SerializedProperty` writes of `m_catalog`, `m_palette`, `m_shapes`, `m_library`. Keep `m_sourceType`, `m_view`, `m_authoredVisual`, `m_generatedSignature`. `ValidateSave` still requires catalog, palette and template (the template must be composable). `Signature` unchanged. |
| `ShapePrefabAuthoring.ValidateDestination` L136-143, L150-153 | Unbound branch: scan only prefabs whose directory equals `folder` (no `PrefabTests_*` subfolders) and match `candidate.SourceType == type`. Bound branch: `library.GetPrefab(type) == asset` or, for the default path, `owner.SourceType == type` inside `folder`. Duplicate detection and the Prefab Mode guard stay. |
| `BoardKitAssets.Build` | Remove L181-183 (template refs), L268-269 and L296-297 (piece palette/colour). Keep palette/library/material/catalog creation, `m_environment` write and `ExportPalette`. Not run for the migration (it recreates piece GameObjects). |
| `BoardArrowAssets.Calibrate` L74-77 | Remove `m_palette` / `m_useArrowColor` writes; keep the height calibration. |
| `BoardKitPreview.RenderAll` L21-27 | `view.Prepare(catalog, cap); view.SetCells(library.Get(type).Cells, palette.GetColor(color))`. |
| `BoardKitPreview.RenderEnvironment` L57-93 | Add a name→colour map: Wall/Corner_1/corner_3/corner_5 → `Wall`, Grid → `Ground`, Door_1x1/Door_Arrow → `Orange`, Arrow_1..3 → `ArrowColor`; call `SetColor` for every root, not only swatches. |
| `PieceEditorWindow` preview (around L513-525) | Load the catalog next to palette and library; `Prepare(catalog, ...)`; `SetCells(cells, palette.GetColor(...))`. Grid fill unchanged. |
| `LevelPreviewOwner` L165-176, `LevelPreviewHost` ctor + L299, `LevelPreviewBlockMapper` L53, `LevelPreviewTests` L87/142/171/201 | Owner loads palette and catalog; host takes `BoardArt` instead of `BlockShapeLibrary` and does `new BlockPieceFactory(m_art, m_blocksRoot)`; mapper checks `piece.SourceType == shape.Type && art.Library.GetPrefab(shape.Type) == piece.gameObject`. Recorded in that task's contract history line. |
| New `BoardKitMigration.Reserialize` menu (`Editor/BoardKit/BoardKitMigration.cs`, "Color Block Jam/Art/Reserialize Board Kit Prefabs") | `AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssets)` over the exact list in section 5. Idempotent; kept for future field removals. |
| Tests | `new BoardArt(...)` from production assets in EditMode; `new OnityContainer()` + `BindInstance` + `Build()` + `Inject` in the PlayMode asmdef (`ColorBlockJam.Presentation.PlayModeTests` already references `Onity.DI` and `Onity.Unity`; the EditMode asmdef does not and is not changed). |

Edit-mode tint: `[ExecuteAlways]` goes away, so Prefab Mode, Project thumbnails and the `GeneratedShape` preview children render `MAT_Board.mat`'s default `_BaseColor`, which is white (verified). Every designer-facing preview (Piece Editor, BoardKitPreview, LevelPreview) tints explicitly. No `#if UNITY_EDITOR` palette lookup goes into a runtime assembly.

## 5. Serialized migration

Each step is a High-risk serialized write and needs the user's target approval, granted once for the exact list in the contract.

1. Code changes compile (Presentation asmdef, class field removals, editor writer removals, environment tints).
2. **Reserialize**, one call: 34 `Prefabs/Board/Shapes/Piece_000..033.prefab`, 14 `Prefabs/Board/Pieces/*.prefab`, `Prefabs/Board/ModularBlock.prefab`, `Prefabs/Environment/FloorBase.prefab`. Same path, same GUID, same fileIDs, same `m_generatedSignature`; only the removed keys vanish. `BlockShapeLibrary.asset`, `PieceDatabases/BlockShapeLibrary.json`, `BoardPieceCatalog.asset`, `GamePalette.asset` and `Environment.prefab` are not touched. `IsCurrent` stays true for every bound shape because the signature hashes only type, name and cells.
3. **`EnvironmentDatabase.asset`**: the two new colour fields take their defaults; save once so the YAML records them.
4. **`Game.unity`**: add `BoardArtInstaller` to `SceneScope`, assign the three assets, append it to `OnityContext.m_installers` (MCP for Unity `manage_components` / `manage_gameobject`, or the Editor by hand).
5. Grep proof, tests, captures (section 7).

Rollback: Plastic history (`cs:5` base). Expected `cm status`: 34 + 14 + 1 + 1 prefabs, 1 asset, 1 scene, plus code and asmdef.

## 6. Risks

| Risk | Mitigation |
|---|---|
| Stale colour on Get once `ResetState` is gone | Structural: both owners tint every placement (no untinted constructor remains). PlayMode test: release a red tile, re-Get it, `SetColor(blue)` → blue. |
| Two MPB colour owners (Rule 1, DEC-96) | After the change exactly one writer per renderer: `BoardPieceView.ApplyState`, called only by its owner. |
| Renderer cache timing | `Awake` runs at `Instantiate` for pooled objects and never in edit mode; the lazy null check in `ApplyState` covers both. |
| Edit-mode previews show white blocks | Accepted for derived prefabs; every designer preview tints explicitly. A tinted `PrefabStage` preview is a separate small task if the user wants it. |
| Reserialize picks a wrong path or a script has not compiled | The menu validates each path exists and each root component type is present before calling `ForceReserializeAssets`; run only after a clean compile. |
| Environment floor/walls now explicitly tinted | Render output identical (same palette colours through the same MPB path). SRP Batcher status unchanged. NFR-P5 can later switch floor/walls to dedicated materials without an MPB. |
| Ownership collisions (four live or queued tasks) | Section 8; auto-dispatch only after every dependency lands. |
| MCP for Unity currently refuses connections (02:08) | Scene edit and test runs stop until the transport is fixed; the isolated `%TEMP%` batchmode copy remains the fallback for tests. |

## 7. Verification

- **EditMode**
  - `BoardKitTests`: `Prepare(catalog, cap)` replaces the `SerializedObject` override at L247 and the `Prepare(int)` calls at L136/L249; the "release resets to Orange" test (L164-184) becomes "release writes no property block; re-Get + `SetColor(blue)` shows blue"; the `(PaletteColor)999` test at L153 moves to `BoardArtTests` (`GetColor` throws); grid colour test (L336) compares to `EnvironmentDatabase.GetFloorColor()`.
  - `ShapePrefabTests`: `Prepare` calls at L127/L459 and `SetShape` at L130/L145/L272 use the new signatures; `IsCurrent` assertions unchanged; L288 still expects `DoubleHorizontal`; L290-296 becomes "returned piece is hidden and the next `Create` shows the requested colour".
  - `ShapeLibraryJsonTests` L139/L153: `library.GetPrefab(shape.Type) == piece.gameObject` and `piece.SourceType == shape.Type`; the GUID snapshot `docs/art/board-kit/json-migration-source.json` and the `BlockShapeLibrary.asset` byte check (L506) stay.
  - New `BoardArtTests`: constructor null checks; `Validate` fails on a missing catalog kind; `GetColor` throws on an unknown value; `Validate` loads `Library.Shapes` without clearing an existing cache.
  - `EnvironmentComposerTests`: every floor and wall placement carries `GetFloorColor()` / `GetWallColor()`.
- **PlayMode**
  - `BoardKitLifecycleTests`: factory from `new BoardArt(...)`, `Prepare` at L57 and `SetShape` at L27 updated; after Create/Return/Dispose every tile and root is pooled with no `Instantiate` after Prepare.
  - `EnvironmentViewLifecycleTests` L95-107 becomes "re-Get with a new tint shows the new tint; release performs no property-block write"; L264 colour reads unchanged.
  - Container symmetry: `new OnityContainer()` + `BindInstance(boardArt)` + `Build()` resolves the same instance; Game scene `SceneContext` build resolves `BoardArt` with `Library.Shapes` already loaded.
  - Allocation: `Is.Not.AllocatingGCMemory()` around `piece.TryRemoveCell(cell)` (one recompose).
- **Grep proof** (repo root):
  - `grep -rlE "guid: (e13fe037917f25e4e9b6f520b747555f|baefb98cfae26c648b5c2cd923368ba5|15e437ff7d9c59d4daa9be1f9a882770)" Assets/ColorBlockJam/Prefabs` → empty.
  - `grep -rlE "m_palette|m_catalog|m_shapes|m_library|m_useArrowColor|m_defaultColor|m_colorType" Assets/ColorBlockJam/Prefabs` → empty.
  - `grep -rln "e13fe037917f25e4e9b6f520b747555f" Assets/ColorBlockJam --include=*.unity --include=*.asset --include=*.prefab` → exactly `Scenes/Game.unity` and `Data/Environment/EnvironmentDatabase.asset`.
  - `grep -rn "System.Linq\|string.Format" Assets/ColorBlockJam/Runtime/Presentation Assets/ColorBlockJam/Runtime/Core` → unchanged from baseline.
- **Visual parity**: Game view captures (1080x1920 and 20:9) of level 1 before and after; one Piece Editor and one BoardKitPreview capture; board pixels identical.
- **Console** clean on compile, reserialize and Game scene open. **Profiler**: no `BoardPieceView` sample outside `Playing`; 0 B GC Alloc over one Rocket removal.

## 8. Ownership and coordination

- `TASK-20260924-191842745-88e64a0d-board-kit`: `Contract status: ready`, `Current phase: verification`; live writer on `Editor/BoardKit/PieceEditorWindow.cs` (01:55) and `Tests/EditMode/BoardKitTests.cs` (01:50); sole writer of the palette, catalog, library, template, the 14 piece prefabs and Piece_000-033. This task is `WAITING_FOR_OWNER` until it releases, then inherits those serialized assets.
- `TASK-20260924-222616741-46522211-level-editor-scene-preview`: `ready`/`planning` on paper, but a live writer on `Editor/LevelPreview/**` and `Tests/LevelPreview/**` (02:03-02:05). Its contract stops if Presentation APIs change (L133), so this task waits for it to land, then makes the narrow host/owner/mapper/test edits and records a history line there.
- `TASK-20260924-224137887-1a445bb0-floor-base-per-tile` (`ready`): owns `EnvironmentView.cs`, `EnvironmentDatabase.asset`, `FloorBase.prefab`, `Environment.prefab`. Lands first; this task then reserializes `FloorBase.prefab` and tints the floor-base placement.
- `TASK-20260924-231413359-d89f8597-environment-rule-set-grid` (`ready`): owns `Environment/**`, `Data/Environment/**`, `Tests/Environment/**` and rewrites the wall rule outputs. Lands first; this task then adds the two colour fields and tints to whatever composer shape exists.
- `TASK-20260924-185655569-dea430a7-play-area-environment` (`verification`): requires board-kit code and palette unchanged; its verifier re-runs `Tests/Environment/**` after this task (history line).
- `remote-level-json`, `cbj-level-import`, `level-json-v2-effects`: disjoint.
- `production/tasks/TASK-20260924-224656127-e361499a-board-art-dependency-injection/` (empty folder, 01:46) receives the contract below.

## 9. Task contract

See `production/tasks/TASK-20260924-224656127-e361499a-board-art-dependency-injection/contract.md`.
