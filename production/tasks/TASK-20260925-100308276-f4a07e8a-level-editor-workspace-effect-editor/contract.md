# TASK-20260925-100308276-f4a07e8a-level-editor-workspace-effect-editor — Level Editor workspace launcher and an Effect Editor window for the clicked block or gate

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-100308276-f4a07e8a-level-editor-workspace-effect-editor
- Supersedes: none
- Title: Level Editor workspace launcher and an Effect Editor window for the clicked block or gate
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level authoring (Level Editor workspace, block and gate property editing)
- Workflow: direct

## Objective and baseline

- Observable objective:
  - One menu command, Color Block Jam/Level Editor, switches to the LevelEditor scene and opens (or focuses) three windows: Piece Editor, Level Editor and Effect Editor.
  - The Effect Editor shows and edits the properties of the block or gate clicked on the Level Editor canvas through the same undoable draft.
- Current behavior/evidence:
  - The user's requests of 2026-09-25 were relayed by session "Ruled tile level sistem", which handed them to this session:
    - "blocka tıkladığımızda farklı bir editör penceresinde propertyler çıkartmak istiyorum … tıkladığım block propertysini ya da gate propertysini editleyebilirim". When a block is clicked, its properties appear in a different editor window, where the clicked block's or gate's properties can be edited.
    - "tools'dan level editöre basınca … level editör sahnesine geçeceğiz ve 3 adet window açılacak eğer kapalıysa ya da focus değiştirecek: piece editor, level editor, effect editor (block, gate gibi özel etki alanlar için)". Pressing Level Editor switches to the LevelEditor scene. Three windows open if closed, or take focus if open: Piece Editor, Level Editor, and Effect Editor for special effect areas such as blocks and gates.
  - `LevelDatabaseWindow.Open()` (`[MenuItem("Color Block Jam/Level Editor")]`) only opens the window.
  - `PieceEditorWindow.Open()` (`Editor/BoardKit`, board-kit owner) is public and opens or focuses the Piece Editor.
  - `LevelPreviewOwner` (`Editor/LevelPreview`, owned by "Ruled tile level sistem") has a menu item "Color Block Jam/Open Level Editor Scene" and shows the last published level on `sceneOpened`. Its owner removes that item once this launcher lands.
  - Canvas selection of a block or a door already exists (`LevelCanvasSelection`).
  - Block commands exist in `Canvas/LevelEditCommands`. Door commands (`TryPlaceOrExtendDoor`, `TryRemoveDoor`, `TryRecolorDoor`) arrive with `TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls`.
  - v2 effects (Arrow, Ice; gate effects are outside case scope) arrive with `TASK-20260924-215645373-33a6a7a4-level-json-v2-effects` and task 5 (`level-canvas-v2-tools`).
  - Evidence update (2026-09-25, implementation start):
    - The dependency `TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls` is closed (2026-09-25T13:55Z). Level JSON v2 has landed: `TASK-20260924-215645373-33a6a7a4-level-json-v2-effects` is closed and the schema owner, session "Ruled tile level sistem", sent SCHEMA FINAL on 2026-09-25. The Effects section therefore no longer waits for v2. It edits real effects through the v2 registry (`Runtime/Levels/LevelEffectRegistry`): for blocks, Arrow (`arrow`, required `axis` Horizontal or Vertical) and Ice (`ice`, required `clearsToThaw` 1 to 20). Gate effects stay out of case scope, because the registry marks no gate effect as editable.
    - The user removed the Mechanics group from the Level Editor tools column (2026-09-25). Effects are edited only in the Effect Editor.
    - New user decision (2026-09-25): "sadece bariyer olsun … starts open da kalkmalı; bu effect olarak eklenebilir olmalı burada durmak yerine 3. windowdan" (only the Barrier stays; Starts open leaves the Obstacles section and becomes an effect setting added from the third window, the Effect Editor). The Effect Editor therefore also shows a selected board object (Barrier) and edits its `barrier` effect's optional `startsOpen` bool (default false = closed): adding, changing or removing the param. A newly painted barrier has no params. The removal of the Obstacles toggle belongs to `TASK-20260925-102907480-9e5cbf34-level-editor-obstacles-barrier` and `TASK-20260925-184759859-c962353f-barrier-starts-closed-no-solid-block`.
    - Barrier selection on the canvas (the `Obstacle` selection kind, `LevelCanvasSelection.ForObstacle`) comes with `TASK-20260925-102907480-9e5cbf34-level-editor-obstacles-barrier`, which another agent is implementing. The kind and factory already exist in `Canvas/LevelCanvasSelection.cs`, so the board-object section codes against them.
    - Other writers are active in the same Level Editor files (Barrier, "+" level pieces, timer row icon). This task keeps its code in new files and limits `LevelDatabaseWindow.cs` to the selection event, one command entry point and the menu item.
