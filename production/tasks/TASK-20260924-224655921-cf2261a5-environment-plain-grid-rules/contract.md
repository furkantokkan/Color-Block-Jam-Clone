# TASK-20260924-224655921-cf2261a5-environment-plain-grid-rules — Environment wall rules in plain C#; remove Tilemap and RuleTile

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-224655921-cf2261a5-environment-plain-grid-rules
- Supersedes: none
- Title: Environment wall rules in plain C#; remove Tilemap and RuleTile
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: refactor
- Area: Play-area environment (wall/corner rules)
- Workflow: direct

## Objective and baseline

- Observable objective: The play area builds exactly the same walls, corners, doors and floor as today, but its wall/corner rules are a small plain C# table on the unit grid. The Tilemap, RuleTile assets and the `com.unity.2d.tilemap.extras` package are gone.
- Current behavior/evidence:
  - User question 2026-09-25: "tile map ve rule tile aktif olarak kullanılıyor mu placement'ta, eğer gerek yoksa kaldırıp normal unit grid sistemine dönebiliriz" ("are the tile map and rule tile actively used in placement? If not needed, we can remove them and go back to a normal unit grid system").
  - Verified in code, with three facts:
    - The Tilemap is renderer-less. It stores only the floor mask, so RuleTile can read neighbours through `ITilemap` (`EnvironmentComposer.cs:40-42`, `:78`).
    - The RuleTile never places or instantiates anything. `TryMatchRule` only asks `RuleMatches` for three rotated rules (InnerCorner, Wall, OuterCorner) and takes the matched angle as the piece yaw (`EnvironmentComposer.cs:150-168`).
    - All positions are already plain C# on the unit grid, as half-cell quadrants.
  - The only users are `ColorBlockJam.Environment` and its two test asmdefs, plus `Data/Environment/WallRuleTile.asset` and `FloorMarker.asset`. The `Environment.prefab` BoardRoot carries a `Grid` component and a `FloorMask` Tilemap child only for this.
  - DEC-100 (2026-09-24) chose the RuleTile on the user's original request; this revision reverses that part on the user's order.
- Expected behavior:
  1. A plain C# rule table replaces the RuleTile evaluation. It reproduces `RuleTile.RuleMatches` exactly for the three authored rules, with the same rule order, the same rotation order (0/90/180/270) and the same rotated neighbour offsets as the package's `GetRotatedPosition`:
     - InnerCorner: right Floor, up Floor → `Corner_1`.
     - Wall: right Floor, up Void → `Wall`.
     - OuterCorner: left Void, down Void, down-left Floor → `Corner_1`.
  2. The floor mask is a reused bool array over the quadrant grid with the one-cell ring. It grows only when a larger board arrives, so recompose stays at 0 B.
  3. `EnvironmentDatabase` holds the three rule outputs as prefab references (the same `Corner_1`/`Wall` prefab GUIDs). `m_wallRules` is removed.
  4. The following are removed:
     - `EnvironmentRuleTile.cs`, `WallRuleTile.asset` and `FloorMarker.asset`, each with its `.meta`.
     - The `Grid` component and the `FloorMask` Tilemap child in `Environment.prefab`.
     - The `Unity.2D.Tilemap.Extras` asmdef references.
     - The `com.unity.2d.tilemap.extras` manifest entry, which also takes `com.unity.2d.tilemap` out of the lock. The built-in `com.unity.modules.tilemap` stays.
  5. Docs follow the change: `docs/Environment-Layout.md` gets the rule table, and PROJECT.md gets a DEC-100 revision row and updated environment and README wording.
- Behavior that must remain unchanged:
  - Every composer placement: piece, local position, yaw and scale for all fixtures (R, W, H, L, P, C, D, transpose) and for the reference levels. The existing expectations are the golden reference.
  - Pools, Clear/Release, Restart no-op, doors and arrows.
  - The `IBoardLayout` contract and the Game scene.

## Ownership

