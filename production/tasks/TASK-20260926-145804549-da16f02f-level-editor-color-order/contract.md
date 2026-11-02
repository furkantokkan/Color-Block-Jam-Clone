# TASK-20260926-145804549-da16f02f-level-editor-color-order — Level Editor: colour order badges with Color Switch, Color Shuffle and Reset Order

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-145804549-da16f02f-level-editor-color-order
- Supersedes: none
- Title: Level Editor: colour order badges with Color Switch, Color Shuffle and Reset Order
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 plus the uncommitted Level Editor work of 2026-09-26 (column order Tools, Edit, Colors and 24 px swatches of session "Edit dropdown ve colors kareleri"; the in-progress TASK-20260926-140520462-2196c1d1-level-editor-pen-edit-templates)
- Task kind: feature
- Area: Level Editor authoring tools, Colors group (Editor assembly `ColorBlockJam.Editor`)
- Workflow: direct

## Objective and baseline

- Observable objective: In the Level Editor's Colors group, every gate colour swatch shows its 1-based position in a colour order in a small framed badge at its top-right. Two buttons (Color Switch, Color Shuffle) directly under the swatches in the Colors group (second and sixth revisions, see Contract history) act as one undo step each. Color Shuffle recolours the board with a random permutation and leaves the order, swatches and badges as they are (sixth revision). Color Switch swaps the clicked block's or gate's colour with a random other colour across the level only (seventh revision, see Contract history). Nothing changes the order any more, so the badges stay in palette order.
- Current behavior/evidence:
  - The Colors group holds 11 swatches (24 px, 2 px gap, one row in a 330 px tools pane) that set the tool colour. With Select and a selected block or gate, a swatch click recolours only that element (`LevelEditCommands.TryRecolorBlock` / `TryRecolorDoor`).
  - There is no colour order, no badge and no level-wide recolour.
  - The CBJ Template has no such feature, and no public source defines these buttons. The user's reference images (`references/`) show a "Color Switch / Color Shuffle / Reset Order" button strip and a swatch with a number at its top-right.
- Expected behavior:
  - Order model: a permutation of the 11 gate colours (`PaletteColor` 0-10, Red to Pink); the default is palette order (Red 1 ... Pink 11).
    - The order is editor draft state on `LevelDraft`: it is undoable together with the level, kept through a script or domain reload with the kept draft, and reset to the default whenever the draft is rebuilt (level or database change, Reload, Restore).
    - It is never written to level JSON; the level JSON format does not change.
  - Recolour rule (user decision "Level'ı yeniden renklendir", 2026-09-26): changing the order from A to B recolours every colour reference in the draft by position. The colour at position k in A becomes the colour at position k in B.
    - References: every block `colorId`, every door `colorId`, and every effect parameter that `LevelEffectRegistry` types as `LevelEffectValueKind.PaletteColor` (a single value or a comma list) on blocks, doors and cell objects. Examples: `outerColorId`, `colorA`/`colorB`, `colorSequence`, `ropeColors`, `layerColors`, `tokenColorId`, `altColorId`, cell-object `colorId`.
    - Values that are not one of the 11 gate colours (Wall, Ground, neutral, unknown text) and effects of an unknown type stay unchanged.
    - Because the map is a bijection, every block keeps matching its gates, effect colour relations hold, and `LevelData.Validate` gives the same result. Geometry, ids and all other data are untouched.
  - Color Shuffle (sixth revision): picks a random permutation of the gate colours that changes at least one colour the level uses (when it uses any) and recolours the level with it, by the recolour rule, as one undo step ("Shuffle Colours"). The draft's colour order does not change, so the swatch grid, the badges and the Pen colour keys stay as they are. The status line names the used colours' new colours, for example "Red → Purple, Blue → Yellow".
  - Reset Order: removed in the sixth revision (button and handler).
  - Color Switch (fifth revision, see Contract history):
    - The source colour A is the colour of the clicked element: the block the Pen edits, a block chosen with Select, or a gate chosen with Select. It is not the tool colour.
    - B is a random gate colour other than A, taken from the colours the level does not use when there are any, so only the A references visibly change; otherwise any colour other than A.
    - Every A reference in the level (blocks, gates and PaletteColor effect parameters) becomes B and every B reference becomes A, as one undo step ("Switch Colour"). Seventh revision: the draft's colour order no longer changes, so the grid, badges and Pen keys stay put.
    - The tool colour becomes B. With nothing chosen it changes nothing, records no undo step, and the tool hint reads "Click a block, then press Color Switch."
  - Badges: each of the Level Editor's 11 swatches shows its 1-based order position in a framed label at its top-right corner.
    - Badges update after every order change, undo/redo and draft rebuild.
    - A badge never takes pointer input: the swatch stays clickable and hoverable, and the badge does not change the swatch layout.
  - Swatch size (second revision): the Level Editor's swatches are 40 px squares in two rows of 6 and 5 in a 330 px tools pane. The Effect Editor keeps its 24 px swatches.
    - The Effect Editor's swatches (the same `LevelColorSwatches` control) show no badge.
  - Buttons (second and sixth revisions, see Contract history): two buttons in a row directly under the swatches in the Colors group (`color-order-actions`). There is no action row under the canvas.
    - Each shows the user's icon as a white 64 x 64 editor icon, followed by its name as text: the swap icon with "Color Switch", the crossing arrows with "Color Shuffle". The Reset Order button, and with it its circular-arrow icon, is removed.
    - Each has a tooltip naming its rule and "one undo step".
    - They are disabled while no level is loaded.
    - The buttons wrap to a second line at a narrow tools width rather than being clipped.
  - The canvas, the LevelEditor scene preview (`DraftLevelPublished`) and the issues list refresh through the existing draft-change path, and the window marks unsaved changes as for any edit.
