# TASK-20260925-002022060-4c9dac46-block-runtime-objects — Blocks spawn only the pieces they can show

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-002022060-4c9dac46-block-runtime-objects
- Supersedes: none
- Title: Blocks spawn only the pieces they can show
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: performance
- Area: Board presentation (block pieces, shape prefabs)
- Workflow: direct

## Objective and baseline

- Observable objective: A block instance (Game scene, LevelEditor preview, factory) contains only the piece objects it can ever display: its own footprint plus the pieces needed by the footprints a Rocket can leave behind. No hidden `GeneratedShape` copy is instantiated at runtime.
- Current behavior/evidence:
  - User screenshot and message 2026-09-25: "çok fazla gereksiz obje var gibi görünüyor, bunları da hiç spawnlamayarak çözebiliriz" ("there seem to be too many unnecessary objects; we can solve this by not spawning them at all"). The `Piece_001(Clone)` hierarchy shows a hidden `GeneratedShape` child, many inactive Center/Edge/OuterCorner/InnerCorner clones and only a few active ones.
  - Verified causes:
    1. `ModularBlockView.Prepare` (`Presentation/Board/ModularBlockView.cs:28-55`) warms every kind's pool to a worst-case bound for any connected n-cell footprint: Center n, Edge 2n+2, OuterCorner 2n+2, InnerCorner 2n. It instantiates all of them. A 2x2 Square therefore instantiates 32 pieces but shows 16 (4 Center, 8 Edge, 4 OuterCorner).
    2. Each derived shape prefab (`Prefabs/Board/Shapes/Piece_*.prefab`) carries a complete authored copy of the shape under `GeneratedShape` (created by `ShapePrefabAuthoring.cs:51-84`). `BlockPiece.Prepare` only deactivates it (`BlockPiece.cs:54`), so every instance carries a hidden duplicate.
       - Its only consumers are authoring and validation: `ShapePrefabAuthoring`, `BlockPieceFactory.cs:53` (presence check), `BlockPiece.Prepare` (presence check) and `ShapePrefabTests`.
       - No runtime visual uses it.
  - `Source_0..3` children of the Center/Edge/Corner piece prefabs are the original kit meshes: Center has 4 renderers, Edge 2, the corners 1. They are the visible geometry and stay (PROJECT.md: use only the original meshes).
- Expected behavior:
  1. **Exact reachable capacity.**
     - At preparation (outside Playing), each block computes, per piece kind, the maximum count over its footprint and every nonempty connected sub-footprint. By the Rocket rule, each removal keeps a nonempty connected remainder; for a connected footprint every connected subset is reachable this way.
     - It warms exactly that many pieces per kind, using `BlockPieceComposer` for the counts. Allocation is allowed here (Home / preparation).
     - Enumeration is bounded: footprints above 12 cells fall back to the current formula, logged once.
     - `SetCells`/`TryRemoveCell` keep rejecting a footprint that exceeds the prepared capacity; no pool grows during Playing.
     - `BlockPiece.SetShape(type)` to a different library shape needs a new `Prepare`, as its callers do outside Playing. Callers that relied on the old bound are updated inside the allowed paths.
  2. **No runtime authored copy.**
     - Derived shape prefabs no longer contain a `GeneratedShape` subtree.
     - Project-window and Inspector thumbnails for shape prefabs come from an editor-only static preview: `RenderStaticPreview`/`HasPreviewGUI` on a `BlockPiece` custom editor that composes the footprint with `BlockPieceComposer` and the original piece meshes in a `PreviewRenderUtility`, and leaves no objects behind.
     - `BlockPieceFactory` and `BlockPiece.Prepare` stop requiring an authored visual.
     - `ShapePrefabAuthoring`/`ShapeLibraryAuthoring` stop generating it. They remove an existing owned `GeneratedShape` from the 34 prefabs through the existing identity-preserving save path, keeping prefab GUIDs, the `BlockPiece` root, `m_sourceType` and custom content outside `GeneratedShape`.
     - Generated signatures are recomputed only through that path.
  3. **Measured result.** Record the total GameObject count and the per-block count for levels 1-5 in the LevelEditor preview and for level 1 in a direct Game scene build, before and after. Expected: a Square block drops from about 50+ objects (root, a 16-piece hidden copy with sources, and 32 pooled pieces with sources) to its reachable pieces only.
  4. PROJECT.md: the board-visuals / derived-prefab sentences and Performance Rule 3 describe the exact reachable warm-up and the editor-only shape preview, with a decision row.
