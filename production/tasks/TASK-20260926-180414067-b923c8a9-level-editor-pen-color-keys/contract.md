# TASK-20260926-180414067-b923c8a9-level-editor-pen-color-keys — Level Editor: number keys pick colours while the Pen is active

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-180414067-b923c8a9-level-editor-pen-color-keys
- Supersedes: none
- Title: Level Editor: number keys pick colours while the Pen is active
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 plus the uncommitted Level Editor work of 2026-09-26, including TASK-20260926-145804549-da16f02f (colour order, second revision, awaiting the user's AC-8), TASK-20260926-170647550-7683988f (Effect Editor cards, awaiting the user's AC-6) and TASK-20260926-140520462-2196c1d1 (Pen edit and template edits, awaiting the user's V-1)
- Task kind: UI
- Area: Level Editor window keyboard shortcuts and Colors tooltips (Editor assembly `ColorBlockJam.Editor`)
- Workflow: direct

## Objective and baseline

- Observable objective: While the Pen tool is active, keys 1-9, 0 and - pick the colour whose Colors badge shows 1-9, 10 and 11, exactly as a click on that swatch does. Outside the Pen, 1, 2 and 3 keep picking Select, Pen and Eraser.
- Current behavior/evidence (`Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs`):
  - `OnKeyDown` → `RunShortcut(KeyCode, shift)`. Alpha1/Keypad1 picks Select, Alpha2/Keypad2 the Pen and Alpha3/Keypad3 the Eraser, whatever tool is active. `[` and `]` call `CycleColor`. No key picks a specific colour.
  - A swatch click runs `PickSwatch` → `PickColor(color)`. That sets the tool colour, recolours the block the Pen edits (or a block or gate the designer chose with Select) as one undo step, and does nothing more when the colour is unchanged.
  - Each swatch shows its colour-order position in a badge (`LevelColorSwatches.SetOrder`, from `LevelDraft.ColorOrder`). Swatch tooltips read "{Colour} ([ / ] cycles)".
  - User reference of 2026-09-26: the Colors group with badges 1-11 (6 + 5 rows), and "boyama modunda renk değiştirebiliriz tuşlara basınca".
- Expected behavior (user answer of 2026-09-26: "Pen only: 1-9, 0, -"):
  - While `m_toolState.Tool == LevelEditorTool.Pen` (any Pen use: plain Pen, editing a block, "+ New piece" freehand, Barrier brush):
    - Alpha1-Alpha9 and Keypad1-Keypad9 pick order positions 1-9. Alpha0 and Keypad0 pick position 10. Minus and KeypadMinus pick position 11.
    - The colour is the one whose badge shows that number: `ColorOrder.GetColorAt(position - 1)` of the current draft, or palette order when no draft is loaded. After Color Shuffle, key 1 picks the new first colour.
    - The effect equals a click on that swatch: the same `PickColor` path, tool colour and outline, one "Recolour Block" undo step for the block the Pen edits, and nothing when the colour does not change.
    - The key is consumed like the other shortcuts.
  - Shift is ignored for these keys, as it is for the tool keys today. Ctrl, Cmd and Alt combinations are not shortcuts, as today.
  - With Select, the Eraser or a rotate/mirror tool active, 1, 2 and 3 pick Select, Pen and Eraser as today. 4-9, 0 and - do nothing, as today.
  - From the Pen, Esc still returns to Select and ends a Pen edit; the Select and Eraser buttons still switch tools.
  - Tooltips name the keys:
    - the Pen tool tooltip and the Colors group tooltip say that with the Pen, 1-9, 0 and - pick the colour with that badge number;
    - each swatch tooltip names its Pen key for the current order (for example "Red: key 3 with the Pen ([ / ] cycles)") and follows every order change through `SetOrder`;
    - key labels and tooltip texts are prebuilt or built on order changes only, never per frame.
  - The key-to-position mapping is a small plain C# helper (for example `LevelColorKeys`), unit-tested without a window.
- Behavior that must remain unchanged:
  - 1/2/3 tool keys outside the Pen; Esc, `[` `]`, Home, arrows, Delete, R, F/Shift+F, Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z.
  - Keys typed into text or number fields (Level Name, Timer Seconds) are never shortcuts (`IsInTextField`).
  - Swatch click, hover and selection outline; Color Switch/Shuffle/Reset Order; the badges and the 40 px two-row layout.
  - `PickColor` rules: the automatic default selection is never recoloured, and a gate is recoloured only with Select after a click.
  - The Pen edit rules of TASK-20260926-140520462-2196c1d1 other than the key change the user chose: Esc, the Select button, the Eraser button, the transform tools, "+ New piece", the Barrier tile, a gate stroke and an empty-space click still end an edit.
  - Existing tests keep what they assert. A test that presses 1 or 3 while the Pen is active only to switch tools uses the equivalent tool button or Esc instead, because those keys now pick colours in the Pen.

## Ownership

- Owner/writer role: implement-task (single writer)
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorSwatches.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorKeys.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorKeys.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelColorKeysTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelColorKeysTests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelColorOrderWindowTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasBlockToolsTests.cs (only the steps that press a digit while the Pen is active, plus assertions on the changed shortcut)
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasGatesEdgesTests.cs (same limit)
  - Assets/ColorBlockJam/Tests/EditMode/LevelFreehandPieceTests.cs (same limit)
  - Assets/ColorBlockJam/Tests/EditMode/LevelObstacleBarrierTests.cs (same limit)
  - Assets/ColorBlockJam/Tests/EditMode/LevelObstaclesSectionTests.cs (same limit)
  - Assets/ColorBlockJam/Tests/EditMode/LevelPenEditTests.cs (same limit)
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260926-180414067-b923c8a9-level-editor-pen-color-keys/contract.md (managed lifecycle block only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditorToolState.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasElement.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectEditorWindow.cs
  - Any other existing test file, and any change to a test's asserted outcome other than the changed shortcut
  - Plastic checkin, shelve, branch, or undo of other sessions' pending changes
  - Changing the user's open Level Editor draft or level JSON
- Serialized assets: none (new `.cs.meta` files are Unity-generated script metadata owned by implement-task)
- Other active writers/worktrees checked (2026-09-26T18:04Z): no lifecycle lock is held. Session "Rotate tool rotation behavior" released the Level Editor paths ("LEVEL EDITOR PATHS FREE") and awaits its user's V-1. Its reviewed revision hashes the `Level*Tests.cs` glob, and it said later edits there are expected to mark it stale. It is told before the first write.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. AC-1: Given the Pen active and the default colour order, when the designer presses 1, 2 … 9, 0 or - (top row or keypad), then the tool colour becomes palette position 1 … 11, and its swatch is outlined, exactly as after a click on that swatch.
2. AC-2: Given a shuffled colour order, when the designer presses key k with the Pen, then the colour whose badge shows k is picked, not the palette colour at k.
3. AC-3: Given the Pen editing a block, when a colour key names another colour, then that block is recoloured as one "Recolour Block" undo step. The same colour records nothing. The plain Pen without an edit only changes the tool colour.
4. AC-4: Given Select, the Eraser or a transform tool, when 1, 2 or 3 is pressed, then Select, Pen or Eraser is picked as before, and 4-9, 0 or - do nothing. Given the Pen, Esc still returns to Select and ends an edit, and `[` `]` still cycle. Given focus in Level Name or Timer Seconds, digits and - are typed into the field and pick neither a colour nor a tool.
5. AC-5: Given the Level Editor, when the designer hovers the Pen tool, the Colors group or a swatch, then the tooltip names the colour keys. A swatch tooltip names its own key for the current order and follows an order change (Color Shuffle, Switch, Reset Order, undo).
6. AC-6: Given the Level EditMode suite, when it runs after the change, then it passes. The new tests cover AC-1 to AC-5, including keypad keys and a shuffled order. Tests that pressed 1 or 3 inside the Pen to switch tools use the equivalent button or Esc and still assert the same outcome.
7. AC-7: Given the live Level Editor, when the user paints with the Pen and presses the number keys, then they accept the behaviour (explicit user acceptance).

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User message of 2026-09-26 in session "Edit dropdown ve colors kareleri": "boyama modunda renk değiştirebiliriz tuşlara basınca", with a Colors screenshot (badges 1-11 in rows of 6 and 5).
  - User answer: "Pen only: 1-9, 0, - (Recommended)". While the Pen is active, 1-9 pick badges 1-9, 0 picks 10 and - picks 11; outside the Pen, 1/2/3 pick Select/Pen/Eraser; Esc returns to Select; `[` `]` still step through colours.
  - docs/Level-Editor-Design.md: section 2 (Keyboard line), "Colour order" and "Pen edit and template edits".
  - TASK-20260926-145804549-da16f02f (colour order and badges).
- Unity version/platform: Unity 2022.3.62f2, Windows Editor only (editor tooling, UI Toolkit)
- Package/architecture constraints:
  - UI Toolkit only. No new package, asmdef, Runtime or schema change.
  - Follow the window's code style: one-sentence class summaries, `m_`/`k_`/`s_` names, and owner/stop-point comments on any new subscription (none expected).
  - Keys are read from `KeyDownEvent.keyCode` like every other shortcut.
- Explicit out of scope:
  - Colour keys outside the Pen, key rebinding, and changes to the tool keys outside the Pen.
  - Info box texts other than Pen and Pen editing.
  - Effect Editor and runtime changes.

## Verification and risk

- Focused test path/command: UnityMCP `run_tests` EditMode, `ColorBlockJam.Tests.EditMode.LevelColorKeysTests`, `LevelColorOrderWindowTests` and every adapted test class, then the Level suite `ColorBlockJam[.]Tests[.]EditMode[.]Level`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: a clean compile after `refresh_unity` with no new Console errors, and the user's open Level Editor draft intact after the reload. Check `mcpforunity://editor/state` first: no compile and no other session's test run in progress.
- EditMode:
  - `LevelColorKeys` mapping: every key including the keypad, and an unmapped key.
  - The window: AC-1 to AC-5 through `SendKey` and `KeyDownEvent`, including a shuffled order, a Pen edit recolour with one undo step, text-field focus, the tool keys outside the Pen, and the tooltips.
- PlayMode: not applicable (editor tooling only)
- Manual/built-player/profiler/device evidence: the user's acceptance in the live Level Editor (AC-7)
- Risk: medium. It changes a shortcut that other tasks' tests pin (keys 1 and 3 inside the Pen), on the user's explicit choice. Editor UI only, with no Runtime or schema change.
- Approval already granted (exact target only): the user's answer of 2026-09-26 ("Pen only: 1-9, 0, -"), including that 1 and 3 pick colours rather than tools while the Pen is active
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: `/implement-task TASK-20260926-180414067-b923c8a9-level-editor-pen-color-keys`

## Stop conditions

- Another live writer holds LevelDatabaseWindow.cs, one of the listed test files or docs/Level-Editor-Design.md.
- Session "Rotate tool rotation behavior" objects to the listed edits of its tests.
- A test can only pass by changing an asserted outcome other than the chosen shortcut.
- A needed change falls in a forbidden path, for example LevelEditorToolState or LevelCanvasElement.

## Contract history (conditions agreed after authoring)

- 2026-09-26T18:15Z: session "Rotate tool rotation behavior" (owner of TASK-20260926-140520462-2196c1d1) had no objection to the listed test edits. It asked that a colour key during a Pen edit act like its swatch: it recolours only the edited block, as one "Recolour Block" step (pen-edit AC-2). That is AC-3 here. It also handed over the Pen and Pen-editing info-box texts, because the key behaviour belongs to this task. `LevelToolInfoBox.cs` (those two texts only) and `LevelToolInfoBoxTests.cs` (assertions on them) moved from forbidden to allowed, and AC-5 gained the info-box sentence.
- 2026-09-26T19:15Z: session "Level editörde görünürlük" removed the tool info box on its user's request: `LevelToolInfoBox.cs` and `LevelToolInfoBoxTests.cs` were deleted, and `LevelPenEditTests.NoToolInfoBoxSitsBetweenTheTopBarAndTheTemplateBanner` pins the removal. The previous revision's info-box sentence in AC-5 and those two allowed paths are withdrawn. The Pen tool, Colors group and swatch tooltips still name the colour keys.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:36e924853a8767abadedb125f05c300636ab2898a0ef1dd73b292001c1549fc5
- Evidence fingerprint: sha256:b5354adde4c60af6abf7e44841d6fc5ee2c1ba2e6ab621f89de0e5ef43be680a
- Reviewed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:671cf9e401f72f9aca8ef99a6daf3ae3059f5a913d7b601f1679323499f3138d
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:671cf9e401f72f9aca8ef99a6daf3ae3059f5a913d7b601f1679323499f3138d
- Closed at: 2026-09-27T17:24:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: LevelColorKeysTests mapping and default-order window tests, top row and keypad (job b8a14ed8, 50/50) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:671cf9e401f72f9aca8ef99a6daf3ae3059f5a913d7b601f1679323499f3138d |
| AC-2 | PASS | automated: LevelColorKeysTests non-default-order tests (SetNonDefaultOrder, since no button changes the order after the colour order task's seventh revision; Level suite job 135d995a); earlier the shuffled-order test (job b8a14ed8) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:671cf9e401f72f9aca8ef99a6daf3ae3059f5a913d7b601f1679323499f3138d |
| AC-3 | PASS | automated: LevelColorKeysTests Pen-edit recolour (one undo step, same colour no step, plain Pen tool colour only) (job b8a14ed8) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:671cf9e401f72f9aca8ef99a6daf3ae3059f5a913d7b601f1679323499f3138d |
| AC-4 | PASS | automated: LevelColorKeysTests boundary tests (tool keys outside the Pen, Shift, Ctrl/Cmd/Alt, [ ], Esc, Level Name and Timer focus) (job b8a14ed8) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:671cf9e401f72f9aca8ef99a6daf3ae3059f5a913d7b601f1679323499f3138d |
| AC-5 | PASS | automated: LevelColorKeysTests tooltip tests (Pen, Colors, swatches following Shuffle, Switch, undo and Reset; the info-box part was withdrawn with the box) (Level suite job 6253b42f) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:671cf9e401f72f9aca8ef99a6daf3ae3059f5a913d7b601f1679323499f3138d |
| AC-6 | PASS | automated: Level suite 723/723 (job 135d995a; earlier 690/690, job 6253b42f; earlier 650/650, job 12fdf01b); 18 adapted steps reviewed step by step (the test-integrity lens found every asserted outcome unchanged) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:671cf9e401f72f9aca8ef99a6daf3ae3059f5a913d7b601f1679323499f3138d |
| AC-7 | UNPROVEN | awaits explicit user acceptance in the live Level Editor | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored 2026-09-26 by session "Edit dropdown ve colors kareleri" from the user's request and answer; ready, with no execution dependency
- Started — implementation — 2026-09-26T18:10Z: the user's answer is the execution order ("tasklerini bitirene kadar devam et" stands). Session "Rotate tool rotation behavior" was told before the first write which of its tests change.
- Verification — 2026-09-26T19:05Z: one workflow (run wf_cfa851ed-74f) implemented the task and reviewed it through three read-only lenses: behaviour CLEAN, test integrity 1 low, scope/docs 2 low. A skeptic confirmed each finding (0 refuted), and a fixer corrected them: a stale assertion message, and key-2/Toolbox wording in the doc. Implementation: `LevelColorKeys` (pure mapping plus prebuilt labels); a Pen-first branch in `RunShortcut` through `PickColorAtPosition` → `PickColor`; Pen, Colors and swatch tooltips (121 prebuilt strings, set in `SetOrder`); 18 adapted test steps, with only a tooltip assertion changed to the new rule; docs. Contract revision: the Pen and Pen-editing info-box texts, handed over by their owner, now name the keys. Focused 50/50 (job b8a14ed8) and the Level suite 650/650 (job 12fdf01b) passed at the reviewed revision. The shared design doc is left out of the digest.
- Evidence stale — 2026-09-26T19:25Z: after this session released its lock, two newer sessions of the user changed owned files on their own user requests. Session "Shuffle ve switch değişiklikleri" put the swatches in their order positions and made [ / ] follow the order (colour order's third revision; LevelColorSwatches.cs, LevelDatabaseWindow.cs, LevelColorKeysTests.cs). Session "Level editörde görünürlük" removed the tool info box: LevelToolInfoBox.cs and LevelToolInfoBoxTests.cs were deleted, so AC-5's info-box sentence no longer applies. The owned digest is now different from the reviewed revision (db2f11a5… at 19:23Z, files still changing), so AC-1 to AC-6 are STALE until they are rerun at the new revision.
- Evidence refreshed — 2026-09-26T20:42Z: both sessions sent PATHS FREE. The contract records the user's removal of the info box: AC-5's info-box sentence and the two allowed paths are withdrawn, and the fingerprint was restamped from sha256:b42a40d3b02c…. The Level suite passed 690/690 (job 6253b42f), and no owned file changed after the run started. The reviewed revision moved to the new digest, which also leaves out the deleted info-box files. The key branch and the tooltips are unchanged from the reviewed code. With swatches now in badge order (colour order's third revision), key k picks the k-th grid swatch.
- Evidence refreshed — 2026-09-26T22:55Z: session "Shuffle ve switch değişiklikleri" finished the colour order task's sixth and seventh revisions. Shuffle and Switch no longer change the order, so the badges and Pen keys stay in palette order in normal use. It rewrote the order-dependent LevelColorKeysTests with a fixed non-default order, so key-follows-badge is still tested. The Level suite passed 723/723 (job 135d995a), with no owned file changed after the run started, and the reviewed revision moved to the new digest.
- 2026-09-27T17:24:19Z Closed by explicit user confirmation (task-done, user_confirmed): the user accepted the live result on 2026-09-27 ('bunlar okay', item 3, the Pen number keys; AC-7) and ordered 'tasklari kapat izin veriyorum'. Criteria rows are preserved as recorded, not re-verified; owned files may have changed after the reviewed revision through other sessions.
<!-- TASK-LIFECYCLE:END -->
