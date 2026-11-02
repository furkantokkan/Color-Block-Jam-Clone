# TASK-20260925-102657924-08cc082b-barrier-board-view — Barrier obstacle is built on the board and can rise and sink

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-102657924-08cc082b-barrier-board-view
- Supersedes: none
- Title: Barrier obstacle is built on the board and can rise and sink
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7
- Task kind: feature
- Area: Play-area environment (board objects: Barrier view)
- Workflow: direct

## Objective and baseline

- Observable objective: Barrier cell objects from the level JSON appear on the play area in the Game scene and the LevelEditor preview: raised wall pieces over the barrier cells when closed, sunk below the floor when open. A presentation API lets the gameplay owner open or close a barrier with a short, owned, cancellable animation.
- Current behavior/evidence:
  - User decisions 2026-09-25: build one obstacle, the Barrier (OBJ-05), shown under a new Obstacles section of the Level Editor.
  - Catalog OBJ-05: "closed cells are walls, open cells floor; the state starts at `startsOpen` and flips on each `BlockCleared`"; "View and assets: `Wall` pieces raised from the floor when closed, sunk when open (tween per piece)"; AC-OBJ-05.1/.2.
  - The play area already composes wall and corner pieces around void cells with the project's `EnvironmentRuleSet` on the Unity Grid, with pooled pieces (`EnvironmentComposer`/`EnvironmentView`). The schema for cell objects comes from `TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects`.
- Expected behavior:
  1. **Composition.** A plain C# barrier composer reuses the rule set and quadrant grid. It treats a barrier's cells as the walled region and composes only that region's pieces, the same way an interior wall block is composed. Floor tiles and floor-base squares stay under the barrier cells. Pieces come from the existing environment pools; no new mesh is created.
  2. **View.** `EnvironmentView` (or a small barrier view it owns):
     - builds barrier pieces once per level on Home from the cell objects;
     - places them raised (closed) or sunk below the floor (open) according to `startsOpen`;
     - tints them with an `EnvironmentDatabase` barrier colour (a new field, default `PaletteColor.Wall`).
  3. **Presentation API for gameplay.** `SetBarrierOpen(string objectId, bool open, bool animate)`:
     - With `animate`, it runs a short PrimeTween per piece (about 0.2-0.3 s), targeting the pieces so `Tween.StopAll(target)` and the session's Suspend/Teardown stop it.
     - Without `animate`, it snaps.
     - It never changes domain state.
     - `Clear`/`Release` stop tweens and return pieces.
  4. **LevelEditor preview.** It shows barriers in their start state. A draft change (cells or `startsOpen`) republishes and rebuilds.
  5. **Docs.** `docs/Environment-Layout.md` gets a board-objects section (Barrier composition, the API and its owner/stop point), and PROJECT.md gets one sentence plus a decision row.
- Behavior that must remain unchanged:
  - Levels without cell objects compose exactly as before (golden environment tests).
  - Pools, Restart no-op and 0 B recompose.
  - Doors, arrows and floor base.

## Ownership

