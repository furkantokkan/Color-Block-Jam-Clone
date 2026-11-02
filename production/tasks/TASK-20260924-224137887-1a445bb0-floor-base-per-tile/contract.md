# TASK-20260924-224137887-1a445bb0-floor-base-per-tile — One grey base square under each floor tile instead of one plane

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-224137887-1a445bb0-floor-base-per-tile
- Supersedes: none
- Title: One grey base square under each floor tile instead of one plane
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Play-area environment (floor base)
- Workflow: direct

## Objective and baseline

- Observable objective: Under the play area, each floor (inner) tile has its own base square. Void cells and hole centres show the background, not a base plane.
- Current behavior/evidence:
  - User revision 2026-09-25: "en alta tek bir plane yerine her inner tile altına bir adet gri kare atabiliriz burada da revizeye gidelim" ("instead of one plane at the bottom, we can put one grey square under each inner tile; let's revise this too").
  - This supersedes the 2026-09-24 single-plane decision recorded in `TASK-20260924-185655569-dea430a7-play-area-environment` (history: "one grey floor-base plane ... keep a single plane").
  - Today `EnvironmentView.PlaceFloorBase` scales one `FloorBase` quad across the whole width x height rectangle. The quad is a child of `Prefabs/Environment/Environment.prefab`, carries a `BoardPieceView`, and is tinted by `EnvironmentDatabase.GetFloorBaseColor()` (currently `PaletteColor.Wall`).
- Expected behavior:
  1. `EnvironmentComposer` adds one floor-base placement per floor cell: the cell centre at a small depth below the floor tile, one cell in size, tinted with the database floor-base colour. Void cells get none.
  2. The base square is a pooled environment piece referenced by `EnvironmentDatabase`: a new `Prefabs/Environment/FloorBase.prefab` with `BoardPieceView` on the root and a flat quad child.
  3. The single plane is removed: the prefab child, the `EnvironmentView.m_floorBase` field and `PlaceFloorBase`.
  4. Pools, Restart, Clear/Release and the 0 B recompose behaviour are unchanged.
- Behavior that must remain unchanged:
  - Wall, corner, door and arrow placement.
  - The floor-base colour source (the database field).
  - The pool lifecycle rules.
  - Other prefabs.

## Ownership

