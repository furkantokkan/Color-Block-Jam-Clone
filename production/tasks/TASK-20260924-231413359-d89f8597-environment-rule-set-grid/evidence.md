# Evidence: TASK-20260924-231413359-d89f8597-environment-rule-set-grid

Implementer: environment/play-area session, 2026-09-25. Live Editor
`ColorBlockJamCase2026@99077e4df1ecfaa4` (Unity 2022.3.62f2) via MCP for Unity
HTTP (`execute_code`, `refresh_unity`, `run_tests`, `read_console`). Scenes were
never saved: `LevelEditor.unity` stayed open and `dirty=False` after every step.
No Plastic checkin or shelve.

## Order followed

1. Before captures (no code touched yet).
2. `EnvironmentRuleSet`, `EnvironmentNeighbor`, `EnvironmentRuleMatcher`, the
   UI Toolkit inspector and the equivalence test were added while the RuleTile
   still existed; `EnvironmentRuleSet.asset` was migrated from
   `WallRuleTile.asset` through `SerializedObject` in the live Editor.
3. Composer, view and database switched to the rule set and the BoardRoot Grid.
4. RuleTile code/assets, the `FloorMask` Tilemap child, the asmdef references
   and the package were removed; the equivalence test became a table test.
5. After captures, console and scene checks.

## Step 1: before captures

Script: capture camera rendered in edit mode (900x1100). Each capture also
writes a sorted placement signature (piece, local position, yaw, scale, board
root position) to a `.txt` next to the PNG, in `captures/`.

- LevelEditor preview levels 1-5, republished through
  `LevelPreviewOwner.OnLevelPublished`, then level-01 restored
  (`before-preview-level-0N.png/.txt`; 56, 65, 74, 74, 72 pieces).
- Hole/notch fixture (7x6, 2x2 hole, top-left notch, corner notch, interior
  1x1 void, left and bottom doors) in a temporary preview scene closed without
  saving (`before-fixture-hole-notch.*`, 104 pieces).
- Game scene level 1: `Game.unity` opened additively, its own Environment
  instance built, closed with `removeScene` and no save
  (`before-game-level-01.*`, 56 pieces; grids=1, tilemaps=1 before).

## Step 2: migration and equivalence proof (RuleTile still present)

- Migration result: `r0 (0,1)=Floor (1,0)=Floor -> Corner_1, Rotated`;
  `r1 (0,1)=Void (1,0)=Floor -> Wall, Rotated`;
  `r2 (-1,0)=Void (-1,-1)=Floor (0,-1)=Void -> Corner_1, Rotated`.
  New asset GUID `c93ae530aacb511479300a3d3f550dd3`.
- EditMode `ColorBlockJam.Environment.EditModeTests`, job
  `e1865eefd5124be88f5c4fc3935f1479`: 44/44 passed, including
  `EnvironmentRuleSetEquivalenceTests.RuleSetMatchesTheWallRuleTileForEveryPatternAndAngle`
  (old `WallRuleTile` on a temporary Tilemap vs rule set + matcher).
- Independent `execute_code` recount with `RuleTile.RuleMatches`: 3,072
  rule/angle comparisons (256 patterns x 3 rules x 4 angles), 0 mismatches;
  255 of 256 patterns match some rule (pattern 0, all void, matches none).
- Recorded table (bit `rule * 4 + angle / 90`, pattern bit `n` = neighbour
  `n` is floor), now in `EnvironmentRuleSetTableTests`:

```
0, 512, 128, 128, 1024, 1536, 128, 128, 64, 64, 72, 72, 1088, 1088, 72, 72,
16, 528, 129, 129, 16, 528, 129, 129, 80, 80, 73, 73, 80, 80, 73, 73,
256, 768, 384, 384, 1280, 1792, 384, 384, 64, 64, 72, 72, 1088, 1088, 72, 72,
272, 784, 385, 385, 272, 784, 385, 385, 80, 80, 73, 73, 80, 80, 73, 73,
32, 544, 160, 160, 1056, 1568, 160, 160, 36, 36, 44, 44, 1060, 1060, 44, 44,
18, 530, 131, 131, 18, 530, 131, 131, 22, 22, 15, 15, 22, 22, 15, 15,
32, 544, 160, 160, 1056, 1568, 160, 160, 36, 36, 44, 44, 1060, 1060, 44, 44,
18, 530, 131, 131, 18, 530, 131, 131, 22, 22, 15, 15, 22, 22, 15, 15,
2048, 2560, 2176, 2176, 3072, 3584, 2176, 2176, 2112, 2112, 2120, 2120, 3136, 3136, 2120, 2120,
16, 528, 129, 129, 16, 528, 129, 129, 80, 80, 73, 73, 80, 80, 73, 73,
2304, 2816, 2432, 2432, 3328, 3840, 2432, 2432, 2112, 2112, 2120, 2120, 3136, 3136, 2120, 2120,
272, 784, 385, 385, 272, 784, 385, 385, 80, 80, 73, 73, 80, 80, 73, 73,
32, 544, 160, 160, 1056, 1568, 160, 160, 36, 36, 44, 44, 1060, 1060, 44, 44,
18, 530, 131, 131, 18, 530, 131, 131, 22, 22, 15, 15, 22, 22, 15, 15,
32, 544, 160, 160, 1056, 1568, 160, 160, 36, 36, 44, 44, 1060, 1060, 44, 44,
18, 530, 131, 131, 18, 530, 131, 131, 22, 22, 15, 15, 22, 22, 15, 15
```

## Step 3: switch to the rule set and the Unity Grid

- `EnvironmentDatabase.m_ruleSet` bound to the rule set (database GUID
  unchanged); `EnvironmentView.m_grid` bound to the BoardRoot Grid (cell
  0.5, swizzle XZY) through `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`.
- EditMode job `ecc45a4478084717a5209b7779ae9801`: 45/46; every golden
  composer test passed. The single failure was the intended post-removal
  check `PrefabHasOneGridOnBoardRootAndNoTilemap` (FloorMask still present).
- PlayMode `ColorBlockJam.Environment.PlayModeTests` job
  `87796339e1904719afc2ae7980b78b5e`: 6/6.
- `switched-*` captures: all seven placement signatures byte-identical to
  `before-*`.

## Step 4: removal

- Prefab: `FloorMask` child (Tilemap) destroyed and saved; the stale
  `m_floorMask` property is gone; one Grid remains, on BoardRoot.
- `WallRuleTile.asset`, `FloorMarker.asset`, `EnvironmentRuleTile.cs` and the
  temporary `EnvironmentRuleSetEquivalenceTests.cs`, each with its `.meta`,
  were moved out of `Assets` (MCP blocks `AssetDatabase.DeleteAsset`) to the
  session scratchpad `removed-ruletile/` and the Editor refreshed. No asset
  referenced their GUIDs.
- Correction (review, 2026-09-25): these files were never checked in. `cm ls
  Assets/ColorBlockJam/Data --tree=cs:5` lists no `Environment` folder, and
  `Environment/` and `Data/Environment/` are pending adds, so no Plastic
  changeset holds them. Earlier wording that they were recoverable from cs:5
  was wrong. They are archived in this task folder under
  `removed-ruletile/`: `.cs` files end in `.cs.txt`, and the assets and `.meta`
  files keep their GUIDs (`EnvironmentRuleTile.cs` `747b89f5…`). Restoring
  them also needs `com.unity.2d.tilemap.extras` 3.1.3 again.
- `Unity.2D.Tilemap.Extras` removed from `ColorBlockJam.Environment`,
  `ColorBlockJam.Environment.EditModeTests` and
  `ColorBlockJam.Environment.PlayModeTests`. The LevelPreview asmdef never
  listed it (unchanged).
- Package: `UnityEditor.PackageManager.Client.Remove("com.unity.2d.tilemap.extras")`
  in the live Editor. `manifest.json` lost the entry; `packages-lock.json` lost
  `com.unity.2d.tilemap.extras` and `com.unity.2d.tilemap`.
  `com.unity.modules.tilemap` stays. After the domain reload
  `scriptCompilationFailed=False` and `UnityEngine.RuleTile` no longer resolves.
