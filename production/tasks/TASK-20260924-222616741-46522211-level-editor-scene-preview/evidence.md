# Independent verification: LevelEditor scene preview

- Date: 2026-09-25, 02:03 to 02:23 (Editor local time).
- Transport: MCP for Unity over HTTP at 127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`.
- Verifier role: I only verified. No production code or test changed, no scene was saved and no VCS command ran.
- Starting setup: only `LevelEditor.unity` was open, active and clean, with the Level Editor window open on Level 1. The brief described a different setup (Game open). The Editor was restored to the setup that was actually found.
- Concurrent activity: another session recompiled `Editor/BoardKit/PieceEditorWindow.cs` and ran `ShapePrefabTests` during this run. That caused two domain reloads and one "Could not restore piece JSON … PrefabTests_…" error. None of these came from this task.

## Hashes (sha256)

| File | Before | After |
|---|---|---|
| `Assets/ColorBlockJam/Scenes/LevelEditor.unity` | `3e743628…502636902de9` | same |
| `Assets/ColorBlockJam/Data/Levels/LevelDatabase.json` | `02c013a5…8f2ad2cf1208` | same |
| `Assets/ColorBlockJam/Scenes/Game.unity` | `78fef9ae…68922181a238276c7` | same |

## Results

| AC | Result | Evidence |
|---|---|---|
| AC-1 | PASS | The Target Level `DropdownField` value was set in the window, so the real hook fired. The host shows a 4x5 layout with 6 door slots and 56 active environment pieces: 20 `Grid`, 24 `Wall`, 4 `Corner_1`, 6 `Door_1x1`, 2 `Door_Arrow`. There are two `Square` blocks: LightBlue with its origin at cell (2,2) and Pink at (0,1). Each block root sits on a `Grid` floor tile at the same board-local x/z, and `TryLocalToCell` returns the authored cells. Of 345 host objects, 0 lack `DontSave`. The Scene view pivot (0,0.36,0) equals the preview bounds centre. Capture: `level1-preview.png`. |
| AC-2 | PASS | Cycling 1 → 3 → 1 → 3 → 1 → 3 → 1 through the dropdown grows the host once to the union of both levels (669 objects) and then holds it at 669. Level 3 shows 5x6, 8 door slots, LTypeB1/LightBlue at (1,2), DoubleHorizontal/Red at (2,2) and LTypeB1/Yellow at (2,1). No new Console errors came from the preview. |
| AC-3 | **FAIL** (partial) | See the finding below. What passed: changing the Piece Type dropdown (Square → Single) updated the preview on the next editor tick (b1 became Single, 4 renderers). A draft with width 0 kept the last valid preview and logged exactly one warning ("…board size outside 3-8 by 3-10") with no exception. Publishing the same invalid draft again added no second warning. A valid draft after that rebuilt the preview. The draft was then discarded through the window's own `LoadSelectedDraft` (draft clean), and the level JSON hash is unchanged. |
| AC-4 | PASS | The scene file was never written. With the preview shown (384 objects), `EditorSceneManager.SaveScene(levelEditor, "Temp/VerifierLevelEditorCopy.unity", saveAsCopy: true)` produced bytes identical to `LevelEditor.unity` (same sha256, `cmp` identical). The temporary copy was deleted afterwards. The scene stayed clean throughout, and every preview object has `DontSave` (0 missing on every check). |
| AC-5 | PASS | **Null publish.** Invoking `DraftLevelPublished(null)` removed the preview completely. Before, non-persistent objects were: preview root 1, `BlockPiece` 3, `EnvironmentView` 1, `BoardPieceView` 154. After, all four counts were 0. **Scene close.** `CloseScene(LevelEditor, removeScene: true)` with Game loaded left 0 preview roots and 0 `BlockPiece`. The only remaining `EnvironmentView` and `BoardPieceView` (one each) belong to the Game scene's own content. The listener was off. **Domain reload.** Two domain reloads (triggered by the concurrent session) each rebuilt exactly one preview root with fresh pools (345 objects). **Editor quit.** Not exercised; the code path was reviewed only. |
| AC-6 | PASS | With only Game loaded (1 root, 9 objects, clean), selecting Level 1 in the window published `level-01`, but Game kept 1 root and 9 objects, stayed clean, and no preview root was created. Reopening LevelEditor additively showed the last published level (level-01) straight away. |
| AC-7 | PASS | `ColorBlockJam.Editor.LevelPreview.Tests`, EditMode: 9/9 passed (job `230d55e67be14377872ff39cdeb0b53d`, 0.30 s). |

## Finding (AC-3): Width, Height, Timer and Name edits do not publish

The window's publish hook listens for `SerializedPropertyChangeEvent` on `m_draftFields`. The Width, Height, Timer Seconds and Level Name fields are plain `IntegerField` and `TextField` controls bound through `bindingPath`, not `PropertyField`s. In Unity 2022.3 a plain bound field writes the SerializedObject and raises `ChangeEvent<T>`, but it does not raise `SerializedPropertyChangeEvent`.

Live probe on the window while its binding was active:

- Temporary callbacks were registered on `m_draftFields`, then Width was set to 6 through the field.
- Results: `ChangeEvent<int>` count 1, `SerializedPropertyChangeEvent` count 0, SerializedObject width 6, `m_publishScheduled` false.
- Even after a forced panel update (`UpdateWithoutRepaint`) and repaints, nothing was published, and the preview kept width 4.
- The window's own dirty status also stayed at "Editing Level 1" although `HasDraftChanges()` was true. That confirms the callback never ran.

The contract names "width" as an example that must update the preview. Doors and Blocks are `PropertyField`s, so they should raise the event, but I could not drive them live: the ListView items were not realised while the Editor was unfocused.

Suggested fix, inside the allowed window hook lines: also schedule the publish when any bound value changes. Either track the whole SerializedObject with `m_draftFields.TrackSerializedObjectValue(m_draftObject, _ => SchedulePublish())`, re-registered in `LoadSelectedDraft`, or register `ChangeEvent<int>` and `ChangeEvent<string>` on `m_draftFields`.

## Observations (not scored)

- Right after the first concurrent domain reload, the Console showed three Inspector exceptions: `GameObjectInspector.OnEnable` ArgumentNullException, `TransformInspector.OnEnable` SerializedObjectNotCreatable, and `GameObjectInspector.OnDisable` NullReference. They are consistent with an Inspector pointing at a destroyed object. Selection was null when checked afterwards, so I could not attribute them to the preview.
- After a domain reload, the recreated window's plain Width field showed 0 while the draft held 4. Its binding had not synced while the Editor was unfocused. This is a window-side issue and does not affect the preview.
- Editor ticks (`delayCall`) are throttled while the Editor is unfocused. Publishes took about 8–50 s to run in this session. This comes from the environment, not from the code.

## D-001 fix and re-verification (2026-09-25)

See `defects/D-001.md`. After the fix:
- Live check: a Width field edit rebuilt the preview at 6x5; reverting to 4 gave 4x5 and a clean draft.
- Tests: LevelPreview 9/9 (job 6b9e90a9…), window and database regression 18/18 (job 1aabe679…).
- `LevelEditor.unity` sha unchanged.
