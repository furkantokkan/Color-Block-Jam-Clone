# TASK-20260926-170647550-7683988f-effect-editor-component-cards — Effect Editor: a title and Add Component-style effect cards

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-170647550-7683988f-effect-editor-component-cards
- Supersedes: none
- Title: Effect Editor: a title and Add Component-style effect cards
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 plus the uncommitted Level Editor work of 2026-09-26, including the Effect Editor of TASK-20260925-100308276-f4a07e8a (in verification) and the in-progress TASK-20260926-140520462-2196c1d1
- Task kind: UI
- Area: Level Editor authoring tools, Effect Editor window (Editor assembly `ColorBlockJam.Editor`)
- Workflow: direct

## Objective and baseline

- Observable objective: For the block, gate or board object selected on the Level Editor canvas, the Effect Editor shows only a title and that entity's effects. Each effect is a collapsible, Unity-component-like card, and one "Add Effect" button adds a block effect from a menu.
- Current behavior/evidence (`LevelEffectEditorWindow.cs`, user screenshot `references/effect-editor-before.png`):
  - A Block section shows Id, Colour swatches, Piece Type, Origin X/Y, four nudge buttons and four rotate/mirror buttons.
  - A Gate section shows Id, Side, Start, Length and Colour. A Board object section shows Id, Type and Cells.
  - The Effects section lists each effect as a flat row: a bold title and a Remove button (except on a board object), then its registry-driven parameter rows. Block effects are added with a row of "+ Arrow" / "+ Ice" buttons.
  - Gate effects are read-only and removable. A board object has exactly one effect whose optional params can be added, changed and removed.