- Behavior that must remain unchanged:
  - Swatch click, hover and selection outline, `[ / ]` cycling, and single-element recolour by swatch click.
  - The Effect Editor's colour rows, including their 24 px swatches.
  - The tools column order: Tools, Edit, Colors, Blocks, Obstacles, Raw data.
  - Save, Reload, Restore, undo/redo, and the reload-kept draft (a kept draft still restores, now with its order).
  - Save output, except for the recoloured values.
  - The predecessor tasks' Pen, Select and template behaviour.

## Ownership

- Owner/writer role: implement-task (single writer), dispatched after the predecessor releases the Level Editor paths
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDraft.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorSwatches.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditorIcons.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorOrder.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorOrder.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorRemap.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelColorRemap.cs.meta
  - Assets/ColorBlockJam/Editor/Levels/Icons/color-switch.png
  - Assets/ColorBlockJam/Editor/Levels/Icons/color-switch.png.meta
  - Assets/ColorBlockJam/Editor/Levels/Icons/color-shuffle.png
  - Assets/ColorBlockJam/Editor/Levels/Icons/color-shuffle.png.meta
  - Assets/ColorBlockJam/Editor/Levels/Icons/reset-order.png
  - Assets/ColorBlockJam/Editor/Levels/Icons/reset-order.png.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelColorOrderTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelColorOrderTests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelColorOrderWindowTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelColorOrderWindowTests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelColorSwatchesLayoutTests.cs
  - docs/Level-Editor-Design.md
  - docs/art/level-editor-icons/README.md
  - docs/art/level-editor-icons/source/color-switch.png
  - docs/art/level-editor-icons/source/color-shuffle.png
  - docs/art/level-editor-icons/source/reset-order.png
  - production/tasks/TASK-20260926-145804549-da16f02f-level-editor-color-order/contract.md (managed lifecycle block only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/** (no level schema, DTO, registry or validation change; call only)
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Data/** (no level JSON or database write; tests never save)
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditCommands.cs (call only)
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelRecordCopy.cs (call only)
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectEditorWindow.cs (its swatches stay badge-free without an edit)
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Scenes/**
  - Any other existing test file, except a minimal update of an assertion that pins the old Colors-group or selection-pane children (the canvas directly followed by the issues list) or the old 24 px Level Editor swatch size, reported in the handoff
  - Plastic checkin, shelve, branch or undo of other sessions' pending changes
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/color-switch.png
    Sole writer: implement-task
    Matching `.meta` owner: implement-task (importer settings copied from the existing Level Editor icons: Editor GUI and Legacy GUI, alpha is transparency, no mipmaps, clamp, bilinear, max size 64, uncompressed)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/color-shuffle.png
    Sole writer: implement-task
    Matching `.meta` owner: implement-task (same importer settings)
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/reset-order.png
    Sole writer: implement-task
    Matching `.meta` owner: implement-task (same importer settings)
- Other active writers/worktrees checked: session "Rotate tool rotation behavior [6fbe39]" holds two lifecycle locks under Library/ccgs/task-lifecycle-locks:
  - TASK-20260926-075624941-a65fbdb1-level-editor-select-mode-placement, since 2026-09-26T13:30:41Z.
  - TASK-20260926-140520462-2196c1d1-level-editor-pen-edit-templates, since 2026-09-26T14:07:17Z, state in_progress. It owns LevelDatabaseWindow.cs, LevelDraft.cs, Level*Tests.cs and docs/Level-Editor-Design.md.
  - Its cross-session message of 2026-09-26 also claimed Canvas/LevelColorSwatches.cs, until it sends "LEVEL EDITOR PATHS FREE".
- Execution dependency: TASK-20260926-140520462-2196c1d1-level-editor-pen-edit-templates, with the same session's TASK-20260926-075624941-a65fbdb1 lock. Stable handoff: session "Rotate tool rotation behavior" sends "LEVEL EDITOR PATHS FREE", and no lifecycle lock under Library/ccgs/task-lifecycle-locks still owns an allowed path of this task. Re-read every owned file before the first write.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. AC-1: Given the Level Editor with a level loaded, when the Colors group shows, then:
   - each of the 11 swatches has a framed badge at its top-right with its order position (1 to 11 in palette order by default);
   - a row directly under the swatches holds the Color Switch and Color Shuffle buttons (no Reset Order, sixth revision), each with the user's icon, its name and a tooltip, and no action row sits under the canvas;
   - the swatches are 40 px squares in two rows (6 and 5) of a 330 px tools pane.
2. AC-2 (sixth revision): Given a draft with blocks, gates and PaletteColor-typed effect parameters, when Color Shuffle is pressed, then:
   - a random permutation that changes at least one colour the level uses recolours every block and door colour and every PaletteColor-typed effect parameter, one to one;
   - non-gate colours and unknown effects stay unchanged;
   - `LevelData.Validate` gives the same result as before;
   - the draft's colour order, the swatch grid, the badges and the Pen colour keys stay unchanged;
   - one Undo restores the previous colours.
3. AC-3: retired in the sixth revision (Reset Order removed). Given the Level Editor, there is no Reset Order button or handler, and the Reset Order icon and its constant are gone when nothing else uses them.
4. AC-4 (fifth revision): Given a clicked element of colour A (the block the Pen edits, or a block or gate chosen with Select), when Color Switch is pressed, then:
   - a random gate colour B other than A is chosen, from the colours the level does not use when there are any;
   - every A reference and every B reference in the level trade colours, effect parameters included, in one "Switch Colour" undo step;
   - the draft's colour order, the grid, the badges and the Pen keys stay unchanged (seventh revision);
   - the tool colour becomes B;
   - one Undo restores the previous colours.
   With nothing chosen, Color Switch changes nothing, records no undo step, and the tool hint reads "Click a block, then press Color Switch."
5. AC-5: Given a changed order and an unsaved draft, when a script or domain reload keeps the draft, then the order is restored with it. When the draft is rebuilt (another level, Reload, Restore or a database change), the order is palette order again.
6. AC-6: Given the Effect Editor, when it shows its block and gate colour rows, then its swatches show no order badges and behave as before.
7. AC-7: Given the existing Level Editor and Effect Editor EditMode fixtures, when they run after the change, then everything they pinned before this task still passes: swatch click, hover and selection, `[ / ]`, single-element recolour, Save, undo and draft reload, and the column order. `ALibraryReloadedElsewhereRefreshesTheShapesAndDisarmsAChangedShape` is excluded; the predecessor owns its stale Assume.
8. AC-8: Given the live Level Editor after the change, when the user looks at the Colors group (screenshot provided), then they accept the badge and button look (explicit user acceptance).

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User request of 2026-09-26 in session "Edit dropdown ve colors kareleri". Durable copies of the attached images are in this task's `references/` folder:
    - `reference-button-strip.png`: the Color Switch / Color Shuffle / Reset Order strip.
    - `reference-numbered-swatch.png`: a swatch with a top-right number.
    - `icon-color-switch.png`, `icon-color-shuffle.png`, `icon-reset-order.png`: 512 px black-on-transparent masters.
    - `reference-bottom-action-row.png` (as sent, rotated) and `reference-bottom-action-row-upright.png` (turned upright): the reference editor's action row under the canvas (Auto Place Walls, Reset, ApplyPrefab, Show Crates, Toggle Elevator Layer, Debug Tools).
  - User decisions of 2026-09-26: order logic "Level'ı yeniden renklendir"; Color Switch "Rastgele renkle takas".
  - docs/Level-Editor-Design.md: section 2 Colors row, the Tools cleanup column-order note, and the Toolbox icon conventions.
  - docs/Level-Data-Format.md: effect registry colour keys.
  - docs/art/level-editor-icons/README.md: icon master and naming conventions.
  - PROJECT.md DEC-119: one undoable draft; level JSON is written only by Save.
- Unity version/platform: Unity 2022.3.62f2, Windows Editor only (editor tooling, UI Toolkit)
- Package/architecture constraints:
  - Plain C# `LevelColorOrder` (permutation, positions, shuffle, swap, map) and `LevelColorRemap` (level-wide remap driven by `LevelEffectRegistry.TryFind` and key kind) in the Editor assembly.
  - The random source is an injectable `System.Random`, so tests are deterministic.
  - The order is a serialized field on `LevelDraft`, recorded with the same `Undo.RecordObject` step as the level. The reload copy (`LevelDraft.ReloadCopy`) carries it.
  - No new package, asmdef, Runtime, Domain or schema change.
  - Follow the surrounding editor code style: `m_` fields, `k_` constants, one-sentence class summary.
- Explicit out of scope:
  - Persisting the order in level JSON.
  - Runtime or random recolouring in the game.
  - Colour readability rules (for example, keeping similar colours apart).
  - Number-key shortcuts.
  - Badges in the Effect Editor.
  - Palette or colour value changes.
  - Save behaviour changes.

## Verification and risk

- Focused test path/command: UnityMCP `run_tests` EditMode, group `ColorBlockJam[.]Tests[.]EditMode[.](LevelColorOrderTests|LevelColorOrderWindowTests|LevelColorSwatchesLayoutTests)[.]`, then the regression group `(LevelCanvasBlockToolsTests|LevelPaletteHoverTests|LevelEffectEditorTests|LevelEditorShellTests|LevelCanvasGatesEdgesTests|LevelObstaclesSectionTests)`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: a clean compile after `refresh_unity` with 0 Console errors, and the user's open Level Editor draft still unsaved and intact after the reload
- EditMode:
  - Order model: default positions, shuffle yields a permutation with a seeded `System.Random`, swap, and position mapping.
  - Remap: every PaletteColor-typed key found by enumerating `LevelEffectRegistry` is remapped. Non-gate colours and unknown effects stay unchanged. `Validate` gives the same result. A remap followed by its inverse restores the level exactly.
  - Window: badges, buttons and tooltips, Shuffle / Reset / Switch as one undo step each, and the no-op Reset. The order survives a kept-draft reload and resets on a draft rebuild. Effect Editor swatches show no badges.
  - Layout: the swatches keep one row with the badges shown.
- PlayMode: not applicable (editor tooling only)
- Manual/built-player/profiler/device evidence: a live Level Editor screenshot of the Colors group (GUIView.GrabPixels into a Linear render texture, flipped in Y), then explicit user acceptance of the look (AC-8)
- Risk: medium. It adds draft state and a level-wide edit command that touches every colour reference; there is no Runtime, schema or package change. The user's 2026-09-26 answers approved the semantics.
- Approval already granted (exact target only): the colour-order semantics and Color Switch behaviour above; adding the three icon textures under Assets/ColorBlockJam/Editor/Levels/Icons with the existing icon importer settings
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: `$implement-task TASK-20260926-145804549-da16f02f-level-editor-color-order`, auto-dispatched by `$game-studio-orchestration` after the owner releases the paths

## Stop conditions

- The predecessor still holds a lock on an allowed path, or has not sent "LEVEL EDITOR PATHS FREE": stay queued and do not write.
- A colour-bearing level field exists that `LevelEffectRegistry` does not type as PaletteColor and that only a Runtime change can reach: stop and report.
- Carrying the order through `KeepDraftForReload` / `RestoreDraftAfterReload` would change their mismatch rules: stop and ask.
- A needed change falls in a forbidden path, or an existing test fails for a reason this task cannot fix inside its allowed paths.
- Any step would save level JSON or open, save or modify a scene.

## Contract history (user decisions after authoring)

- 2026-09-26, before any implementation, in session "Edit dropdown ve colors kareleri". The user sent the reference editor's bottom action row and wrote: "butonlar da alt tarafa gelebilir bu referanstaki gibi".
  - The Color Switch, Color Shuffle and Reset Order buttons move from the Colors group to a new action row directly under the canvas, above the issues list. Each shows its icon and its name.
  - This revises the observable objective, the Buttons bullet under Expected behavior and AC-1. The order model, badges, recolour rule, allowed paths and risk are unchanged.
- 2026-09-26, after the verified handoff, in the same session. The user wrote "colors karelerini büyült ve istediğim butonlar gelmemiş" with a screenshot of the tools column (`references/feedback-tools-column.png`). Intake classified it as a revision, not a defect: the buttons existed under the canvas as contracted. The user then answered:
  - Buttons: "Renk butonları Colors altına". The three buttons move directly under the swatches in the Colors group, and the row under the canvas is removed. This supersedes the first revision's placement.
  - Size: "40 px, iki satır". The Level Editor's swatches become 40 px squares in two rows (6 and 5) at 330 px, replacing the preserved 24 px one-row layout. The Effect Editor keeps 24 px, through a size the Level Editor passes to `LevelColorSwatches` rather than an edit of `LevelEffectEditorWindow.cs`.
  - This revises the objective, the Badges, Swatch size and Buttons bullets, the preserved behaviour and AC-1. The evidence recorded before this revision is stale.
- 2026-09-26, while AC-8 awaited acceptance. The user sent the Colors group and wrote "değişikliğe gidelim shuffle yapsak da veya switch buradaki sayılar aynı kalabilir".
  - The swatches move to their order positions, so the grid's badges always read 1 to 11 and the colours change places after Shuffle, Switch or Reset. `[` / `]` step through that grid order. Each swatch keeps its colour's badge number, tooltip and click.
  - This revises the Badges bullet and AC-1's badge line. The order model, recolour rule, allowed paths and risk are unchanged.
- 2026-09-26, user order given in session "Shuffle ve switch değişiklikleri" and relayed by that session: "color switch sadece tıkladığımız parçanın rengini rastgele bir renk yapacak şekilde güncellensin".
  - Color Switch recolours only the chosen block (the block the Pen edits, or one clicked with Select) with a random gate colour other than its current one, as one "Switch Colour" undo step. The tool colour follows it. The colour order and every other block and gate stay unchanged. With no chosen block it changes nothing and hints "Click a block, then press Color Switch."
  - This supersedes the level-wide swap ("Rastgele renkle takas"). It revises the objective, the Color Switch bullet, AC-4 and the Color Switch tooltip. Shuffle, Reset Order and the order model are unchanged.
  - Consequence to show the user: a block recoloured alone may lose its matching gate. The canvas issues list and Save Level report that like any other recolour.
  - Implementer: session "Shuffle ve switch değişiklikleri". It updates `LevelDatabaseWindow.cs` (SwitchColor and its tooltip), `LevelColorOrderWindowTests.SwitchTradesTheToolColourWithAnotherEverywhere` and the switch step in `LevelColorKeysTests`, after session "Parça template boş kareler" releases `LevelDatabaseWindow.cs`.
  - Superseded before implementation by the fifth revision below.
- 2026-09-26, a further user order given in session "Shuffle ve switch değişiklikleri" and relayed by it: "color switch şuanda gate rengi değiştirmeye tıkladığım parçanın rengini değiştirmeli örnek olarak kırmızı bir parçaya tıklayıp switch yaparsam bütün kırmızıları değiştirmeli".
  - Source colour A is the clicked block's colour (Pen-edited or Select-chosen) or a Select-chosen gate's colour, not the tool colour.
  - B is a random gate colour other than A, preferring colours the level does not use, so only A visibly changes.
  - Every A and B reference in the level trade colours (blocks, gates, PaletteColor effect parameters), and A and B trade order positions, in one "Switch Colour" step. The tool colour becomes B. With nothing chosen: no change, no undo step, and the hint "Click a block, then press Color Switch."
  - This supersedes the fourth revision and keeps the level-wide swap of the original decision with a new source and target rule. It revises the objective, the Color Switch bullet, AC-4 and the tooltip. Because every block keeps matching its gate, the fourth revision's gate-mismatch consequence no longer applies. Implementer and files are as for the fourth revision.
- 2026-09-27, a user order given in session "Shuffle ve switch değişiklikleri" and relayed by it: "reset order kaldır ve shuffle renkleri editör panelinde değiştirmesin sadece board da renkleri değiştirse yeterli".
  - Reset Order is removed: its button and `ResetColorOrder`. AC-3 is retired. The `reset-order` icon, its `.meta` and `LevelEditorIcons.k_resetOrder` go too when nothing else uses them; the icon's docs README entry is updated to match.
  - Color Shuffle recolours only the level, with the same random permutation that changes at least one used colour, as one "Shuffle Colours" step. It no longer changes the draft's colour order, so the swatch grid, badges and Pen keys stay put. This revises the objective, the Color Shuffle and Buttons bullets, AC-1's button line and AC-2.
  - Color Switch is unchanged: it still trades A and B in the order, so the order changes only through Switch. With Reset Order gone, the order returns to palette order only through undo or a draft rebuild. The relaying session left this for the user to confirm.
  - Implementer: session "Shuffle ve switch değişiklikleri", queued behind TASK-20260926-204508638-a3ad6ee3-level-editor-architecture, which holds a lock on `LevelDatabaseWindow.cs` and `Level*Tests.cs`.
- 2026-09-27, the user's answer relayed by session "Shuffle ve switch değişiklikleri": "evet sadece board boyasın color switch de".
  - Color Switch recolours only the level: A (the clicked element's colour) and B (random, unused preferred) trade in the level as one "Switch Colour" step, and the tool colour becomes B. It no longer changes the draft's colour order.
  - With Reset gone and Shuffle level-only, nothing changes the order any more: it stays palette order, and the grid, badges and Pen keys never move. The order model and its draft field stay for now, and the user is told they are fixed; removing them is a separate cleanup. AC-5 (order kept through a reload) still holds, trivially.
  - This revises the objective, the Color Switch bullet and AC-4. Same implementer and queue as the sixth revision.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:ea470334269a28138acedc89e71c1b435b781a0f479fa70127f334219cec6f4c
- Evidence fingerprint: sha256:1c67707691c2bb16e28c991d543d351aeba86a4ae79950de5bf321b7e1e0959a
- Reviewed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079
- Attempt count: 1
- Last feedback: "colors karelerini büyült ve istediğim butonlar gelmemiş" (references/feedback-tools-column.png). Classified as a revision: buttons under the swatches in the Colors group, and 40 px swatches in two rows.
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079
- Closed at: 2026-09-27T17:24:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated (sixth revision): LevelColorOrderWindowTests.ActionRowSitsUnderTheSwatchesWithIconsNamesAndTooltips asserts exactly Color Switch and Color Shuffle; TheButtonsShareOneLineOfTheToolsPane (289/302); SwatchesShowTheirOrderNumbers; LevelColorSwatchesLayoutTests (Level suite job 135d995a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079 |
| AC-2 | PASS | automated (sixth revision): LevelColorOrderWindowTests.ShuffleRecoloursOnlyTheBoardOneToOneInOneUndoStep (order and swatches stay, one-to-one recolour, one undo) and LevelColorOrderTests (Level suite job 135d995a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079 |
| AC-3 | PASS | retired in the sixth revision; checked absent: no Reset Order button (the row test lists two buttons), and no ResetColorOrder, m_resetOrderButton, k_resetOrder or reset-order icon anywhere under Assets/ColorBlockJam (grep) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079 |
| AC-4 | PASS | automated (seventh revision): LevelColorOrderWindowTests Switch test (the clicked colour trades level-wide with an unused partner, the order stays, one undo) and LevelColorOrderTests.SwitchPartnerIsAnUnusedColourWhileOneExists (Level suite job 135d995a); a Select-chosen gate source still has no dedicated test | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079 |
| AC-5 | PASS | automated: the window's reload test, now AShuffledBoardSurvivesAKeptDraftReload (Level suite job 135d995a; earlier 6253b42f; earlier job 07ae440f) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079 |
| AC-6 | PASS | holds trivially: since TASK-20260926-170647550-7683988f (user request) the Effect Editor has no colour rows to badge, and LevelEffectEditorTests asserts the swatches are absent (job 079d3551) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079 |
| AC-7 | PASS | automated: Level suite 723/723 (job 135d995a; earlier 690/690, job 6253b42f) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:ed438d5337de4db66a530a7a7442d57bc42902b0e11baf65af44b3faf8cba079 |
| AC-8 | UNPROVEN | the user did not accept the first look; awaits acceptance of the revised look | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored 2026-09-26 by session "Edit dropdown ve colors kareleri"; queued behind TASK-20260926-140520462-2196c1d1 (Level Editor paths held by session "Rotate tool rotation behavior")
- Contract revision — 2026-09-26T15:05Z: the "Contract history" section records the user's decision to put the three buttons in an action row under the canvas (icon and name); fingerprint restamped from sha256:636f08a0bd45…; still ready and queued
- Started — implementation — 2026-09-26T16:06Z: session "Rotate tool rotation behavior" sent LEVEL EDITOR PATHS FREE and both of its lifecycle locks were gone at 16:00:19Z; auto-dispatched on the direct lane
- Verification — 2026-09-26T16:40Z: implementation complete; independent verifier CLEAN for AC-1 to AC-7. Its low-severity note (an order-only change leaves no unsaved change) is kept as intended, pinned by a test and documented. Focused fixtures 22/22 (job a6f69b83) and the Level suite 613/613 (job ab0098e9) passed at the reviewed revision. AC-8 awaits the user's acceptance (ACTION_REQUIRED: USER_ACCEPTANCE).
- Contract revision — 2026-09-26T16:51Z: the user's feedback was routed through task-bug as CONTRACT_CHANGE_REQUIRED (no defect). Buttons move under the Colors swatches, and the swatches become 40 px in two rows. Fingerprint restamped from sha256:2782ccbef222…; all evidence stale; queued behind the Rotate session's pen-edit-templates lock.
- Partial implementation — 2026-09-26T17:40Z: the Rotate session cleared `LevelColorSwatches.cs` and the colour test files for this session. The swatches are now 40 px with 4 px gaps (6 + 5 rows at 289 and 302 px), with larger badges. Since TASK-20260926-170647550-7683988f the Effect Editor shows no swatches, so the size is the class constant rather than a size the Level Editor passes; `LevelEffectEditorWindow.cs` is unchanged by this task. `LevelColorSwatchesLayoutTests` was rewritten; 13/13 focused tests (job df688d51) and 40/40 swatch-clicking/shell tests (job a783c385) passed. The action row move is still queued.
- Verification — 2026-09-26T18:35Z: session "Rotate tool rotation behavior" sent LEVEL EDITOR PATHS FREE. The action row moved into the Colors group under the swatches. The buttons share the row with 11 px names and tight padding, so they stay on one line at 289 and 302 px and wrap in a narrower pane. The hidden Piece Type comment was updated, and the design doc describes the second revision. An independent verifier returned CLEAN. Focused 37/37 (Level suite job 6253b42f; earlier job 07ae440f) and the Level suite 627/627 (job e7b0c7b6) passed at the reviewed revision, and the live window kept the user's draft. AC-8 awaits the user's acceptance (ACTION_REQUIRED: USER_ACCEPTANCE).
- Evidence refresh — 2026-09-26T19:10Z: TASK-20260926-180414067-b923c8a9 (Pen colour keys) changed three owned files: `LevelDatabaseWindow.cs` (Pen and Colors tooltips, the key branch), `LevelColorSwatches.cs` (swatch tooltips naming the Pen key) and the design doc. This task's behaviour is unchanged. Its fixtures passed in the Level suite 650/650 (job 12fdf01b) at the new digest, so the reviewed revision and freshness moved to it; the jobs named in the rows stay the earlier evidence.
- Contract revision implemented — 2026-09-26: swatches reorder to their order positions (`LevelColorSwatches.SetOrder`), and `[` / `]` follow the order (`LevelDatabaseWindow.CycleColor`). `LevelColorSwatchesLayoutTests` now checks rows and left-to-right positions in order, and `LevelColorKeysTests.AfterAShuffleTheSwatchesSitInOrderAndTheBracketsFollowIt` is new. Clean compile with 0 Console errors; the colour, palette, block tools and Pen edit fixtures passed 96/96 (job 7af2f28c). The full Level suite was not re-run. AC-8 still awaits the user's acceptance of the new look.
- Contract revisions recorded — 2026-09-26T19:35Z, under this session's lock, by the owning session. The third revision's Contract history entry had been written by session "Shuffle ve switch değişiklikleri" without a fingerprint restamp. The fourth revision (Color Switch recolours only the chosen block) was relayed by the same session from its user. Both are now in the authority, and the fingerprint is restamped from sha256:e28f3dfd044c…. Since the reviewed revision, owned files have changed (the third revision, the tool-hint box and the Pen colour keys), so AC-1 to AC-7 are STALE until they are rerun. That session's suite run 646/649 (job f6be7548) failed only in LevelCanvasCornerVoidsTests, from the staircase-corners work in progress under another lock.
- Contract revision recorded — 2026-09-26T19:45Z: a fifth revision, relayed by session "Shuffle ve switch değişiklikleri" from its user, replaces the unimplemented fourth one. Color Switch takes the clicked block's or gate's colour and swaps it level-wide with a random colour, preferring one the level does not use. Fingerprint restamped from sha256:f006974c8e6d….
- Evidence refreshed — 2026-09-26T20:44Z: session "Shuffle ve switch değişiklikleri" implemented the fifth revision (TryGetChosenColor, LevelColorRemap.PickSwitchPartner, the tool hint and the tooltip, with tests and docs) and sent PATHS FREE. The owning session read the new switch tests against AC-4 and ran the Level suite: 690/690 (job 6253b42f), with no owned file changed after the run started. The reviewed revision moved to the new digest. From now on the digest leaves out the shared design doc, which several sessions edit. AC-1 to AC-7 pass at it. One gap remains: a Select-chosen gate as the Switch source has no dedicated test.
- Contract revision recorded — 2026-09-26T21:20Z: a sixth revision, relayed by session "Shuffle ve switch değişiklikleri" from its user's order of 2026-09-27: Reset Order removed, and Shuffle recolours only the board. Fingerprint restamped from sha256:69ebee730e06…. AC-1 to AC-3 are STALE until the change lands. It is queued behind TASK-20260926-204508638-a3ad6ee3-level-editor-architecture (lock owner "/root task-cycle Level Editor architecture", held since 21:03Z).
- Contract revision recorded — 2026-09-26T21:58Z: a seventh revision, relayed by session "Shuffle ve switch değişiklikleri" from its user ("evet sadece board boyasın color switch de"). Color Switch recolours only the level, and nothing changes the order any more. Fingerprint restamped from sha256:a05e9af9e470…. AC-4 is STALE until the change lands.
- Evidence refreshed — 2026-09-26T22:50Z: session "Shuffle ve switch değişiklikleri" implemented the sixth and seventh revisions (Reset Order removed; RecolorLevel leaves the order; tooltips; icon, README and docs) and sent PATHS FREE. The owning session read the rewritten tests against AC-1 to AC-4 and grepped that Reset Order is gone. The Level suite passed 723/723 (job 135d995a), and no owned file changed after the run started (22:23:10Z). The reviewed revision moved to the new digest, which leaves out the retired reset-order icon.
- 2026-09-27T17:24:19Z Closed by explicit user confirmation (task-done, user_confirmed): the user accepted the live result on 2026-09-27 ('bunlar okay', item 2, the Colors group; AC-8) and ordered 'tasklari kapat izin veriyorum'. Criteria rows are preserved as recorded, not re-verified; owned files may have changed after the reviewed revision through other sessions.
<!-- TASK-LIFECYCLE:END -->
