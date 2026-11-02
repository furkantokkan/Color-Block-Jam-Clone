# Ice checkin scope review

The user authorized a checkin after Ice verification on 2026-09-27. No checkin has been performed.

The user's later paired-image reference reopened the visual refinement. The verification described at the end of this note is historical; the dense cyan shader and centered outlined numeral require fresh verification before any Ice checkin.

- Current Plastic workspace: `/main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity`, changeset 13 (rechecked during the new visual correction).
- The earlier full inventory contained approximately 2,095 pending paths. That count predates changeset 13. Do not check in the workspace root or use a directory-wide private-file add.
- Fifteen of the 36 saved pre-Ice file snapshots already differed from changeset 11. An Ice-owned path is therefore not necessarily an Ice-only change.

| Shared path or group | Other pending work included by a whole-file checkin |
| --- | --- |
| `Gameplay/Board/BoardPresenter.cs` | ExitFragments integration, requiring pending `ExitFragmentPlacement`, `ExitFragments`, `ExitFragmentEmitter` and `ExitFragmentSettings` sources. The fragment task still awaits visual acceptance. |
| `Gameplay/Composition/GameAttemptLoader.cs` | Pending Test Level selection integration; its `Runtime/Levels/LevelTestPlayRequest.cs` prerequisite was added in changeset 13. |
| `Runtime/Levels/LevelEffectRegistry.cs` | Separate solid-block registration removal. |
| Canvas, preview and `Scenes/Game.unity` | Changeset 13 includes broad canvas/preview and LevelEditor scene updates, including IceCanvasEffectLayer and IcePreviewEffect. Game scene composition remains pending; re-inventory remaining exact files before any checkin. |

Paths in the table are relative to `Assets/ColorBlockJam/`. The native `cm checkin` command accepts exact file paths and checks in whole file contents; directory inputs recurse. An incomplete Ice-only subset would omit required integration and is not a valid feature checkin. The baseline's 15 differences are attribution evidence, not an automatically approved checkin list; no protected board-data or LevelEditor scene change is selected merely because it was already modified.

Preferred sequence: let the prerequisite owners check in their reviewed changes, re-read the head and pending diffs, then check in the remaining exact Ice files and metadata. A combined changeset requires explicit scope authorization for those prerequisite changes. Never temporarily revert another owner's work to stage a checkin in the live Unity workspace.

## Renderer refinement checkpoint

The latest user request is a bounded visual correction. A separate source/VCS reviewer rechecked changeset 13 and confirmed this exact six-file subset contains only Ice rendering changes and has committed compile-time dependencies:

- `Assets/ColorBlockJam/Editor/BoardKit/BoardIceAssets.cs`
- `Assets/ColorBlockJam/Presentation/Board/BlockIceOverlay.cs`
- `Assets/ColorBlockJam/Presentation/Board/IceCounterLabels.cs`
- `Assets/ColorBlockJam/Tests/PlayMode/BlockIcePresentationTests.cs`
- `Assets/ColorBlockJam/Art/Shaders/Ice.shadergraph`
- `Assets/ColorBlockJam/Art/Materials/Effects/MAT_Ice.mat`

All six matching metas are clean. The existing texture, board catalog and panel settings are committed dependencies. This rendering checkpoint is covered by the user's Ice commit instruction and can be checked in after the final visual and focused-test evidence passes. It does not publish the mixed gameplay integration in the table above. Do not include the new IceAuthoringTests file, whose broader runtime case still depends on pending integration.

Only the narrow `RebuildIceMaterial` method was used for calibration. The separate `CreateApprovedAssets` helper also writes panel/import/scene data and expects pending installer fields; it must not be invoked as part of this checkpoint. Independent final hashes match root's handoff. Final tests and the exact checkin result are still pending.

Read-only inspection used `cm status`, `cm help checkin`, provider history and `plastic.exe diff`. No checkout, revert, update, add or checkin was executed.

The earlier [reference-verification.md](reference-verification.md) checkpoint passed 24 unique focused checks but is superseded for visual acceptance by the latest user comparison. Changeset 12 contained only the Onity package update. A fresh changeset 12-to-13 read lists 262 paths, including broad editor/canvas/preview updates and LevelTestPlayRequest. Unfiltered exact-path `cm status` still reports BoardPresenter, GameAttemptLoader and LevelEffectRegistry as Changed. A prior filtered query returned no paths and was insufficient to establish cleanliness; its clean interpretation is discarded. Fragment sources and Game scene work also remain pending. The user's Ice checkin authorization remains valid, but the earlier combined-scope question has no answer. No permission is inferred from continued visual repair requests. Recheck the head and exact pending inputs after the current visual work is verified.
