# TASK-20260925-005313341-47831bb7-runtime-shapes-from-json — Unknown pieces in updated level JSON are built, registered and saved on the client

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-005313341-47831bb7-runtime-shapes-from-json
- Supersedes: none
- Title: Unknown pieces in updated level JSON are built, registered and saved on the client
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Board presentation and level loading (runtime shape catalog)
- Workflow: direct

## Objective and baseline

- Observable objective: When an updated (for example remotely delivered) level JSON uses a piece (`shapeId`) that the client's built-in shape library does not have, the client builds that block from the cells in the JSON, registers the shape in a runtime shape catalog and saves it locally. The level plays and previews without a client update.
- Current behavior/evidence:
  - User request 2026-09-25: "json güncellendiğinde, örnek olarak istediğimiz piece eğer yoksa, oyunda client bunu json'dan bakıp üretip kayıt ederek yazabilir mi; bu şekilde bunu ayarla, ek bir task aç" ("when the JSON is updated and a piece we want does not exist, can the client read it from the JSON, build it and save it? Set it up this way, open an additional task").
  - Verified facts:
    - Every level block record already carries its footprint: `LevelBlockData` has `origin` and relative `cells`; `shapeId` is a label (`Runtime/Levels/LevelData.cs`, `LevelDatabaseJson.cs:165/219/345`).
    - `LevelData.Validate` only requires a non-empty `shapeId` (`LevelData.cs:249`).
    - Blocks are composed from the modular kit by `BlockPieceComposer` for any footprint, and `BlockPiece.Prepare(cellCapacity, resolvedCells, color)` already renders shipped footprints (`BlockPiece.cs:42`).
    - But `BlockPieceFactory` pools only library shapes that have a derived prefab (`BlockPieceFactory.cs:42-54`), and `LevelPreviewBlockMapper` rejects an unknown `shapeId` (`LevelPreviewBlockMapper.cs:36-38`).
    - `BlockShapeType` is a generated, append-only enum, so it cannot gain entries at runtime.
- Expected behavior:
  1. **Runtime shape catalog.** A plain C# `BlockShapeCatalog` resolves a block's `shapeId`:
     - a built-in library shape (by `TypeName`) if one exists;
     - otherwise a learned runtime shape;
     - otherwise it learns the shape from the block's cells (normalized to a lower-left origin).
     Runtime shapes are keyed by the `shapeId` string and never by `BlockShapeType`.
  2. **Validation of learned shapes, at level load (outside Playing).** Cells are non-empty, unique, four-connected, at most 25 cells and inside a 5x5 box. Learned footprints are compared as canonical footprints, up to the 8 rotations and mirrors: a block whose cells are a rotation or mirror of the already learned footprint for the same `shapeId` is the same shape and keeps that id. Only a genuinely different footprint for the same unknown `shapeId` rejects that level with a `Level {id} block {id} shape {shapeId}` message. The catalog and cache store the canonical footprint. Custom-piece ids from the Level Editor (`Custom_` + a 7-hex canonical footprint code, from TASK-20260925-082521498-8e54a900-level-editor-custom-pieces) are learned like any unknown id. A built-in `shapeId` whose level cells differ keeps the level's resolved cells for display (current rule) and logs once.
  3. **Spawning.**
     - `BlockPieceFactory` gains a generic source: one base block prefab (`BlockPiece` + `ModularBlockView`, no fixed source type) composes any footprint through `Prepare(cellCapacity, resolvedCells, color)`.
     - It is prepared for the level's counts on Home, exactly like library shapes. No Instantiate during Playing, and no prefab creation at runtime.
     - The LevelEditor preview uses the same catalog, so unknown shapes show there too.
  4. **Save ("kayıt ederek").** Learned shapes are saved to a versioned JSON cache under `Application.persistentDataPath` (for example `shapes/runtime-shapes.json`).
     - Newtonsoft, `TypeNameHandling.None`.
     - Temporary file plus atomic replace, written by one app-scoped owner with its own cancellation token, never in a Playing tick.
     - The cache is loaded at project-scope startup, so learned shapes survive restarts.
     - A corrupt cache is ignored with a warning and rebuilt from the level data. Built-in shapes always win over cached entries with the same id.
     - Editor test play uses an isolated in-memory store (PROJECT.md save rule).
  5. **Editor (optional in this task, recorded if skipped).** The Level Editor lists unknown shapes and can offer "Add to Piece Library" through the Piece Editor's existing save path. This is out of scope unless trivial; if skipped, it is recorded as a follow-up.
  6. **Docs.** PROJECT.md gets a decision row (built-in library plus runtime-learned shapes, and the cache rule), and `docs/Level-Data-Format.md` documents that `cells` is authoritative and unknown `shapeId`s are learned.