- Expected behavior:
  1. **Launcher.** Color Block Jam/Level Editor, the existing entry point:
     - calls `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo`, and stops on Cancel;
     - opens `Assets/ColorBlockJam/Scenes/LevelEditor.unity` single unless it is already the active scene; this step refuses in Play Mode with a message;
     - opens or focuses the Piece Editor (its public `Open`), the Level Editor and the Effect Editor. The Effect Editor docks as a tab beside the Level Editor the first time, and later calls keep the designer's layout;
     - never writes the scene.
  2. **Effect Editor window.**
     - `LevelEffectEditorWindow` is a UI Toolkit `EditorWindow`, "Effect Editor". It follows the Level Editor's canvas selection through one editor-only event owned by the Level Editor window.
     - A block or door click, or a Selected Block choice, shows that entity. With no selection it shows the hint "Click a block or a gate in the Level Editor". With no Level Editor open it says so.
     - A click updates the Effect Editor when it is open. If it is closed, the click opens it without taking focus.
  3. **Block section.**
     - Read-only id.
     - Colour swatches (the 11 gate colours).
     - Piece Type (library pieces).
     - Origin X/Y with nudge buttons.
     - Rotate clockwise and counter-clockwise, and mirror horizontally and vertically, using the toolbox's `LevelPieceTools`.
  4. **Gate section.**
     - Read-only id.
     - Side, start and length.
     - Colour.
  5. **Effects section.** It edits the selected entity's v2 effects through the registry, each change as one undo step:
     - Block: add Arrow or Ice, change `axis` or `clearsToThaw`, remove an effect. A value outside its rule, or a stacking conflict, is refused with the validation's message.
     - Gate: gate effects are outside case scope. An existing gate effect (hand-written JSON) is listed read-only and can be removed.
     - Board object (Barrier, the `Obstacle` selection): read-only id and cell count, and the `barrier` effect's optional `startsOpen`: add it, change it or remove it (default false = closed). Closing a barrier over a block cell is refused with the validation's message.
  6. **Edits go through the draft path.**
     - Every edit goes through `LevelEditCommands` and `LevelDraft.Replace` in the Level Editor window, as one undo group. The dirty marker, Save Level and `DraftLevelPublished` behave as for canvas gestures.
     - A refused edit (overlap, off board, door collision) shows the same message as the canvas, in the Effect Editor and the Level Editor status line, and changes nothing.
     - The Effect Editor never writes JSON or the draft itself.
  7. **Docs.** `docs/Level-Editor-Design.md` describes the workspace launcher, the Effect Editor sections and the selection event.
- Behavior that must remain unchanged:
  - Every behavior of tasks 1-3, of the toolbox task and of the gates/edges/walls task.
  - `DraftLevelPublished`, `LastPublishedLevel` and the `TrackSerializedObjectValue` publish that the LevelEditor scene preview listens to.
  - Piece Editor behavior (it is only opened).
  - LevelEditor scene content (it is never saved by the launcher).

## Ownership