- Owner/writer role: environment/play-area session (this conversation), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Data/Environment/**
  - Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab
  - Assets/ColorBlockJam/Tests/Environment/**
  - Assets/ColorBlockJam/Editor/LevelPreview/ColorBlockJam.Editor.LevelPreview.asmdef
  - Packages/manifest.json
  - Packages/packages-lock.json
  - docs/Environment-Layout.md
  - PROJECT.md
  - README.md
  - production/tasks/TASK-20260924-224655921-cf2261a5-environment-plain-grid-rules/**
  - production/tasks/TASK-20260924-185655569-dea430a7-play-area-environment/contract.md (lifecycle history line only)
- Forbidden paths/actions:
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Prefabs/Board/**
  - Assets/ColorBlockJam/Editor/Levels/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Any package change other than removing `com.unity.2d.tilemap.extras`
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session (GUID unchanged)
  - Asset: Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session (GUID unchanged)
  - Asset: Assets/ColorBlockJam/Data/Environment/WallRuleTile.asset and FloorMarker.asset (deleted)
    Sole writer: environment/play-area session
    Matching `.meta` owner: environment/play-area session (deleted with the asset)
- Other active writers/worktrees checked:
  - `TASK-20260924-222616741-46522211-level-editor-scene-preview` (same session) runs first and may reference Tilemap.Extras in its asmdef.
  - `TASK-20260924-224137887-1a445bb0-floor-base-per-tile` edits the same composer, database and prefab; run it after this task, sequentially in the same session.
  - No other session references Tilemap types (grep over `Assets/ColorBlockJam`).
- Execution dependency: TASK-20260924-222616741-46522211-level-editor-scene-preview reaches a stable verifier handoff
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the existing composer fixtures and reference levels, when composed by the plain rule table, then every placement equals the current expectations byte-for-byte: no expected value in `EnvironmentComposerTests` changes except the removal of RuleTile-specific setup.
2. Given the rule table, when each rule/angle case is unit-tested against the documented neighbour patterns, then it returns the same rule and yaw as RuleTile did (EditMode; each case derived from the old asset before deletion).
3. Given the project after the change, then no `UnityEngine.Tilemaps` usage, no RuleTile asset and no `com.unity.2d.tilemap.extras` entry remains; compile is clean and all Environment EditMode and PlayMode tests pass, including 0 B recompose.
4. Given level 1 in the Game scene or the LevelEditor preview, then the capture matches the pre-change capture.

## References and constraints

- Story/GDD/ADR/design/log/image: user message 2026-09-25; `docs/Environment-Layout.md` rule section; PROJECT.md DEC-100; `Library/PackageCache/com.unity.2d.tilemap.extras@3.1.3/Runtime/Tiles/RuleTile/RuleTile.cs` (`RuleMatches`, `GetRotatedPosition`) as the semantic reference before removal.
- Unity version/platform: 2022.3.62f2
- Package/architecture constraints: plain C#, no new package, no System.Linq, zero allocation on recompose.
- Explicit out of scope: changing wall geometry (half-cell pieces stay), door rules, and the floor base (tracked in `TASK-20260924-224137887-1a445bb0-floor-base-per-tile`).

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/Environment/**` (EditMode and PlayMode)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile after the package removal and a domain reload
- EditMode: acceptance 1, 2 and 3
- PlayMode: environment lifecycle tests
- Manual/built-player/profiler/device evidence: before/after capture of level 1 (acceptance 4)
- Risk: high (package removal, prefab and data asset writes, asset deletion)
- Approval already granted (exact target only): user message 2026-09-25 ("gerek yoksa kaldırıp") for removing the Tilemap/RuleTile usage and the `com.unity.2d.tilemap.extras` package, plus "her şeyi sen yap"
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-224655921-cf2261a5-environment-plain-grid-rules, then an independent verifier

## Stop conditions

- Any placement differs from the golden expectations and the difference cannot be traced to a RuleTile semantic that the table must copy.
- Another assembly outside the Environment references Tilemap types.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: none
- Contract fingerprint: sha256:c2443a9ac9c2bde8bc1f45a6bd114a39f2b5fef2d12d99066aa2fefde9cce22f
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: none
- Closed at: 2026-09-24T23:13Z
- Closure mode: superseded
- Closed by: task-done --superseded-by (user decision: keep the RuleTile, drop only Tilemap/Grid)
- Superseded by: TASK-20260924-231230227-11a5940c-ruletile-without-tilemap

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Golden placements unchanged | UNPROVEN | none | none |
| AC-2 Rule table cases | UNPROVEN | none | none |
| AC-3 Tilemap/RuleTile/package removed, tests green | UNPROVEN | none | none |
| AC-4 Visual parity | UNPROVEN | none | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's question; verified that the Tilemap is data-only and the RuleTile only matches three rules
- 2026-09-24T23:13Z — closed (superseded) — user: RuleTile is in use, so no removal; only the Tilemap/Grid pair goes (TASK-20260924-231230227-11a5940c-ruletile-without-tilemap)
<!-- TASK-LIFECYCLE:END -->
