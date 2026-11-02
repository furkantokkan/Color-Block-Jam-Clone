# TASK-20260924-231230227-11a5940c-ruletile-without-tilemap — Keep the RuleTile rules; drop the Tilemap and Grid; verify the scenes

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-231230227-11a5940c-ruletile-without-tilemap
- Supersedes: TASK-20260924-224655921-cf2261a5-environment-plain-grid-rules
- Title: Keep the RuleTile rules; drop the Tilemap and Grid; verify the scenes
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: refactor
- Area: Play-area environment (wall/corner rule evaluation)
- Workflow: direct

## Objective and baseline

- Observable objective: The play area is built by the same `WallRuleTile` rules as today, evaluated against a plain floor array. No Tilemap and no Grid component take part, and the Game scene and the LevelEditor preview are verified to look and behave correctly.
- Current behavior/evidence:
  - User decision 2026-09-25: "kullanıyorsak remove gerekli değil; bu durumda sahnede her şeyin düzgün olduğundan emin ol; tilemap ve grid aynı anda kullanmak gereksizse kullanma". In English: if we use the RuleTile, removal is not needed; make sure everything in the scene is right; if using the tilemap and grid together is unnecessary, don't use them.
  - This supersedes `TASK-20260924-224655921-cf2261a5-environment-plain-grid-rules`, which planned to delete the RuleTile and the package.
  - Verified in `Library/PackageCache/com.unity.2d.tilemap.extras@3.1.3/Runtime/Tiles/RuleTile/RuleTile.cs:765-782`: `RuleMatches` touches the Tilemap only through `tilemap.GetTile(GetOffsetPosition(position, GetRotatedPosition(neighborPosition, angle)))`. `GetRotatedPosition` (`:815`), `GetOffsetPosition` (`:853`) and `RuleMatch(int, TileBase)` (`:743`) are public virtual members of the RuleTile.
  - The composer builds a renderer-less Tilemap only to satisfy that one call:
    - `EnvironmentComposer.cs:40-42`: `new ITilemap(floorMask)`.
    - `EnvironmentComposer.cs:65-78`: `SetTile` of the floor marker per floor quadrant.
    - `EnvironmentView.cs:25`, `:62`, `:150-152`: the `m_floorMask` field.
    - `Environment.prefab`: a `Grid` component (cell 0.5, swizzle XZY) on BoardRoot, and a `FloorMask` Tilemap child.