- Owner/writer role: environment/play-area session (this conversation), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Environment/EnvironmentComposer.cs
  - Assets/ColorBlockJam/Environment/EnvironmentDatabase.cs
  - Assets/ColorBlockJam/Environment/EnvironmentView.cs
  - Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset
  - Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab
  - Assets/ColorBlockJam/Prefabs/Environment/FloorBase.prefab
  - Assets/ColorBlockJam/Prefabs/Environment/FloorBase.prefab.meta
  - Assets/ColorBlockJam/Tests/Environment/**
  - docs/Environment-Layout.md
  - docs/environment/**
  - production/tasks/TASK-20260924-224137887-1a445bb0-floor-base-per-tile/**
  - production/tasks/TASK-20260924-185655569-dea430a7-play-area-environment/contract.md (lifecycle history line only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Prefabs/Board/**
  - Assets/ColorBlockJam/Data/Art/**
  - Assets/ColorBlockJam/Editor/**
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session (unchanged GUID)
  - Asset: Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session (unchanged GUID)
  - Asset: Assets/ColorBlockJam/Prefabs/Environment/FloorBase.prefab
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session
- Other active writers/worktrees checked: `TASK-20260924-185655569-dea430a7-play-area-environment` (same session) owns these paths and waits on the level-pipeline v2 schema; this revision replaces its floor-base behaviour. `TASK-20260924-222616741-46522211-level-editor-scene-preview` consumes `EnvironmentView` read-only.
- Execution dependency: TASK-20260924-222616741-46522211-level-editor-scene-preview reaches a stable verifier handoff (it exercises EnvironmentView in the live Editor)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given fixture layouts, including a 2x2 hole and a corner notch, when composed, then there is exactly one floor-base placement per floor cell at that cell's centre, and none for void cells (EditMode).
2. Given a built view, then no single stretched plane exists, every base square is a pooled `BoardPieceView` tinted with the database floor-base colour, and Clear/Release return and destroy them (PlayMode).
3. Given level 1 in the Game scene or the LevelEditor preview, then each floor tile has a base square under it, and the captures show the background through hole cells.
4. Given the existing environment tests, then all pass after updating only the floor-base expectations; the 0 B recompose test stays green.

## References and constraints

- Story/GDD/ADR/design/log/image: user revision message 2026-09-25; `docs/Environment-Layout.md` floor base section; PROJECT.md Performance Rule 3 (pooled environment pieces), DEC-100.
- Unity version/platform: 2022.3.62f2
- Package/architecture constraints: Onity pools; no new package; composer stays plain C#.
- Explicit out of scope: changing the floor-base colour, the Grid tile mesh, and wall rules.

## Verification and risk

- Focused test path/command: `EnvironmentComposerTests`, `EnvironmentViewLifecycleTests` (EditMode/PlayMode under Tests/Environment)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1 and 4
- PlayMode: acceptance 2
- Manual/built-player/profiler/device evidence:
  - Captures of level 1 and of the hole fixture (acceptance 3).
  - User acceptance of the look.
- Risk: high (prefab and data asset writes)
- Approval already granted (exact target only): user revision 2026-09-25 plus "her şeyi sen yap" for the environment prefab, database and new FloorBase prefab listed above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-224137887-1a445bb0-floor-base-per-tile, then an independent verifier

## Stop conditions

- Removing the plane would change another prefab or a scene.
- The pool cap (`k_poolMaxSize` 1024) would be exceeded by the largest supported board.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:a46a8df6bb6ba9ad3fada1336ceb965c7aa690f63a0ca59847a36058f4954bc0
- Evidence fingerprint: sha256:ae1b35ea5a60e584b1eed8e663d4d13a42f23904200a0d1d0a86c4842f8c9070
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:66daa5df19fedaef095e8007e8015964553bf65b9e210205b615147dd2b5ef13
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:66daa5df19fedaef095e8007e8015964553bf65b9e210205b615147dd2b5ef13
- Closed at: 2026-09-25T03:15Z
- Closure mode: automatic
- Closed by: implement-task (direct, empty ledger; independent verifier PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 One base per floor cell | PASS | EditMode run via MCP (job 31656ee39a784e76bf4b0beb4e058003) plus live per-cell check with execute_code on two fixtures and on level 1 (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:66daa5df19fedaef095e8007e8015964553bf65b9e210205b615147dd2b5ef13 |
| AC-2 Pooled squares, no plane | PASS | PlayMode run via MCP (job f9a37e65c45e4c539cafb6bfc4e1e6e2) plus live Editor inspection with reflection and pool counts (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:66daa5df19fedaef095e8007e8015964553bf65b9e210205b615147dd2b5ef13 |
| AC-3 Visual check | PASS | Live builds of LevelEditor preview level-01 and Game.unity level-01 (opened additively, closed without saving) and two fixtures, captured to captures/verify2-*. (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:66daa5df19fedaef095e8007e8015964553bf65b9e210205b615147dd2b5ef13 |
| AC-4 Environment tests green | PASS | Fresh MCP test runs: EditMode ColorBlockJam.Environment.EditModeTests + ColorBlockJam.Editor.LevelPreview.Tests, PlayMode ColorBlockJam.Environment.PlayModeTest (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:66daa5df19fedaef095e8007e8015964553bf65b9e210205b615147dd2b5ef13 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's floor-base revision; supersedes the single-plane decision of the environment task
- 2026-09-25T03:15Z — in_progress — task-chain workflow: implement, 3 reviewers (5 findings), fix, independent verify
- 2026-09-25T03:15Z — TASK_CLOSED — all criteria PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
