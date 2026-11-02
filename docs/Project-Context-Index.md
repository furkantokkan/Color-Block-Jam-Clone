# Project Context Index

Source-grounded navigation for architecture questions and technical interview preparation. Reviewed on **2026-10-01** against the current Plastic SCM workspace at **changeset 42**, with local changes present. This document maps implemented behavior, design intent, tradeoffs and verification limits; it is not a release verdict or a replacement for the product rules.

## Navigation and evidence

- [Project and scope](#project-and-scope)
- [Coverage and entry points](#coverage-and-entry-points)
- [Assembly boundaries](#assembly-boundaries)
- [Lifetimes and scene flow](#lifetimes-and-scene-flow)
- [Gameplay, input and commands](#gameplay-input-and-commands)
- [Boosters and effect extension](#boosters-and-effect-extension)
- [Data authority and content loading](#data-authority-and-content-loading)
- [Persistence, economy and lives](#persistence-economy-and-lives)
- [Editor and solver](#editor-and-solver)
- [UI, rendering, art and haptics](#ui-rendering-art-and-haptics)
- [Tests and verification limits](#tests-and-verification-limits)
- [Strengths, tradeoffs and known gaps](#strengths-tradeoffs-and-known-gaps)
- [Interview question navigation](#interview-question-navigation)
- [Refresh procedure](#refresh-procedure)

Evidence labels used below:

| Label | Meaning |
| --- | --- |
| **S — source** | The current source, assembly definition or serialized file supports the statement. This does not establish a fresh runtime pass. |
| **H — historical** | A dated document or task records execution or a decision. Its revision, platform and scope bound the claim. |
| **I — interpretation** | An architectural consequence inferred from the implementation, rather than a reproduced defect. |
| **U — unverified** | A relevant runtime, device, profiler or failure-injection check was not executed in this review. |

When answering a question, read the linked implementation and its direct collaborators first, then the relevant design and tests. The [case brief](Game-Developer-Case-2026.md) defines requested scope, [PROJECT.md](../PROJECT.md) defines project rules and the decision log, and [AGENTS.md](../AGENTS.md) defines tooling and ownership. The current source establishes implemented behavior; disagreements with those authorities are recorded explicitly below. Tracked [task contracts](../production/tasks) retain lifecycle and closure authority.

The coverage combines a project-wide authored C# inventory and assembly scan with deeper inspection of critical flows and their collaborators. It includes all owned runtime and Editor areas, test boundaries, authored data, UI, rendering, native haptics and build integration. **It does not mean every source line, every test case, every asset's pixels or every vendor implementation was audited.** Vendor packages, generated `Library`/`Temp` files and submission copies under `output` are outside authored-code counts.

### Review snapshot

- Canonical root: `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026`; Unity Version Control/Plastic SCM, not Git.
- CLI `1.0.0-beta.11`; file-pinned Editor `2022.3.62f2 (7670c08855a9)`.
- CLI discovery reported this exact project path running without Pipeline. Unity 2022 uses the repository-approved MCP for Unity route for live control. MCP discovery exposed its name, complete version and instance/session ID, but did not expose an instance-bound absolute path; strict live identity selection/readback was not completed. No live Editor state is claimed here.
- Pre-existing dirty files: `ProjectSettings/EntitiesClientSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset`; deleted `Assets/AddressableAssetsData/link.xml` and its `.meta`; private submission output under `output/rollic-submission-2026-09-30`. These were preserved.
- A reproducible partial source/settings fingerprint covers **937 files**, with SHA-256 `17f892e2b7040f7ecefe2f39f33b97c3ba717c1f66e17784526960235cee1734`. Inputs: `.cs`, `.asmdef`, `.json`, `.xml`, `.asset`, `.txt` under `Assets/ColorBlockJam/{Runtime,Editor,Tests}`, `Assets/Haptics/Runtime`, `Assets/ColorBlockJam/Data/Json`, and `ProjectSettings`, plus `Packages/manifest.json` and `Packages/packages-lock.json`. Sort by repository-relative POSIX path; hash each `path UTF-8 + NUL + binary SHA256(file bytes) + LF` into the aggregate. This excludes most serialized art, scenes, prefabs and Addressables settings and is not a complete project fingerprint.
- This documentation request owns only this Markdown file. No source, tests, assets, settings, packages, tracked-task state or VCS content was changed; no build, compile, Unity test run or profiler capture was performed.

## Project and scope

The project is a portrait Android/iOS Color Block Jam vertical slice with a designer-facing level editor. The case fixes Unity 2022.3.62f2 and a 1080 × 1920 reference resolution, with 20:9 device layout expectations. Its evaluation order is architecture, editor usability, code quality, correctness, performance, then scope control. Rabbyte Games branding is reserved for PC games and does not apply here.

The required slice includes Home, settings toggle states, the first five levels, timer, coins, fail/restart/Home/next-level flow, and visual level authoring in the format the game reads. Other Hub sections are placeholders; functional boosters, Ice, Arrow and solvability validation are optional in the brief. The current [Main manifest](../Assets/ColorBlockJam/Data/Json/Levels/Main/manifest.json) lists six levels. Functional boosters, lives/refills, save recovery, native haptics, preloading and a solver extend the minimal brief. Their presence is useful interview material, but raises scope and maintenance questions.

| Stack | Current source authority | Role |
| --- | --- | --- |
| Unity 2022.3.62f2 | [ProjectVersion](../ProjectSettings/ProjectVersion.txt) | Case constraint; no Editor upgrade assumed. |
| Onity 0.4.0 | [manifest](../Packages/manifest.json), [lock](../Packages/packages-lock.json) | Containers, lifetimes, reactive properties, events, async helpers, pooling/factories. |
| PrimeTween 1.4.11 | Same package files | Owned presentation tweens; pause/stop by target. |
| Input System 1.19.0 | Package lock; [pointer input](../Assets/ColorBlockJam/Runtime/Gameplay/Input/GameplayPointerInput.cs) | New Input System only; transitive package dependency. |
| UI Toolkit | [UI assets](../Assets/ColorBlockJam/UI), [UI authoring](UI-Authoring.md) | Runtime screens and Editor tools. Installed UGUI/TMP packages do not imply a UGUI/TMP screen architecture. |
| Addressables 1.22.3 | Package files; [level loader](../Assets/ColorBlockJam/Runtime/Core/Levels/LevelCatalogLoader.cs) | Authored manifest/pack delivery and handle ownership. |
| URP 14.0.12 | Package files; [settings](../Assets/ColorBlockJam/Settings) | Board rendering and mobile rendering configuration. |
| Newtonsoft Json.NET 3.2.2 | Package files; [level JSON](../Assets/ColorBlockJam/Runtime/Core/Levels/LevelDatabaseJson.cs), [save JSON](../Assets/ColorBlockJam/Runtime/Player/Storage/PlayerSnapshotJson.cs) | Explicit data boundaries, validation and DTO serialization. |
| ZString 2.6.0 | Package files; [HUD text](../Assets/ColorBlockJam/Runtime/Gameplay/UI/HudText.cs) | Dynamic text without intermediate formatting allocations where applicable. |

For opening/running and tool usage, start with [README](../README.md). For intent beyond the case, use [PRD](Color-Block-Jam-PRD.md), [mechanics guide](Color-Block-Jam-Mechanics-Guide.md), [mechanics catalog](Color-Block-Jam-PRD-Mechanics-Catalog.md) and [reference levels](Reference-Levels.md). Cataloged mechanics are not automatically implemented.

## Coverage and entry points

Counts are C# source files, including fixtures and helpers, not executable test-case counts. The game root contains **401 runtime, 162 Editor and 306 test files: 869 total**. The reusable native-haptics library adds five runtime C# files outside the game root.

| Owned area | Files | Responsibility and first source to open |
| --- | ---: | --- |
| Runtime/Core | 64 | Composition, startup, scene flow, authored contracts/catalogs, layout/art/currency contracts. [ProjectScopeInstaller](../Assets/ColorBlockJam/Runtime/Core/Composition/ProjectScopeInstaller.cs), [StartupLoader](../Assets/ColorBlockJam/Runtime/Core/Startup/StartupLoader.cs). |
| Runtime/Domain | 77 | Engine-independent board, movement, gates, commands, hooks, picking and session rules. [BoardSimulation](../Assets/ColorBlockJam/Runtime/Domain/Session/BoardSimulation.cs), [BoardState](../Assets/ColorBlockJam/Runtime/Domain/Board/BoardState.cs). |
| Runtime/Gameplay | 129 | Game composition, input, attempt/session orchestration, effects, boosters, board adapter and HUD/menu/result flows. [GameSceneInstaller](../Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameSceneInstaller.cs), [LevelSession](../Assets/ColorBlockJam/Runtime/Gameplay/Session/LevelSession.cs). |
| Runtime/Player | 35 | Save transactions, storage, progress, wallet/settings/inventory ports and lives. [PlayerDataInstaller](../Assets/ColorBlockJam/Runtime/Player/PlayerDataInstaller.cs), [PlayerSaveService](../Assets/ColorBlockJam/Runtime/Player/PlayerSaveService.cs). |
| Runtime/Hub | 36 | Home, tabs, overlays, pre-level selection, launch and life refill. [HubSceneInstaller](../Assets/ColorBlockJam/Runtime/Hub/Composition/HubSceneInstaller.cs), [HubScreen](../Assets/ColorBlockJam/Runtime/Hub/Composition/HubScreen.cs). |
| Runtime/UI | 8 | Shared purchase and settings presenters/views plus button haptics. [BoosterPurchasePresenter](../Assets/ColorBlockJam/Runtime/UI/Purchase/BoosterPurchasePresenter.cs), [SettingsPopupPresenter](../Assets/ColorBlockJam/Runtime/UI/Settings/SettingsPopupPresenter.cs). |
| Runtime/Presentation | 32 | Modular piece/tile pools, visual effects, markers/ice, safe areas and custom visual elements. [BlockPieceFactory](../Assets/ColorBlockJam/Runtime/Presentation/Board/BlockPieceFactory.cs), [ModularBlockView](../Assets/ColorBlockJam/Runtime/Presentation/Board/ModularBlockView.cs). |
| Runtime/Environment | 15 | Floor/wall/door composition, ordered quadrant rules, pooled view and camera framing. [EnvironmentComposer](../Assets/ColorBlockJam/Runtime/Environment/EnvironmentComposer.cs), [EnvironmentRuleMatcher](../Assets/ColorBlockJam/Runtime/Environment/EnvironmentRuleMatcher.cs). |
| Runtime/Haptics | 5 | Game feedback mapping and committed-setting gate. [HapticsService](../Assets/ColorBlockJam/Runtime/Haptics/HapticsService.cs), [HapticStyles](../Assets/ColorBlockJam/Runtime/Haptics/HapticStyles.cs). |
| Assets/Haptics/Runtime | 5 | Reusable Android JNI/iOS native device output. [DeviceHapticOutput](../Assets/Haptics/Runtime/DeviceHapticOutput.cs), [AndroidVibratorHaptics](../Assets/Haptics/Runtime/AndroidVibratorHaptics.cs), [IosFeedbackHaptics](../Assets/Haptics/Runtime/IosFeedbackHaptics.cs). |
| Editor/Core | 122 | Level/Piece/Effect tools, drafts, canvas, save/backups, Addressables tooling and build filtering. [LevelDatabaseWindow](../Assets/ColorBlockJam/Editor/Core/Levels/LevelDatabaseWindow.cs), [PieceEditorWindow](../Assets/ColorBlockJam/Editor/Core/BoardKit/PieceEditorWindow.cs). |
| Editor/Environment | 3 | Rule-set inspector and solid asset tooling. [EnvironmentRuleSetEditor](../Assets/ColorBlockJam/Editor/Environment/EnvironmentRuleSetEditor.cs). |
| Editor/Hub | 2 | Navigation catalog and tab drawers. [HubNavigationCatalogEditor](../Assets/ColorBlockJam/Editor/Hub/HubNavigationCatalogEditor.cs). |
| Editor/LevelPreview | 9 | Editor-only scene preview and its pooled lifetime. [LevelPreviewOwner](../Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewOwner.cs), [LevelPreviewHost](../Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs). |
| Editor/Validation | 26 | Solver model, domain parity, search, budgets, verdicts and solution frames/listing. [LevelSolvabilityCheck](../Assets/ColorBlockJam/Editor/Validation/LevelSolvabilityCheck.cs). |
| Tests | 306 | Editor 100, Environment 14, Gameplay 129, Hub 19, LevelPreview 5, Player 10, Presentation 7, UI 9, Validation 13. [Test root](../Assets/ColorBlockJam/Tests). |

[Folder Layout](Folder-Layout.md) explains the 2026-09-28 move and old-to-current path translation. Its overview omits some later Haptics/Hub Editor additions; the table above uses the current inventory. Historical review documents also contain old paths and counts.

## Assembly boundaries

The current asmdefs form separate compilation boundaries, rather than merely naming folders:

| Assembly | Dependency direction and role |
| --- | --- |
| [ColorBlockJam.Domain](../Assets/ColorBlockJam/Runtime/Domain/ColorBlockJam.Domain.asmdef) | `noEngineReferences: true`, no explicit assembly references. Pure board/gameplay rules; no Unity or Onity dependency. |
| [ColorBlockJam.Runtime](../Assets/ColorBlockJam/Runtime/Core/ColorBlockJam.Runtime.asmdef) | Core contracts/composition depend on Onity, PrimeTween and Addressables; references neither Domain nor Gameplay. |
| [ColorBlockJam.Presentation](../Assets/ColorBlockJam/Runtime/Presentation/ColorBlockJam.Presentation.asmdef) | References Runtime and visual/pooling packages; no Domain reference. Gameplay bridges domain readers to view contracts. |
| [ColorBlockJam.Environment](../Assets/ColorBlockJam/Runtime/Environment/ColorBlockJam.Environment.asmdef) | References Runtime and Presentation; composes board surroundings. |
| [ColorBlockJam.Player](../Assets/ColorBlockJam/Runtime/Player/ColorBlockJam.Player.asmdef) | References Runtime, Haptics/game adapter and Onity; owns persistent player policy. It is not the engine-free Domain assembly. |
| [ColorBlockJam.UI](../Assets/ColorBlockJam/Runtime/UI/ColorBlockJam.UI.asmdef) | References Runtime, Player and game Haptics; shared presenters know ports/views rather than scene navigation. |
| [ColorBlockJam.Hub](../Assets/ColorBlockJam/Runtime/Hub/ColorBlockJam.Hub.asmdef) | Composes Runtime, Player, UI, Presentation and Haptics; no Gameplay/Domain dependency. |
| [ColorBlockJam.Gameplay](../Assets/ColorBlockJam/Runtime/Gameplay/ColorBlockJam.Gameplay.asmdef) | Integrates Domain, Runtime, Player, UI, Environment and Presentation plus Input System and Highlight Plus. This is an integration boundary with intentionally broad references. |
| [ColorBlockJam.Haptics](../Assets/ColorBlockJam/Runtime/Haptics/ColorBlockJam.Haptics.asmdef) / [Haptics](../Assets/Haptics/Runtime/Haptics.asmdef) | Adapter references Onity.Reactive and reusable Haptics. Reusable library has no game or Onity reference, but uses Unity platform APIs. |
| Editor and tests | Five Editor asmdefs restrict `includePlatforms` to Editor. Test asmdefs explicitly reference their subjects and do not become runtime dependencies. |

`Core` folder and `ColorBlockJam.Runtime`/`ColorBlockJam.Editor` assembly names intentionally differ. Historical names survive for serialized type identity; do not propose a cosmetic rename without checking `[SerializeReference]` and migration requirements.

SOLID discussion should stay concrete: narrow player ports support ISP/DIP; pure rules and shared solver predicates support testability; composition roots bind implementations; effect hooks provide extension seams. Those strengths do not make every coordinator small or every new mechanic a one-file change. See [SOLID refactor](SOLID-Refactor-2026-09-30.md) and [older architecture review](Architecture-Review-2026-09-26.md), with their dates and limits.

## Lifetimes and scene flow

| Owner | What it owns | Stop point / boundary |
| --- | --- | --- |
| Persistent ProjectContext | Catalog, player save/economy, lives, haptics, fade/readiness and one preloaded Game scene | Application scope disposal. [ProjectScopeInstaller](../Assets/ColorBlockJam/Runtime/Core/Composition/ProjectScopeInstaller.cs), [PlayerDataInstaller](../Assets/ColorBlockJam/Runtime/Player/PlayerDataInstaller.cs). |
| Hub SceneContext | Home presenters, overlays, navigation and launch wait | Hub unload; [HubSceneInstaller](../Assets/ColorBlockJam/Runtime/Hub/Composition/HubSceneInstaller.cs). |
| Game SceneContext | Session service, input, board adapter, booster coordinator and HUD/result/menu services | Game unload; reverse registration/disposal ordering matters in [GameSceneInstaller](../Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameSceneInstaller.cs). |
| Level attempt | Mutable simulation, generation, cancellation source and disposables | Session teardown on end/Home/restart; [LevelAttempt](../Assets/ColorBlockJam/Runtime/Gameplay/Session/LevelAttempt.cs), [AttemptLifetime](../Assets/ColorBlockJam/Runtime/Gameplay/Session/AttemptLifetime.cs). It is manually owned, not another DI scope. |
| Bound view / presenter | Callbacks, subscriptions, local tweens, safe-area hooks and async result generation | Unbind/OnDisable/Dispose; concrete hosts determine the boundary. |
| Save service | Serialized write queue and a started storage transaction | App lifetime, not the calling scene's lifetime. Leaving a view can cancel its wait without undoing a started write. |
| Editor window / preview / validation job | Draft, Undo state, preview pool and copied solver work | Window disable, draft invalidation, job cancellation, domain reload or Test Level round trip. |

The single app EventSystem lives under [ProjectContext.prefab](../Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab), with InputSystemUIInputModule. Its persistence prevents UI Toolkit handlers from being destroyed with the scene whose EventSystem first acquired them. Its empty action fields intentionally generate module defaults. The Resources bootstrap is a fixed Onity loader exception, not a general content-loading pattern.

Runtime scenes are [Init](../Assets/ColorBlockJam/Scenes/Init.unity), [Hub](../Assets/ColorBlockJam/Scenes/Hub.unity) and [Game](../Assets/ColorBlockJam/Scenes/Game.unity). [LevelEditor](../Assets/ColorBlockJam/Scenes/LevelEditor.unity) is authoring-only and excluded from player navigation/builds.

1. Onity creates the persistent app context. The project callback checks content updates and loads the manifest; the player callback loads player data.
2. Init shows the only loading screen, builds Hub additively, waits for its context and initial layout, then unloads behind the app fade.
3. Hub prepares the offered level in a hidden Game scene. [LevelPreloader](../Assets/ColorBlockJam/Runtime/Core/SceneFlow/LevelPreloader.cs) keeps one prepared level and chains preparation operations under its own token. Caller cancellation ends the caller's wait.
4. Play or Continue can reveal an already prepared scene, hide the previous one in the same frame, then unload it. If preparation is unavailable or fails, navigation uses [SceneFadeTransition](../Assets/ColorBlockJam/Runtime/Core/SceneFlow/SceneFadeTransition.cs).
5. Restart rebuilds an attempt in place; result flow preloads the next level behind the popup. Direct Play of Hub/Game remains a supported development entry point.

**Tradeoff (I):** preloading moves pack parsing, board construction and pool warmup out of the visible start transition, at the cost of retaining a second scene and its prepared resources. It does not remove that work or prove a memory/latency target on every device.

**Current gap (S/I):** [SceneRevealPolicy](../Assets/ColorBlockJam/Runtime/Core/SceneFlow/SceneRevealPolicy.cs) is used by [ActiveSceneReadiness](../Assets/ColorBlockJam/Runtime/Core/SceneFlow/ActiveSceneReadiness.cs), but returns only `isSceneReady`; its frame/time inputs are unused. Completed, faulted or destroyed ReadyTasks can reveal; an indefinitely pending build has no policy deadline and waits until completion or transition-owner cancellation. XML comments promise settled frames and avoidance of indefinite black, which the current placeholder does not implement. No hung-scene scenario was reproduced here.

## Gameplay, input and commands

### One session authority

[SessionStateMachine](../Assets/ColorBlockJam/Runtime/Domain/Session/SessionStateMachine.cs) and [SessionTransitions](../Assets/ColorBlockJam/Runtime/Domain/Session/SessionTransitions.cs) define `Home`, `Playing`, `Paused`, `Won`, `Failed`. Saving, Purchase, Background and User are pause reasons, not extra states. Finish is presentation, not a sixth state.

[GameplayTickOwner](../Assets/ColorBlockJam/Runtime/Gameplay/Session/GameplayTickOwner.cs) is the one game `IOnityTickable`; it forwards `Time.deltaTime` only in Playing. This is a frame-driven ordered simulation, not a fixed-step lockstep engine. UI schedules, startup waits and terminal visual deadlines have their own non-gameplay lifetimes; “one gameplay tick” does not mean the entire application has no other timed work.

[LevelSession.RunTick](../Assets/ColorBlockJam/Runtime/Gameplay/Session/LevelSession.cs) orders the phases:

1. Advance command stamps and clear the per-tick event buffer.
2. Apply a committed in-game booster or starting-booster plan, then an interrupted move, pointer input and an attached command source.
3. Return immediately if a phase leaves Playing. Check board completion before countdown.
4. Advance the opening hold or countdown. The countdown starts only after the first accepted block touch.
5. Synchronize presentation and changed HUD values.

Therefore the last required block exiting in the same tick as timeout wins. This is a deliberate rule expressed by ordering, not a race between independent Update methods.

Suspend disables gameplay input, pauses clocks and owned tweens and clears transient drag commands. Teardown disables input, stops clocks/tweens, cancels the attempt, disposes resources and returns pooled views. Restart builds fresh runtime state; persistent purchases and spent stock remain. No `Time.timeScale` pause is used.

Won rejects domain work immediately. A bounded foreground Finish may retain final exit/Hammer/Vacuum visuals for at most 0.8 seconds through [PrimeTweenFinishDeadline](../Assets/ColorBlockJam/Runtime/Gameplay/Session/PrimeTweenFinishDeadline.cs); timeout, background, Home or disposal forces cleanup. Failed tears down immediately, and result UI observes the released attempt. **Static board-result capture is deferred, not implemented:** LevelSession's Failed comment calls it a later task, and PROJECT DEC-160 records it as not done. The broader lifecycle rules describe the intended capture, but no current runtime result-backdrop capture path was established. Popup navigation and save status remain separate from visual Finish. Synchronous observer reentry and late async results are guarded by state/generation checks.

### Input to committed board

- [GameplayPointerInput](../Assets/ColorBlockJam/Runtime/Gameplay/Input/GameplayPointerInput.cs) owns a code-built, polled InputActionMap, latches the pressing device, handles very short taps and cancellation, and ignores a second finger as gameplay pointer.
- [UiToolkitPointerFilter](../Assets/ColorBlockJam/Runtime/Gameplay/Input/UiToolkitPointerFilter.cs), [CameraBoardProjector](../Assets/ColorBlockJam/Runtime/Gameplay/Input/CameraBoardProjector.cs), [BlockPicker](../Assets/ColorBlockJam/Runtime/Domain/Picking/BlockPicker.cs) and [DragInputRouter](../Assets/ColorBlockJam/Runtime/Gameplay/Session/DragInputRouter.cs) form the UI veto/projection/pick/route seam. Claiming a pointer lasts until release/cancel; a gesture does not transfer between blocks mid-drag.
- [DragResolver](../Assets/ColorBlockJam/Runtime/Domain/Movement/DragResolver.cs) resolves movement and presents an [occupancy preview](../Assets/ColorBlockJam/Runtime/Domain/Board/PreviewBoard.cs); held visual movement does not directly mutate the committed board.
- [MoveRules](../Assets/ColorBlockJam/Runtime/Domain/Movement/MoveRules.cs) and [ExitRules](../Assets/ColorBlockJam/Runtime/Domain/Exit/ExitRules.cs) check grid legality. Exits require flush placement, one door covering the footprint extent, matching color, clear outward swept cells and agreeing hooks. Concave footprints cannot pass through a blocked notch merely because their bounding edge fits.
- [CommandExecutor](../Assets/ColorBlockJam/Runtime/Domain/Commands/CommandExecutor.cs) dispatches typed handlers and logs only applied commands. Internal Domain mutators prevent external assemblies from bypassing this path during play.

[BoardState](../Assets/ColorBlockJam/Runtime/Domain/Board/BoardState.cs) stores flattened occupancy and preallocated footprint/split buffers. Rocket removal uses four-connected fragments, reuses the parent's ID for one part and appends bounded child IDs; fragment metadata preserves source relationships. Capacities derive from authored cell counts, not arbitrary per-frame list growth.

[ReplayCommandSource](../Assets/ColorBlockJam/Runtime/Domain/Commands/ReplayCommandSource.cs) reuses command legality and tick/time stamps. The [simulation](../Assets/ColorBlockJam/Runtime/Domain/Session/BoardSimulation.cs) allocates a 1,024-command log; longer attempts can continue with a truncated log. **This is board-command replay**, not complete touch/UI/economy/network replay or a claim of cross-platform deterministic networking. Time Freeze and other surrounding policy need separate state if a broader replay is required.

Deeper references: [Gameplay: Drag and Gate](Gameplay-Drag-And-Gate.md), [Game HUD](Game-Hud.md).

## Boosters and effect extension

[BoosterUseCoordinator](../Assets/ColorBlockJam/Runtime/Gameplay/Boosters/BoosterUseCoordinator.cs) accepts a press only for a current Playing attempt outside opening/busy/write states. Empty stock opens purchase; selecting/switching/canceling a target spends nothing. [BoosterTargeting](../Assets/ColorBlockJam/Runtime/Gameplay/Boosters/BoosterTargeting.cs) and Domain target/restriction gates validate against the committed board.

| Booster | Implemented intent |
| --- | --- |
| Rocket | Remove one valid cell, refusing a block's last cell; a split changes footprints through the command handler. |
| Hammer | Remove one valid block. |
| Vacuum | Remove valid blocks of a selected color. |
| Time Freeze | Freeze countdown for its authored duration; no board command or independent gameplay clock. |
| Extra Time | Starting-booster selection only; add authored time before normal interaction. |

The in-game sequence is capture generation/immutable intent → suspend with Saving → app save commits stock debit → current attempt records success → next resumed command phase applies once. Failed persistence changes neither stock nor effect. Background latches pause, so a late result cannot automatically resume gameplay. A committed debit can outlive a scene; if the attempt tears down before application, its effect is discarded without refund. That is an explicit product tradeoff, not an atomic save/animation transaction.

[StartingBoosterPreparer](../Assets/ColorBlockJam/Runtime/Gameplay/Boosters/StartingBoosterPreparer.cs) validates and warms the board/targets on Home, captures selection and commits a combined debit. [OpeningPlan](../Assets/ColorBlockJam/Runtime/Gameplay/Boosters/OpeningPlan.cs) applies Extra Time before Rocket after the intro, then holds normal input/countdown for presentation. Pause retains intro/hold progress; resume does not spend or roll again. Closing after commit can discard the prepared intent without refund.

Effects use typed interfaces across schema/attempt/rule/view layers: [IBlockEffectModule](../Assets/ColorBlockJam/Runtime/Gameplay/Effects/IBlockEffectModule.cs), [BlockEffectContextSource](../Assets/ColorBlockJam/Runtime/Gameplay/Effects/BlockEffectContextSource.cs), [AttemptHooks](../Assets/ColorBlockJam/Runtime/Domain/Hooks/AttemptHooks.cs), [LevelEffectRegistry](../Assets/ColorBlockJam/Runtime/Core/Levels/LevelEffectRegistry.cs). GameSceneInstaller currently registers **Ice and Arrow**. Ice vetoes drag until required clears; Arrow constrains axis/exit behavior. Barrier has partial source and tests, but its registry support gate is false and its module is not registered by the Game installer. Other catalog objects are likewise explicitly runtime-gated.

Adding an effect requires schema validation and parameters, runtime support registration, attempt state/hooks, presentation, Editor controls/preview, solver support or an honest NotChecked result, and focused tests. The hook seam avoids teaching the session every mechanic, but does not eliminate integration work. See [Block Effects Architecture](Block-Effects-Architecture.md), [Booster Gameplay](Booster-Gameplay.md), [Ice Visibility](Ice-Visibility.md).

## Data authority and content loading

| Dataset / state | Editable or runtime authority | Consumers / conversion |
| --- | --- | --- |
| Level database | [Data/Json/Levels](../Assets/ColorBlockJam/Data/Json/Levels): one folder per database; ordered manifest plus packs of at most 100 records | Editor writes the same format runtime loads. [LevelManifestJson](../Assets/ColorBlockJam/Runtime/Core/Levels/LevelManifestJson.cs), [LevelDatabaseJson](../Assets/ColorBlockJam/Runtime/Core/Levels/LevelDatabaseJson.cs). No editable SO wrapper. |
| Active database choice | `m_levelJson` Addressables manifest reference on [ProjectContext.prefab](../Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab) | Level Editor selection updates this choice; app loads its own LevelCatalog. Changing the selection writes a protected prefab. |
| Piece/template authoring library | [Data/Json/Pieces](../Assets/ColorBlockJam/Data/Json/Pieces), read/written by [ShapeLibraryFile](../Assets/ColorBlockJam/Editor/Core/BoardKit/ShapeLibraryFile.cs) and Piece Editor | Editor-only reusable templates/names. Saved levels carry their own footprint cells/piece names; runtime does not load the piece library. |
| Booster/currency/navigation/environment definitions | Feature-grouped assets under [Data](../Assets/ColorBlockJam/Data) | Read-only authored ScriptableObjects supply catalogs/rules/art references; mutable player/attempt state lives elsewhere. |
| Attempt state | LevelSession-owned plain C# BoardSimulation/BoardState/countdown/hooks | Validated loaded values copied for play; authored data is never changed by an attempt. [AttemptLevelConverter](../Assets/ColorBlockJam/Runtime/Gameplay/Levels/AttemptLevelConverter.cs). |
| Player data | One immutable, versioned PlayerSnapshot committed through app save service | Separate encrypted player file in players, memory store in Editor; never a level asset or shared SO cache. |
| Editor draft | Window-owned draft/Undo and saved baseline | Saved explicitly; preview/solver frames do not become the authored database. |

[LevelJsonFiles](../Assets/ColorBlockJam/Runtime/Core/Levels/LevelJsonFiles.cs) derives `LevelDatabase/<name>/<file>` addresses and the `level-json` label. The database folder name must match its manifest name. [LevelCatalog](../Assets/ColorBlockJam/Runtime/Core/Levels/LevelCatalog.cs) loads the manifest at startup and retains one parsed pack. The Game attempt loader obtains the required pack while on Home; the Editor has a separate catalog containing all levels. Addressables handles are released in `finally`, while parsed values remain owned by the catalog. [DTO linker preservation](../Assets/ColorBlockJam/Runtime/Core/Levels/link.xml) protects reflection-bound JSON types for IL2CPP.

[LevelContentUpdater](../Assets/ColorBlockJam/Runtime/Core/Levels/LevelContentUpdater.cs) contains a once-at-startup catalog check/update/label download path, with shipped-catalog fallback when updated bundles are unavailable. It is not a custom downloader, ongoing polling system or second level authority.

**Current configuration (S):** [AddressableAssetSettings](../Assets/AddressableAssetsData/AddressableAssetSettings.asset) has remote catalog off, the Default profile, catalog timeout 0 and automatic catalog-update disabling off. The [level content schema](<../Assets/AddressableAssetsData/AssetGroups/Schemas/Level Database_ContentUpdateGroupSchema.asset>) has StaticContent off. [Level Content Updates](Level-Content-Updates.md) describes intended configuration and publishing steps, but its Limits section says those settings remain unapplied by user choice. Do not quote its Settings table as the current applied configuration or claim a working production remote-delivery service. A hosting URL, deliberate configuration and target build/device validation remain future work.

For schema details, use [Level Data Format](Level-Data-Format.md); for authored asset rationale, [Board Art DI Design](Board-Art-DI-Design.md) and [Art/PRD mapping](Art-Asset-PRD-Mapping.md).

## Persistence, economy and lives

### Prepare, persist, publish

[PlayerSaveService](../Assets/ColorBlockJam/Runtime/Player/PlayerSaveService.cs) implements narrow wallet, progress, settings, inventory, shop, consumer, currency-grant and win-recording ports. [PlayerEconomyRules](../Assets/ColorBlockJam/Runtime/Player/PlayerEconomyRules.cs) extracts pure transformations; [PlayerSnapshot](../Assets/ColorBlockJam/Runtime/Player/PlayerSnapshot.cs) is the immutable committed value.

A SemaphoreSlim serializes writers. Each operation prepares against the latest committed snapshot inside the lock, advances revision, awaits storage, then updates committed/reactive values. Purchase joins currency debit and stock grant; win joins reward and progress; starting boosters join debits. UI/coordinator pending gates refuse duplicate submission in their flows; the service also protects state from concurrent lost updates. These mechanisms should not be described as universal idempotency for every arbitrary repeated API call.

Caller cancellation can stop a queued/pre-write operation. Once storage starts, the app save lifetime owns it, so scene unload does not undo a committed spend. Late results check owner generation before changing a view/session. Store failure publishes no economy mutation. Persisted zeros prevent spent booster stock from being reseeded; stock caps at 99 and currencies honor catalog maxima. [PlayerProgress](../Assets/ColorBlockJam/Runtime/Player/PlayerProgress.cs) maps the growing logical index modulo database count, so levels loop while the displayed progression grows.

[LevelCompleteFlow](../Assets/ColorBlockJam/Runtime/Gameplay/Hud/Result/LevelCompleteFlow.cs) starts one +10 coin/progress save on Won, independently of Finish. Continue remains gated until commit; failed save exposes Retry without a second reward. Editor Test Level outside the active database does not grant normal progression/reward.

### Storage and trust boundary

[PlayerDataInstaller](../Assets/ColorBlockJam/Runtime/Player/PlayerDataInstaller.cs) chooses MemoryPlayerSnapshotStore for Editor play and FilePlayerSnapshotStore under `persistentDataPath/player` in players. Thus ordinary Editor play does not exercise real-device disk persistence.

[FilePlayerSnapshotStore](../Assets/ColorBlockJam/Runtime/Player/Storage/FilePlayerSnapshotStore.cs) performs thread-pool file work: serialize/encrypt → write `.tmp` and flush → replace main keeping `.bak` (move on first save). Load tries main, then backup, then seeded defaults; it deletes stale temporary files and emits bounded recovery warnings. Future JSON format versions refuse writes rather than overwriting newer player data. Device crash durability still requires fault-injection evidence, not just reading File.Replace code.

[PlayerSnapshotJson](../Assets/ColorBlockJam/Runtime/Player/Storage/PlayerSnapshotJson.cs) validates explicit fields, duplicate names, sizes/depth and version; TypeNameHandling is None. [Player linker preservation](../Assets/ColorBlockJam/Runtime/Player/link.xml) retains DTOs. Stable string IDs identify currencies/stock at the JSON boundary; enum ordinals are not the persisted authority.

[PlayerSaveCipher](../Assets/ColorBlockJam/Runtime/Player/Storage/PlayerSaveCipher.cs) uses AES-256-CBC, a random IV and HMAC-SHA256 over the container before decryption, with constant-time MAC comparison. [PlayerSaveKey](../Assets/ColorBlockJam/Runtime/Player/Storage/PlayerSaveKey.cs) derives separate keys via PBKDF2-SHA256 from device ID and fixed app salt. **The client owns the key material and the economy.** This deters casual editing/copying and detects corrupt/tampered containers; it is not server authority, robust anti-cheat, account synchronization or portable cloud saves. Local UTC clock and device identity remain limitations. No external commerce/backend is implemented by these local booster purchases.

### Lives and settings

[LivesService](../Assets/ColorBlockJam/Runtime/Player/Lives/LivesService.cs) wraps the `life` currency without a second mutable lives store. Current [Life.asset](../Assets/ColorBlockJam/Data/Currencies/Life.asset) starts/caps at five, refills one per 1,800 seconds and prices full refill at one coin. Refills and purchase/spend use the same transactional service and local UTC clock.

[AttemptLifeCost](../Assets/ColorBlockJam/Runtime/Gameplay/Hud/Menu/AttemptLifeCost.cs), owned by [GameMenuFlow](../Assets/ColorBlockJam/Runtime/Gameplay/Hud/Menu/GameMenuFlow.cs), charges at most once per attempt on fail, confirmed in-level retry or leaving in-progress gameplay for Home. Retry from the same fail result does not double-charge; a new attempt rearms the owner. A failed life write takes no life and is logged; the current charge owner latches the attempt and does not automatically retry it. With no available lives, flow routes to Hub/refill rather than restarting. Home after Won is free. DEC-174 supersedes older no-deduction/placeholder descriptions.

[HubScreen](../Assets/ColorBlockJam/Runtime/Hub/Composition/HubScreen.cs) owns a 250 ms UI schedule for life display/refill refresh, stopped with its binding lifecycle. [LifeRefillOverlay](../Assets/ColorBlockJam/Runtime/Hub/Lives/LifeRefillOverlay.cs) reuses purchase UI with current count/countdown. This schedule is outside the gameplay tick.

[DummyCurrencyReward](../Assets/ColorBlockJam/Runtime/Hub/Home/DummyCurrencyReward.cs) implements case-only Home plus-button feedback as a persisted grant: +1 life or +100 coins, with one pending grant at a time. These buttons are a deliberate placeholder for a shop/IAP flow, not evidence of trusted external purchase validation.

[SettingsPopupPresenter](../Assets/ColorBlockJam/Runtime/UI/Settings/SettingsPopupPresenter.cs) optimistically flips a shown toggle, keeps one pending task per toggle, then reconciles to committed state; failure restores the old value and shows feedback. Bind generation/token prevents stale UI results. This presentation optimism is separate from commit-first wallet/inventory publication. [PlayerSettings](../Assets/ColorBlockJam/Runtime/Player/PlayerSettings.cs) explicitly says Music/Sound only store/show state; Vibration gates implemented haptics. The case requires toggle visuals, not a complete audio system.

Design and dated evidence: [Hub and Player Data Design](Hub-And-Player-Data-Design.md), [player implementation run](verification/player-data/2026-09-27-implementation-run.md), [life-economy task](../production/tasks/TASK-20260928-124512603-1f645a0d-life-economy-fail-refill/contract.md).

## Editor and solver

### Designer workflow and ownership

Open the Level Editor/Piece Editor through the documented menus in [README](../README.md) and [Level Editor Design](Level-Editor-Design.md). [LevelDatabaseWindow](../Assets/ColorBlockJam/Editor/Core/Levels/LevelDatabaseWindow.cs) connects the UI Toolkit canvas, selected database/level, draft, piece tools, validation and Test Level. It remains **5,407 lines**; [LevelPieceEditController](../Assets/ColorBlockJam/Editor/Core/Levels/LevelPieceEditController.cs) is **1,804 lines**. Extraction improves boundaries but leaves substantial coordination cost.

The editor exposes dimensions, voids/walls/gates, block footprint/color/placement, timer and effects visually. [LevelDraftController](../Assets/ColorBlockJam/Editor/Core/Levels/LevelDraftController.cs) owns the editable draft and saved baseline; [LevelPieceEditController](../Assets/ColorBlockJam/Editor/Core/Levels/LevelPieceEditController.cs) owns piece editing; [LevelSaveController](../Assets/ColorBlockJam/Editor/Core/Levels/LevelSaveController.cs) prepares validation/template/save policy. Undo/session edits remain separate from committed files. Template edits may restamp dependent levels; refusal/confirmation policies prevent silently changing incompatible footprints.

[LevelDatabaseFiles](../Assets/ColorBlockJam/Editor/Core/Levels/LevelDatabaseFiles.cs) validates all texts in memory, rejects an external disk change against the read snapshot, requires a separate backup, writes only changed packs, writes a changed manifest last, then deletes unlisted packs with their metadata through the Editor route. Restore Previous Save uses the backup. **A folder save is not a single crash-atomic transaction:** existing pack paths can already be overwritten before the manifest step, and a same-ID content edit may not change the manifest at all. Backups/individual file writes mitigate failures but do not establish whole-database atomicity.

[LevelDatabaseWindow.TestLevel](../Assets/ColorBlockJam/Editor/Core/Levels/LevelDatabaseWindow.cs) saves unsaved draft changes through SaveLevel first; if validation/save refuses, Test Level stops. It then passes the active database GUID and saved level's stable ID to [LevelTestPlay](../Assets/ColorBlockJam/Editor/Core/Levels/LevelTestPlay.cs). Game reads that selected authored JSON level, while LevelTestPlay changes the Play Mode start scene and restores editing afterwards. **Test Level can write the database through its save policy; it does not send an unsaved draft copy directly into Game.** [LevelPreviewOwner](../Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewOwner.cs) and pooled preview hosts separately render the draft in the authoring scene with editor-only lifetime. Preview and runtime share environment/modular composition rules, not mutable attempts. See [Level Editor Scene Preview](Level-Editor-Scene-Preview.md).

### Solvability is a bounded result

[LevelValidationFlow](../Assets/ColorBlockJam/Editor/Core/Levels/LevelValidationFlow.cs) owns validation results/jobs and invalidates them on draft changes. Designer Validate Level can run copied state on a worker through [LevelValidationJob](../Assets/ColorBlockJam/Editor/Core/Levels/LevelValidationJob.cs); cancellation and shutdown waits are bounded. Save-time validation also has a synchronous/waiting path, so do not claim every Editor search operation is non-blocking.

[LevelSolvabilityCheck](../Assets/ColorBlockJam/Editor/Validation/LevelSolvabilityCheck.cs) builds a Domain model and shared rule tables. [LevelSolverSearch](../Assets/ColorBlockJam/Editor/Validation/LevelSolverSearch.cs) uses breadth-first search, a visited dictionary and predecessor arrays. [LevelStateKey](../Assets/ColorBlockJam/Editor/Validation/LevelStateKey.cs) packs positions/exited state into 128 bits for at most 16 blocks. [LevelSolverMoves](../Assets/ColorBlockJam/Editor/Validation/LevelSolverMoves.cs) shares movement/exit legality rather than maintaining a disconnected editor rule set; Ice/Arrow have specific solver integration.

[LevelCheckOptions](../Assets/ColorBlockJam/Editor/Validation/LevelCheckOptions.cs) currently gives the main search 15 seconds and `int.MaxValue` positions; no small default memory/position cap is implied. Listing separately allows 1,000,000 positions, 10 seconds and up to 20 retained solutions. Search complexity remains exponential and memory growth is a real scaling tradeoff.

| Verdict | Meaning |
| --- | --- |
| Solvable | A supported rule-model route was found. |
| Unsolvable | A sound static precheck or exhaustive supported search established no route. |
| Unknown | A search budget ended without a proof. It must not be translated to impossible. |
| NotChecked | Missing/unsupported input, too many blocks, or another explicit inability to check. |

The solver excludes timer and booster simulation. Eager exits, pruning and merged solution steps mean the displayed route is not a proof of minimum finger drags. [LevelSolutionFrames](../Assets/ColorBlockJam/Editor/Validation/LevelSolutionFrames.cs) and [LevelSolutionPlayback](../Assets/ColorBlockJam/Editor/Core/Levels/LevelSolutionPlayback.cs) show copied states without editing the draft. Source parity tests strengthen confidence in shared rules, but a solvable spatial puzzle is not automatically beatable within its authored time.

## UI, rendering, art and haptics

### Human-editable UI

Authored UXML/USS documents under [UI](../Assets/ColorBlockJam/UI) contain fixed slots, layout, static labels/styles and representative samples. Presenters bind named elements and replace actual player/offer/state values; variable-length lists may clone templates. [HomePresenter](../Assets/ColorBlockJam/Runtime/Hub/Home/HomePresenter.cs), [GameplayHud](../Assets/ColorBlockJam/Runtime/Gameplay/UI/GameplayHud.cs) and shared popup presenters own reactive bindings, callbacks and cleanup.

The [purchase template](../Assets/ColorBlockJam/UI/Purchase/BoosterPurchasePopup.uxml) previews visibly by itself; embedded Hub/Game instances are hidden before binding so sample offers cannot intercept input. [BoosterPurchaseView](../Assets/ColorBlockJam/Runtime/UI/Purchase/BoosterPurchaseView.cs) starts closed and SetOffer replaces samples. [SafeAreaPadding](../Assets/ColorBlockJam/Runtime/Presentation/UI/SafeAreaPadding.cs) recalculates on geometry changes, not per-frame polling. UI Builder canvas size is document-specific and must match PanelSettings at 1080 × 1920; Fit Canvas changes zoom only. This review did not reopen UI Builder or visually verify each document.

[HubNavigator](../Assets/ColorBlockJam/Runtime/Hub/Navigation/HubNavigator.cs) separates overlays from tab selection; [HubNavigationCatalog](../Assets/ColorBlockJam/Runtime/Hub/Tabs/HubNavigationCatalog.cs) authors tab presentation/availability. [HubTabRules](../Assets/ColorBlockJam/Runtime/Hub/Tabs/HubTabRules.cs) deliberately keeps LevelGated tabs locked until the user's reached/completed-level rule is chosen. Other tab destinations remain case-appropriate placeholders.

References: [UI Authoring](UI-Authoring.md), [UI Asset Mapping](UI-Asset-Mapping.md), [UI Asset Import](UI-Asset-Import.md), [Game HUD](Game-Hud.md).

### Board visuals and performance boundaries

[BoardPresenter](../Assets/ColorBlockJam/Runtime/Gameplay/Board/BoardPresenter.cs) adapts Domain preview/events to view positions and exit/booster effects. Static board-result capture remains deferred as described above. [BlockPieceFactory](../Assets/ColorBlockJam/Runtime/Presentation/Board/BlockPieceFactory.cs), [BoardTilePool](../Assets/ColorBlockJam/Runtime/Presentation/Board/BoardTilePool.cs), modular views and fragment capacity planning warm resources before Playing, including Rocket split capacity. Views release pooled pieces and stop local presentation work at their lifecycle boundary.

Environment composition uses half-cell quadrants, a reusable floor mask and ordered first-match rules with rotations, reserving gates before placing walls/corners. [EnvironmentComposer](../Assets/ColorBlockJam/Runtime/Environment/EnvironmentComposer.cs), [EnvironmentRuleMatcher](../Assets/ColorBlockJam/Runtime/Environment/EnvironmentRuleMatcher.cs) and [EnvironmentView](../Assets/ColorBlockJam/Runtime/Environment/EnvironmentView.cs) support both runtime and editor. [BoardCameraFraming](../Assets/ColorBlockJam/Runtime/Environment/BoardCameraFraming.cs) fits the playable board to the view. See [Environment Layout](Environment-Layout.md).

Colors share palette/material data and property blocks; [BoardLit shader](../Assets/ColorBlockJam/Art/Shaders/Board/BoardLit.shader) and [GamePalette](../Assets/ColorBlockJam/Runtime/Core/Art/GamePalette.cs) provide the rendering seam. Shared material/property blocks alone do not guarantee one draw call or SRP batching. Ice uses shared shell geometry/material; [Ice shader graph](../Assets/ColorBlockJam/Art/Shaders/Ice.shadergraph) and [Time Freeze graph](../Assets/ColorBlockJam/Art/Shaders/TimeFreezeOverlay.shadergraph) are separate visual effects.

Allocation-conscious choices include preallocated board buffers/event logs, pooled roots/tiles, input polling, plain loops, change-only reactive HUD updates and prebuilt countdown strings. [FrameRatePolicy](../Assets/ColorBlockJam/Runtime/Core/Startup/FrameRatePolicy.cs) currently selects a constant 60 fps target; the supplied display-rate argument is unused. A target is not a measured sustained frame rate. Whole-game zero GC, single-draw rendering, thermal stability and all-device smoothness are not established by these choices.

Historical README/device notes describe Galaxy A16 profiling, disabling SSAO and lowering held-block outline rendering cost. Booster LOD/flipbook choices trade geometry/CPU work for texture/material memory. Use [Rendering Reference Review](Rendering-Reference-Review.md), [shared art](art/shared-art/README.md), [booster art](art/boosters/README.md), [booster motion](art/boosters/motion/README.md), [wall brick](art/wall-brick/README.md) and [Android Player Settings](Android-Player-Settings.md) for rationale and dated evidence; do not present those measurements as fresh results.

Supplied FBX/UI assets remain separated from authored art. Modular center/edge/corner pieces compose cell footprints. `ModularBlock_Base`, `Base_Side`, `Gate_Orange` and `Arrow_Gate_Orange` are source materials, not missing meshes. Supplied `corner_3.002` has an intersecting polygon/stray plane and is unused. UI sprites have fixed-aspect versus measured nine-slice classes; atlas sources are uncompressed, screen atlases carry mobile ASTC 6×6, and full backgrounds stay outside atlases. [Board kit](art/board-kit/README.md) and [UI import ledger](UI-Asset-Import.md) are the asset authorities. No fresh Blender/material/asset-pixel audit was conducted for this index.

### Native haptics and player build integration

The reusable [Haptics library](../Assets/Haptics) owns Android Vibrator JNI, iOS UIKit feedback and Android permission support. The game adapter maps moments and observes the committed Vibration setting; turning it off stops output. The app context disposes subscription/output. [Haptics](Haptics.md) records removal of Lofelt/Nice Vibrations due to a 16 KB native alignment problem. The repository [alignment checker](../.agents/skills/cbj-performance-profiling/scripts/check_16kb.py) is the future plugin-check route; it was not run here.

[PlayerBuildAssemblyFilter](../Assets/ColorBlockJam/Editor/Core/PlayerBuild/PlayerBuildAssemblyFilter.cs) implements IFilterBuildAssemblies to drop `MCPForUnity.Runtime.dll`, keeping development transport out of players without editing vendor code. Its source/test is not current binary proof. Onity activator generation/AOT fallback, Json.NET linker preservation, native haptics and rendering still need target-specific IL2CPP/player verification. Deleted generated Addressables `link.xml` must not be confused with the two existing project-authored DTO linker files.

## Tests and verification limits

| Boundary | Representative source to inspect | What it can demonstrate when freshly executed |
| --- | --- | --- |
| Architecture | [GameplayArchitectureTests](../Assets/ColorBlockJam/Tests/Gameplay/EditMode/Gameplay/GameplayArchitectureTests.cs), [EditorSolidBoundaryTests](../Assets/ColorBlockJam/Tests/Editor/EditMode/EditorSolidBoundaryTests.cs) | Single tick owner, no Domain/Gameplay Unity frame loops/async void, assembly direction, internal mutation and extracted Editor boundaries. Reflection checks do not prove every lifecycle path is safe. |
| Domain/commands | [DragResolverTests](../Assets/ColorBlockJam/Tests/Gameplay/EditMode/Domain/DragResolverTests.cs), [CommandExecutorTests](../Assets/ColorBlockJam/Tests/Gameplay/EditMode/Domain/CommandExecutorTests.cs), [ReplayTests](../Assets/ColorBlockJam/Tests/Gameplay/EditMode/Domain/ReplayTests.cs) | Drag/collision/exit behavior, application/refusal, stamps and command replay. |
| Session/boosters | [LevelSessionTests](../Assets/ColorBlockJam/Tests/Gameplay/EditMode/Gameplay/LevelSessionTests.cs), [BoosterIntentSessionTests](../Assets/ColorBlockJam/Tests/Gameplay/EditMode/Gameplay/BoosterIntentSessionTests.cs), [BlockEffectCompositionTests](../Assets/ColorBlockJam/Tests/Gameplay/EditMode/Gameplay/BlockEffectCompositionTests.cs) | State ordering, pause/end/reentry, commit intents and effect seams under supplied fixtures. |
| Save/storage | [PlayerSaveServiceTests](../Assets/ColorBlockJam/Tests/Player/EditMode/PlayerSaveServiceTests.cs), [FilePlayerSnapshotStoreTests](../Assets/ColorBlockJam/Tests/Player/EditMode/FilePlayerSnapshotStoreTests.cs), [PlayerSnapshotJsonTests](../Assets/ColorBlockJam/Tests/Player/EditMode/PlayerSnapshotJsonTests.cs) | Controlled delayed/failing writes, committed publication, recovery/version/JSON behavior. Fakes do not replace real-device crash checks. |
| UI/Hub | [HomePresenterTests](../Assets/ColorBlockJam/Tests/Hub/EditMode/HomePresenterTests.cs), [SettingsPopupPresenterTests](../Assets/ColorBlockJam/Tests/UI/EditMode/SettingsPopupPresenterTests.cs), [InGamePurchasePopupTests](../Assets/ColorBlockJam/Tests/Gameplay/PlayMode/InGamePurchasePopupTests.cs) | Binding cleanup/data changes, settings rollback, popup integration. Purchase tests have the recorded disagreement below. |
| Editor/data | [LevelDatabaseFilesTests](../Assets/ColorBlockJam/Tests/Editor/EditMode/LevelDatabaseFilesTests.cs), [LevelDraftControllerTests](../Assets/ColorBlockJam/Tests/Editor/EditMode/LevelDraftControllerTests.cs), [LevelSaveControllerTests](../Assets/ColorBlockJam/Tests/Editor/EditMode/LevelSaveControllerTests.cs), [LevelTestPlayRoundTripTests](../Assets/ColorBlockJam/Tests/Editor/EditMode/LevelTestPlayRoundTripTests.cs) | External-change/save policies, draft/save separation and authoring round trip. |
| Solver | [LevelCommandParityTests](../Assets/ColorBlockJam/Tests/Validation/EditMode/LevelCommandParityTests.cs), [LevelArrowSolverTests](../Assets/ColorBlockJam/Tests/Validation/EditMode/LevelArrowSolverTests.cs) | Shared solver/runtime legality for sampled fixtures, including effects. |
| Views/build | [BoardPresenterLifecycleTests](../Assets/ColorBlockJam/Tests/Gameplay/PlayMode/BoardPresenterLifecycleTests.cs), [EnvironmentViewLifecycleTests](../Assets/ColorBlockJam/Tests/Environment/PlayMode/EnvironmentViewLifecycleTests.cs), [PlayerBuildAssemblyFilterTests](../Assets/ColorBlockJam/Tests/Editor/EditMode/PlayerBuildAssemblyFilterTests.cs) | Pool/tween/view lifetime and filter policy; a shipped player still requires inspection. |

**Actually verified for this index:** authored-area inventory, current source/asmdefs/data/settings, package pins, relevant documentation/test source, read-only CLI/version/Pipeline discovery, Plastic status, source fingerprint and all Markdown targets in this file. No new runtime pass is inferred from source existence.

**Historical evidence:** [SOLID Refactor 2026-09-30](SOLID-Refactor-2026-09-30.md) records 998 EditMode cases by combining an initial run with a one-case retry. It records 81 completed PlayMode executions with three initial failures, one corrected Time Freeze fixture retry and two retained purchase disagreements. The initial detailed PlayMode MCP result was null. Do not compress this into “all tests passed” or a fabricated unified rerun. [README](../README.md) records Android 16/Galaxy A16 IL2CPP ARM64 flow checks; those do not qualify current iOS or every dirty workspace revision.

**Still U:** current Unity compilation/test results, exact live Editor readiness, current APK/IL2CPP tooling exclusion and DTO/AOT behavior, iOS device behavior, real disk crash/restore fault injection, full-flow visual/20:9 checks, profiler allocation/frame-time/thermal/memory evidence, and configured remote delivery. The review deliberately did not build, run broad tests, mutate the Editor or resolve local asset changes.

## Strengths, tradeoffs and known gaps

### Defensible strengths

| Strength | Evidence and practical consequence |
| --- | --- |
| Executable separation of rules and Unity integration | **S:** Domain asmdef, command/rule sources and architecture tests. Movement/exit legality can be tested and reused by the solver without loading Game. |
| Explicit ownership and terminal ordering | **S:** LevelSession, attempt lifetime, tick owner and view cleanup. Pause/end policies do not rely on globally freezing Unity time; the final-exit/timeout outcome is defined. |
| Commit-first economy | **S:** PlayerSaveService/store/economy rules. A failed purchase/save does not grant stock or update committed balances; win reward/progress share a transaction. |
| One authored level authority | **S:** Manifest/packs, direct-save Editor and runtime loader. No editable SO/JSON mirror or merge/export synchronization is required. |
| Honest bounded solver outcomes | **S:** Budget/status sources and shared rules. Unknown/NotChecked avoid false impossible claims and editor/runtime drift is testable. |
| Human-editable UI and shared presenters | **S/H:** Real templates, hosts and authoring evidence. Artists edit the runtime document; shared logic remains independent of scene navigation. |
| Performance work placed at ownership boundaries | **S/H:** Preallocated Domain buffers, prewarmed pools, change-only HUD and historical device notes. Hot-path work is reduced in specific paths rather than by broad allocation slogans. |
| Reusable device seam and player-tooling filter | **S:** Standalone Haptics and build filter. Native feedback is portable across games; development tooling has an explicit exclusion mechanism. |

### Tradeoffs and concrete gaps

| Concern / limitation | Confidence, evidence and interview framing |
| --- | --- |
| Purchase-success contract disagreement | **S + H:** Current BoosterPurchasePresenter raises CloseRequested immediately on Purchased. [InGamePurchaseHost](../Assets/ColorBlockJam/Runtime/Gameplay/UI/InGamePurchaseHost.cs) hides the popup and closes its purchase pause. DEC-138/test expectations keep it open with success feedback. Sept 30 records `BuyGrantsStockRefreshesTheBadgeAndKeepsThePopupOpenWithoutCasting` and `ASecondBuyWhileTheFirstIsSavingIsRefused` failures. This review confirmed the branch/test disagreement, not a new test execution. The product expectation must be resolved before claiming a green integration contract. |
| Fade readiness is unfinished | **S/I:** SceneRevealPolicy TODO is active, ignores settled-frame/time arguments and has no pending-build deadline. Narrow correction would define the intended policy and tests in its owning task; no fix was authorized here. |
| Static board-result backdrop is deferred | **S/H:** LevelSession's Failed comment and PROJECT DEC-160 mark capture as later/not done. The lifecycle design describes a future static capture; the current released board/popup flow must not be presented as an implemented RenderTexture result-background system. |
| Remote delivery is implemented but not configured | **S/H:** Current Addressables settings are local/unconfigured; Level Content Updates contains intended and unapplied settings. A source path is not an operational production service. |
| Large Editor orchestration surface | **S/I:** LevelDatabaseWindow 5,407 lines and PieceEditController 1,804. Extraction helped, but navigation, dirty-state, template and input interactions still create maintenance/regression cost. Do not invent a current bug solely from size. |
| Pack save is not a database-wide crash transaction | **S/I:** Existing pack paths are replaced before a changed manifest, and some content edits leave manifest unchanged. Backups/restore help; whole-folder interruption behavior needs separate evidence/design. |
| Solver memory/scaling and scope | **S/I:** Up to 16 blocks, 15-second search, default main position cap int.MaxValue, exponential visited growth; no timer/boosters. Extension requires state representation and budget policy, not merely more seconds. |
| Preloading retains memory | **S/I/U:** One hidden Game scene reduces visible start work, while retaining views/pools and parsed content. Current peak-memory/device evidence was not taken here. |
| Local trust and persistence portability | **S/I/U:** Device-derived client keys and local UTC prevent neither determined client manipulation nor clock changes. Atomic file code does not prove all mobile filesystem crash cases. |
| Committed spends can outlive their effect | **S/H:** Attempt teardown discards a committed pending booster without refund. State ownership is clear, but user-facing loss policy is a deliberate tradeoff. |
| Placeholder features must be described precisely | **S/H:** Hub destinations and level-gated unlock are design-pending/case placeholders; Music/Sound store state only. Lives and haptics are implemented. Barrier source is partial/runtime-gated. |
| Optional scope is large for the case | **I:** Solver, custom pieces/effects, lives, encryption, native haptics, preloading and authored visual work increase code/evidence volume. Explain why each improves the evaluation goals and which was beyond the minimum; do not assume every addition was necessary. |
| Verification/doc freshness | **H/U:** Old paths/counts, historical retries/null payloads and pre-existing dirty/deleted files prevent a blanket current all-clear. Narrow evidence with a revision/platform is more defensible than a large count. |

No gameplay fix, test change, lifecycle decision or task closure follows from this index. The [README known-problems section](../README.md) is short; the table above supplies more specific context without changing that release document.

## Interview question navigation

| Question | Start here | Key point to explain / challenge |
| --- | --- | --- |
| Why these layers/asmdefs? | [Assembly boundaries](#assembly-boundaries), GameplayArchitectureTests | Compile-time directions and shared rule reuse; integration assemblies are broader than Domain. |
| Why Onity rather than global singletons? | ProjectScopeInstaller, PlayerDataInstaller, GameSceneInstaller; [lifetimes](#lifetimes-and-scene-flow) | Constructor ports and scope disposal; attempt lifetime is manual. DI does not by itself prevent ownership bugs. |
| What happens on pause/background/restart? | LevelSession, AttemptLifetime, GameMenuFlow | Input/clocks/tweens stop through the owner; writes survive scene waits; stale completions cannot resume gameplay. |
| Last block exits when time reaches zero? | LevelSession.RunTick, ExitRules | Applied-command phase and win check precede countdown; result is deliberate. |
| Why command pattern and how complete is replay? | CommandExecutor, BoardSimulation, ReplayCommandSource | One mutation path, applied-only bounded log; board replay excludes surrounding UI/economy policy. |
| How does drag avoid collision/visual-state drift? | DragResolver, PreviewBoard, MoveRules, BoardPresenter | Preview occupancy versus committed validated commands; exact footprint/swept-gate rules. |
| What if saving fails or two buttons are pressed? | PlayerSaveService, BoosterUseCoordinator, popup presenter, storage tests | Serialized preparation, duplicate gates, persist-before-publish; distinguish queue safety from general idempotency. |
| Does a purchase cast a booster immediately? | BoosterPurchasePresenter, InGamePurchaseHost, BoosterUseCoordinator | Purchase grants stock only; current success closes popup. Old keep-open tests disagree. |
| Why JSON for levels and SO for catalogs? | [Data authority](#data-authority-and-content-loading), LevelDatabaseJson/manifest | Volume/tooling favors packs; feature definitions favor SOs. One authority per dataset, runtime state copied. |
| Can designers save/test without touching files? | LevelDatabaseWindow, Draft/Save controllers, LevelTestPlay | Unsaved visual draft → SaveLevel with validation/backup → selected authored level by database GUID/stable ID → Game → restored editing. Test Level refuses if required save fails. |
| Can validation prove a level impossible? | LevelSolvabilityCheck, LevelCheckOptions, LevelCommandParityTests | Only supported exhaustive/proven outcomes; budget/unsupported cases remain Unknown/NotChecked. No timer proof. |
| How is a new mechanic integrated? | LevelEffectRegistry, IBlockEffectModule, AttemptHooks, solver/editor tools | Extension seam plus schema, view, authoring and verification work; false runtime gates reject partial mechanics. |
| How are lives/progression persisted? | LivesService, AttemptLifeCost, PlayerEconomyRules, PlayerProgress | Same snapshot/clock, once-per-attempt charge; logical index loops authored content. |
| Why faster level starts, and what does it cost? | LevelPreloader, GameAttemptLoader, SceneFadeTransition | Hidden scene/pool warmup shifts work and retains memory; ready-only reveal policy has an unfinished safeguard. |
| Is the game allocation-free / 60 fps? | BoardState/pools/HUD, FrameRatePolicy, dated profiler notes | Name measured paths and conditions. Source intent and target fps do not prove sustained whole-app performance. |
| How safe is encrypted saving? | FilePlayerSnapshotStore, PlayerSaveCipher/Key, PlayerSnapshotJson | Commit/recovery and integrity boundaries; client-owned keys/local clock remain offline limits. |
| Why native haptics and a build filter? | Haptics docs/library, PlayerBuildAssemblyFilter | Reuse and Android alignment constraint; verify the final binary separately. |
| What would you prioritize next? | [Known gaps](#strengths-tradeoffs-and-known-gaps), [verification limits](#tests-and-verification-limits) | Resolve purchase contract, define reveal safeguard, verify current device/build boundaries, then address Editor/scaling needs justified by scope. |
| What did AI tools do? | [README LLM disclosure](../README.md) | Use the declared division of art/rendering versus PRD/code/review work; the disclosure is not line-level authorship proof. Explain architectural choices and evidence yourself. |

## Refresh procedure

1. Read AGENTS/CLAUDE/PROJECT and the current task scope. Preserve foreign dirty files; inspect Plastic changeset and pending changes without checkin/cleanup. Recheck allowed document ownership.
2. Reinventory Runtime/Editor/Tests and standalone Haptics, inspect asmdef references and package pins, and identify changed authorities/entry points. Exclude vendor/generated/submission copies from authored counts.
3. Recompute the explicitly defined 937-file fingerprint using the same input rules. Record the new count/hash/date/revision; compare actual changed paths, not only the aggregate. Separately inspect relevant prefabs/scenes/Addressables/art settings because that fingerprint omits most of them.
4. For each affected topic, read implementation, direct collaborators, schema/composition and tests. Follow PROJECT decision supersession; do not carry an old placeholder or old folder path into a current claim.
5. Keep source facts, inference, historical execution and newly executed verification separate. Record the exact run/revision/platform, original result plus failed-only retry, and unavailable payloads. Do not update a historical note into a fresh pass.
6. Verify CLI/version and the Unity version transport gate. Complete exact instance/root/version proof before live queries. For documentation-only refresh, source inspection with explicit live limits is sufficient; do not silently build/run/mutate the Editor.
7. Validate every Markdown file/directory target and local anchor. Use relative links from this `docs` folder and no guessed source line numbers. Show the resulting documentation diff/summary.

Completion note, 2026-10-01: added this reusable map after authored-area coverage and critical-flow source review. Only `docs/Project-Context-Index.md` was written. Link/anchor and source-snapshot checks validate the index; runtime/build/profiler checks remain outside this documentation change.
