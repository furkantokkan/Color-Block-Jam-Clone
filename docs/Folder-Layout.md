# Folder Layout

Where code and UI Toolkit assets live under `Assets/ColorBlockJam`, and how to
translate a path written before the 2026-09-28 restructure.
Decision: DEC-157 in `PROJECT.md`. Ad-hoc change without a task contract.

## Rules

- **Code lives in three roots, one folder per assembly.**
  - `Runtime/<Area>/`: player assemblies.
  - `Editor/<Area>/`: Editor-only assemblies.
  - `Tests/<Area>/EditMode|PlayMode/`: test assemblies.
- **Nothing nests.** A new assembly gets its own folder at that level; it never
  sits inside another assembly's folder. No runtime assembly sits under a folder
  named `Editor`.
- **`Core` is the main assembly of each root.** `Runtime/Core` holds
  `ColorBlockJam.Runtime` and `Editor/Core` holds `ColorBlockJam.Editor`. The
  names differ from the folders on purpose: renaming an assembly would break
  `[SerializeReference]` data, which stores the assembly name.
- **UI Toolkit files have their own asset folder.** UXML, USS and TSS files live
  in `UI/<Screen>/`, never beside scripts. Editor-only icons and editor USS are
  the exception: they stay beside the editor code that loads them.
- **Asset folders hold no scripts.** This covers `Art`, `Data`, `Prefabs`,
  `Resources`, `Scenes` and `Settings`.