- Behavior that must remain unchanged:
  - Built-in shapes, their prefabs and pools.
  - Level JSON schema (no new required field).
  - The pool lifecycle and no-growth rule.
  - The Rocket, Hammer and Vacuum behaviour on learned shapes (they use the same composer).
  - Existing levels load identically.

## Revision 2026-09-25 (takes precedence over any conflicting text above or below)

- **Game-scene path.** Blocks in the Game scene resolve shapes through the gameplay session's `Gameplay/Levels/ShapeResolver.cs` and `AttemptLevelConverter.cs`, and spawn through `Gameplay/Board/BoardPresenter.cs`. Today these reject unknown shapeIds.
  - This task routes them through `BlockShapeCatalog`: learned shapes resolve, and BoardPresenter uses the factory's generic source for them.
  - The factory stays the only construction site in BoardPresenter.
- **Access granted by the owner.** Session "Buluan parça sürükleme ve gate mekanikleri" chose narrow sequential write access for this task: ShapeResolver.cs, AttemptLevelConverter.cs, BoardPresenter's source-type lines for learned shapes, and the `Tests/Gameplay/**` fixtures that resolve shapes (for example AttemptLevelConverterTests, PresenterScene). Message that session before starting and when releasing.
- **Extra verification.** Before handoff, `ColorBlockJam.Gameplay.EditModeTests` (215) and `ColorBlockJam.Gameplay.PlayModeTests` (11, including `GameSceneDragTests`) are green, and a Game-scene level with a learned shape plays.
- **Rebase.** Build on board-art DI (`BoardArt`) and block-runtime-objects (exact warm-up, 4-argument Prepare).

## Revision 2 2026-09-25: pieces added by JSON, later shipped by a store patch (takes precedence)

User requirement, relayed by the Level Editor session: "json ile oyuna güncelleme atmadan parça ekleyebilelim ileride yani blockshapelibrarydeki parçalar loading esnasında üretilebilir eğer eksikse ilk açılışta normal patch attığımızda da store üzerinden zaten elimizde olacağından block çakışma olmayacağından da emin ol". Meaning:
- Pieces can be added through level JSON without a game update.
- Pieces missing from `BlockShapeLibrary` are generated during loading on first launch.
- When a later store patch ships the same pieces in the library, no block conflict occurs.

7. **Learned at load, never mid-attempt.** The project scope learns every level's unknown shapes right after `LevelDatabase.LoadAsync` succeeds. It first loads the cache, then learns, validates and writes the cache once. This happens before Init routes to Hub. After that step the catalog is sealed. Building an attempt, entering Playing or opening the Level Editor preview only resolves; none of them learns, validates or does file I/O. The Level Editor preview learns from its draft through a separate, editor-only catalog instance, so the project catalog stays sealed.
8. **Canonical footprint is the identity; ids are aliases.** Learned shapes are stored by canonical footprint code, the same 8-transform canonical form the Level Editor uses for `Custom_` ids: bit `y*5+x` in the 5x5 box. An id maps to one footprint.
   - Two different unknown ids with the same canonical footprint are one learned shape.
   - The same unknown id with two different footprints still rejects the level (Expected 2).
9. **Resolution order (library always wins).** For each block `shapeId`:
   1. A library shape whose `TypeName` equals the id.
   2. For an id that is not a library `TypeName`, including every `Custom_` id, a library shape whose canonical footprint equals the block's, if it has a prefab.
   3. A learned shape.
   4. Learn from the block's cells.

   So a level still saying `Custom_XXXXXXX`, or a remote `NewPiece` learned before the patch, resolves to the library entry and its prefab pool once a patch adds that footprint. It is never learned twice. A cached entry whose footprint (or id as `TypeName`) now exists in the library is ignored at load and dropped at the next cache write, and a cached shape never shadows a library shape.
