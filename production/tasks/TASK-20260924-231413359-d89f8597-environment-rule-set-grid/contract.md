# TASK-20260924-231413359-d89f8597-environment-rule-set-grid — Own environment rule set on the Unity Grid; remove RuleTile and Tilemap

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-231413359-d89f8597-environment-rule-set-grid
- Supersedes: TASK-20260924-231230227-11a5940c-ruletile-without-tilemap
- Title: Own environment rule set on the Unity Grid; remove RuleTile and Tilemap
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: refactor
- Area: Play-area environment (wall/corner rules and grid coordinates)
- Workflow: direct

## Objective and baseline

- Observable objective: Walls and corners come from a project-owned rule set asset. It has a readable 3x3 neighbour editor (like the RuleTile's, but showing Floor/Void/Any instead of raw numbers) and is evaluated on a Unity `Grid`. The RuleTile, Tilemap and `com.unity.2d.tilemap.extras` package are gone, and the Game scene and LevelEditor preview look exactly as before.
- Current behavior/evidence:
  - User decision 2026-09-25: "rule tile kaldırıp buna benzer bir sistem ayarlayıp unity grid'de kullanabiliriz, böylece sistemimiz daha doğru çalışır" ("we can remove the rule tile, set up a similar system and use it on the Unity grid, so our system works more correctly").
  - The accompanying screenshot shows the `WallRuleTile` inspector. It has three Rotated rules (Corner_1, Wall, Corner_1), with neighbour cells displayed as the raw numbers 3 (Floor) and 4 (Void) because the custom neighbour class has no icons.
  - This supersedes `TASK-20260924-231230227-11a5940c-ruletile-without-tilemap`, which kept the RuleTile.
  - Verified facts:
    - `RuleTile.RuleMatches` (`Library/PackageCache/com.unity.2d.tilemap.extras@3.1.3/Runtime/Tiles/RuleTile/RuleTile.cs:765-782`) reads the Tilemap only through `GetTile`. Rotation comes from `GetRotatedPosition` (`:815`) and the offset from `GetOffsetPosition` (`:853`).
    - The composer builds a renderer-less Tilemap for that (`EnvironmentComposer.cs:40-42`, `:65-78`; `EnvironmentView.cs:25`, `:62`, `:150-152`).
    - `Environment.prefab` has a `Grid` on BoardRoot (cell 0.5, swizzle XZY) and a `FloorMask` Tilemap child.
- Expected behavior:
  1. **Rule set asset.** `EnvironmentRuleSet : ScriptableObject` in `ColorBlockJam.Environment`, instance `Data/Environment/EnvironmentRuleSet.asset`.
     - It holds ordered rules; each rule has an output `BoardPieceView` prefab, eight neighbour states (Any / Floor / Void) and a Rotated flag (0/90/180/270).
     - It is migrated from `WallRuleTile.asset`: the same three rules, order, neighbour positions, values and outputs (`Corner_1`, `Wall`, `Corner_1`).
     - `Validate` checks outputs, neighbour data and at least one non-Any neighbour per rule.
  2. **Matcher.** A plain C# matcher evaluates the rules against a reused floor `bool[]` over the quadrant grid with the one-cell ring.
     - It keeps the rule order and angle order and reproduces RuleTile's rectangular-grid rotation semantics exactly.
     - The matched angle is the piece yaw, as today.
     - Recompose stays at 0 B.
  3. **Unity Grid.** Quadrant and cell coordinates map to local positions through the `Grid` component on BoardRoot (`GridLayout.GetCellCenterLocal` or equivalent, cell 0.5, swizzle XZY). The plain composer keeps producing grid coordinates, so it stays testable, and the view converts them with the Grid. Resulting local positions equal today's positions.
  4. **Custom inspector.** A UI Toolkit inspector (editor-only asmdef under `Environment/Editor/`) shows each rule as:
     - an output prefab field;
     - a 3x3 grid whose centre is fixed and whose neighbour buttons cycle Any → Floor (green) → Void (red), with tooltips;
     - a Rotated toggle;
     - add/remove/reorder, with Undo and dirty handling.
  5. **Equivalence proof before removal.** An EditMode test builds the old `WallRuleTile` on a temporary Tilemap and the new rule set and matcher. It compares all 256 patterns of the 8 neighbours at all 4 angles and must return the same rule and angle for every case. It runs green and its result is recorded in `evidence.md`. The test is then converted to a table test that keeps the recorded expectations without the package.
  6. **Removal.**
     - `EnvironmentRuleTile.cs`, `WallRuleTile.asset` and `FloorMarker.asset`, each with its `.meta`.
     - The `FloorMask` Tilemap child and every Tilemap usage.
     - The `Unity.2D.Tilemap.Extras` asmdef references (Environment, the tests, and LevelPreview if present).
     - The `com.unity.2d.tilemap.extras` manifest entry (the lock drops `com.unity.2d.tilemap`).
     - Kept: the built-in `com.unity.modules.tilemap` (and the Grid module).
  7. `EnvironmentDatabase` references `EnvironmentRuleSet` instead of the RuleTile (same asset GUID for the database).
  8. **Scene verification.** In the live Editor, the Game scene (level 1) and the LevelEditor preview (levels 1-5) build with 0 environment errors or warnings, and the before/after captures match. No `Tilemap` remains, and exactly one `Grid` remains, on BoardRoot.
  9. **Docs.** `docs/Environment-Layout.md` gets the rule set, inspector and Grid section, and PROJECT.md gets a DEC-100 revision row plus the environment bullet and Performance Rule 3 wording. The README drops Tilemap from the runtime-loop notes.
- Behavior that must remain unchanged:
  - Every composer placement (piece, local position, yaw, scale) for all fixtures (R, W, H, L, P, C, D, transpose) and the reference levels. The existing expectations are the golden reference.
  - Pools, Clear/Release, Restart no-op, doors, arrows and the floor.
  - Game scene flow.

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
  - production/tasks/TASK-20260924-231413359-d89f8597-environment-rule-set-grid/**
  - production/tasks/TASK-20260924-185655569-dea430a7-play-area-environment/contract.md (lifecycle history line only)
- Forbidden paths/actions:
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/** (verification opens scenes without saving)
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Prefabs/Board/**
  - Assets/ColorBlockJam/Editor/Levels/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Any package change other than removing `com.unity.2d.tilemap.extras`
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Environment/EnvironmentRuleSet.asset (new)
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session
  - Asset: Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session (GUID unchanged)
  - Asset: Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab
    Sole writer: environment/play-area session through Unity APIs
    Matching `.meta` owner: environment/play-area session (GUID unchanged)
  - Asset: Assets/ColorBlockJam/Data/Environment/WallRuleTile.asset and FloorMarker.asset (deleted after the equivalence proof)
    Sole writer: environment/play-area session
    Matching `.meta` owner: environment/play-area session (deleted with the asset)
- Other active writers/worktrees checked:
  - `TASK-20260924-222616741-46522211-level-editor-scene-preview` (same session) is verifying in the live Editor; this task starts after its handoff.
  - `TASK-20260924-224137887-1a445bb0-floor-base-per-tile` and `TASK-20260924-224656127-e361499a-board-art-dependency-injection` touch the same composer, database and view, and run after this task.
  - No code outside Environment and its tests references Tilemap types (grep over `Assets/ColorBlockJam`).
- Execution dependency: TASK-20260924-222616741-46522211-level-editor-scene-preview verifier handoff
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given all 256 neighbour patterns at 4 angles, when the old RuleTile (on a temporary Tilemap) and the new rule set and matcher are evaluated, then both return the same rule and angle for every case. This is proven before any removal and kept afterwards as a recorded table test.
2. Given every composer fixture and reference level, then all placements equal the current expectations after the switch, and again after the removal.
3. Given the rule set inspector, then each rule shows a 3x3 grid with Floor/Void/Any states in colour, the output prefab and the Rotated toggle; edits support Undo and mark the asset dirty (EditMode UI test plus a capture).
4. Given the project after the change, then no `UnityEngine.Tilemaps` usage, no RuleTile asset and no `com.unity.2d.tilemap.extras` entry remains; the environment hierarchy has one `Grid` and no `Tilemap`; compile is clean; all Environment EditMode and PlayMode tests pass, including 0 B recompose.
5. Given the Game scene (level 1) and the LevelEditor preview (levels 1-5) in the live Editor, then each builds without environment errors, and before/after captures match.

## References and constraints

- Story/GDD/ADR/design/log/image: user decision and screenshot 2026-09-25; `RuleTile.cs` lines above; `docs/Environment-Layout.md`; PROJECT.md DEC-100 and Performance Rule 3.
- Unity version/platform: 2022.3.62f2
- Package/architecture constraints:
  - The rule set is the single rule authority; the composer stays plain C# on grid coordinates.
  - The Unity `Grid` is the single coordinate authority for local positions.
  - No new package; no System.Linq; zero allocation on recompose.
  - UI Toolkit inspector in an Editor-only asmdef.
- Explicit out of scope: new rules or geometry changes, the floor base (separate task), and the camera (separate task).

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/Environment/**` (EditMode and PlayMode), including the new rule set, matcher and inspector tests
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile after the package removal and a domain reload
- EditMode: acceptance 1-4
- PlayMode: the environment lifecycle tests
- Manual/built-player/profiler/device evidence:
  - Acceptance 5: live Editor checks and captures, with scenes left unsaved.
  - Inspector capture.
  - User acceptance.
- Risk: high (package removal, new and changed serialized assets, asset deletion)
- Approval already granted (exact target only): user decision 2026-09-25 ("rule tile kaldırıp ... unity grid'de kullanabiliriz") plus "her şeyi sen yap", for the serialized assets and the package removal listed above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-231413359-d89f8597-environment-rule-set-grid, then an independent verifier

## Stop conditions

- The equivalence test differs for any pattern and the difference cannot be explained by RuleTile semantics that the matcher must copy: stop before any removal.
- Grid-derived positions differ from the golden positions.
- Code outside the environment references Tilemap types.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:cb41aba74880b40e8fdf082763151386bf0c19c71f8a889d78107dfaf7584463
- Evidence fingerprint: sha256:c3cddaa023e1febb426510e46c69a55a7b9530d66e321c2f2d8c6dbef3728d07
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:be959c0485f67d6f3da4acc75e127448a8b5acad36a08ab54d78e4559cf88123
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:be959c0485f67d6f3da4acc75e127448a8b5acad36a08ab54d78e4559cf88123
- Closed at: 2026-09-25T03:15Z
- Closure mode: automatic
- Closed by: implement-task (direct, empty ledger; independent verifier PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Rule set equals RuleTile on all patterns | PASS | EditMode table test via MCP run_tests, a source check of the matcher against RuleTile.cs 3.1.3 in the global package cache, and a rerun of the independent Pytho (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:be959c0485f67d6f3da4acc75e127448a8b5acad36a08ab54d78e4559cf88123 |
| AC-2 Golden placements unchanged | PASS | EditMode Environment and LevelPreview suites via MCP, plus live placement signatures compared with the before captures (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:be959c0485f67d6f3da4acc75e127448a8b5acad36a08ab54d78e4559cf88123 |
| AC-3 Readable 3x3 rule inspector | PASS | Live Editor.CreateEditor inspection of the UI Toolkit tree, 4 named EditMode UI tests, and the existing inspector capture (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:be959c0485f67d6f3da4acc75e127448a8b5acad36a08ab54d78e4559cf88123 |
| AC-4 RuleTile, Tilemap and package removed; tests green | PASS | Grep of sources and serialized files, live Client.List and AssetDatabase checks, cm cat of cs:5, and EditMode and PlayMode runs via MCP (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:be959c0485f67d6f3da4acc75e127448a8b5acad36a08ab54d78e4559cf88123 |
| AC-5 Scenes verified in the live Editor | PASS | New live-Editor captures (verify2-*) of LevelEditor preview levels 1-5, the hole/notch fixture, and Game level 1 (opened additively, closed unsaved), compared w (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:be959c0485f67d6f3da4acc75e127448a8b5acad36a08ab54d78e4559cf88123 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — user decision: replace the RuleTile with an own rule set on the Unity Grid; supersedes ruletile-without-tilemap
- 2026-09-25T03:15Z — in_progress — task-chain workflow: implement, 3 reviewers (10 findings), fix, independent verify
- 2026-09-25T03:15Z — TASK_CLOSED — all criteria PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