- Grep over `Assets/ColorBlockJam`: no `UnityEngine.Tilemaps` usage; the only
  `Tilemap` text is the type-name check in `PrefabHasOneGridOnBoardRootAndNoTilemap`.

## Step 5: final verification

| Check | Result | Evidence |
|---|---|---|
| Clean compile after package removal and domain reload | Pass, console 0 errors/warnings (only the MCP WebSocket notice) | `read_console` |
| EditMode Environment + LevelPreview tests | 55/55 | job `8ef197ed1a9543e9ae1f85cbd43fce36` |
| PlayMode Environment lifecycle tests | 6/6 | job `07fd129b04df4c7a94bab6b2db4d5f53` |
| Recompose 0 B / matching 0 B | Pass | `RecomposingAPreparedBoardAllocatesZeroBytes`, `MatchingAPreparedMaskAllocatesZeroBytes` |
| Before/after placement signatures (preview 1-5, fixture, Game level 1) | 7/7 byte-identical | `captures/before-*.txt` vs `after-*.txt` |
| Before/after pixels | 7/7 identical (difference bounding box `None`) | `captures/before-*.png` vs `after-*.png` |
| Game scene hierarchy | 1 Grid, 0 Tilemap (before: 1 Tilemap) | capture log |
| LevelEditor preview hierarchy | 1 Grid per level | capture log |
| Console during after captures | 0 errors, 0 warnings | `read_console` |
| Inspector | 3 rule cards, colour-coded 3x3 grids, output fields, Rotated toggles | `captures/inspector-rule-set.png` |

Inspector capture note: the Inspector tab was focused for the screenshot and the
Level Editor tab was focused again afterwards; the selection was empty before
and after.

## Review fixes (2026-09-25)

| Finding | Verdict | Change |
|---|---|---|
| Inspector status box goes stale after Output/Rotated edits | Real | `EnvironmentRuleSetEditor`: `RefreshStatus()` is split out of `RebuildRules`, and the root calls `TrackSerializedObjectValue(serializedObject, …)`, so every asset change refreshes the box. Test: `StatusFollowsEditsMadeThroughTheBoundFields` (hosted in an EditorWindow; it clears and restores an output through a SerializedObject). |
| Live rule set edits break a kept view (`m_pools[-1]`) | Real (latent, from the RuleTile path) | `EnvironmentComposer.Compose` validates the database each time and throws `InvalidOperationException("The environment database is invalid: …")` before it touches the output. `EnvironmentView.Build` recreates the pools on Home when a placement's piece has no pool. Tests: `ComposeNamesARuleSetEditMadeAfterTheComposerWasCreated` (EditMode) and `RuleSetEditsAfterTheFirstBuildGetNewPoolsOrAClearError` (PlayMode). Recompose still allocates 0 B. |
| Removed files "recoverable from cs:5" | Real | Corrected in Step 4. Archived under `removed-ruletile/` in this task folder. |
| Test comment called rule 0 the outer corner rule | Real | `EnvironmentRuleSetTests`: the comment now says inner corner rule. |
| Docs, asmdefs and tests written by Python scripts; PROJECT.md line endings | Real | PROJECT.md had 93 LF-only lines at cs:5 and 0 after `edit_docs.py`. LF was restored on the 75 lines whose text is unchanged since cs:5. Now 0 unchanged-text lines differ in line ending from cs:5. README.md and Environment-Layout.md were LF-only and kept LF. |
| PRD AC-3.14 still names RuleTile rules | Real, out of scope | `docs/Color-Block-Jam-PRD.md` is not an allowed path. Follow-up: a docs task that changes AC-3.14 to the project-owned `EnvironmentRuleSet` (DEC-100, revised 2026-09-25). |

Verification after the fixes (live Editor, `LevelEditor.unity` open,
`dirty=False` throughout):

- Compile clean (`scriptCompilationFailed=False`). After the refresh, the
  console showed three Unity inspector exceptions
  (`TransformInspector`/`GameObjectInspector.OnEnable`/`OnDisable`). They came
  from the selected preview clone `Piece_001(Clone)` being destroyed by the
  domain reload, and none came from project code. The console was then
  cleared and stayed at 0 errors and 0 warnings. The only entry left was an
  MCP TestJobManager notice about a job started during a reload, which was
  then rerun.
