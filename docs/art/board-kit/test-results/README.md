# Board kit verification

## JSON authority and readability extension

- `json-editmode.json`: **50/50 passed**, job `2f7ffe0a00cc4e3d9a9ebf14f63a57f7`.
  Full per-test results are retained. Includes the previous board/prefab/factory
  coverage plus strict JSON round-trip and schema validation, case-variant field
  rejection, import/merge/export, stable compiled identities, external-edit
  protection, saved JSON/prefab Undo, unrelated Undo after cache reload and
  failed runtime preparation. The permanent integrity test compares the current
  JSON/cache/prefabs and retained GUIDs, allowing future additions, edits,
  deletion and prefab renames without hardcoding the original piece count.
- `../json-migration-verification.json`: one-time audit confirms all 34 original
  IDs, names and footprints exactly match the pre-migration snapshot, prefab
  GUIDs are unchanged and the four source FBX hashes still match cs:3.
- `json-playmode.json`: **2/2 passed**, focused `BoardKitLifecycleTests`.
  Prepared root reuse and final disposal remain correct in PlayMode.
- `../json-readback.json`: 34 active records, 34 prefab bindings and 34 current
  prefab signatures. The SO has no serialized shape definitions. Game scene
  remained clean; the Editor was neither playing nor compiling.
- `../PieceEditorJson.png`: reviewed UI capture confirms the four JSON controls,
  wide action rows, centered cross, shared grid/preview tint and shortcut help.
- `json-console.json`: no compiler errors. Controlled Undo errors are expected
  by `UnrelatedUndoAfterCacheReloadCannotRestoreAnOldLibrarySnapshot`, proving
  that Undo refuses to replace an externally edited JSON file. The final query
  also contains separate level-loading errors: missing Android Addressables
  runtime settings and unresolved `Data/Levels/LevelDatabase.json`. That concurrent
  level-data/Composition task owns the affected files; board tests pass, but this
  report does not claim a clean whole-game launch.
- Independent read-only reviews accepted the final runtime preparation/factory
  changes and JSON transaction/codec fixes. `dotnet format whitespace --verify-no-changes`
  passes for the scoped board C# and tests; the project skill validator passes.
- Changed responsibilities: portable codec, reference-only SO, authoring commands,
  rollback transaction and transient Undo snapshots. JSON parsing stays outside
  prepared checkout/return; their 0 B allocation checks still pass.
- Review identity uses the current Plastic changeset plus sorted owned paths and
  raw bytes (`path`, NUL, bytes, NUL). The contract excludes its managed lifecycle
  block. The generated `../verification-inputs.json` inventory is excluded from
  its own digest, as are lifecycle records, caches and temporary test assets.
  Original FBX/import metadata, runtime assembly references and package pins are
  additional verification inputs. Evidence hashing follows the installed task-cycle
  protocol: contract/review identities, criterion rows, defect identity/status/
  mapping/revision, then the verification-channel records in that order.

## Earlier retained evidence

- Exact Editor: ColorBlockJamCase2026, Unity 2022.3.62f2. Unity CLI 1.0.0-beta.11
  verified; approved pre-Unity-6 MCP transport targets the pinned project only.
- `editmode.json`: focused `BoardKitTests` and `ShapePrefabTests` result.
  **27/27 passed**, job `5de650afc5d14e398bf35ee172859dcc`.
  Covers all reference shapes/rotations, original source references, shader
  passes, 0 B composition after warmup, prepared pool/material reuse, palette
  state/reset, authoring validation, centered sliders, grid tint and Undo/Redo.
  New/rename/paint preserve library and enum bytes/write time until Save;
  Save routes new/existing entries correctly, and delete preserves enum source.
  Grid shortcut tests restore complete strokes, Clear, Rotate and dimensions;
  no-op paint/rejected resizing add no step. Clean Undo redraws the preview,
  and name/color/enum/library contents remain unchanged by grid history.
  Also covers safe derived prefab Save, GUID/custom-content preservation, collision
  and read-only rejection, full batch preflight, library Undo across selection,
  copied attempt cells, valid/invalid Rocket survivors and source preservation.
  Onity factory tests cover warmup, 0 B measured managed allocation across 50
  checkout/return cycles, exhaustion, foreign/double return, invalid input rollback,
  malformed prefab rejection, state/color/clip reset and outstanding-root disposal.
- `factory-playmode.json`: **2/2 passed**, job `f93cbaf875024369b4624f1540b3d538`.
  Real PlayMode disable/re-enable stays idle; factory returns/reuses the same root
  after mutation and disposes active and inactive roots plus all nested pools.
- `../shape-prefabs.json`: 34/34 owned prefabs match SO signatures and have
  BlockPiece/root-view references. Every mesh comes from original BlockParts.fbx.
- `playmode.json`: historical booster views and board pool lifecycle results. Includes
  **13/13 passed**, job `048560259c5e4b91aadff757da37e72d`.
  actual elapsed motion, pause/replay/disable cleanup, target contact, all 32
  Vacuum frames and intake availability during the front-facing interval.
- `../source-preservation.json`: all four original FBX byte hashes equal cs:3
  at their earlier `Assets/Art/Models` paths. The subsequent project-folder
  migration is preserved.
- `../ShapeLibrary.png`, `../EnvironmentPalette.png` and `../PieceEditor.png`:
  Unity render/UI evidence reviewed after calibration and UI corrections.
- MCP can drop detailed result payloads across domain reload; retained successful
  job state, completed counts and empty failure lists identify completed runs.
- `final-console.json` records the final live Console query. The separate Level
  Editor owner's earlier compilation errors are resolved. No Level Editor source
  was changed by this task. A temporary Android device reconnect log during work
  was unrelated to the scoped tests; it was not used as verification evidence.

No device build, GPU timing, instancing count or whole-game integration result
is implied by these focused Editor checks. The active Game scene remained clean
during prefab generation/readback; no scene asset was saved by this extension.
