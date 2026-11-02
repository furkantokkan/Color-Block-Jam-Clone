# V6 verification (Claude session [7336b1], Opus 5.5, 2026-09-27)

All work ran in isolated batchmode copies (`%TEMP%/cbj-envgate-7336b1` = V5 then V6, `%TEMP%/cbj-envgate-7336b1-base` = unpatched production at Plastic cs:13 + pending workspace changes). The shared workspace, its live Editor (Hub, PID 45732) and Booster's copy were not touched.

## V5 result (first Unity run of the reviewed patch)

- Environment EditMode 88/91, Environment PlayMode 23/24, Gameplay PlayMode 55/56; baseline Environment EditMode 76/76 and PlayMode 14/14.
- Failures: two out-of-scope test couplings (`EnvironmentBarrierComposerTests` compares barriers with composer walls; `EnvironmentSolidViewTests` counts every BlockFill), one stale count and one missing epsilon in `EnvironmentComposerTests`, and a test harness bug in `BoardPresenterGateTests` (second press without ending the first).
- Independent review (`v5-envgate-code-review` workflow): V5's 4x4 strip bricks break the Level Editor 2D canvas (`LevelCanvasWalls` draws one glyph per composer wall placement), and the recompose allocation assertion used `GC.GetAllocatedBytesForCurrentThread`, which reads 0 in Unity 2022.3 Mono.
- Captures (`captures-v5` in the temp results folder): separate units, but every brick reads as a recessed tray, not the reference's rounded brick. Not selected.

## V6 design (selected under the user's Blender fallback: "eğer hala gerekli olursa blenderdan edit yapma hakkın var")

- `EnvironmentComposer.Compose(layout, output)` is byte-for-byte the baseline rule placement (canvas, barrier oracle and count tests unchanged).
- `ComposePlayArea(layout, output, gates)` (only `EnvironmentView`) closes every maximal straight wall run with `Brick_End` caps (mouth into the run, rule `Wall` between), a one-quadrant run with two half caps; corners keep their rule piece; one `EnvironmentGateSpan` per door. `EnvironmentSolidComposer` is unchanged.
- `Brick_End`: latest approved generator (`implementation/approved-brick/build_brick_end.py`, rectangular end after user-1, rerun with Blender 5.2 into `out-v6`, round trip OK, End/Wall step 0.0001). `ArtSource/BoardKit/WallBrick.blend` + `export_wall_brick.py` export it with the kit's FBX UnitScaleFactor 100 (the first export used 1.0 and imported 100x small; the Unity helper's bounds guard caught it).
- `EnvironmentWallBrickAssets` (menu *Color Block Jam > Art > Build Wall Brick*) copies WallAndDoor's importer settings (materials/animation/cameras/lights/Read-Write off), requires Brick_End bounds == Wall bounds, saves `Brick_End.prefab` from `Wall.prefab` contents with only the mesh replaced, and binds `EnvironmentDatabase.m_brickEndPiece` (the only asset diff: one field).
- Gate motion is V2/V5 unchanged; `BoardPresenterGateTests` staggered test now ends the first press (`Drag.TryEnd`).

## V6 results