- EditMode `ColorBlockJam.Environment.EditModeTests`: 48/48, job
  `1234d272230545acac60fa369afdc1d8`.
- PlayMode `ColorBlockJam.Environment.PlayModeTests`: 7/7, job
  `fa44c194c5e1467fb4a5b3002423c88f`.
- EditMode `ColorBlockJam.Editor.LevelPreview.Tests`: 9/9, job
  `f0c7eaa8a45a4f50ae5185140c55c242`.
- The golden composer tests pass unchanged. The fixes change no placement,
  so the before/after captures were not repeated.

## Not done / for the verifier

- The Game scene was verified by building its own Environment instance in edit
  mode (additive open, closed unsaved), not by entering Play Mode, to leave the
  user's open LevelEditor scene untouched.
- `docs/Color-Block-Jam-PRD.md` AC-3.14 still names the RuleTile-authored
  rules. It is outside this contract's allowed paths and needs a follow-up
  docs task.
- User acceptance of the inspector look is pending.

## Independent verification (2026-09-25, verifier)

Verifier: independent session, no production code or test edits. Live Editor
`ColorBlockJamCase2026@99077e4df1ecfaa4` over MCP for Unity HTTP. Editor state
at start: `LevelEditor.unity` only, `dirty=False`, not playing, compile clean,
Game view focused, preview on level-01. The same state held at the end; no
scene was saved. (At the end the selection was the scene object `Source_1`;
nothing in the verifier's code selects objects, so this was left alone.)

| AC | Result | Channel and evidence |
|---|---|---|
| AC-1 | PASS | `EnvironmentRuleSetTableTests.RuleSetReproducesTheRecordedRuleTileMatchesForEveryPatternAndAngle` passed (EditMode named run, 9/9). Independent recheck `verify-ruletile-table-check.py`: it parses the archived `removed-ruletile/WallRuleTile.asset` and applies the matching logic from `RuleTile.RuleMatches`, `GetRotatedPosition`, `GetOffsetPosition` and the rotation loop (angle 0, then 90/180/270). That logic was read from the cached `com.unity.2d.tilemap.extras@3.1.3` source, along with `EnvironmentRuleTile.RuleMatch` (Floor = marker, Void = not marker). The script matches the recorded 256-entry table exactly (0 mismatches). The pre-removal RuleTile run itself (job `e1865eef…`) cannot be repeated after the package was removed; this recheck stands in for it. |
| AC-2 | PASS | EditMode `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests`: 57/57 (job `15def59c68c141b18452b7dec2dc2950`), including every golden composer fixture and the reference-level tests. The implementer's `edit_composer_tests.py` shows that only the mechanical parts changed (`LocalPosition` became `ToLocal(placement)` through a copy of the prefab Grid, and the RuleTile accessors became RuleSet accessors); no golden value changed. |
| AC-3 | PASS | Live: `Editor.CreateEditor(EnvironmentRuleSet.asset)` resolves `EnvironmentRuleSetEditor`. It shows 3 rules, 24 neighbour buttons (4 Floor, 3 Void, 17 Any, matching the migrated data), 3 Rotated toggles and 3 Output fields, and the asset is not dirty afterwards. Named EditMode UI tests pass: `EachRuleShowsAColouredThreeByThreeGridTheOutputAndTheRotatedToggle`, `CyclingANeighbourGoesAnyFloorVoidMarksDirtyAndUndoes`, `RulesCanBeAddedReorderedAndRemovedWithUndo` and `StatusFollowsEditsMadeThroughTheBoundFields`. The existing capture `captures/inspector-rule-set.png` was reviewed: the green/red/grey cells match the rule data. The user has not yet accepted the look. |
| AC-4 | PASS | No `UnityEngine.Tilemaps`, `Tilemap.Extras` or `RuleTile` usage under `Assets` outside comments and the test's type-name check. `manifest.json` and `packages-lock.json` list only `com.unity.modules.tilemap` (`Client.List` in the live Editor also shows only that tilemap package). `UnityEngine.RuleTile, Unity.2D.Tilemap.Extras` does not resolve. The RuleTile files are absent on disk and not loadable. The prefab has one Grid (BoardRoot, cell 0.5, XZY) and no Tilemap. Compile is clean (`scriptCompilationFailed=False`). EditMode 57/57, PlayMode `ColorBlockJam.Environment.PlayModeTests` 7/7 (job `dc80876b18b74280a36c3a681ddec8eb`). The 0 B tests `RecomposingAPreparedBoardAllocatesZeroBytes` and `MatchingAPreparedMaskAllocatesZeroBytes` pass. The database GUID `2538a2c7…` and the prefab `.meta` are unchanged. |
| AC-5 | PASS | New `captures/verify-*` captures from the live Editor (LevelEditor preview levels 1-5, hole/notch fixture, Game level 1 opened additively and closed unsaved). All 7 placement signatures are byte-identical to `before-*.txt`, and all 7 PNGs are pixel-identical to `before-*.png` (PIL difference bounding box `None`). Game scene: 1 Grid, 0 Tilemaps. LevelEditor scene: 1 Grid, 0 Tilemaps. The console showed 0 new errors or warnings; the only entry was an older MCP TestJobManager notice. The Game scene was built in edit mode, not Play Mode. |

Files changed by this task: all inside the allowed paths. These are
`Environment/**` (including the new `Editor/` asmdef, inspector and USS),
`Data/Environment/EnvironmentRuleSet.asset` (new) and
`EnvironmentDatabase.asset`, `Prefabs/Environment/Environment.prefab`,
`Tests/Environment/**`, `Packages/manifest.json`, `Packages/packages-lock.json`,
`docs/Environment-Layout.md`, `PROJECT.md`, `README.md` and this task folder.
Removed: `EnvironmentRuleTile.cs`, `WallRuleTile.asset`, `FloorMarker.asset`
and the temporary equivalence test, each with its `.meta`.

- `ProjectSettings/ProjectSettings.asset` and `EntitiesClientSettings.asset`
  have newer timestamps, but their bytes are identical to cs:5 (`cmp`), so
  their content did not change.
- The manifest's `com.unity.addressables` addition and the whitespace change
  on the Onity line come from Unity re-serialising the file and from another
  task, not from this removal.
- Other files outside the allowed paths changed in the task window
  (BoardKit, Levels, Controls, Addressables) have timestamps from
  02:15-02:28. That is before this task's first capture at 02:34, so they
  belong to concurrent sessions.

Open items (not blocking):
- `docs/Color-Block-Jam-PRD.md` AC-3.14 still names RuleTile; this needs a
  separate docs task.
- The user has not yet accepted the inspector look.
- `EnvironmentView` also carries the FloorBase plane. It existed before this
  task (it appears in the `before-*` captures) and is outside this task's scope.

## Implementer disposition of the verification (2026-09-25)

- All five criteria are PASS; there is no FAIL or UNPROVEN to fix, so no code,
  test or asset changed after the verifier's run and its jobs
  (`15def59c…`, `dc80876b…`) remain the current-revision results.
- Out-of-scope entries are justified, not reverted:
  - `ProjectSettings/*.asset`: byte-identical to cs:5; nothing to revert.
  - `Packages/manifest.json` `com.unity.addressables` 1.22.3: owned by
    `TASK-20260924-210432886-2f31f26b-remote-level-json` (its contract pins
    that version); reverting it would break that task. This task's only
    package change is the `com.unity.2d.tilemap.extras` removal.
  - BoardKit, Levels, Controls, AddressableAssetsData and other files dated
    02:15-02:28: concurrent sessions; this task did not touch them.
- AC-3 stays demonstrable for user acceptance through
  `captures/inspector-rule-set.png` and the four named inspector tests.
- Follow-up (outside allowed paths): update `docs/Color-Block-Jam-PRD.md`
  AC-3.14 from RuleTile to `EnvironmentRuleSet`.

## Second independent verification (2026-09-25, verifier 2)

Verifier 2 ran in a separate session and changed no production code, tests,
assets, packages or scenes. It used the live Editor
`ColorBlockJamCase2026@99077e4df1ecfaa4` over MCP for Unity HTTP. The last
code edit was at 03:07, and this pass ran from 03:18 to 03:25, so it checks
the current revision. At the start, `LevelEditor.unity` was the only scene,
`dirty=False`, not playing, with a clean compile. At the end the scene was
still `dirty=False` and had not been saved. During the pass, someone else
changed the Editor selection and focus: first to none and the Level Editor
window, later to `Visual` and the Scene view. The verifier left that as it
was and did not restore its own starting snapshot over it.

| AC | Result | Channel and evidence |
|---|---|---|
| AC-1 | PASS | EditMode `EnvironmentRuleSetTableTests.RuleSetReproducesTheRecordedRuleTileMatchesForEveryPatternAndAngle` passed (job `b495c663589943f88eaca2fe7638f772`). The matcher source was checked against `RuleTile.cs` 3.1.3 in the global package cache (`%LOCALAPPDATA%/Unity/cache/packages/.../com.unity.2d.tilemap.extras@3.1.3`). `RuleMatches` and `GetRotatedPosition` use the same turns: 90 → (y,-x), 180 → (-x,-y), 270 → (-y,x). Rotated rules try angles 0-270 in steps of 90. The archived `EnvironmentRuleTile.RuleMatch` treats Floor as the marker and Void as null or not the marker, which matches the matcher's rule that an outside quadrant counts as void. Rerunning `verify-ruletile-table-check.py` on the archived `WallRuleTile.asset` gave 256/256 equal and 0 mismatches. The migrated asset (Floor=1, Void=2) has the same 3 rules as the archive (Floor=3, Void=4), with the same order, neighbour indices, outputs (`07a6d55b…` Corner_1, `735cc7cb…` Wall, `07a6d55b…`) and Rotated flags. |
| AC-2 | PASS | EditMode `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests` passed 57/57 (job `b495c663…`). This covers the rectangle, corridor, hole, L/corner notch, pinched, door, transpose and interior-wall fixtures, the reference-level tests and `GridMapsCellsAndQuadrantsToTheBoardLayoutPositions`. The live placement signatures in `captures/verify2-*.txt` (below) are byte-identical to `before-*.txt`. |
| AC-3 | PASS (the user has not yet accepted the look) | In the live Editor, `Editor.CreateEditor` resolves `EnvironmentRuleSetEditor`. It shows 3 rules and 24 neighbour cells (4 Floor, 3 Void, 17 Any), and all 24 cells have tooltips (for example "Upper-left neighbour: Any. Click to cycle Any → Floor → Void."). It also shows 3 Rotated toggles and 3 Output fields, and the asset is not dirty afterwards. The four UI tests passed in job `b495c663…`: `EachRuleShowsAColouredThreeByThreeGridTheOutputAndTheRotatedToggle`, `CyclingANeighbourGoesAnyFloorVoidMarksDirtyAndUndoes`, `RulesCanBeAddedReorderedAndRemovedWithUndo` and `StatusFollowsEditsMadeThroughTheBoundFields`. The capture is `captures/inspector-rule-set.png`. |
| AC-4 | PASS | A grep of `.cs` and `.asmdef` files under `Assets` finds no `UnityEngine.Tilemaps` or `Tilemap.Extras` reference. `RuleTile` appears only in comments and test names, and `Tilemap` only in the `PrefabHasOneGridOnBoardRootAndNoTilemap` type-name check. No `.asset`, `.prefab` or `.unity` file contains a Tilemap class (`!u!1839735485`) or a RuleTile. `Environment.prefab` has 1 Grid (on BoardRoot, cell 0.5, XZY) and 0 Tilemaps, and every scene file has 0 Grids and 0 Tilemaps. `Client.List` shows only `com.unity.modules.tilemap@1.0.0`. `manifest.json` and `packages-lock.json` contain no `com.unity.2d.tilemap*` entry. `cm cat` at cs:5 shows that cs:5 never listed the extras package, so it was a pending addition that is now reverted. The removed GUIDs (`8c8f76b4…` WallRuleTile, `7787b76e…` FloorMarker, `747b89f5…` EnvironmentRuleTile.cs, `9cf0eedc…` equivalence test) do not resolve to existing assets (`OnlyExistingAssets` gives an empty result and the load returns null), and `t:TileBase` finds 0 assets. `UnityEngine.RuleTile, Unity.2D.Tilemap.Extras` does not resolve. The compile is clean (`scriptCompilationFailed=False`). EditMode passed 57/57 (job `b495c663…`) and PlayMode `ColorBlockJam.Environment.PlayModeTests` passed 7/7 (job `abf87dd1fea74770af9f63ed7673176b`). Both 0 B tests passed: `RecomposingAPreparedBoardAllocatesZeroBytes` and `MatchingAPreparedMaskAllocatesZeroBytes`. The database is valid and its GUID `2538a2c7…` is unchanged. |
| AC-5 | PASS | New live captures are in `captures/verify2-*` (the previous verifier's capture script, run with phase `verify2`). They cover LevelEditor preview levels 1-5 (56, 65, 74, 74 and 72 pieces, 1 Grid each, preview restored to level-01), the hole/notch fixture in a temporary preview scene (104 pieces), and Game level 1 (56 pieces; the scene has 1 Grid and 0 Tilemaps; it was opened additively and closed without saving). All 7 placement signatures are byte-identical to `before-*.txt`, and all 7 PNGs are pixel-identical to `before-*.png` (the PIL difference bounding box is `None`). The console had 0 errors and 0 warnings after the captures and after both test runs. The Game scene was built in edit mode, not in Play Mode. |

Note: the first EditMode request (job `f203d44b…`) failed with "tests did not
start within timeout" before any test ran. The same request run again was job
`b495c663…`.

Files this task changed (Plastic `cm status` plus modification times after
02:30), all inside the allowed paths:
- `Assets/ColorBlockJam/Environment/**`: new `EnvironmentNeighbor`,
  `EnvironmentRuleSet` and `EnvironmentRuleMatcher`; the new `Editor/` asmdef,
  inspector, USS and AssemblyInfo; changes to `EnvironmentComposer`,
  `EnvironmentView`, `EnvironmentDatabase`, `EnvironmentPiecePlacement` and the
  asmdef; `EnvironmentRuleTile.cs` removed.
- `Assets/ColorBlockJam/Data/Environment/`: `EnvironmentRuleSet.asset` (new),
  `EnvironmentDatabase.asset`; `WallRuleTile.asset` and `FloorMarker.asset`
  removed.
- `Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab`.
- `Assets/ColorBlockJam/Tests/Environment/**`: both test asmdefs; the new
  RuleSet, RuleSetEditor and RuleSetTable tests; the changed Composer and
  ViewLifecycle tests.
- `Packages/manifest.json` and `Packages/packages-lock.json`.
- `docs/Environment-Layout.md`, `PROJECT.md` and `README.md`.
- This task folder.

Outside the allowed paths:
- Generated IDE files (`*.csproj`, `.sln`, `.vs/`) were regenerated by Unity.
  They are not project sources.
- `ProjectSettings/ProjectSettings.asset` (03:20) and
  `EntitiesClientSettings.asset` (03:18) were rewritten by Unity during this
  pass's test runs. Their bytes match cs:5 exactly (`cmp`), so no content
  changed.
- The `manifest.json` `com.unity.addressables` 1.22.3 entry belongs to
  `TASK-20260924-210432886-2f31f26b-remote-level-json`.
- `production/tasks/TASK-20260925-002022060-4c9dac46-block-runtime-objects/contract.md`
  (03:21) and the other pending changes in `cm status` (BoardKit, Board
  prefabs, AddressableAssetsData, Presentation asmdef) belong to concurrent
  sessions. This task changed none of them.

Still open, not blocking: the user has not accepted the inspector look (AC-3).
`docs/Color-Block-Jam-PRD.md` AC-3.14 still names RuleTile and needs a separate
docs task. Game level 1 has not been checked in Play Mode.