- Behavior that must remain unchanged:
  - Every visible block (piece kinds, positions, rotations, colours) in the Game scene, the LevelEditor preview and the Piece Editor preview.
  - Rocket one-cell removal (nonempty connected remainder, no pool growth), Hammer and Vacuum presentation paths, pool return and teardown.
  - Prefab GUIDs and shape identities.
  - `BlockShapeLibrary` JSON and enum.

## Ownership

- Owner/writer role: environment/play-area session (this conversation, by "her şeyi sen yap"), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Presentation/Board/ModularBlockView.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockPiece.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockPieceFactory.cs
  - Assets/ColorBlockJam/Runtime/Board/BlockPieceComposer.cs (only if a count helper is needed; no behaviour change)
  - Assets/ColorBlockJam/Editor/BoardKit/ShapePrefabAuthoring.cs
  - Assets/ColorBlockJam/Editor/BoardKit/BlockPieceEditor.cs
  - Assets/ColorBlockJam/Editor/BoardKit/BlockPieceEditor.cs.meta
  - Assets/ColorBlockJam/Prefabs/Board/Shapes/*.prefab (through the authoring save path only; GUIDs unchanged)
  - Assets/ColorBlockJam/Tests/EditMode/ShapePrefabTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/BlockPiece*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/BlockPiece*Tests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/ModularBlockView*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/ModularBlockView*Tests.cs.meta
  - Assets/ColorBlockJam/Tests/LevelPreview/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - PROJECT.md
  - production/tasks/TASK-20260925-002022060-4c9dac46-block-runtime-objects/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Prefabs/Board/Pieces/** (the original-mesh piece prefabs stay as they are)
  - Assets/ColorBlockJam/Art/**
  - Assets/ColorBlockJam/Data/Board/BlockShapeLibrary.asset content other than what the existing save path rewrites
  - Assets/ColorBlockJam/Data/Board/PieceDatabases/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Editor/Levels/**
  - Creating new mesh assets or combining meshes
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Prefabs/Board/Shapes/Piece_*.prefab (34 derived shape prefabs)
    Sole writer: environment/play-area session through `ShapePrefabAuthoring`'s identity-preserving save path
    Matching `.meta` owner: unchanged (GUIDs preserved)
- Other active writers/worktrees checked:
  - The board-kit session (`TASK-20260924-191842745-88e64a0d-board-kit`, `ready_to_close`) owns `Presentation/Board`, `Editor/BoardKit` and the shape prefabs. It ran `ShapePrefabTests` in the live Editor around 02:00 on 2026-09-25. Execution waits until it is released (no write for 10 minutes and its task closed, or the user confirms).
  - `TASK-20260924-224656127-e361499a-board-art-dependency-injection` (same session) edits the same files and reserializes the same prefabs; it runs after this task and rebases on it.
- Execution dependency: board-kit released; the current environment chain (rule-set-grid, floor-base-per-tile, camera) has handed off
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given each library shape and the level 1-5 footprints, when prepared, then each kind's warmed count equals the maximum over the footprint and its connected sub-footprints (EditMode, against a brute-force reference), and Rocket removals within that set never grow a pool.
2. Given the 34 shape prefabs after migration, then none contains `GeneratedShape`, GUIDs and `m_sourceType` are unchanged, and the Project window and Inspector preview still show the shape. The preview is checked by a capture and by a test that renders a non-empty preview texture and leaves no objects in any scene.
3. Given levels 1-5 in the LevelEditor preview and level 1 in the Game scene, then the visible result is identical to before (captures), and the recorded object counts drop accordingly.
4. Given the existing board-kit, LevelPreview and environment tests, then all pass after updating only the expectations about the authored visual and pool sizes.

## References and constraints

- Story/GDD/ADR/design/log/image: user screenshot and message 2026-09-25; PROJECT.md Performance Rule 3 (pools warm to the level's counts, no Instantiate during an attempt), board-visuals bullet (derived shape prefabs), DEC-74 (Rocket keeps a nonempty connected remainder).
- Unity version/platform: 2022.3.62f2; Android/iOS
- Package/architecture constraints: Onity pools; plain C# counting; no new package; no mesh combining; editor preview in the Editor assembly only.
- Explicit out of scope: the Onity DI provider (separate task), combining the Source meshes, and environment pools.

## Verification and risk

- Focused test path/command: `ShapePrefabTests`, new `ModularBlockViewCapacityTests`, `BlockPieceEditorPreviewTests`, and the LevelPreview tests (EditMode)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1, 2 and 4
- PlayMode: existing board or scene-flow smoke tests stay green
- Manual/built-player/profiler/device evidence:
  - Acceptance 3: before/after captures and object counts.
  - User acceptance of the hierarchy.
- Risk: high (34 prefab rewrites, cross-session files)
- Approval already granted (exact target only): user message 2026-09-25 ("hiç spawnlamayarak çözebiliriz") plus "her şeyi sen yap" and "devam et verdiğim görevleri bitirene kadar", for the paths above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260925-002022060-4c9dac46-block-runtime-objects, then an independent verifier

## Stop conditions

- A runtime system (effects, ice overlay, vacuum, hammer) turns out to use the authored visual.
- The identity-preserving save path cannot remove `GeneratedShape` without changing GUIDs or custom content.
- The board-kit session writes these files during the task.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:6444168dcedb9a6a06a0ea299d7845cfde4b6bb26bbc8ef8b7f7e86bd7024bb9
- Evidence fingerprint: sha256:59aa8999a722104e690055e3f975a63def52b877c9681cb108b28017909dcd7d
- Reviewed revision: vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:140dbb86c8357d06563a758fb50fb322828e95e748cb85e4a2d0f26126844fe6
- Attempt count: 1
- Last feedback: Game-scene recount after BoardPresenter 4-argument Prepare: Square 30 objects, capture identical
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:140dbb86c8357d06563a758fb50fb322828e95e748cb85e4a2d0f26126844fe6
- Closed at: 2026-09-25T19:32Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Exact reachable warm-up | PASS | automated: ModularBlockViewCapacityTests in full EditMode 800/801 (ShapePrefabTests file-lock flake passed 1/1 on rerun) + PlayMode 44/44, barrier-view re-verify run; live Game Square 11 views | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:140dbb86c8357d06563a758fb50fb322828e95e748cb85e4a2d0f26126844fe6 |
| AC-2 No GeneratedShape; editor preview kept | PASS | automated/manual: BlockPieceEditorPreviewTests + ShapePrefabTests in full EditMode 800/801 (ShapePrefabTests file-lock flake passed 1/1 on rerun) + PlayMode 44/44, barrier-view re-verify run; Game Square has no GeneratedShape | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:140dbb86c8357d06563a758fb50fb322828e95e748cb85e4a2d0f26126844fe6 |
| AC-3 Visual parity and fewer objects | PASS | manual: Game level 1 Square 113->30 objects, Blocks 226->61, scene 386->220, capture identical at 540x960; LevelEditor preview 30/Square, pixel parity (evidence.md) | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:140dbb86c8357d06563a758fb50fb322828e95e748cb85e4a2d0f26126844fe6 |
| AC-4 Existing tests green | PASS | automated: full EditMode 800/801 (ShapePrefabTests file-lock flake passed 1/1 on rerun) + PlayMode 44/44, barrier-view re-verify run; BoardKitTests 11-vs-12 failures gone | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:140dbb86c8357d06563a758fb50fb322828e95e748cb85e4a2d0f26126844fe6 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's hierarchy screenshot (worst-case pools and the hidden GeneratedShape copy)
- 2026-09-25T11:08Z — in_progress — task-chain: implement, 3 reviewers (11 findings), fix, verify, retry-fix, re-verify; AC-1/2/4 PASS; AC-3 FAIL only in the Game scene (BoardPresenter outside scope)
- 2026-09-25T11:08Z — WAITING_FOR_OWNER — BoardPresenter exact-capacity change requested from the gameplay session
- 2026-09-25T14:55Z — contract narrowed — ShapeLibraryAuthoring.cs released to the board-kit writer ("Database oluşturma popup", TASK-20260925-145158848-5858b722-piece-editor-level-link); this task never changed it (evidence.md) and its remaining work is the Game-scene recount
- 2026-09-25T15:48Z — verification — owner released (BoardPresenter 4-argument Prepare landed); Game-scene recount AC-3 PASS; AC-1..AC-4 PASS at the reviewed revision; waiting for user acceptance of the hierarchy
- 2026-09-25T19:32Z — closed — USER_CONFIRMED closure — user order "bunları kapat" (2026-09-25) accepting the block hierarchy (Square 113 -> 30 objects in the Game scene and preview); all AC rows preserved as recorded
<!-- TASK-LIFECYCLE:END -->