10. **Id and enum safety.**
   - `Custom_` ids resolve by footprint only, never by name.
   - A library `TypeName` that starts with `Custom_` is reported once as a library error, and it is never used to resolve a `Custom_` id by name. The Piece Editor refusing such names belongs to the board-kit owner and is recorded as a follow-up request.
   - Learned shapes never use `BlockShapeType`, which stays generated and append-only, so a patch never renumbers an existing shape.
   - A remote level using a `TypeName` the installed build lacks is learned from its cells (step 4). After the patch it resolves by name (step 1). If its cells then differ from the library footprint beyond the 8 transforms, the existing built-in rule applies: the block keeps the level's resolved cells for display, and one warning names the level, block and shape. The level is not rejected, so a remote level keeps playing as authored after a patch.

## Ownership

- Owner/writer role: environment/play-area session (this conversation, by "her şeyi sen yap" and this request), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Gameplay/Levels/ShapeResolver.cs
  - Assets/ColorBlockJam/Gameplay/Levels/AttemptLevelConverter.cs
  - Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs (source-type lines for learned shapes only)
  - Assets/ColorBlockJam/Tests/Gameplay/** (fixtures that resolve shapes only)
  - Assets/ColorBlockJam/Runtime/Board/BlockShapeCatalog*.cs
  - Assets/ColorBlockJam/Runtime/Board/BlockShapeCatalog*.cs.meta
  - Assets/ColorBlockJam/Runtime/Board/RuntimeShapeCache*.cs
  - Assets/ColorBlockJam/Runtime/Board/RuntimeShapeCache*.cs.meta
  - Assets/ColorBlockJam/Runtime/Board/link.xml
  - Assets/ColorBlockJam/Runtime/Composition/** (project-scope binding of the catalog and cache owner only)
  - Assets/ColorBlockJam/Presentation/Board/BlockPieceFactory.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockPiece.cs
  - Assets/ColorBlockJam/Prefabs/Board/ModularBlock.prefab (only if the generic source needs a `BlockPiece`; through Unity APIs, GUID unchanged)
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Tests/EditMode/BlockShapeCatalog*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/BlockShapeCatalog*Tests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/RuntimeShapeCache*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/RuntimeShapeCache*Tests.cs.meta
  - Assets/ColorBlockJam/Tests/LevelPreview/**
  - docs/Level-Data-Format.md
  - PROJECT.md
  - production/tasks/TASK-20260925-005313341-47831bb7-runtime-shapes-from-json/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Levels/** (the schema and validation owner is the level-pipeline session; shape learning validates in the catalog)
  - Assets/ColorBlockJam/Runtime/Board/BlockShapeType.cs (generated, append-only)
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/AddressableAssetsData/**
  - Enabling `TypeNameHandling`
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Prefabs/Board/ModularBlock.prefab (only if needed)
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: unchanged GUID
- Other active writers/worktrees checked:
  - `TASK-20260925-002022060-4c9dac46-block-runtime-objects` and `TASK-20260924-224656127-e361499a-board-art-dependency-injection` (same session) change `BlockPieceFactory`/`BlockPiece` first; this task rebases on them.
  - `TASK-20260924-210432886-2f31f26b-remote-level-json` (level-pipeline session) delivers the remote JSON; this task works with any loaded JSON and does not depend on it to close.
  - The board-kit session owns `Editor/BoardKit`, which is not touched here.
- Execution dependency: TASK-20260924-224656127-e361499a-board-art-dependency-injection handed off; message session "Buluan parça sürükleme ve gate mekanikleri" before starting
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a level JSON whose block uses `shapeId` "ZigZag5" with five connected cells that are not in the library, when the database loads, then the catalog learns "ZigZag5", the level validates, and the LevelEditor preview shows the block with those cells and its colour (EditMode plus a live capture).
2. Given the learned shape, when the project scope restarts (cache reload), then "ZigZag5" is known without the level. The cache file is versioned JSON written atomically, and a corrupt cache is ignored with one warning and rebuilt (EditMode with a temp directory).
3. Given invalid learned cells (disconnected, duplicated, more than 25, outside 5x5), or the same unknown id with two footprints that are not rotations or mirrors of each other, then loading rejects that level with a message naming the level, block and shape, and nothing is cached. A rotated or mirrored block with the same unknown id is accepted as the same learned shape (EditMode).
4. Given a level mixing built-in and learned shapes, when blocks are created through the factory, then built-in shapes use their prefab pools and learned ones use the generic source. All are prepared on Home, and creating them during Playing instantiates nothing (EditMode/PlayMode pool diagnostics).
5. Given built-in shapes and existing levels 1-5, then behaviour and visuals are unchanged, and the existing tests pass.
6. Given a level with a block `Custom_X` whose footprint the library lacks, it is learned at load and cached. When the library then gains the same footprint, rotated or mirrored, as `NewPiece` with a prefab, and the project scope reloads:
   - there is one shape, resolved to `NewPiece`'s prefab pool;
   - the cached entry is not used, and it is absent after the next cache write;
   - nothing is learned twice (EditMode with a test library and a temp cache directory).
7. Id safety:
   - Given a remote level using an unknown `TypeName` `NewPiece`, it is learned from its cells; after the library gains `NewPiece`, the id resolves by name to the library.
   - If that level's cells differ from the library footprint beyond the 8 transforms, the block keeps the level's cells and one warning is logged, and the level is not rejected.
   - Two unknown ids with one footprint give one learned shape.
   - A `Custom_` id never resolves by name, even when a library `TypeName` starts with `Custom_`; that library entry gets one reported error (EditMode).
8. Given the project scope has loaded, then the catalog is sealed:
   - building an attempt and entering Playing resolve shapes without learning, validating or file I/O (a sealed-catalog test, plus the PlayMode pool diagnostics of acceptance 4);
   - the cache is written at most once per load, outside Playing.

## References and constraints

- Story/GDD/ADR/design/log/image: user request 2026-09-25; user custom-piece decision 2026-09-25, relayed by the Level Editor session (canonical footprint comparison); PROJECT.md level-data rules (Newtonsoft, no TypeNameHandling, no JSON in Playing), DEC-92 atomic save pattern, Performance Rule 3 (pools), DEC-101 (remote-updatable level JSON); `docs/Board-Art-DI-Design.md`.
- Unity version/platform: 2022.3.62f2; Android/iOS IL2CPP (`link.xml` preserves the cache DTOs)
- Package/architecture constraints: Onity DI for the catalog and cache owner; plain C# validation; ZLinq instead of System.Linq; no runtime prefab or enum generation.
- Explicit out of scope: the remote delivery mechanism itself, new composer geometry, and the "Add to Piece Library" editor button unless trivial.

## Verification and risk

- Focused test path/command: `BlockShapeCatalogTests`, `RuntimeShapeCacheTests`, the LevelPreview tests (EditMode); factory pool diagnostics (PlayMode)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1-3 and 5
- PlayMode: acceptance 4
- Manual/built-player/profiler/device evidence:
  - A live preview capture of the learned shape.
  - An IL2CPP smoke check of the cache read/write in the next device build (recorded as pending if no build runs).
- Risk: medium (new runtime service and local file write)
- Approval already granted (exact target only): user request 2026-09-25 ("bu şekilde bunu ayarla, ek bir task aç") and "devam et verdiğim görevleri bitirene kadar"
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260925-005313341-47831bb7-runtime-shapes-from-json, then an independent verifier

## Stop conditions

- Learning shapes requires changing `Runtime/Levels` validation or the schema: stop and record the needed change for the level-pipeline session.
- The generic source cannot be pooled without a per-shape prefab.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:c656f0946e5d8ca5c9bdf228aff73c7254981b8ddb1a01f2bdbec7fd73aa7439
- Evidence fingerprint: sha256:b4cbbfeeb546c214864323a95ba58caf5e9a0ba4461bc9aab4b24c28413ba5d4
- Reviewed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6
- Attempt count: 3
- Last feedback: D-001 independently verified; Gameplay EditMode 282/282 and focused PlayMode 19/19 pass. Existing AC-8 integration approval and older stale acceptance proof remain.
- Blocker: user approval to assign the shape library on Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab (ProjectScopeInstaller m_shapeLibrary); the gameplay session granted the one GameAttemptLoader.cs:89 call, which must land together with it
- Next action: None
- Closed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6
- Closed at: 2026-09-28T08:46:45Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Unknown shape learned and previewed | STALE | independent re-verifier (chain wf_3b45c785-27f, evidence.md): BlockShapeCatalogTests learn/resolve + LevelPreviewTests learned shape from the generic pool | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |
| AC-2 Learned shapes saved and reloaded | STALE | independent re-verifier (chain wf_3b45c785-27f, evidence.md): RuntimeShapeCacheTests 16/16 with a temp-directory file store (atomic write, corrupt cache rebuilt) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |
| AC-3 Invalid learned shapes rejected | STALE | independent re-verifier (chain wf_3b45c785-27f, evidence.md): invalid learned cells and conflicting footprints rejected; rotated/mirrored accepted | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |
| AC-4 Generic pooled spawning, no Playing instantiation | STALE | independent re-verifier (chain wf_3b45c785-27f, evidence.md): LearnedShapeAttemptTests pooled on Home, no Instantiate during Playing | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |
| AC-5 Built-in behaviour unchanged | STALE | independent re-verifier (chain wf_3b45c785-27f, evidence.md): full EditMode 1024/0 (1 ignored) and PlayMode 56/56 incl. Gameplay and GameSceneDragTests | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |
| AC-6 Store patch adds a learned footprint: library wins, no duplicate | STALE | independent re-verifier (chain wf_3b45c785-27f, evidence.md): store patch takes over a learned footprint, one shape, cache entry dropped | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |
| AC-7 Id and enum safety | STALE | independent re-verifier (chain wf_3b45c785-27f, evidence.md): Custom_ never by name, unknown TypeName learned then resolved by name, mismatch warns once | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |
| AC-8 Learned at load; catalog sealed during attempts | FAIL | sealed-catalog tests pass, but the shipped ProjectContext binds no shape library and GameAttemptLoader uses the library-only overload; needs the user-approved ProjectContext.prefab field and the gameplay-granted GameAttemptLoader line together | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |

| P-1 Project load stops when its installer is destroyed | PASS | independent root: VERIFY: PASS (19/19), PlayMode b585f1d023844e079397a9c3ef300f00 includes all 4 ProjectScopeShapeCatalogTests; VERIFY: PASS (282/282), Gameplay EditMode 4c0a6d4d772c47dba919266488eef498; source review PASS, early cancellation before cache resolution | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | preservation_failure:project-load-lifetime:installer-destroy-does-not-cancel-load | VERIFIED | PRESERVATION_FAILURE | P-1 / PROJECT.md runtime Rule 4 | Destroying the project installer does not cancel its database/cache load while the container remains alive. | Independent root: VERIFY: PASS (19/19), PlayMode b585f1d023844e079397a9c3ef300f00; VERIFY: PASS (282/282), Gameplay EditMode 4c0a6d4d772c47dba919266488eef498; original 3 failing cases now pass. | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:2fb694dccec36d6785ddfeaadc95239439c78cbdf051b565d4d83b59134547c6 |

### Transition history

- 2026-09-26T19:34:41Z — verification — D-001 VERIFIED by independent root: VERIFY: PASS (19/19), PlayMode b585f1d023844e079397a9c3ef300f00; VERIFY: PASS (282/282), Gameplay EditMode 4c0a6d4d772c47dba919266488eef498. Source/test bytes frozen, task authority preserved, 174 review paths hashed. Existing AC-8 approval gate remains and AC-1..AC-7 historical proof remains STALE; task is not close-ready. No serialized changes, checkin or closure.

- 2026-09-26T19:29:17Z — verification — D-001 FIXED_UNVERIFIED: baseline 3 new cases failed, repaired ProjectScopeShapeCatalogTests 4/4 passed; compileFailed=false, LevelEditor scene clean; no serialized assets changed. Root owns the next independent verification pass.

- 2026-09-26T19:23:56Z — reproduction — NEW_DEFECT D-001; attempt 3; exact task claim held by /root/scope_fix; Unity preflight READY WITH WARNINGS (pre-Unity-6 transport), unity-game-dev; source/tests only, no serialized ownership. Prior AC-1..AC-7 proof is stale; AC-8 integration approval remains pending.

- Created — planning — contract authored from the user's request to build, register and save pieces missing from updated level JSON
- 2026-09-25T08:29Z — contract revised before execution — learned shapes compare canonical footprints (8 rotations and mirrors); Custom_ ids from the Level Editor custom-piece task are learned; AC-3 updated
- 2026-09-25T11:11Z — contract revised before execution — Game-scene path through Gameplay ShapeResolver/AttemptLevelConverter/BoardPresenter added with the owner's narrow access grant; Gameplay test gate added
- 2026-09-25T17:18Z — contract revised before execution (Revision 2) — user requirement relayed by the Level Editor session: JSON-added pieces are learned at load on first launch and a later store patch adding the same footprint causes no block conflict; canonical footprint identity, library-first resolution, sealed catalog; AC-6..AC-8 added
- 2026-09-25T22:27Z — in_progress/verification — chain wf_3b45c785-27f: implement, 3 reviewers, fix, verify (AC-8 FAIL), retry-fix (GameSceneLearnedShapeTests added), re-verify: AC-1..AC-7 PASS, AC-8 blocked on two out-of-contract edits
- 2026-09-28T08:46:45Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
