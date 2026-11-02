# TASK-20260925-025854248-9383f38c-held-outline-exit-fragments — Held-block white outline

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-025854248-9383f38c-held-outline-exit-fragments
- Supersedes: none
- Title: Held-block white outline
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:14
- Task kind: feature
- Area: Gameplay presentation (held block outline)
- Workflow: direct

## Objective and baseline

- Observable objective: While a pointer holds a block, one continuous white outline surrounds the block's whole footprint silhouette, with no seams between its modular tiles, and it disappears when the hold ends.
- Current behavior/evidence:
  - User report 2026-09-28: "taşıdığım objede beyaz outline olmuyor bunu ekle örnek olarak 2. resim mevcut" ("the block I carry gets no white outline; add it; image 2 is the example"). Image 1, our Game scene: a held cyan 4x4 block with no outline. Image 2, the reference game: a held purple 2x4 block with a white band around its whole footprint, close to the block top, drawn over the neighbouring walls and blocks.
  - `BoardPresenter.SetHeld`/`SnapHeld` (Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs) track the held block and follow it, but draw nothing for the held state.
  - Original reference study (2026-09-25): the CBJ Template's `Block.shader` inverted-hull `Outline` pass is seam-free only because each template block is one mesh. Our blocks are composed from modular Center/Edge/Corner tiles, so a per-tile hull would show seams (DEC-96 point 5).
  - The exit-fragments half of the original draft was delivered by TASK-20260927-010019989-11b501c1-gate-exit-fragments (DEC-131) and is removed from this contract.
- Expected behavior:
  1. **Outline geometry.** A plain C# tracer (`Gameplay/Board/Outline/FootprintOutline.cs`, no Unity types) returns the outer boundary loop(s) of a set of cells in cell units. It handles concave, single-cell and holed footprints and emits no edge between two cells of the same block. It writes into caller-owned reusable buffers.
  2. **Outline mesh.** `Gameplay/Board/Outline/BlockOutlineMesh.cs` turns a loop into a thin ribbon just outside the footprint, at block-top height, with rounded or mitred convex and concave corners. It fills one reused `Mesh` through pre-sized vertex and index lists. The mesh follows the block's current cells, so a Rocket remainder gets a matching outline. Rebuilding happens on press (and on a footprint change of the held block), never per frame. The buffers are sized on Home for the level's largest reachable footprint.
  3. **Outline view.** `Gameplay/Board/Outline/HeldOutlineView.cs` owns one MeshRenderer with `MAT_BlockOutline`, created on the first build under the Blocks root and destroyed on Dispose.
     - It uses the new `BlockOutline.shader`: unlit, white by default, URP 14, and no shadow-caster or depth-only pass. It draws after opaque geometry with ZTest set so the band reads over neighbouring walls and blocks, as in reference image 2.
     - `SetHeld(block)` shows it parented to the held root. Release, cancel, Suspend (`SnapHeld`), Teardown, `ReturnPieces`, an exit start and Dispose hide it. A pooled root never carries a stale outline.
     - It creates no material instance and changes no keyword or pass on a shared material; any colour goes through one cached `MaterialPropertyBlock`.
  4. **Composition.** `GameSceneInstaller` serializes the outline material and passes it to the presenter or view. `Game.unity` sets only that reference.
- Behavior that must remain unchanged:
  - Drag, exit, pick and session rules and their tests; DEC-112 follow lead and DEC-139 gate motion.
  - Exit fragments (DEC-131) and the Ice overlay.
  - `BoardLit.shader`, `MAT_Gameplay.mat`, piece prefabs, `ModularBlockView`, `BlockPiece`, `BlockPieceFactory`.
  - 0 B GC Alloc in the Playing tick, and no Instantiate or Destroy during an attempt.

## Ownership