- Expected behavior:
  1. The composer keeps `WallRuleTile` as its only rule source.
     - For each candidate quadrant and each rule and angle (same order as today), it walks the rule's neighbours exactly like `RuleMatches`: `offset = m_rules.GetOffsetPosition(position, m_rules.GetRotatedPosition(neighborPosition, angle))`.
     - It passes `other = floorMask[offset] ? m_rules floor marker : null` to `m_rules.RuleMatch(neighbor, other)`.
     - Rule and angle order, and the yaw mapping, are unchanged.
  2. The floor mask is a reused `bool[]` over the quadrant grid, including the one-cell ring. It grows only when a larger board arrives (0 B recompose kept).
  3. `EnvironmentComposer` no longer takes a Tilemap. The Tilemap-related members are removed:
     - `EnvironmentView.m_floorMask` and the `ClearAllTiles` call;
     - the `FloorMask` child of `Environment.prefab`;
     - the BoardRoot `Grid` component, but only if nothing else reads it (check the LevelPreview host and the tests).
  4. Kept unchanged: `EnvironmentRuleTile`, `WallRuleTile.asset`, `FloorMarker.asset` (still the rule's Floor identity), the `EnvironmentDatabase` fields, and the `com.unity.2d.tilemap.extras` package.
  5. Scene verification ("make sure everything in the scene is right"):
     - In the live Editor, the Game scene (direct Play and level 1 build) and the LevelEditor preview (levels 1-5) build with 0 Console errors or warnings from the environment.
     - Walls, corners, doors, arrows and the floor match the pre-change captures, and no Tilemap or Grid component exists under the environment hierarchy.
     - Before/after captures are stored in the task folder.
  6. Docs: `docs/Environment-Layout.md` explains that the RuleTile is evaluated directly against a floor array (no Tilemap/Grid), and PROJECT.md DEC-100 and the environment bullet are corrected ("One renderer-less Tilemap stores the floor mask …" is removed).
- Behavior that must remain unchanged:
  - Every composer placement: piece, local position, yaw and scale for all fixtures (R, W, H, L, P, C, D, transpose) and the reference levels. The existing test expectations are the golden reference.
  - Pools, Clear/Release, Restart no-op, doors and arrows.
  - Game scene flow.

## Ownership

- Owner/writer role: environment/play-area session (this conversation), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Environment/EnvironmentComposer.cs
  - Assets/ColorBlockJam/Environment/EnvironmentView.cs
  - Assets/ColorBlockJam/Environment/EnvironmentRuleTile.cs
  - Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab
  - Assets/ColorBlockJam/Tests/Environment/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - docs/Environment-Layout.md
  - PROJECT.md
  - README.md
  - production/tasks/TASK-20260924-231230227-11a5940c-ruletile-without-tilemap/**
  - production/tasks/TASK-20260924-185655569-dea430a7-play-area-environment/contract.md (lifecycle history line only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Data/Environment/WallRuleTile.asset (kept unchanged)
  - Assets/ColorBlockJam/Data/Environment/FloorMarker.asset (kept unchanged)
  - Assets/ColorBlockJam/Scenes/** (verification opens scenes without saving)
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Editor/Levels/**
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session (GUID unchanged)
- Other active writers/worktrees checked: `TASK-20260924-222616741-46522211-level-editor-scene-preview` (same session) is verifying in the live Editor; this task starts after its handoff. `TASK-20260924-224137887-1a445bb0-floor-base-per-tile` edits the same composer and view and runs after this task.
- Execution dependency: TASK-20260924-222616741-46522211-level-editor-scene-preview verifier handoff
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given every existing composer fixture and reference level, when composed without a Tilemap, then all placements equal the current expectations. `EnvironmentComposerTests` changes only in setup (no Tilemap/Grid).
2. Given a quadrant neighbourhood for each rule at each angle, then the array evaluation returns the same rule and angle as `RuleMatches` on a Tilemap. This is an equivalence test that builds both inside the test and compares all 256 neighbour patterns of a 3x3 quadrant window.
3. Given the Environment prefab and a built view, then no `Tilemap` or `Grid` component exists under the environment, and recomposing the same or a smaller board allocates 0 B.
4. Given the Game scene (level 1) and the LevelEditor preview (levels 1-5) in the live Editor, then each builds with no environment errors or warnings, and before/after captures match.

## References and constraints

- Story/GDD/ADR/design/log/image: user decision 2026-09-25; `RuleTile.cs` lines above; `docs/Environment-Layout.md`; PROJECT.md DEC-100 and Performance Rule 3.
- Unity version/platform: 2022.3.62f2
- Package/architecture constraints: RuleTile stays the rule authority; no new package; no System.Linq; zero allocation on recompose.
- Explicit out of scope: changing rules or geometry, the floor base (separate task), and the camera (separate task).

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/Environment/**` (EditMode and PlayMode)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1-3
- PlayMode: the environment lifecycle tests
- Manual/built-player/profiler/device evidence:
  - Acceptance 4: live Editor checks and captures, with scenes left unsaved.
  - User acceptance.
- Risk: medium (a prefab write; behavior must stay identical)
- Approval already granted (exact target only): user decision 2026-09-25 plus "her şeyi sen yap", for the Environment prefab and code above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-231230227-11a5940c-ruletile-without-tilemap, then an independent verifier

## Stop conditions

- The equivalence test finds any pattern where the array result differs from `RuleMatches`.
- Something outside the environment reads the Grid or Tilemap.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: none
- Contract fingerprint: sha256:27f635da5236eaa298d4f865ad138c3bbdb1061fea0dc9a0cf3a34b6c4dfb1a9
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: none
- Closed at: 2026-09-24T23:15Z
- Closure mode: superseded
- Closed by: task-done --superseded-by (user decision: own rule set on the Unity Grid)
- Superseded by: TASK-20260924-231413359-d89f8597-environment-rule-set-grid

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Golden placements unchanged | UNPROVEN | none | none |
| AC-2 Array evaluation equals RuleMatches | UNPROVEN | none | none |
| AC-3 No Tilemap/Grid, 0 B recompose | UNPROVEN | none | none |
| AC-4 Scenes verified in the live Editor | UNPROVEN | none | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — user decision: keep the RuleTile, drop the Tilemap/Grid pair, verify the scenes; supersedes the plain-grid-rules contract
- 2026-09-24T23:15Z — closed (superseded) — user: remove the RuleTile, build a similar own system on the Unity Grid (TASK-20260924-231413359-d89f8597-environment-rule-set-grid)
<!-- TASK-LIFECYCLE:END -->