- Owner/writer role: Level Editor session "Parça template boş kareler" (user decision 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectEditorWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectEditorWindow.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/LevelEditorWorkspace.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelEditorWorkspace.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260925-100308276-f4a07e8a-level-editor-workspace-effect-editor/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/** (opened, never saved or edited)
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Editor/BoardKit/** (call only `PieceEditorWindow.Open`)
  - Assets/ColorBlockJam/Editor/LevelPreview/** (its owner removes its own menu item)
  - Writing level JSON except through the existing Save Level path during manual checks
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: "Ruled tile level sistem" owns Editor/LevelPreview/** and is editing it (block-runtime-objects chain). It relayed both requests and does not touch Editor/Levels/**. The board-kit session owns Editor/BoardKit/**.
- Execution dependency: TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls handed off (its door commands exist) with its Level Editor paths released
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given another scene open with unsaved changes, when Color Block Jam/Level Editor runs, then the save prompt appears. Cancel keeps everything. Otherwise LevelEditor.unity becomes the active scene and the Piece Editor, Level Editor and Effect Editor are open. A second run changes no scene and only focuses the windows.
2. Given the Level Editor with a level, when a block is clicked, then the Effect Editor shows its id, colour, piece type and origin. When a gate is clicked, it shows side, start, length and colour. With nothing selected it shows the hint.
3. Given the Effect Editor, when the colour, origin, piece type, rotation or mirror of a block, or the colour, start or length of a gate, is changed, then the draft changes through the command path as one undo step. Ctrl+Z restores it, the dirty marker updates and `DraftLevelPublished` fires once.
4. Given an edit that overlaps, leaves the board or collides with a door, then it is refused with the canvas's message and the draft is unchanged.
5. Given the Effect Editor closed, when a block is clicked, then it opens without taking focus from the Level Editor. Given a selected block, Arrow and Ice are added, changed and removed through the draft as one undo step each; given a selected barrier, its `startsOpen` param is added, changed and removed the same way. An out-of-range value, or closing a barrier over a block cell, is refused with the validation's message and changes nothing. (Revised 2026-09-25: level JSON v2 has landed, so the former "disabled with Needs level JSON v2" state is dropped.)

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User requests of 2026-09-25, relayed by session "Ruled tile level sistem", quoted above.
  - User decision of 2026-09-25 on the Barrier's Starts open (quoted above) and `production/tasks/TASK-20260925-184759859-c962353f-barrier-starts-closed-no-solid-block/contract.md`.
  - `docs/Level-Data-Format.md` (the effect registry and its validation messages).
  - `docs/Level-Editor-Design.md`.
  - PRD 11.7 (Effects panel: select a block or door to add an effect).
  - `docs/Color-Block-Jam-Mechanics-Guide.md` (Arrow and Ice in scope; gate effects outside case scope).
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - UI Toolkit only.
  - No new package or asmdef.
  - One writer of the draft: the Level Editor window applies commands, and the Effect Editor only raises edit requests.
  - Editor-only event with a named owner and stop point.
  - No schedule loops.
- Explicit out of scope:
  - Authoring gate effects (outside case scope; existing ones can only be removed).
  - Block effects other than Arrow and Ice, and board objects other than the Barrier (existing ones are listed read-only and can be removed).
  - Removing the Obstacles section's Starts open toggle (TASK-20260925-102907480-9e5cbf34, TASK-20260925-184759859-c962353f).
  - Freehand custom pieces.
  - Changing the Piece Editor or the LevelPreview code.
  - Saving scenes.

## Verification and risk

- Focused test path/command: new `LevelEffectEditorTests` (selection follow, block, gate and barrier edits through commands, undo and refusal; the launcher's window set and scene switch through injectable steps, so no real scene or user window opens in tests) and `LevelEffectCommandsTests` (effect, param and gate span commands)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console in the live Editor or an isolated batchmode copy
- EditMode: the focused tests above plus every Level* suite, `DatabaseControlsTests` and the LevelPreview tests stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - The menu command in the live Editor (scene switch, three windows).
  - Block and gate edits in the Effect Editor.
  - User acceptance of the layout.
- Risk: medium (new editor window and a scene-switching menu command)
- Approval already granted (exact target only): the user's relayed requests of 2026-09-25 for this launcher and Effect Editor, and this session's ownership of Level Editor work
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: $implement-task TASK-20260925-100308276-f4a07e8a-level-editor-workspace-effect-editor, then an independent verifier

## Stop conditions

- The launcher needs a change in Editor/BoardKit or Editor/LevelPreview: stop and send it to that owner.
- A Level Editor path changed within the last 10 minutes by another session: wait, do not edit.
- A required change falls outside the allowed paths: stop and record it.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:5727793cbc096449f7c9e2037074d591012e4f0c22e62c7b1843c049d0ba34a1
- Evidence fingerprint: sha256:00cbe3084d403747609d16473517ace32501740ea116245827e9ffa4236df95a
- Reviewed revision: vcs:cs:7+owned-sha256:39d301496f7e7516a200528951ee73e2ab4122b03842cde1a8642959bbf451fe
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:39d301496f7e7516a200528951ee73e2ab4122b03842cde1a8642959bbf451fe
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Launcher opens the scene and three windows | UNPROVEN | automated: LevelEditorWorkspace logic through seams (Cancel, first and second run, Play Mode refuses only the scene switch); the real menu run, scene switch and docking are a manual check (the menu is never run against the user's Editor by agents) | vcs:cs:7+owned-sha256:39d301496f7e7516a200528951ee73e2ab4122b03842cde1a8642959bbf451fe |
| AC-2 Effect Editor follows the canvas selection | PASS | automated: LevelEffectEditorTests (follows block, gate and board-object selection; hint with no selection; hands over when a Level Editor closes); Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 529/529 incl. 28 new; verifier 505/505; independent verifier: VERIFY: PASS (0 findings) | vcs:cs:7+owned-sha256:39d301496f7e7516a200528951ee73e2ab4122b03842cde1a8642959bbf451fe |
| AC-3 Edits go through the undoable draft path | PASS | automated: LevelEffectEditorTests (each edit one undo group through the draft; dirty marker; one preview publish); Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 529/529 incl. 28 new; verifier 505/505; independent verifier: VERIFY: PASS (0 findings) | vcs:cs:7+owned-sha256:39d301496f7e7516a200528951ee73e2ab4122b03842cde1a8642959bbf451fe |
| AC-4 Refused edits change nothing | PASS | automated: LevelEffectEditorTests.ARefusedEditShowsTheCanvasMessageInBothWindowsAndChangesNothing, LevelEffectCommandsTests (closing a barrier over a block refused); Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 529/529 incl. 28 new; verifier 505/505; independent verifier: VERIFY: PASS (0 findings) | vcs:cs:7+owned-sha256:39d301496f7e7516a200528951ee73e2ab4122b03842cde1a8642959bbf451fe |
| AC-5 Opens without focus; edits effects (Arrow, Ice, Barrier startsOpen) | UNPROVEN | automated: effects edited through the v2 registry (Arrow axis, Ice clearsToThaw 1-20, Barrier startsOpen) pass; the real open-without-focus behaviour on a canvas click is a manual check after one run of the menu | vcs:cs:7+owned-sha256:39d301496f7e7516a200528951ee73e2ab4122b03842cde1a8642959bbf451fe |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's relayed requests of 2026-09-25 (properties window and three-window launcher)
- Started — implementation — 2026-09-25T18:55Z: 465fec81 closed and level JSON v2 landed (SCHEMA FINAL), so the Effects section edits Arrow and Ice through the registry; the Mechanics group is gone from the tools column; the user's Barrier decision adds the board-object section with the optional `startsOpen` param; code goes into new files beside the concurrent Barrier, "+" piece and timer-icon writers; fingerprint recomputed
- Verification — 2026-09-25T19:43Z: implemented by an agent (launcher, Effect Editor with block, gate, board-object and effect sections; startsOpen moved here per the user); independent verifier VERIFY: PASS; AC-1 and AC-5 need one manual run of Color Block Jam/Level Editor by the user
- Updated — verification — 2026-09-26: TASK-20260926-170647550-7683988f-effect-editor-component-cards (session "Edit dropdown ve colors kareleri") redesigned the Effect Editor on the user's request: a title plus collapsible effect cards (`LevelEffectCard`), one Add Effect `AdvancedDropdown` (`LevelEffectDropdown`) listing only registry-allowed block effects, the board-object card shown by default with Barrier Starts open editable and its closing-over-a-block refusal; the Block/Gate/Board object field sections are removed. AC-2, AC-3 and AC-4 are kept and their tests ported; only the `SelectionChanged` and `TryApplyEditRequest` hooks are used; that task's run: Level EditMode 650/650. The reviewed revision is stale; re-stamp at closure. AC-1 (launcher) and AC-5 (edit an effect) stay manual on the new panel. Follow-up: `LevelEffectCommands.TrySetDoorSpan` has no production caller any more (dead code candidate, not removed here).
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