| Channel | Result |
|---|---|
| All 7 EditMode assemblies | 1482/1483 (baseline 1473/1473). New: wall-run tests, play-area gate spans with Recorder-based `Is.Not.AllocatingGCMemory` recompose. One V6 failure: `PreviewArrowMarkerTests.RebuildsReturnEveryMarkerAndReuseThePoolsAndDisposeLeavesNone` (394 vs 398): switching preview levels whose doors split runs differently grows the Wall pool by two pieces once, on Home. File is outside this task's paths. |
| All 4 PlayMode assemblies | 132/135 (baseline 112/115): 2 Hub capture tests skipped by design; `BoardArtContainerTests` "2 audio listeners" fails identically in baseline (pre-existing). All gate, lifecycle, fragment, Game-scene drag/Won tests pass. |
| Captures (real Game camera, 1080x1920 and 1080x2400) | `captures-v6/`, `captures-baseline/`, sheets `v6-walls-before-after.png`, `v6-gate-sequence.png`, `v6-interior-hole-question.png`. |
| Gate telemetry | Lean: sink 0 to 0.68 over 5 frames before crossing; exit holds 0.68; rise 0.15 s after the body is gone (seq-047 body gone, gate down); settled pixel-identical to idle. Flick from a raised gate: block waits flush for the 4-frame sink, then crosses. Level 5: door 0 down, door 1 up. Won Finish 0.49 s (<= 0.8). |
| Independent review (`v6-envgate-review` workflow) | Visual AC-1, AC-2/3 and defect lenses: PASS_WITH_NOTES. Code: 2 confirmed (the preview pool coupling above; PROJECT.md still lacks DEC-138 and says "use only these original meshes"). Notes: interior holes and one-cell protrusions render as several units while project reference 02/17 shows interior walls as one continuous frame; Corner_1's flat arm face shows beside a lowered gate (also V5, prefab forbidden); background pinholes at inner floor corners and 1 px lines under some gate fronts exist in V5/baseline too. |

Patch `environment-gate-v6.patch` (text files, SHA-256 in chat record) applies to current production; `staging-v6/` holds all 25 files including binary assets and generated `.meta` GUIDs (`staging-v6-sha256.txt`).

## User decisions and final state (2026-09-27)

- Answers: interior holes "Outer ring only"; preview test "Approve test fix"; integration "Everything now"; visual "Accept both" (AC-1 walls, AC-2 gate motion).
- Mid-turn user notes: the Level Editor 2D canvas may look as before and need not show bricks, possibly through a second editor-only rule set. No change was needed: the canvas draws `Compose`, the unchanged rule placements, and only `ComposePlayArea` bricks the play area, so no second rule set was added.
- Final V6 composer: a flood fill from the ring marks the outer boundary; only its straight runs are bricked, and interior holes keep one continuous rule frame (`AnInteriorHoleKeepsOneContinuousRuleFrameWhileANotchIsBricked`). The preview test takes its pool baseline after one warm-up cycle.
- DEC number: DEC-138 was reserved by the pre-level task, so this decision is DEC-139 (code comments, docs, PROJECT.md row).
- Final copy: EditMode 1484/1484 (`v6c-all-editmode.xml`), PlayMode 132/135 (`v6c-all-playmode.xml`; the pre-existing baseline failure and 2 designed skips), captures `captures-final/`.
- Integrated into the shared workspace at about 16:18Z with `integrate_v6.py`. The drift guard found no foreign edit, and all 29 written files are byte-identical to the verified copy. The Booster session got a merge heads-up listing the exact shared files it also claims.
- Proof run on a fresh copy of the integrated shared workspace, with the 32 owned files byte-identical: EditMode 1497/1497 (`int-all-editmode.xml`, which includes other sessions' newer tests), PlayMode 132/135 (`int-all-playmode.xml`, same pre-existing failure only).
- After integration, another session appended its DEC-138 row to PROJECT.md (doc-only). The reviewed revision includes it.
- `staging-v6/` (28 files, `staging-v6-sha256.txt`) and `environment-gate-v6.patch` hold the final bytes.

## Decisions asked before integration (answered above)

1. Interior holes: brick them (current V6, literal AC-1) or keep them as one continuous frame like references 02/17 and barriers.
2. Test-only contract amendment for `Tests/LevelPreview/PreviewArrowMarkerTests.cs` (measure pool objects after one warm-up cycle).
3. Integration order with the live Booster claim (Gameplay/Board, Tests/Gameplay/PlayMode, PROJECT.md, PRD) and the Hub-owned live Editor.
4. User visual acceptance of AC-1/AC-2 from the captures.

Not run: Android/iOS builds, device Profiler/GC, live-Editor capture in the shared workspace.