- Owner/writer role: environment/play-area session (this conversation), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Tests/Environment/**
  - Assets/ColorBlockJam/Tests/LevelPreview/**
  - docs/Environment-Layout.md
  - docs/environment/**
  - PROJECT.md
  - production/tasks/TASK-20260925-102657924-08cc082b-barrier-board-view/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Levels/** (schema task)
  - Assets/ColorBlockJam/Editor/Levels/** (Level Editor session: Obstacles brush)
  - Gameplay rules (occupancy, flip on BlockCleared): the gameplay owner
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/Board/**
  - New meshes or FBX edits
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session (GUID unchanged)
- Other active writers/worktrees checked:
  - The Level Editor session builds the Obstacles section and Barrier brush.
  - The gameplay session owns block movement and gate exit (`TASK-20260925-012336316-a88e25db-block-drag-gate-exit`) and receives the Barrier rule request.
  - `Editor/LevelPreview/**` is also edited by this session's earlier tasks; this one runs after them.
- Execution dependency: TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects closed
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a level with a closed barrier (a 3x1 region) and one open barrier, when the play area builds, then the closed barrier shows raised wall pieces covering exactly its cells, and the open one's pieces sit below the floor. The EditMode composer tests check piece kinds, positions and yaws for straight, L-shaped and single-cell barriers.
2. Given `SetBarrierOpen(id, open, animate: true)`, then the pieces tween to the target height within 0.3 s. Suspend pauses the tween and Teardown stops it and returns the pieces, and no tween remains on the targets (PlayMode lifecycle test).
3. Given levels without cell objects, then all existing environment golden tests and 0 B recompose stay green.
4. Given the LevelEditor preview with a barrier level, then barriers appear in their start state and follow draft edits (live check plus capture).

## References and constraints

- Story/GDD/ADR/design/log/image: user decisions 2026-09-25; catalog section 5 and OBJ-05; `docs/Environment-Layout.md`; PROJECT.md Performance Rules 3 and 4, lifecycle rules (tween owner and stop point).
- Unity version/platform: 2022.3.62f2
- Package/architecture constraints: PrimeTween for motion; Onity pools; plain C# composer; no Update loop; views never change domain state.
- Explicit out of scope: barrier gameplay rules, the Level Editor brush, and other board objects.

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/Environment/**` (new barrier composer and view tests), LevelPreview tests
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1 and 3
- PlayMode: acceptance 2
- Manual/built-player/profiler/device evidence:
  - Acceptance 4 with a capture.
  - User acceptance of the look.
- Risk: medium (a data asset field and a new presentation API)
- Approval already granted (exact target only): user decisions 2026-09-25 (Barrier) plus "her şeyi sen yap", for the paths above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260925-102657924-08cc082b-barrier-board-view, then an independent verifier

## Stop conditions

- The barrier composition needs a rule-set change or a new mesh.
- The schema task's cell-object API differs from this contract's assumptions: adapt within the allowed paths, or stop if the schema must change.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:6aeeecb41dd14fe05db7153671e57773500df083004f4b4c206baaba78d53683
- Evidence fingerprint: sha256:a0c14f631b0f187c10d0ca927d5b7e6c16502ff763fee53c33eb6b83080911db
- Reviewed revision: vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c644862a3e7dd07909da2e7f283b9775df2a5f76637140b36154ef9a5bb05d32
- Attempt count: 1
- Last feedback: verifier 2: AC-1..AC-4 PASS; wide-barrier gap D-001 escalated
- Blocker: D-001 needs a user decision (one-cell-wide barrier validation vs rule-set/piece change); user acceptance of the look pending (captures in verify/)
- Next action: None
- Closed revision: vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c644862a3e7dd07909da2e7f283b9775df2a5f76637140b36154ef9a5bb05d32
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Closed/open barrier composition | PASS | independent verifier 2 (evidence.md): EnvironmentBarrierComposerTests single/3x1/L + edge/void, PlayMode view test, live 1 piece per quadrant | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c644862a3e7dd07909da2e7f283b9775df2a5f76637140b36154ef9a5bb05d32 |
| AC-2 Owned rise/sink animation | PASS | independent verifier 2 (evidence.md): PlayMode 13/13 + full 44/44, 0.3 s flip, Suspend/Teardown, Is.Not.AllocatingGCMemory | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c644862a3e7dd07909da2e7f283b9775df2a5f76637140b36154ef9a5bb05d32 |
| AC-3 Existing environment unchanged | PASS | independent verifier 2 (evidence.md): full EditMode 800/801 (ShapePrefabTests file-lock flake passed on rerun), goldens + 0 B recompose | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c644862a3e7dd07909da2e7f283b9775df2a5f76637140b36154ef9a5bb05d32 |
| AC-4 LevelEditor preview shows barriers | PASS | independent verifier 2 (evidence.md): live LevelEditor preview start + edited drafts, captures verify/verifier2-ac4-*.png | vcs:plastic:cs:7@ColorBlockJamCase2026@efurk64@unity+owned-sha256:c644862a3e7dd07909da2e7f283b9775df2a5f76637140b36154ef9a5bb05d32 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| D-001 | barrier-view/wide-barrier-inner-quadrants | BLOCKED | AC_FAILURE | objective (AC-1 scenario passes) | A closed barrier 2+ cells wide leaves its inner quadrants without a piece (2x2: 12 of 16); needs a user decision: one-cell-wide validation in the v2 schema, or a rule-set/piece change | [defects/D-001.md](defects/D-001.md) | none |

### Transition history

- Created — planning — user chose the Barrier as the first obstacle; view and presentation API for the play area and preview
- 2026-09-25T15:43Z — in_progress/verification — chain: implement, 3 reviewers (8 findings), fix (6 fixed, wide-barrier escalated), verify PASS, retry-fix (stray InitTestScene removed), re-verify PASS; AC-1..AC-4 PASS; D-001 BLOCKED on a user decision
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