- Owner/writer role: gameplay presentation implementer (sequential after the predecessors below), independent verifier afterwards
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Gameplay/Board/Outline/**
  - Assets/ColorBlockJam/Gameplay/Board/Outline.meta
  - Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs
  - Assets/ColorBlockJam/Gameplay/Composition/GameSceneInstaller.cs
  - Assets/ColorBlockJam/Art/Shaders/Gameplay/**
  - Assets/ColorBlockJam/Art/Shaders/Gameplay.meta
  - Assets/ColorBlockJam/Art/Materials/Gameplay/**
  - Assets/ColorBlockJam/Art/Materials/Gameplay.meta
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/FootprintOutlineTests.cs
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/FootprintOutlineTests.cs.meta
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/HeldOutlineTests.cs
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/HeldOutlineTests.cs.meta
  - Assets/ColorBlockJam/Scenes/Game.unity
  - Assets/HighlightPlus/** (vendor import, Highlight Plus 2 v32.6.1 URP runtime and editor, user approval 2026-09-28)
  - Assets/HighlightPlus.meta
  - Assets/ColorBlockJam/Settings/URP-Balanced-Renderer.asset
  - Assets/ColorBlockJam/Settings/URP-HighFidelity-Renderer.asset
  - Assets/ColorBlockJam/Settings/URP-Performant-Renderer.asset
  - Assets/ColorBlockJam/Gameplay/ColorBlockJam.Gameplay.asmdef
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/ColorBlockJam.Gameplay.PlayModeTests.asmdef
  - docs/Gameplay-Drag-And-Gate.md
  - PROJECT.md
  - production/tasks/TASK-20260925-025854248-9383f38c-held-outline-exit-fragments/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Settings/** except the three URP renderer assets above (their only intended change is the Highlight Plus render feature)
  - Assets/ColorBlockJam/Art/Shaders/Board/**
  - Assets/ColorBlockJam/Art/Materials/Shared/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Gameplay/Session/IBoardPresenter.cs (the existing SetHeld/SnapHeld/ReturnPieces seams suffice)
  - Runtime material instances (`.material`), keyword or pass toggles on shared materials
  - Plastic shelve or publishing; checkins other than the one slice checkin the user ordered on 2026-09-28
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Art/Shaders/Gameplay/BlockOutline.shader and Assets/ColorBlockJam/Art/Materials/Gameplay/MAT_BlockOutline.mat (removed by the Highlight Plus pivot, with their folders)
    Sole writer: gameplay presentation implementer
    Matching `.meta` owner: gameplay presentation implementer
  - Asset: Assets/ColorBlockJam/Settings/URP-{Balanced,HighFidelity,Performant}-Renderer.asset (HighlightPlusRenderPassFeature added; Unity's URP 14 reserialization of the same files is accepted)
    Sole writer: gameplay presentation implementer through Unity APIs
    Matching `.meta` owner: unchanged
  - Asset: Assets/HighlightPlus/** (vendor files with their original GUIDs, plus new HighlightPlus.Runtime and HighlightPlus.Editor asmdefs)
    Sole writer: gameplay presentation implementer
    Matching `.meta` owner: gameplay presentation implementer
  - Asset: Assets/ColorBlockJam/Scenes/Game.unity (removal of the GameSceneInstaller outline-material reference only)
    Sole writer: gameplay presentation implementer through the live Editor (unityMCP)
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked (2026-09-28): TASK-20260927-123500933-c1e67965-environment-runs-gate-motion (reopened, implementation) owns `BoardPresenter.cs`. TASK-20260927-004448215-54655902-booster-gameplay-flow (in_progress, lifecycle lock held by Claude session [f1ed7e]) owns `Gameplay/Board/*.cs`, `Gameplay/Session/*.cs`, `Domain/Board/*.cs`, `GameSceneInstaller.cs` and the Vacuum selected-silhouette presentation. TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay also waits on both. This task's new files live in the disjoint `Gameplay/Board/Outline/**` folder; only the `BoardPresenter.cs`, `GameSceneInstaller.cs` and `Game.unity` edits wait for the owners.
- Execution dependency: TASK-20260927-123500933-c1e67965-environment-runs-gate-motion releases `BoardPresenter.cs` AND TASK-20260927-004448215-54655902-booster-gameplay-flow releases its lifecycle lock on `Gameplay/Board/*.cs` and `GameSceneInstaller.cs` (stable handoff or closure of each). Revalidate the `SetHeld`/`SnapHeld`/`ReturnPieces` seams and any Vacuum silhouette the booster task added before starting.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a held I, L, T, U, 2x2, 4x4 or single-cell block, when the pointer holds it, then one continuous white outline surrounds its whole footprint, with no line between internal tiles and none inside an L's notch, and it moves with the block through the drag follow lead (AC-3.17, DEC-89).
2. Given a held block, when the hold ends by release, cancel, Pause, Restart, Home, exit start or Teardown, then the outline is hidden, and the next press on any block, including a re-pooled root, shows only that block's outline.
3. Given a block that Rocket reduced to a remainder, when it is held, then the outline follows the remaining cells.
4. Given the Playing tick with a held block, then GC Alloc is 0 B in steady state, no material is instantiated, and no Instantiate or Destroy occurs during the attempt (NFR-P2, NFR-3). The press-time mesh rebuild stays within the Home-sized buffers.
5. Given the Game scene at 1080x1920 and 20:9, when a block is held, then a capture shows the white outline close to reference image 2 (user's 2026-09-28 screenshot) and reference 14, accepted by the user.

## References and constraints

- Story/GDD/ADR/design/log/image: PRD AC-3.17; DEC-89, DEC-96 point 5, DEC-117, DEC-131; `docs/Color-Block-Jam-Visual-References.md` reference 14; user screenshots of 2026-09-28 (ours without outline, reference with outline); `docs/Rendering-Reference-Review.md`; CBJ Template `Block.shader`/`UniversalOutlinePass.hlsl` as technique references (not copied).
- Unity version/platform: 2022.3.62f2, URP 14; Android/iOS portrait
- Package/architecture constraints: one shared outline material, no material instance, colour through an MPB when needed; `BoardLit`/`MAT_Gameplay` read-only; ZLinq/ZString rules of PROJECT.md; one tick owner (the view has no Update); after the 2026-09-28 pivot the outline is a Highlight Plus `HighlightEffect` per pooled block root, owned by the presenter and switched off by SetHeld(-1), SnapHeld, an exit start, ReturnPieces and ReturnPiece; its `HighlightPlusRenderPassFeature` on the three URP renderers is approved. The user's later request adds the same outline to Vacuum targets until their piece is gone.
- Explicit out of scope: exit fragments (done, DEC-131), Ice overlay, Hammer/Rocket target silhouettes, result capture, a block tint/brighten on hold.

## Verification and risk

- Focused test path/command: PlayMode `Tests/Gameplay/PlayMode/HeldOutlineTests.cs` (one white Children outline per root, off at start; only the pressed block outlined; release, Pause, Restart and Home clear it) plus the Gameplay EditMode/PlayMode suites, including `BoardPresenterBoosterAllocationTests`. `FootprintOutlineTests.cs` was removed with the ribbon. Run once in the final stage per the Unity test timing rule.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn compile check on every change; clean Console; Unity's own compile of the HighlightPlus asmdefs.
- EditMode: acceptance 1 (geometry part)
- PlayMode: acceptance 1–4
- Manual/built-player/profiler/device evidence: acceptance 5 by capture plus user acceptance; Profiler GC Alloc note for acceptance 4; Frame Debugger note of the outline's draw call.
- Risk: high (new serialized shader and material; Game.unity reference)
- Approval already granted (exact target only): user approval 2026-09-28 ("Approve (Recommended)") for the footprint-perimeter ribbon technique, the new `Art/Shaders/Gameplay/BlockOutline.shader`, the new `Art/Materials/Gameplay/MAT_BlockOutline.mat`, and the `GameSceneInstaller` outline-material reference in `Game.unity`; then user approval 2026-09-28 ("Approve, apply all") to delete that system and import Highlight Plus 2 under `Assets/HighlightPlus` with new asmdefs, reference it from the Gameplay and Gameplay PlayMode test asmdefs, add its render feature to the three URP renderers and give each block root a `HighlightEffect`; and the user's request to outline Vacuum targets until their piece is gone.
- Commit/checkin constraint: one Plastic checkin of this slice with an explicit file list, ordered by the user on 2026-09-28 ("commit atabilirsin burayı"); no push or publishing
- Current invocation permission: execution authorized by the user on 2026-09-28, queued behind the execution dependency
- Handoff destination: /agent-orchestration TASK-20260927-123500933-c1e67965-environment-runs-gate-motion TASK-20260927-004448215-54655902-booster-gameplay-flow TASK-20260925-025854248-9383f38c-held-outline-exit-fragments, then /implement-task TASK-20260925-025854248-9383f38c-held-outline-exit-fragments

## Stop conditions

- The outline cannot be made seam-free without editing board-kit, Presentation or Shared material files.
- The outline needs a material instance or a BoardLit pass (the Highlight Plus renderer feature is approved).
- A predecessor changes the `SetHeld`/`SnapHeld`/`ReturnPieces` seams, or already ships a white block silhouette this task should reuse instead: revalidate the contract before writing.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:6bfd4bf3a4a43751382bdd90d27a12bf96e0b36377f2c193f8d27138f94ce59c
- Evidence fingerprint: sha256:ebddb5422a7645f76deaf31d071af544e3691a02beaf60dfe9552bcdcafe483f
- Reviewed revision: vcs:cs:16+owned-sha256:c400383facfc726ddd0188036978b22dbd9346929f0fbac449636e21f071fd41
- Attempt count: 3
- Last feedback: 2026-09-28T07:30Z user: "çektiği blocklar da beyaz outline açılmalı" (D-001). Earlier: 2026-09-28 user: outline Vacuum targets until their piece is gone; then "commit atabilirsin burayı"
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:16+owned-sha256:c400383facfc726ddd0188036978b22dbd9346929f0fbac449636e21f071fd41
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Seam-free outline around the whole held footprint | PASS | automated: PlayMode HeldOutlineTests.Prepare_ConfiguresOneWhiteChildrenOutlineAndStartsOff (one Children-group effect, so all tiles form one screen-space silhouette with no inner seams) and GameScene_PressHighlightsOnlyThePressedBlockAndReleaseClearsIt; Gameplay PlayMode 125/125 on the mirrored live bytes | vcs:cs:16+owned-sha256:c400383facfc726ddd0188036978b22dbd9346929f0fbac449636e21f071fd41 |
| AC-2 Hidden on every end path; no stale outline on pooled roots | PASS | automated: HeldOutlineTests (release, Pause, Restart, Home in the Game scene; only block roots carry the effect); code path: SnapHeld, StartExit, ReturnPieces and ReturnPiece switch it off | vcs:cs:16+owned-sha256:c400383facfc726ddd0188036978b22dbd9346929f0fbac449636e21f071fd41 |
| AC-3 Outline follows a Rocket remainder | UNPROVEN | code path only: Recompose and a split part reset the root's effect, so the next switch-on re-reads the current tiles; BoardPresenterBoosterAllocationTests 11/11 exercise the Rocket split and recompose but do not observe the outline; needs a manual hold of a Rocket-cut block or the user's acceptance | vcs:cs:16+owned-sha256:c400383facfc726ddd0188036978b22dbd9346929f0fbac449636e21f071fd41 |
| AC-4 Zero allocation, no material instance, no Instantiate/Destroy | UNPROVEN | automated: Gameplay PlayMode 125/125 including BoardPresenterBoosterAllocationTests (0 B on the Rocket split after the lazy prepare); deviation: Highlight Plus creates its own per-effect material instances when a root is prepared (Home) and when a Rocket split part is first outlined (Playing); needs the user's acceptance of the vendor behaviour or a Profiler check | vcs:cs:16+owned-sha256:c400383facfc726ddd0188036978b22dbd9346929f0fbac449636e21f071fd41 |
| AC-5 Visual match with the 2026-09-28 reference and reference 14 | PASS | explicit user acceptance 2026-09-28 ("düzgün duruyor highlight beğendim") on the Highlight Plus captures | vcs:cs:16+owned-sha256:c400383facfc726ddd0188036978b22dbd9346929f0fbac449636e21f071fd41 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | ac_failure:vacuum-target-outline:pulled-blocks-show-no-visible-outline | VERIFIED | AC_FAILURE | Vacuum-target outline (user-added requirement) | Pulled blocks do not read as outlined: they shrank to a few pixels over the board (shared InQuad), taking the outline with them | fixed by Booster D-004 (InExpo scale); isolated copy 25/25 with both targets asserted outlined; Game-scene capture shows the outline to the nozzle | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:fd340842892c2dba66c17c4654d8730011f78fbb671c8724417a8ded0625ee0a |

### Transition history

- Created — planning — draft from the user's full-shader choice and the reference study; waits for target approval and the drag task's handoff
- 2026-09-28 — planning — ready: user report of the missing outline and approval of the ribbon technique, shader, material and Game.unity reference; exit-fragments half removed (delivered by DEC-131); queued behind the BoardPresenter and GameSceneInstaller owners
- 2026-09-28 — implementation — in_progress on the user's "devam et": disjoint Outline files, shader, material and tests built in an isolated copy; integration waits for the owners
- 2026-09-28 — implementation — staged: silhouette band (lower band where a side faces the camera, upper band at the vertical-side top where it faces away, tube in the turn zone; ZTest Always, drawn after transparents) replaces the first floor-ring/fence hull, which walls and neighbours hid; copy-only integration of BoardPresenter/GameSceneInstaller/Game.unity verified (Gameplay EditMode 325/325, PlayMode 65/65, shader compiles for Vulkan/GLES3/Metal); outputs in implementation/staging, integration.patch and evidence/; lock released until the owners release their paths
- 2026-09-28 — verification — user review: look accepted, overlap onto neighbouring blocks to be fixed, then integrate. Fix: ZTest LEqual (neighbours and walls in front hide the outline), lower band at 0.01. This deviates from Expected behavior 2-3's wording ("at block-top height", "reads over neighbouring walls and blocks"): the mesh has a floor band and a band at the vertical-side top (0.40) and the shader tests depth, by the user's review; recorded as DEC-145. Integrated into the shared workspace at the user's order while Booster [f1ed7e] still owns BoardPresenter/GameSceneInstaller/Game.unity (gate-motion had closed); Booster notified to three-way merge (implementation/integration.md). Verified on a mirror of the shared workspace: Gameplay EditMode 325/325, PlayMode 65/65; live Roslyn compile OK; independent review PASS WITH NOTES (stale Expected-behavior wording; harmless double Dispose). Staging removed. Awaiting AC-5 confirmation.
- 2026-09-28 — verification — second user review: the depth-tested outline hid under the wall and the neighbouring blocks; it must neither hide under nor paint over them. Block bases reach the cell edge, so a side touching a wall, gate, void or block (and its corners) now gets a seam line across the bevel groove (±0.05 at 0.47; the flat top starts 0.052 in) instead of the band, with a post where a side turns; `IOutlineNeighbourhood` over the preview board, rebuilt on every cell step. Integrated at the user's order ("gameplay entegre et"); Booster's concurrent merge kept both hunk sets. Verified: EditMode FootprintOutlineTests 20/20, PlayMode HeldOutlineTests + GameSceneDragTests 10/10, live Roslyn compile OK with dependents. Awaiting AC-5 confirmation.
- 2026-09-28 — verification — third user review: a visible step where a free side's floor band met the seam. The posts are replaced by ramps: each path point has a blend (0 band, 1 seam) and the band rises into the seam over 0.3 cell of the free side, before a blocked corner and before a blocked cell boundary. Written at the current paths after telling the folder-restructure session; Booster's merged BoardPresenter.cs keeps every outline hunk; ColorBlockJam.Gameplay compiles in the workspace (its EditMode test assembly is broken by Booster's in-flight test files, not by this task). Verified in the copy: EditMode FootprintOutlineTests 20/20, PlayMode HeldOutlineTests + GameSceneDragTests 10/10. Awaiting AC-5 confirmation.
- 2026-09-28 — implementation — user pivot: remove our outline system and use the Highlight Plus 2 asset for the same look. Approved plan: import the URP package (runtime + editor, no demo/docs) under Assets/HighlightPlus with new HighlightPlus.Runtime/Editor asmdefs; reference it from the Gameplay and Gameplay PlayMode test asmdefs; add HighlightPlusRenderPassFeature to the three URP renderers; delete our Outline mesh/view/tracer, shader, material and tracer tests; BoardPresenter prepares a white, Highest-quality, AlwaysOnTop, Children HighlightEffect per root on Home (HeldBlockHighlight) and toggles it at the old hold end points. Verified in the copy: Gameplay PlayMode 59/59, EditMode 309/309, captures. Supersedes the contract's ribbon technique by the user's order. Booster carries the ramp version in its checkin; this replacement lands after it.
- 2026-09-28 — implementation — the user accepted the Highlight Plus look ("düzgün duruyor highlight beğendim"). AC-5 becomes PASS by explicit user acceptance at the revision where the replacement lands in the shared workspace; the apply still waits for the Booster checkin.
- 2026-09-28 — verification — applied in the shared workspace on top of the Booster checkin cs:16 and checked in on the user's order ("commit atabilirsin burayı"): Highlight Plus 2 under Assets/HighlightPlus, HighlightPlus.Runtime referenced by the Gameplay and Gameplay PlayMode test asmdefs, the render feature on the three URP renderers, one HighlightEffect per root (HeldBlockHighlight), our ribbon, shader, material and FootprintOutlineTests removed; the user's added request outlines Vacuum targets until their piece is gone. The authority was revised to the user-approved pivot targets and the checkin order. Rocket split parts prepare lazily (an eager prepare failed BoardPresenterBoosterAllocationTests). Gameplay PlayMode 125/125, EditMode 521/522 (the one failure is pre-existing at cs:16, owned by the Booster task). AC-3 and AC-4 remain UNPROVEN: no test observes a Rocket remainder's outline, and Highlight Plus instantiates its own materials per effect.
- 2026-09-28T08:35Z — defect recorded and verified — in_progress — task-bug routed the user's "çektiği blocklar da beyaz outline açılmalı". NEW_DEFECT D-001: the outline was on, but the pulled pieces shrank away over the board; fixed by Booster D-004 in BoardPresenter's Vacuum scale ease (Booster ownership), verified in an isolated copy (25/25) and a Game-scene capture. No change to this task's Outline files; attempt count unchanged. AC-3 and AC-4 remain awaiting the user. Lock taken and released by Claude session [a36df4].
- 2026-09-28T08:52Z — D-001 revision refreshed — in_progress — the Hammer session's final cast-anchor bytes keep the Vacuum outline and scale hunks; its windowed 59/59 copy run covers the both-targets-outlined assert. Verified revision moved to owned-sha256:fd340842…ee0a.
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