- **Authored JSON lives under `Data/Json/<Kind>/`.** `Levels` holds the level
  databases and `Pieces` the one piece library. No ScriptableObject points at
  them: the project scope installer names the active level JSON (DEC-194) and
  the piece library is a fixed path (DEC-195). See
  [Data JSON folder](#data-json-folder-dec-165).
- **Vendor packages stay where they were installed.** This covers
  `Assets/Plugins`, `Assets/HighlightPlus`, `Assets/Packages`,
  `Assets/TextMesh Pro` and `Assets/UI Toolkit`.

```
Assets/ColorBlockJam/
├─ Runtime/   Core, Domain, Environment, Gameplay, Hub, Player, Presentation, UI
├─ Editor/    Core, Environment, LevelPreview, Validation
├─ Tests/     Editor, Environment, Gameplay, Hub, LevelPreview, Player,
│             Presentation, UI, Validation   (each with EditMode and/or PlayMode)
├─ UI/        Common, Game, Hub, Purchase, Settings, Startup, Themes
└─ Art, Data, Prefabs, Resources, Scenes, Settings
```

| Folder | Assembly |
| --- | --- |
| `Runtime/Core` | `ColorBlockJam.Runtime` |
| `Runtime/Domain` | `ColorBlockJam.Domain` |
| `Runtime/Environment` | `ColorBlockJam.Environment` |
| `Runtime/Gameplay` | `ColorBlockJam.Gameplay` |
| `Runtime/Hub` | `ColorBlockJam.Hub` |
| `Runtime/Player` | `ColorBlockJam.Player` |
| `Runtime/Presentation` | `ColorBlockJam.Presentation` |
| `Runtime/UI` | `ColorBlockJam.UI` |
| `Editor/Core` | `ColorBlockJam.Editor` |
| `Editor/Environment` | `ColorBlockJam.Environment.Editor` |
| `Editor/LevelPreview` | `ColorBlockJam.Editor.LevelPreview` |
| `Editor/Validation` | `ColorBlockJam.Editor.Validation` |

## Old to new paths

The mapping applies the rows top to bottom; the first row whose old path matches
(the path itself or a path under it) wins. All paths are under `Assets/ColorBlockJam`.

| Old | New |
| --- | --- |
| `Editor/LevelPreview/` | `Editor/LevelPreview/` (unchanged) |
| `Editor/Levels/Validation/` | `Editor/Validation/` |
| `Environment/Editor/` | `Editor/Environment/` |
| `Editor/` | `Editor/Core/` |
| `Hub/UI/` | `UI/Hub/` |
| `Gameplay/UI/GameplayHud.uxml`, `.uss` | `UI/Game/` |
| `Runtime/` | `Runtime/Core/` |
| `Domain/` | `Runtime/Domain/` |
| `Environment/` | `Runtime/Environment/` |
| `Gameplay/` | `Runtime/Gameplay/` |
| `Hub/` | `Runtime/Hub/` |
| `Player/` | `Runtime/Player/` |
| `Presentation/` | `Runtime/Presentation/` |
| `UI/ColorBlockJam.UI.asmdef`, `UI/**/*.cs` | `Runtime/UI/` (same subpath) |
| `Tests/EditMode/` | `Tests/Editor/EditMode/` |
| `Tests/PlayMode/` | `Tests/Presentation/PlayMode/` |
| `Tests/LevelPreview/` | `Tests/LevelPreview/EditMode/` |
| other `Tests/<Area>/`, `UI/*` assets | unchanged |

Also deleted:

- `Assets/TutorialInfo/` and `Assets/Readme.asset`, which the URP template left behind.
- The stale `Assets/InitTestScene*.unity`.
- The folder `UI/Haptics/`, which was empty once its script moved.

## How it was done

- **Checkins first.** The move waited until the Hub, Booster, outline and haptics
  sessions had checked in (cs:15 to cs:17) and no session was writing.
- **Whole folders with `cm mv`.** Each folder moved as a unit, followed by its
  `.meta`, so Plastic keeps the history. Before the move, 800 GUIDs were recorded
  and compared afterwards: all unchanged. The one reported difference was the
  deleted empty `UI/Haptics`.
- **String paths in one mapping pass.** One pass over `.cs`, `.uxml`, `.uss`,
  `.tss`, `PROJECT.md` and `docs/*.md` (except `docs/verification`) rewrote the
  string paths. That covers 11 C# files, 4 UXML files and 7 docs. The
  `Path.Combine` in `LevelDatabaseJsonTests` was fixed by hand.
- **History is left as it was.** Task contracts, verification records and older
  DEC rows keep their old paths; translate them with the table above.

## Verification

- **Compile:** all 24 `ColorBlockJam.*` assemblies recompiled in the live Editor
  after the move with no `error CS`, the same state as before the move.
- **EditMode:** 115/115 passed. This run covered the tests that load moved files
  by path:
  - Settings, purchase and pre-level UXML/USS asset tests
  - `LevelDatabaseJsonTests` (it reads `link.xml`)
  - Level Editor icons, timer icon and database controls
  - shape palette and shape library JSON
  - booster serialized assets and in-game purchase host
  - environment rule set
- **PlayMode:** 45/45 passed. This run covered `GameplayHudLayoutTests`,
  `GameplayHudCompositionTests`, `InGamePurchasePopupTests`, `HubSceneTests`
  (it loads `Hub.uxml` with its templates) and `BoosterEffectViewTests`.
- **Not run:** the full EditMode/PlayMode suites and an Android IL2CPP build.
  The move does not change `link.xml` content or assembly names.

## Data JSON folder (DEC-165)

On 2026-09-28 the user asked to delete unused Data assets and to keep all JSON
in one folder. A GUID and path-string audit of the 22 Data assets found every
one referenced, so nothing was deleted. `GamePalette` is still the colour
source for the atlas's white tint row (see DEC-165 in `PROJECT.md`).

| Old | New |
| --- | --- |
| `Data/Levels/LevelDatabase.json` | `Data/Json/Levels/LevelDatabase.json` |
| `Data/Board/PieceDatabases/` | `Data/Json/Pieces/` (the folder itself moved, same GUID) |
| `Data/Levels/LevelDatabase.asset`, `Data/Board/*.asset` | unchanged |

- The files moved with `cm mv` together with their `.meta` files. Both JSON GUIDs
  are unchanged and resolve to the new paths in the live Editor.
- Code constants: `PieceDatabaseFiles.DirectoryPath`, `BoardKitAssets.ShapesJsonPath`,
  `LevelDatabaseAddressables.DatabaseDirectory` (JSON only; `DefaultDatabasePath` still
  names `Data/Levels/LevelDatabase.asset`), plus the paths hard-coded in 12 test files.
- Each kind keeps its own subfolder, because each editor lists and accepts only
  the JSON in its folder.

## Editor-only piece library (DEC-193)

On 2026-09-29 the piece library left the player assemblies: the game builds every
block from its level cells, so only the Piece Editor and the Level Editor read it.

| Old | New |
| --- | --- |
| `Runtime/Core/Board/BlockShapeLibrary.cs` | `Editor/Core/BoardKit/BlockShapeLibrary.cs` |
| `Runtime/Core/Board/BlockShapeLibraryJson.cs` | `Editor/Core/BoardKit/BlockShapeLibraryJson.cs` |
| `Runtime/Core/Board/StrictJson.cs` | `Editor/Core/BoardKit/StrictJson.cs` |

- The files moved with `cm mv` together with their `.meta` files, so
  `Data/Board/BlockShapeLibrary.asset` keeps resolving its script GUID.
- Deleted: `Runtime/Core/Board/BlockShapeType.cs`, the shape catalog and
  learned-shape cache files, `Runtime/Core/Board/link.xml`,
  `Editor/Core/BoardKit/ShapePrefabAuthoring.cs` and the 13
  `Prefabs/Board/Shapes/Piece_0xx.prefab` files.

## Level databases as folders (DEC-196)

On 2026-09-29 each level database became a folder with a manifest and pack files
of about 100 levels, so 5,000 levels never load at once.

| Old | New |
| --- | --- |
| `Data/Json/Levels/LevelDatabase.json` | `Data/Json/Levels/Main/manifest.json` (moved with `cm mv`, same GUID; its text is the manifest) |
| (its five levels) | `Data/Json/Levels/Main/pack-001.json` (new file, byte for byte the old JSON) |

- The project scope installer's `m_levelJson` keeps the same GUID, so
  `ProjectContext.prefab` did not change.
- Addresses: `LevelDatabase/Main/manifest.json` and `LevelDatabase/Main/pack-001.json`,
  both with the `level-json` label.
- Code constants: `LevelJsonFiles.ManifestFile`, `LevelJsonFiles.GetAddress`,
  `LevelDatabaseAddressables.DefaultManifestPath` (replaces `DefaultJsonPath`).