- Expected behavior (user request and answers of 2026-09-26, see References):
  - Title: one bold title names the selection, for example "Block b1", "Gate d1" or "Board object barrier-1".
    - The Block, Gate and Board object field sections are removed: Colour, Piece Type, Origin X/Y, nudges, rotate/mirror, Side, Start, Length, gate Colour, Type and Cells. The Level Editor canvas and tools already edit those.
    - The handlers and loads only those fields used are removed with them.
  - Effect cards: each effect is a card whose header is a foldout named after the effect (existing `ToLabel` names). The body holds the existing parameter rows, unchanged.
    - Clicking the header collapses or expands the card. There is no enabled flag and no data change.
    - A collapsed card stays collapsed through later edits and refreshes of the same effect type while the window is open.
    - The Remove action moves into the card header wherever removal exists today (block and gate effects, not a board object's effect).
    - Gate effect cards stay read-only; a board object's card keeps its optional-param add/change/remove.
  - Add Effect (revised 2026-09-26, see Contract history): for a block, one wide, centred "Add Effect" button below the cards, like the Inspector's Add Component button.
    - Clicking it opens a searchable popup like Unity's Add Component window, built on Unity's `AdvancedDropdown`: a title, a search field and one row per addable block effect type (`LevelEffectCommands`: today Arrow and Ice).
    - A type the block already has is shown disabled. Choosing a type adds it as one undo step, and its card appears expanded.
    - Gates and board objects have no Add Effect button, as today.
    - The menu lists only the block effect types the v2 registry and `LevelEffectCommands` allow: today Arrow (axis) and Ice (clearsToThaw 1-20).
  - A board object's single effect card is always shown, for example the Barrier with its optional Starts open. It is never listed in Add Effect.
  - "No effects." shows when the entity has none. The gate and board-object notes, the refusal message, the hints and the follow-the-selection behaviour stay.
  - Every edit remains one request through `LevelEditRequest` and `LevelDatabaseWindow.TryApplyEditRequest` (one undo step). The window never writes the draft or the level JSON itself.
- Behavior that must remain unchanged:
  - Effect add, parameter set/remove and effect remove results, undo steps and refusal messages.
  - Registry-driven parameter fields, and the "default" marker for absent optional keys.
  - Gate effects read-only and removable; a board object's single effect with its optional params.
  - The Barrier's Starts open stays editable on its card, including the refusal for closing a barrier over a block (`LevelEffectCommandsTests`). The user decided it is edited only in the Effect Editor (TASK-20260925-102907480-9e5cbf34).
  - TASK-20260925-100308276-f4a07e8a's accepted AC-2, AC-3 and AC-4:
    - the panel follows the block, gate and board-object selection, with the hint when nothing is selected;
    - every edit is one undo group through the draft, with the dirty marker and one preview publish;
    - a refused edit shows the canvas message and changes nothing.
    Their tests are kept or ported to the cards; only assertions on the removed fields are retired.
  - The Effect Editor follows the Level Editor selection and hands over when a Level Editor closes; the workspace launcher.
  - Everything in the Level Editor window, including canvas block/gate editing, swatch recolour and rotate/mirror tools.
  - Level JSON format and gameplay.

## Ownership

- Owner/writer role: implement-task (single writer), dispatched after the predecessor below
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectEditorWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectCard.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectCard.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectDropdown.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectDropdown.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelEffectEditorTests.cs
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260926-170647550-7683988f-effect-editor-component-cards/contract.md (managed lifecycle block only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/** (no level schema, registry or effect-rule change)
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs (call only: `TryApplyEditRequest`, `CanvasSelection`, `GetDraftLevel`, `SelectionChanged`)
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEffectCommands.cs (call only)
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorSwatches.cs (owned by TASK-20260926-145804549-da16f02f)
  - Assets/ColorBlockJam/Scenes/**
  - Any other existing test file
  - Plastic checkin, shelve, branch, or undo of other sessions' pending changes
- Serialized assets: none (a new `LevelEffectCard.cs.meta` is Unity-generated script metadata owned by implement-task)
- Other active writers/worktrees checked (2026-09-26T17:07Z):
  - Session "Rotate tool rotation behavior" holds the TASK-20260926-140520462-2196c1d1-level-editor-pen-edit-templates lock (since 16:44:51Z); its ownership includes `Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs` and docs/Level-Editor-Design.md.
  - TASK-20260926-145804549-da16f02f (this session) is queued first behind it and also writes docs/Level-Editor-Design.md.
  - TASK-20260925-100308276-f4a07e8a (owner session "Parça template boş kareler", idle) owns the current Effect Editor. It holds no lock and has finished implementation; it waits for the user's manual AC-1/AC-5 run. This task retires its block/gate/object field editing on the user's request, and its owner is told before the first write.
- Execution dependency: TASK-20260926-145804549-da16f02f-level-editor-color-order (FIFO in this session). It starts after that task's second-revision implementation is verified and its lifecycle lock is released, which itself waits for session "Rotate tool rotation behavior" to send "LEVEL EDITOR PATHS FREE". Re-read every owned file before the first write.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. AC-1: Given a block, a gate and a board object selected in turn on the Level Editor canvas, when the Effect Editor shows each, then:
   - it shows the title "Editing {colour} {ID}", "Editing {colour} Gate {ID}" or "Editing {type} {ID}" (2026-09-27 revision; for example "Editing Green B2", "Editing Red Gate D1", "Editing Barrier O1") and that entity's effect cards or "No effects.";
   - no Colour, Piece Type, Origin, nudge, rotate/mirror, Side, Start, Length, Type or Cells control exists in the window.
2. AC-2: Given a block with an effect, when the designer clicks its card header, then the card collapses (params hidden) and expands again on the next click. A collapsed card stays collapsed after a later edit of the same block while the window is open.
3. AC-3: Given a block, when the designer presses the wide Add Effect button under the cards, then:
   - a searchable Add Component-style popup lists the addable block effect types, with the ones the block already has disabled;
   - choosing one adds that effect as one undo step and shows its card expanded;
   - gates and board objects show no Add Effect button.
4. AC-4: Given effect cards, when the designer edits a parameter, adds or removes an optional key, or removes a block or gate effect from its card header, then the draft changes exactly as before, one undo step each, and a refusal shows the same message and changes nothing. Gate cards stay read-only; a board object's card has no remove.
5. AC-5: Given the Level EditMode suite, when it runs after the change, then it passes. `LevelEffectEditorTests` drops or rewrites only the assertions on the removed fields and covers the title, cards, collapse and Add Effect menu.
6. AC-6: Given the live Effect Editor after the change, when the user looks at a block with effects, then they accept the look (explicit user acceptance).

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User request of 2026-09-26 in session "Edit dropdown ve colors kareleri": "Effect editörde bunların gözükmesine gerek yok burada add component tarzı bir sistem istiyorum kısacası block üzerindeki effectleri görebileceğim editleyebileceğim kapatıp açabileceğim tarzda". Screenshot: `references/effect-editor-before.png`.
  - User answers of 2026-09-26: "Kartı katla/aç" (collapse/expand only, no enabled flag) and "Sadece başlık + effectler" (a title plus effects for blocks, gates and board objects).
  - docs/Level-Editor-Design.md, section "Workspace and Effect Editor"; TASK-20260925-100308276-f4a07e8a (the Effect Editor this task revises).
- Unity version/platform: Unity 2022.3.62f2, Windows Editor only (editor tooling, UI Toolkit)
- Package/architecture constraints:
  - UI Toolkit only (`Foldout` or an equivalent card element).
  - The Add Effect popup is a `UnityEditor.IMGUI.Controls.AdvancedDropdown` subclass. It may live in `LevelEffectCard.cs` or its own file under the allowed paths.
  - No new package, asmdef, Runtime or schema change.
  - Follow the window's code style: one-sentence class summary, `m_`/`k_` names, owner and stop-point comments for subscriptions.
  - A card class may move to `LevelEffectCard.cs` when that keeps the window's responsibility clear.
- Explicit out of scope:
  - An enabled/disabled flag or any level JSON or gameplay change.
  - New effect types, and adding gate or board-object effects.
  - Level Editor window changes.
  - Reordering effects.

## Verification and risk

- Focused test path/command: UnityMCP `run_tests` EditMode, group `ColorBlockJam[.]Tests[.]EditMode[.]LevelEffectEditorTests[.]`, then the Level suite `ColorBlockJam[.]Tests[.]EditMode[.]Level`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: a clean compile after `refresh_unity` with no new Console errors, and the user's open Level Editor draft still intact after the reload
- EditMode:
  - Title per entity kind, and the removed controls absent.
  - Card collapse/expand, and collapse kept across a refresh.
  - The Add Effect menu's items and disabled state.
  - Add, param edit, optional-param remove and effect remove as one undo step each, and refusal messages.
  - Gate cards read-only; a board-object card without remove.
- PlayMode: not applicable (editor tooling only)
- Manual/built-player/profiler/device evidence: a live Effect Editor screenshot, then explicit user acceptance (AC-6)
- Risk: medium. It retires editing paths accepted under another task (f4a07e8a) on the user's request; editor UI only, with no Runtime or schema change.
- Approval already granted (exact target only): the user's 2026-09-26 answers above, including removing the Effect Editor's block, gate and board-object field sections
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: `$implement-task TASK-20260926-170647550-7683988f-effect-editor-component-cards`, auto-dispatched after the predecessor above

## Contract history (conditions agreed after authoring)

- 2026-09-26T17:08Z: session "Parça template boş kareler", owner of TASK-20260925-100308276-f4a07e8a and the Barrier task 9e5cbf34, had no objection. It set four conditions, recorded above:
  - keep f4a07e8a's AC-2, AC-3 and AC-4 true for the cards, porting their tests;
  - use only the existing `LevelDatabaseWindow.SelectionChanged` and `TryApplyEditRequest` hooks, and tell that session first if a new hook is needed;
  - keep the Barrier's Starts open editable on its card, with the refusal message;
  - list only registry-allowed block effects in Add Effect, and show the board object's card by default.
  - That session gets "PATHS FREE" when this task is done.
- 2026-09-26T17:12Z, user message before implementation: "buradaki gibi ama Add Effect yazabilir tıklayınca effectler çıkar tıpkı unity gibi". It came with two screenshots that were not saved as files: the Inspector's wide, centred "Add Component" button, and its popup (a search field, a "Search" title and a list row).
  - The Add Effect control becomes that wide, centred button, opening an `AdvancedDropdown` popup (Unity's Add Component control) instead of a plain menu. `AdvancedDropdown` is chosen over the project's `ChoiceSearchPopup` because it has the title bar and disabled rows the user's reference shows.
  - Tests check the listed choices and the add path through a window seam, not the popup itself, which cannot open in batch mode.
- 2026-09-27, a user request made in session "Shuffle ve switch değişiklikleri" and relayed by it, with a screenshot of the title "Block b2": "burada B harfi büyük yazsın ve rengi de yazsın örnek olarak ve Editing Green B2 gibi bir şey yazabilir örnek olarak". The user gave the block form ("Editing Green B2"). The gate and board-object forms ("Editing Red Gate D1", "Editing Barrier O1") were that session's proposal, not yet confirmed by the user; they are shown to the user with the result.
  - Title format:
    - blocks: "Editing {colour name} {ID}";
    - gates: "Editing {colour name} Gate {ID}";
    - board objects: "Editing {type display name} {ID}".
  - {ID} is the entity id upper-cased as the canvas labels it (`LevelCanvasElement` draws `Id.ToUpperInvariant()`). The colour name is the gate colour's display name (for example Green, Red); the type display name comes from the effect or type label (Barrier).
  - This revises AC-1's title line and the Title bullet. Everything else is unchanged.
  - Implementer: this session, queued behind TASK-20260926-204508638-a3ad6ee3-level-editor-architecture, whose lock covers `Tests/EditMode/Level*Tests.cs` and so `LevelEffectEditorTests.cs`. `LevelEffectEditorWindow.cs` itself is outside that lock, but its title tests must change in the same step.

## Stop conditions

- The predecessor still runs, or another writer holds LevelEffectEditorWindow.cs, LevelEffectEditorTests.cs or docs/Level-Editor-Design.md: stay queued.
- Session "Parça template boş kareler" objects, or has resumed writing the Effect Editor: stop and ask the user.
- A needed change falls in a forbidden path, for example a LevelEffectCommands or LevelDatabaseWindow change.
- An existing Effect Editor behaviour outside the removed fields would change.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:f99864895da0bda62d21d781c032f50d36284f36a36df095135e3cf39786b0e3
- Evidence fingerprint: sha256:8d6a44f1938f31e5f3604235dba309f42f072128d9c888eadefb82c0f7cffa5c
- Reviewed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:31fe6af92620dee7ae46be6245e5b8dd70e51db873a2bc3bc068173589bbc67d
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:31fe6af92620dee7ae46be6245e5b8dd70e51db873a2bc3bc068173589bbc67d
- Closed at: 2026-09-27T17:24:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated (title revision): LevelEffectEditorTests.TitlesNameTheColourAndTheCanvasLabel and the updated title assertions (job d8b3bbc1, 32/32); earlier automated: LevelEffectEditorTests.TheEffectEditorFollowsTheCanvasSelectionWithATitleAndCards (job 079d3551, 31/31) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:31fe6af92620dee7ae46be6245e5b8dd70e51db873a2bc3bc068173589bbc67d |
| AC-2 | PASS | automated: ACollapsedCardStaysCollapsedThroughEditsAndSelectionChanges, AnAddedEffectShowsItsCardExpandedEvenWhenItsTypeWasFolded, the Barrier toggle fold check (job 079d3551) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:31fe6af92620dee7ae46be6245e5b8dd70e51db873a2bc3bc068173589bbc67d |
| AC-3 | PASS | automated through the window seam: AddEffectListsTheRegistryBlockEffectsAndDisablesTheOnesTheBlockHas, AnEffectEditIsOneUndoStepWithTheDirtyMarkerAndOnePublish (job 079d3551); the popup opening is part of the AC-6 look | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:31fe6af92620dee7ae46be6245e5b8dd70e51db873a2bc3bc068173589bbc67d |
| AC-4 | PASS | automated: BlockEffectsAreAddedChangedAndRemovedThroughTheDraft, TheBarrierStartsOpenParamIsAddedChangedAndRemoved, GateEffectsAreReadOnlyAndCanBeRemoved, ARefusedEditShowsTheCanvasMessageInBothWindowsAndChangesNothing, LevelEffectCommandsTests (job 079d3551) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:31fe6af92620dee7ae46be6245e5b8dd70e51db873a2bc3bc068173589bbc67d |
| AC-5 | PASS | automated: Level suite 723/723 with the title change compiled (job 135d995a; earlier 625/625, job 6c640692) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:31fe6af92620dee7ae46be6245e5b8dd70e51db873a2bc3bc068173589bbc67d |
| AC-6 | UNPROVEN | awaits explicit user acceptance; screenshots sent 2026-09-26 (live "Block b2" with Add Effect, and sample Ice/Arrow cards) | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored 2026-09-26 by session "Edit dropdown ve colors kareleri" from the user's request and answers; queued FIFO behind TASK-20260926-145804549-da16f02f
- Contract revision — 2026-09-26T17:08Z: the conditions of the Effect Editor owner session ("Parça template boş kareler") are recorded in "Contract history"; fingerprint restamped from sha256:29db58e9a2f5…; still ready and queued
- Contract revision — 2026-09-26T17:12Z: the user's Unity Add Component reference makes Add Effect a wide centred button that opens an `AdvancedDropdown` popup; allowed paths gain LevelEffectDropdown.cs; fingerprint restamped from sha256:0a68c17233c2…; still ready and queued
- Started — implementation — 2026-09-26T17:15Z: the user ordered parallel work ("tasklerini bitirene kadar devam et paralel çalışabilirsin"), so this task starts ahead of its FIFO place. Its files are disjoint from the active writers: session "Rotate tool rotation behavior" was told which files this session takes and does not edit them; session "Parça template boş kareler" agreed. docs/Level-Editor-Design.md waits for its owner.
- Verification — 2026-09-26T18:00Z: an independent general-purpose verifier returned CLEAN for AC-1 to AC-5 (jobs 18b39f70 30/30 and 565b1f50 624/624). Its low-severity notes were fixed in the owned files: an owner/stop-point comment on the card's foldout callback, Add Effect choices cleared when nothing is shown, and two tests that fail without the foldout's own-event filter or without Add Effect's re-expand. Folding stays per entity kind and effect type, like the Inspector. Focused 31/31 (job 079d3551) and the Level suite 625/625 (job 6c640692) passed at the reviewed revision. The shared design doc is left out of the reviewed digest. Follow-ups outside this task's paths: `LevelEffectCommands.TrySetDoorSpan` has no production caller; Piece Type is now in neither window (reported to the user); the hidden Piece Type comment in `LevelDatabaseWindow.cs` belongs to TASK-20260926-145804549-da16f02f.
- Docs — 2026-09-26T18:25Z: session "Rotate tool rotation behavior" sent LEVEL EDITOR PATHS FREE. docs/Level-Editor-Design.md "Workspace and Effect Editor" now describes the title, the effect cards (folding per entity kind and effect type), Add Effect, the commands (with `TrySetDoorSpan` unused) and the tests. The owned code is unchanged since the reviewed revision (digest recomputed, b3f9e743…). Level suite 627/627 (job e7b0c7b6) with the colour order task's later changes.
- Record note — 2026-09-26T19:20Z (gap reported by session "Effect properties paneli planlama"): before this task started, session "Grid size slider ve editor menüsü" had added `[MenuItem("Color Block Jam/Effect Editor", false, 1001)]` on `LevelEffectEditorWindow.Open`. That followed the user's request "Color Block Jam altına al". No task record held it. This task kept it in the rewrite and tests it (`TheEffectEditorHasAColorBlockJamMenuItem`), and docs/Level-Editor-Design.md now names it. This is not a scope change; the owned code is unchanged.
- Contract revision recorded — 2026-09-26T22:02Z: a user request relayed by session "Shuffle ve switch değişiklikleri" changes the title to "Editing {colour} {ID}" and the like (Contract history). Fingerprint restamped from sha256:1aa055772858…. AC-1 is STALE until it lands, queued behind the architecture lock (LevelEffectEditorTests.cs is in its glob).
- Contract note — 2026-09-26T22:06Z: the history now quotes the user's exact words; the gate and board-object title forms are marked as proposals. Fingerprint restamped from sha256:5648e5f4de88….
- Title revision implemented — 2026-09-26T22:45Z: after the architecture lock was released, the title reads "Editing {colour} {ID}", "Editing {colour} Gate {ID}" and, for board objects, "Editing Barrier 1" (a {type}-{n} id read as words; another id keeps the upper-case canvas form). The helpers are GetBlockTitle, GetGateTitle and GetObjectTitle; colour names use ObjectNames.NicifyVariableName ("Dark Green"). LevelEffectEditorTests has updated assertions and a new TitlesNameTheColourAndTheCanvasLabel. Focused 32/32 (job d8b3bbc1). Level suite 723/723 (job 135d995a, run by another session with this code compiled). No owned file changed after the focused run. The design doc's Title bullet is updated. The reviewed revision moved to the new digest.
- 2026-09-27T17:24:19Z Closed by explicit user confirmation (task-done, user_confirmed): the user accepted the live result on 2026-09-27 ('bunlar okay', item 1, the Effect Editor cards and titles; AC-6) and ordered 'tasklari kapat izin veriyorum'. Criteria rows are preserved as recorded, not re-verified; owned files may have changed after the reviewed revision through other sessions.
<!-- TASK-LIFECYCLE:END -->
