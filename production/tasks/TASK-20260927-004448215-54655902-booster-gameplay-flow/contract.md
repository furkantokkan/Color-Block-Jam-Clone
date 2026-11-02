# TASK-20260927-004448215-54655902-booster-gameplay-flow — Playable booster flow, gameplay bar and starting effects

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none from the user for this task; wait for predecessor stable handoffs and ownership release before production writes
- Task ID: TASK-20260927-004448215-54655902-booster-gameplay-flow
- Supersedes: none
- Title: Playable booster flow, gameplay bar and starting effects
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:11 plus existing uncommitted work; planning snapshot 2026-09-27T00:44Z
- Task kind: feature
- Area: Booster commands, runtime block fragments, countdown, gameplay booster UI, persistent stock consumption and starting-effect application
- Workflow: trio

## Objective and baseline

- Observable objective: A player can use Time Freeze, Rocket, Hammer and Vacuum from the gameplay bottom bar with the referenced information panel and feedback, and can enter from Hub with selected Extra Time and Rocket applied exactly once.
- Current behavior/evidence: Booster definitions, icons and pooled-effect-capable presentation components exist. Time Freeze is configured for 10 seconds; Extra Time for +10 seconds. Gameplay still binds InactiveCountdown and has no booster command/application/HUD flow. BoardState, readers, movement and presenters assume authored block counts/footprints. PlayerSaveService supports purchases but lacks a stock-consumption transaction. Hub UI and its launch request are being built in the separate task chain below, not yet present at planning. Three required UI sprites currently have zero borders.
- Expected behavior: Reuse existing definitions, effects, Onity scope, command executor and player save owner. Add only the runtime capacity/footprint support required for Rocket fragments, the actual attempt countdown, transactional application, UI and opening integration. Do not implement a second Hub selection or purchase economy.
- Behavior that must remain unchanged: authored level/shape data and save schema; ordinary drag/gate rules; direct Game Play; pause/terminal/session ownership; existing ice and arrow behavior except the explicitly revised Hammer removal; barrier notifications; purchases and other player-data operations; no reseeding/refund on restart.

### Product decisions for this task

- Latest user instruction overrides the old Rocket connected-remainder restriction: deleting one selected cell may produce multiple independent connected components. A bridge cell of an L can leave two independently movable pieces; an end cell may leave an I. Keep exactly the remaining original occupied cells and color. Each component receives stable runtime identity and independent mutable state; keep applicable axis/ice state and source provenance without shared mutable aliases.
- Preserve the existing documented prohibition on deleting a final/only cell (AC-BST-3.3), because the new request explicitly changes splitting but does not explicitly change that boundary. Such a tap and such a starting candidate spend nothing. State this interpretation in the implementation handoff, rather than silently implementing a different rule.
- Latest user instruction says Hammer removes a block completely. In this task a selected block is removed whole, including an active Ice shell; the old shell-only Hammer exception does not count as completion. Standalone obstacle/layer destruction is outside this task.
- Vacuum removes every remaining block of the selected color, including eligible Ice/Arrow blocks, and shows no targeting crosshair. Use selected-outline and suction feedback.
- Time Freeze stops the main countdown for exactly 10 seconds of Playing time, presents a separate 10-to-0 counter and frost feedback, and leaves ordinary movable blocks playable. Extra Time is a distinct pre-level +10-second bonus from its definition.
- HUD order is Time Freeze, Rocket, Hammer, Vacuum. Information panels have background, icon, title and readable instruction; targeted modes have cancel. Rocket copy follows the supplied reference: “Tap and destroy one unit of a block!” Vacuum uses the same information-panel language without crosshairs.
- Targeting does not pause the clock. Close, same-button tap, empty-space tap or Back cancels without spending; switching tools is free. Actual blocked/removed/mid-exit targets are rejected. Preserve documented target gates not superseded above.
- Starting Rocket samples one eligible cell once from a stable candidate list, now including split-causing cells. Extra Time applies before Rocket; no candidate means no Rocket debit. Selection itself never spends.

## Ownership

- Owner/writer role: New Codex chat is the decision/lifecycle owner; one implementer writes source and approved serialized targets, then releases its role to an independent verifier; a bugfixer runs only after verified failure routing.
- Ownership transfer (2026-09-27T14:20Z, user decision in Claude session "Booster gameplay flow [f1ed7e]"): the user chose "Claude takes over" and "üstüne devral ve sen devam et o tarafı durdurup". That Claude session is now the sole decision and lifecycle owner and orchestrates through Claude workflows; the Codex orchestrator (chat 01a0e056) is stopped by the user and no longer dispatches. The Codex Ice chat released its lock for this takeover (Ice lifecycle: "control released for user-directed Claude takeover"); Booster builds on top of Ice's current uncommitted changes. Ice's own remaining verification and renderer-only checkin stay with the Ice task and are not performed by this task.
- Execution route (2026-09-27): implementation and focused verification run in an isolated project copy (`%TEMP%/cbj-booster-f1ed7e`, never checked in), so live sessions sharing this workspace and its Editor see no half-finished compile. Only verified files inside this contract's allowed paths are copied back, after a byte comparison against the copy baseline; a shared file changed by another owner since the baseline is merged, never overwritten. Serialized targets are produced by copy-only Editor scripts or the live Editor and are never hand-edited when an Editor route exists.
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Domain/Boosters/**
  - Assets/ColorBlockJam/Domain/Boosters.meta
  - Assets/ColorBlockJam/Domain/Board/*.cs
  - Assets/ColorBlockJam/Domain/Board/*.cs.meta
  - Assets/ColorBlockJam/Domain/Commands/*.cs
  - Assets/ColorBlockJam/Domain/Commands/*.cs.meta
  - Assets/ColorBlockJam/Domain/Movement/*.cs
  - Assets/ColorBlockJam/Domain/Movement/*.cs.meta
  - Assets/ColorBlockJam/Domain/Exit/ExitRules.cs
  - Assets/ColorBlockJam/Domain/Picking/*.cs
  - Assets/ColorBlockJam/Domain/Picking/*.cs.meta
  - Assets/ColorBlockJam/Domain/Hooks/*.cs
  - Assets/ColorBlockJam/Domain/Hooks/*.cs.meta
  - Assets/ColorBlockJam/Domain/Session/*.cs
  - Assets/ColorBlockJam/Domain/Session/*.cs.meta
  - Assets/ColorBlockJam/Gameplay/Boosters/**
  - Assets/ColorBlockJam/Gameplay/Boosters.meta
  - Assets/ColorBlockJam/Gameplay/UI/**
  - Assets/ColorBlockJam/Gameplay/UI.meta
  - Assets/ColorBlockJam/Gameplay/Board/*.cs
  - Assets/ColorBlockJam/Gameplay/Session/*.cs
  - Assets/ColorBlockJam/Gameplay/Effects/*.cs
  - Assets/ColorBlockJam/Gameplay/Input/*.cs
  - Assets/ColorBlockJam/Gameplay/Composition/GameAttemptLoader.cs
  - Assets/ColorBlockJam/Gameplay/Composition/GameSceneInstaller.cs
  - Assets/ColorBlockJam/Gameplay/ColorBlockJam.Gameplay.asmdef
  - Assets/ColorBlockJam/Runtime/Boosters/*.cs
  - Assets/ColorBlockJam/Runtime/Boosters/*.cs.meta
  - Assets/ColorBlockJam/Presentation/Boosters/**
  - Assets/ColorBlockJam/Presentation/Board/BlockPiece.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockPieceFactory.cs
  - Assets/ColorBlockJam/Presentation/Board/ModularBlockView.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockArrowMarkers.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockIceOverlay.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockIceOverlays.cs
  - Assets/ColorBlockJam/Presentation/Board/IceCounterLabels.cs
  - Assets/ColorBlockJam/Player/PlayerSaveService.cs
  - Assets/ColorBlockJam/Player/PlayerDataInstaller.cs
  - Assets/ColorBlockJam/Player/IBoosterInventory.cs
  - Assets/ColorBlockJam/Player/IBoosterConsumer.cs
  - Assets/ColorBlockJam/Player/IBoosterConsumer.cs.meta
  - Assets/ColorBlockJam/Player/BoosterConsumeResult.cs
  - Assets/ColorBlockJam/Player/BoosterConsumeResult.cs.meta
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/**
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/**
  - Assets/ColorBlockJam/Tests/Player/EditMode/PlayerSaveServiceTests.cs
  - Assets/ColorBlockJam/Tests/Player/EditMode/BoosterConsumptionTests.cs
  - Assets/ColorBlockJam/Tests/Player/EditMode/BoosterConsumptionTests.cs.meta
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/ColorBlockJam.Gameplay.EditModeTests.asmdef (add the ColorBlockJam.Player reference only)
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/ColorBlockJam.Gameplay.PlayModeTests.asmdef (add the ColorBlockJam.Player reference only)
  - Assets/ColorBlockJam/Tests/PlayMode/BoosterEffectViewTests.cs
  - Assets/ColorBlockJam/Scenes/Game.unity
  - Assets/ColorBlockJam/Data/Boosters/Definitions/Hammer.asset
  - Assets/ColorBlockJam/Art/UI/Icons/Gameplay/target.png
  - Assets/ColorBlockJam/Art/UI/Icons/Gameplay/target.png.meta
  - Assets/ColorBlockJam/Art/UI/Panels/bg_popup_info_holder.png.meta
  - Assets/ColorBlockJam/Art/UI/Panels/bg_gameplay_time_holder.png.meta
  - Assets/ColorBlockJam/Art/UI/Buttons/Btn_booster_normal.png.meta
  - Assets/ColorBlockJam/Art/Shaders/TimeFreezeOverlay.shadergraph
  - Assets/ColorBlockJam/Art/Shaders/TimeFreezeOverlay.shadergraph.meta
  - Assets/ColorBlockJam/Art/Materials/TimeFreezeOverlay.mat
  - Assets/ColorBlockJam/Art/Materials/TimeFreezeOverlay.mat.meta
  - PROJECT.md
  - docs/Color-Block-Jam-PRD.md
  - docs/Color-Block-Jam-Visual-References.md
  - docs/UI-Asset-Import.md
  - docs/Gameplay-Drag-And-Gate.md
  - docs/Booster-Gameplay.md
  - docs/verification/booster-gameplay/**
  - production/tasks/TASK-20260927-004448215-54655902-booster-gameplay-flow/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Hub/**
  - Assets/ColorBlockJam/Runtime/Levels/**
  - Assets/ColorBlockJam/Data/Levels/**
  - Assets/ColorBlockJam/Data/Board/**
  - Assets/ColorBlockJam/Editor/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Resources/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Art/Models/**
  - Assets/ColorBlockJam/Player/Storage/**
  - Assets/ColorBlockJam/Player/PlayerSnapshot.cs
  - Every scene, ScriptableObject, material, importer or assembly definition not explicitly listed above
  - Editing predecessor task contracts, claims, or their evidence
  - New packages/assemblies, save-format changes, authored-level migrations, engine upgrades
  - Concurrent code writers in this shared Plastic workspace; deleting existing test artifacts or dirty files
  - Checkin before required verification passes, or inclusion of unrelated changes; shelve, push, build, publishing or task closure without separate explicit instruction
  - Modifying the CBJ Template reference project or copying its unreviewed particle dependencies
- Serialized assets: exact paths below; all require the plan/target gate below
  - Asset: Assets/ColorBlockJam/Scenes/Game.unity
    Sole writer: active implementer, booster HUD/effect/definition references and UI hit-filter wiring only, after Ice owner releases
    Matching `.meta` owner: active implementer; preserve existing GUID and meta bytes
  - Asset: Assets/ColorBlockJam/Data/Boosters/Definitions/Hammer.asset
    Sole writer: active implementer, whole-block targeting/copy only
    Matching `.meta` owner: active implementer; preserve existing GUID and meta bytes
  - Asset: Assets/ColorBlockJam/Gameplay/ColorBlockJam.Gameplay.asmdef
    Sole writer: active implementer, Player assembly reference only
    Matching `.meta` owner: active implementer; preserve existing GUID and meta bytes
  - Asset: Assets/ColorBlockJam/Art/UI/Icons/Gameplay/target.png
    Sole writer: active implementer, import the supplied source as a fixed aspect icon
    Matching `.meta` owner: active implementer
  - Asset: Assets/ColorBlockJam/Art/UI/Panels/bg_popup_info_holder.png.meta
    Sole writer: active implementer, measured nine-slice border/import settings only
    Matching `.meta` owner: active implementer
    Narrowed 2026-09-27: TASK-20260926-235915698-c5020d50-hub-settings-overlay already applied border 43/35/42/26 and locks it in `Tests/UI/EditMode/SettingsPopupAssetTests.cs`; this task does not write this meta and uses its existing border
  - Asset: Assets/ColorBlockJam/Tests/Gameplay/EditMode/ColorBlockJam.Gameplay.EditModeTests.asmdef
    Sole writer: active implementer, the ColorBlockJam.Player reference only
    Matching `.meta` owner: none; the `.meta` stays byte-identical
  - Asset: Assets/ColorBlockJam/Tests/Gameplay/PlayMode/ColorBlockJam.Gameplay.PlayModeTests.asmdef
    Sole writer: active implementer, the ColorBlockJam.Player reference only
    Matching `.meta` owner: none; the `.meta` stays byte-identical
  - Asset: Assets/ColorBlockJam/Art/UI/Panels/bg_gameplay_time_holder.png.meta
    Sole writer: active implementer, measured nine-slice border/import settings only
    Matching `.meta` owner: active implementer
  - Asset: Assets/ColorBlockJam/Art/UI/Buttons/Btn_booster_normal.png.meta
    Sole writer: active implementer, measured nine-slice border/import settings only
    Matching `.meta` owner: active implementer
  - Asset: Assets/ColorBlockJam/Art/Shaders/TimeFreezeOverlay.shadergraph
    Sole writer: active implementer, bounded screen-edge frost presentation only
    Matching `.meta` owner: active implementer
  - Asset: Assets/ColorBlockJam/Art/Materials/TimeFreezeOverlay.mat
    Sole writer: active implementer, the preceding frost graph's shared material only
    Matching `.meta` owner: active implementer
- Other active writers/worktrees checked: Planning saw Ice lock for TASK-20260926-231349009-945d78fe-ice-visibility (Codex chat 01a0dffa-1f1f-7530-a600-152f48a7cdf3) and player-data lock for TASK-20260926-235915696-6b7f82f4-player-data-currencies (session “Hub sahnesini oluşturma [5c52bd]”). Shared gameplay/presentation/Game scene and player save seams overlap. Parent's two inventory delegates were read-only and released ownership. There is no Git worktree: preserve the shared Plastic workspace.
- Execution dependency: TASK-20260926-235915699-78356740-hub-pre-level-launch supplies the stable launch-request/selection/purchase handoff after its existing player-data -> Hub Home -> Settings chain. Also require Ice task's stable handoff and claim release before touching its shared paths/Editor. Revalidate Arrow successors and other live claims before dispatch; a completed contract alone is not release.
- Queue policy: auto-dispatch-after-owner-release
- Reuse read-only: BoosterCatalog and all current booster definitions except the specified Hammer change; Models/Effects prefabs and their materials; Data/UI/BoardOverlayPanelSettings.asset (already 1080 x 1920). No new PanelSettings is needed.
- Queue execution: Keep authorized work queued under canonical agent-orchestration. Read-only planning may continue while predecessors run. Do not duplicate unfinished Hub UI, take over its chain, or treat missing files as permission to rebuild them. Revalidate released files and exact Editor identity, then dispatch automatically without another “continue”.
- Approved source amendment (2026-09-27): the user approved adding ExitRules.cs, BlockIceOverlay.cs, BlockIceOverlays.cs and IceCounterLabels.cs after the read-only review identified authored-footprint assumptions. Keep LevelModel/BlockDefinition as immutable source data, expose copied runtime footprints through the board reader, and extend the existing Ice pools/labels for bounded fragment recomposition. Preserve these existing files' .meta bytes. This adds no serialized target.
- Approved target amendment (2026-09-27T14:05Z, user answer "Approve (Recommended)" in Claude session [f1ed7e]): add only the `ColorBlockJam.Player` reference to the two Gameplay test asmdefs listed above, with their metas unchanged. No new assembly, package or other asmdef change.
- Approved target amendment (2026-09-27T22:25Z, user answer "Add it and show the popup (Recommended)" in Claude session [f1ed7e]): add the `ColorBlockJam.UI` reference to `Assets/ColorBlockJam/Gameplay/ColorBlockJam.Gameplay.asmdef` (meta unchanged). In-game hosting of the shared booster purchase popup (`UI/Purchase/**`, owned by the Hub pre-level task, used read-only) is added under `Paused(Purchase)` through an `IInGamePurchaseHost` implementation in `Gameplay/Boosters/**` and a HUD template in `Gameplay/UI/**`. Buy goes through `IBoosterShop`, with no second economy and no auto-cast. This implements AC-12 in this task.
- Execution snapshot (2026-09-27T01:43Z): Ice released its claim while explicitly withholding a stable implementation handoff; its D-001/D-002 verification is still pending. Player-data released its claim after verification but records an out-of-scope setup regression requiring its own resolution. Hub Home now has the live claim for session “Hub sahnesini oluşturma [5c52bd]”; Settings and pre-level launch remain dependencies. TASK-20260927-010019989-11b501c1-gate-exit-fragments has a live claim and owns BoardPresenter integration. Wait for these owners' stable handoffs and revalidate Arrow before production dispatch. Claim disappearance alone is insufficient.

## Acceptance scenarios

1. Given a running level, the bottom bar shows Time Freeze, Rocket, Hammer and Vacuum in that order, with current shared stock/count or plus badges, correct icon/background aspect, selected/disabled states and safe-area spacing; touches never move the board behind the bar. Verify at 1080 x 1920 and 1080 x 2400.
2. Given an eligible booster, its information UI shows background, title, icon and correct effect description. Rocket shows reference-style per-cell crosshairs and cancel. Hammer selects whole blocks. Vacuum permits color selection with no crosshairs; its selected silhouette and existing suction presentation remain readable.
3. Given main time 0:03, activating Time Freeze once holds 0:03 and starts a separate 10-second countdown; after four Playing seconds it shows six. Expiry resumes the held main time using only the remaining delta of the expiry frame; no +10 is added. Reactivation while active is refused without spending.
4. Given pause, saving, background or opening hold, neither normal gameplay nor the appropriate clocks advance. Resume preserves time. Main-time expiry fails once; a last clear processed in the command phase wins first. Restart/teardown reset freeze and opening state.
5. Given an L fixture, Rocket deletes exactly the tapped cell. Removing its bridge leaves two independent connected blocks; removing a suitable end leaves an I. Coordinates/color are preserved, block count and remaining-win count are correct, and no authored JSON/model is mutated.
6. Given Rocket fragments (including repeated uses and learned shapes), each moves, collides, exits and receives later boosters independently with the applicable inherited effect/axis state. IDs and replay outcomes are deterministic; runtime cells drive picking/movement/presentation instead of stale authored footprints. Removing a bridge is not a BlockExited or a whole-block-clear notification.
7. Given the documented invalid targets (including final/only cell, removed/mid-exit or otherwise ineligible block), Rocket cannot debit, mutate or emit a hit. Starting candidate generation uses the same rule except that split-causing cells are valid.
8. Given a valid block, Hammer removes exactly that entire block regardless of footprint, including active Ice/Arrow content; other blocks are unchanged. It raises BlockRemoved once, never BlockExited, and plays the existing 3D hammer hit.
9. Given a color selected for Vacuum, every remaining block of that color disappears in one command, other colors remain, and each actual removal raises one BlockRemoved. No crosshair appears. Matching visuals shrink toward the existing nozzle; a final cast is retained only within the existing bounded Won Finish.
10. Given any valid use, capture its immutable intent, suspend into Paused(Saving), persist exactly one stock debit, then apply once before the timer in the first allowed command phase. A failed save changes neither stock nor board; duplicate/stale callbacks cannot double-apply, and backgrounding cannot auto-resume. Cancellation/invalid targeting before commit spends nothing.
11. Given a committed pending effect, Home/Restart discards it without refund or replay as documented. Purchases persist, restart does not reseed stock, and an attempt never mutates its definition assets. Board state remains unchanged during serialization/file I/O.
12. Given zero stock, the HUD opens the existing-style purchase presentation through Paused, uses IBoosterShop for the same configured coin offer, rejects duplicate submissions and refreshes stock on success without automatically casting. Closing resumes only if foreground rules allow. Reuse predecessor purchase assets/ports without implementing a second economy.
13. Given Hub's existing pre-level selectors, neither/either/both selections arrive via its launch request. Prepare/warm/validate while Home and commit all applicable selected charges together; Extra Time adds its configured +10 once before Rocket. Both share the normal application handlers/stock, and a no-candidate Rocket is neither applied nor charged.
14. Given an injected random seed, starting Rocket chooses a repeatable eligible target once, including a split-causing cell when selected. Opening icons/effects hold countdown/ordinary board/HUD input, pause preserves progress, double Play cannot duplicate debit or reroll, and backgrounded commit waits for explicit Play. Direct Game Play, Restart and direct Next Level have no starting selection.
15. Given pause, cancellation, Home, restart, result or scene unload, owned crosshairs, frost, banner, tweens, subscriptions, tasks and pooled objects stop or return at their defined stop point. No separate Update owner, Time.timeScale pause, fire-and-forget task or lifecycle leak is introduced.
16. Given worst-case supported Rocket fragmentation, Home preparation warms fragment roots, piece/effect/target-marker pools and capacities from reachable cells, including later purchased charges. Repeated casts/drag/timer display in Playing do not instantiate or grow pools; focused allocation tests and an Editor Profiler sample report actual evidence.
17. Given existing ice/arrow/barrier and drag/gate/command suites, they continue to pass except assertions explicitly superseded by this contract. Barrier flips once per actual cleared block; Ice counters still count actual exits only. Test source fixtures rather than changing authored player levels for proof.
18. Given final review, the accepted new split and whole-block rules, precise changed files, lifecycle/stock rules, measured UI borders and test/visual results are documented concisely. Only the listed targets changed; existing dirty work and all asset GUIDs were preserved.

## References and constraints

- Story/GDD/ADR/design/log/image: User request and continuation on 2026-09-27: implement the four boosters, top information background, existing lower booster navigation, Hub starting Rocket/Extra Time, reuse 3D models/infrastructure, optional Shuriken references; create task first, then implement in a new session with orchestration.
- Screenshot: C:/Users/e-fur/AppData/Local/Temp/codex-clipboard-f7d00705-ed4a-4358-914a-de25e30f7a80.png (target appearance, not current-project evidence).
- Reticle: C:/Users/e-fur/Downloads/target.png (512 x 512 RGBA; black RGB with varying alpha, visible bounds 23,24 to 489,488; render its alpha silhouette visibly against blocks).
- Documents: PROJECT.md lifecycle, architecture, DEC-74/75/120; docs/Color-Block-Jam-PRD.md section 7.8, AC-5.7, AC-BST and save rules; docs/Color-Block-Jam-Visual-References.md references 01–04, 09–13, 16, 18; docs/Hub-And-Player-Data-Design.md; docs/Gameplay-Drag-And-Gate.md; docs/art/boosters/motion/README.md; docs/art/shared-art/Organization.md.
- Reference project (read-only): C:/Users/e-fur/Documents/Repos/CBJ Template. Existing local booster effects suffice initially. Optional particle assets may be inspected, but adding their serialized dependency closure needs a separate explicit target approval; it is not required to finish this task.
- Unity version/platform: 2022.3.62f2; Android/iOS portrait, 1080 x 1920 and 20:9.
- Package/architecture constraints: current Onity, Input System, PrimeTween, UI Toolkit, Newtonsoft and shared materials; no dependencies or framework migration. Domain remains Unity-free. Existing CommandExecutor stays the sole gameplay mutation entry point. One session tick owns all new behavior.
- Explicit out of scope: Hub reconstruction, new obstacle mechanics, full game menus/result screen design, level editor/solver changes, save schema migration, new 3D modeling, optional imported VFX and releases. A new timer's terminal hook must work with the existing session; lack of a separate finished result UI is not permission to build an unrelated screen.
- Duplicate search result: Hub pre-level launch explicitly excludes spending/applying starting boosters (its AC-8 and non-goals). Vacuum suction task TASK-20260924-125312690-d6a25edf-vacuum-suction covers art only and excludes gameplay. This task adds a distinct playable application flow and does not replace either.

## Verification and risk

- Focused test path/command: existing ColorBlockJam.Gameplay.EditModeTests and ColorBlockJam.Gameplay.PlayModeTests; focused Player save/consumption tests; Tests/PlayMode/BoosterEffectViewTests.cs. Discover installed runner schema and filter new/affected fixtures first, then the affected existing suites once.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean recompile and error-free owned Game/Hub flow after queued work settles; preserve unrelated Console evidence.
- EditMode: countdown delta/expiry; fragment connectivity/identity/occupancy/commands/replay; targeting and effect rules; stock commit/failure/duplicate/background paths; starting selection/order/seed.
- PlayMode: actual Game composition, UI hit blocking, scene wiring, HUD targeting/cancel, existing 3D casts, lifecycle cleanup and Hub-to-Game opening.
- Manual/built-player/profiler/device evidence: required Editor visual captures at both portrait sizes, actual touch-equivalent flow and focused allocation/Profiler evidence. Android/IL2CPP/device acceptance remains a release check requiring separate build authorization; do not claim it from Editor evidence.
- Risk: high
- Approval already granted (exact target only): On 2026-09-27 the user answered “evet devam et ve alt ajanlara işi devret orkestra yap” to the combined plan, the nine listed serialized target sets, and the four-source amendment listed above. This explicitly approves the stock/fragment boundary, all nine listed scene/asset targets, the four additional C# files, implementation and sequential delegated verification. Rocket still rejects its final/only cell; Hammer removes the whole block including active Ice. No repeated plan/file approval is required within this scope. Predecessor ownership, unrelated task approvals, build/publishing and explicit closure gates remain unchanged. The later scoped checkin authorization is recorded below.
- Commit/checkin authorization: On 2026-09-27 the user ordered “booster bitince commit atmayı unutma bunu da görev ekle”. After the Booster implementation and all required verification pass, perform a scoped Plastic SCM checkin of the verified task changes, matching metas and task documentation without requesting the same checkin approval again.
- Final checkin procedure: Inspect current Plastic status, exact diffs, the captured implementation baseline and active ownership before selecting explicit file paths. Account for predecessor changes already present in shared files; do not include unrelated work or discard edits to manufacture a clean checkin. If unrelated or separately owned changes cannot be isolated safely, report the exact files and dependency before widening scope. Do not use checkout, undo, shelve or a forced update to bypass that boundary.
- Checkin evidence: Use a nonempty concise English comment stating the change and verification. Record the successful changeset ID, included file list and verification status in this task's completion note, and report the changeset to the user. Reconcile reviewed/evidence identities with the resulting Plastic revision under the lifecycle lock; unchanged verified bytes do not require repeated tests solely because of the checkin.
- Current invocation permission: implement and orchestrate this approved contract after predecessor stable handoffs and release, then perform the authorized verified Booster checkin; no build, publishing or task closure
- Handoff destination: new Codex chat -> canonical agent-orchestration -> implement-task TASK-20260927-004448215-54655902-booster-gameplay-flow -> independent verifier -> task-cycle only if defects are found -> authorized scoped checkin. Leave the verified task READY_TO_CLOSE after recording the checkin; explicit user closure is required.
- Planning preflight: READY WITH WARNINGS for file-only task authoring. CLI 1.0.0-beta.11 verified, Unity version gate 2022.3.62f2, Plastic cs:11. MCP discovery exposed ColorBlockJamCase2026@99077e4df1ecfaa4 but reloads are ongoing and exact live routing was not claimed or used. No live Editor state, test pass, or runtime readiness is asserted. Implementation must freshly prove the exact session and queue its Editor slot.
- Planning write boundary: only this contract and its exclusive task-directory allocation; no production/serialized changes. Parent used read-only design/code delegates. No source tests were run for task authoring.

## Stop conditions

- Before protected writes, the combined plan/target decision above is absent or rejects a target.
- A live predecessor owns a required path or the Editor slot: keep WAITING_FOR_OWNER, observe stable handoff and claim release, then revalidate and continue automatically.
- Hub's existing launch/purchase contract is unavailable or unfinished: retain the dependency rather than duplicating its scope.
- Required implementation expands beyond listed paths/public scope or needs a package, new assembly, save migration, model edit or unlisted serialized target.
- The new fragment model cannot preserve command/ice/axis/exit/lifecycle correctness with bounded capacity; resolve the concrete design issue before dependent writes rather than weakening acceptance.
- Exact Unity identity is unproven for a requested live operation; preserve unsaved scene work and do not select another project.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:5e72db61a10ab3140d9de6c4970f2b455ff5889e6edf82577dbd57a28a6f6c3b
- Evidence fingerprint: sha256:1de70517b3a194c7966a2934dcaa98dad519cada93512fb4bc2945769449c757
- Reviewed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020
- Attempt count: 4
- Last feedback: 2026-09-28T12:35Z user: "Intake pozisyonunu hedef alsın parçalar ve küçülerek ilerlesin vaccum da ek olarak magnet efekti de çok aşağıdaydı onu da yukarı çıkardım intake pozisyonuna ve güncelleme yap gereksiz booster modeli vaccumu sil ve prefab yap bu visual'ı effect içindeki ve yıldırımı düz çizgi ile değiştir" (D-003, D-004 revised). Earlier: 2026-09-28T07:30Z user: "vaccum cleaner tam süpürgenin ucunu hedef almalı vfx ve block pozisyonu olarak ve çektiği blocklar da beyaz outline açılmalı bunları düzelt" (D-003, D-004). Earlier: 2026-09-28T04:05Z Hub session [5c52bd] live-Editor report: the BoosterSerializedAssetTests wiring failure (pre-fix build, D-001), the final Vacuum Finish test failing in the throttled Editor (D-002), and the Gameplay PlayMode count 130 to 125 (explained: HeldOutlineTests 9 to 3 in the held-outline rewrite, plus GameSceneUiInputTests; nothing dropped). Earlier: 2026-09-28T02:59Z held-outline session report (via task-bug): BoosterSerializedAssetTests.GameScene_WiresTheHudDocument_EventSystem_AndInstallerFields fails at cs:16 against the Game-scene input fix (D-001, verified). Earlier: 2026-09-28 user orders in Claude session [f1ed7e]: "bu oturum bütün işlerini bitirdi gibi gözüküyor kapat artık tamamlayıp" and "bitirip projeyi artık toparlamalısın" (close the task after finishing); "Combined checkin (Recommended)"; the CBJ Template Shuriken particles "Add now, before checkin" with Cut Shatter for Rocket (DEC-152); follow-up flexibility work split into TASK-20260928-015130392-6c288254-booster-availability-extension ("Checkin first, then new task").
- Waiting reason: user_input
- Action required: accept AC-1, AC-2 and AC-16 (or run the Editor captures at 1080 x 1920 and 1080 x 2400 and the Editor Profiler sample), or order the close again with /task-done; D-001 and D-002 are VERIFIED and every other criterion is PASS
- Blocker: none. Editor captures at 1080 x 1920 and 1080 x 2400 (AC-1, AC-2) and the Editor Profiler sample (AC-16) were not performed; device checks remain release checks.
- Next action: None
- Closed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | UNPROVEN | Automated: GameplayHudLayoutTests 9/9 and GameplayHudCompositionTests 5/5 pass; Editor captures at 1080 x 1920 and 1080 x 2400 not performed | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-2 | UNPROVEN | Automated: BoosterBarPresenterTests 13/13, RocketReticlePresenterTests 10/10 and BoosterEffectViewTests 21/21 pass; visual review of banner, crosshairs and silhouette not performed | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-3 | PASS | Automated: AttemptCountdownTests 16/16, TimerPresenterTests 6/6 (copy run 2026-09-28) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-4 | PASS | Automated: BoosterIntentSessionTests 21/21, OpeningTests 17/17, AttemptCountdownTests 16/16 | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-5 | PASS | Automated: Domain split and handler tests in Gameplay EditMode 522/522 and Player EditMode 88/88 at cs:17 | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-6 | PASS | Automated: Domain fragment identity, move, exit and replay tests (Gameplay EditMode 522/522 at cs:17); BoardPresenterBoosterTests 11/11 | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-7 | PASS | Automated: target rule and refused-target tests (Gameplay EditMode 522/522 at cs:17), BoosterIntentSessionTests 21/21 | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-8 | PASS | Automated: Hammer handler tests (EditMode) and BoardPresenterBoosterTests 11/11 (cast and hit burst) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-9 | PASS | D-002 fixed: the final Vacuum Finish test also accepts the proven long-frame timeout (copy run under a forced 1 s frame passes; Gameplay PlayMode 125/125 at cs:17); Automated: Vacuum handler tests (EditMode) and BoardPresenterBoosterTests 11/11 including the bounded Won Finish | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-10 | PASS | Automated: BoosterIntentSessionTests 21/21, BoosterConsumptionTests 11/11 | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-11 | PASS | Automated: session Home/Restart discard tests, BoosterConsumptionTests 11/11 | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-12 | PASS | Automated: InGamePurchaseHostTests 9/9, InGamePurchasePopupTests 7/7, GameSceneBoosterCompositionTests 10/10 | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-13 | PASS | Automated: GameSceneBoosterCompositionTests 10/10 (launch requests with neither, either and both boosters) and OpeningTests 17/17; the live Hub-to-Game check was not performed | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-14 | PASS | Automated: OpeningTests 17/17 (seeded target, hold, double Play), GameSceneBoosterCompositionTests 10/10 (background during the Home commit) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-15 | PASS | Automated: BoardPresenterLifecycleTests 12/12, BoosterEffectViewTests 21/21 (suspend and stop of casts and particles), frost and HUD teardown tests; Task and Observable Tracker checks not performed | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-16 | UNPROVEN | Automated: BoardPresenterBoosterAllocationTests 8/8 (0 B with every cast and particle playing), HudPresenterAllocationTests pass; the Editor Profiler sample was not performed | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-17 | PASS | Automated at cs:17 with the D-001 test update (copy runs 2026-09-28): Gameplay EditMode 522/522, Gameplay PlayMode 125/125, Player EditMode 88/88, BoosterEffectViewTests 21/21, and Gameplay PlayMode 125/125 again with the D-002 fix; BoosterSerializedAssetTests line 32 carries the folder-restructure session's pending UXML path update, which only its move satisfies; earlier windowed 55/55 after the Game-scene input fix (regression test GameSceneUiInputTests), EditMode 629/629; superseded assertions and foreign order-dependent failures listed in docs/verification/booster-gameplay/README.md | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| AC-18 | PASS | Diff and documentation review: docs/Booster-Gameplay.md (B-1 to B-33), PROJECT.md DEC-146 to DEC-152, docs/verification/booster-gameplay/README.md | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | task_regression:ac-17:game-scene-wiring-test-expects-shared-default-ui-actions | VERIFIED | TASK_REGRESSION | AC-17 | GameScene_WiresTheHudDocument_EventSystem_AndInstallerFields asserts the shared DefaultInputActions GUID; Game.unity intentionally holds empty module actions | test now asserts the 11 module references stay empty; Gameplay EditMode 542/542 at cs:16 and 522/522 at cs:17 (target test Passed) in the isolated copy | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| [D-002](defects/D-002.md) | ac_failure:ac-9:final-vacuum-finish-test-fails-on-throttled-editor | VERIFIED | AC_FAILURE | AC-9 | AFinalVacuumRunsInsideTheSessionsBoundedWonFinishAndTeardownReturnsItsRootsAndCast: 'Teardown waits for the final presentation' fails in the live Editor at 97% paged memory; passes in the isolated copy | red: a forced 1 s frame in the copy reproduces the exact failure; green: the fixed test passes under it and Gameplay PlayMode is 125/125 without it; live-Editor re-run offered by the Hub session | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e1f82deb1050176439076f332e76f46bc41f976bce27f6e33a8e6d1733c6a020 |
| [D-003](defects/D-003.md) | ac_failure:ac-2:vacuum-suction-particles-not-at-nozzle | VERIFIED | AC_FAILURE | AC-2 | Magnet Effect bolts spread beside the hose, not at the nozzle: the ring was edge-on to the camera and radius 1.57; the template's world z -5 drift also carried them below it | isolated copy (live-equal bytes): PlayMode 49/49 incl. the authored-on-intake, straight-line and ring asserts; Game-scene capture shows straight lines meeting at the nozzle mouth | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:9ab5fa15875052a3b4c12222d17d2e038e5a2485eed9cd7d50f1fd2ae9b22548 |
| [D-004](defects/D-004.md) | ac_failure:ac-9:vacuumed-blocks-vanish-before-nozzle | VERIFIED | AC_FAILURE | AC-9 | Pulled blocks must target the intake and shrink as they advance (user clarification 2026-09-28) | scale and position share Ease.InQuad, so size equals the remaining share of the way; isolated copy PlayMode 49/49 (size follows remaining distance within 0.01, every target outlined, 0 B); Game-scene capture | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:9ab5fa15875052a3b4c12222d17d2e038e5a2485eed9cd7d50f1fd2ae9b22548 |

### Transition history

- 2026-09-27 — Created — planning — contract authored from user request, repository evidence and two read-only inventories; automatic new-session dispatch requested, predecessor/protected-target gates retained.
- 2026-09-27T01:43Z — Approved amendment — planning — user's combined approval recorded; four exact source paths added without new serialized targets. Initial authoring checksum mismatch independently confirmed (authority was sha256:48238fd95c39d29613e7876f93b41dd5403f6ed57fd89755a44aa1b3e0237a7c); fingerprint recomputed for the approved authority. No production write or implementation attempt; queue remains WAITING_FOR_OWNER.
- 2026-09-27T02:04Z — Preparation handoff — planning — delegated implementation design, pixel measurements and independent focused QA plan completed with no production/test/asset writes or test runs. The required two test asmdef references are outside the approved nine target sets, so their exact-target question remains pending. Existing scope remains approved; task stays ready, attempt 0, empty defect ledger. Ice and Hub retain active ownership; Gate released its stable source/test handoff.
- 2026-09-27T02:31Z — Final checkin authorized — planning — the user requested a commit when Booster is complete. Added the verified, task-scoped Plastic checkin and changeset handoff to completion work; no checkin is performed now. This does not approve the pending two test asmdef targets or task closure. Contract authority fingerprint recomputed; attempt 0 and the empty defect ledger are preserved.
- 2026-09-27T14:20Z — Ownership transfer and start — in_progress — Claude session [f1ed7e] acquired the lifecycle lock (owner token recorded in Library/ccgs) after the user's takeover decision; the two test asmdef targets were approved; bg_popup_info_holder.png.meta was narrowed out (Hub Settings owns its border). Read-only subsystem and CBJ Template maps completed; Unity preflight READY WITH WARNINGS (instance ColorBlockJamCase2026@99077e4df1ecfaa4, PID 45732, root and 2022.3.62f2 read back). Contract authority fingerprint recomputed. Empty defect ledger preserved.
- 2026-09-28T02:53Z — Implementation complete and checked in — in_progress — cs:16 (374 explicit paths: the Booster files, the template particles of DEC-152, the Game-scene UI input fix with GameSceneUiInputTests, and the carried Ice, held-outline, exit-fragment, shape-resolver and Vacuum-sprite dependencies; compile-proven on cs:15). Authority fingerprint recomputed after the AC-12 amendment; reviewed revision and criteria recorded from the 2026-09-28 copy runs. Empty defect ledger preserved.
- 2026-09-28T02:52Z — Closed (user_confirmed) — closed — the user's close order ("bu oturum bütün işlerini bitirdi gibi gözüküyor kapat artık tamamlayıp"; "bitirip projeyi artık toparlamalısın"). AC-1, AC-2 and AC-16 stay UNPROVEN (captures and the Profiler sample not performed) and are preserved, not verified; the other 15 criteria keep their recorded automated evidence. Empty defect ledger preserved.
- 2026-09-28T02:59Z — Reopened — implementation — task-bug routed the held-outline session's report: the task's own wiring test contradicts its cs:16 Game-scene input fix (TASK_REGRESSION on AC-17). Recorded D-001 (FIXING); AC-17 STALE until Gameplay EditMode is re-run. Lock re-acquired by Claude session [f1ed7e].
- 2026-09-28T03:08Z — D-001 verified — waiting — BoosterSerializedAssetTests now asserts the Game-scene module's 11 action references stay empty (fresh default actions), matching the cs:16 input fix; Gameplay EditMode 542/542 in the cs:16 copy. Reviewed revision recomputed over the cs:16 file list with other sessions' pending edits read at cs:16 (scratchpad slice/reviewed_revision_cs16.py). AC-1, AC-2 and AC-16 remain UNPROVEN, so the task waits for user acceptance or a close order instead of re-closing automatically. Changes not checked in.
- 2026-09-28T03:40Z — Evidence refreshed at cs:17 — waiting — the held-outline task checked in cs:17 (Highlight Plus; BoardPresenter now switches the outline on held and Vacuum-target roots, GameSceneInstaller and Game.unity changed). Re-verified in the isolated copy at cs:17 with the pending D-001 test update: Gameplay EditMode 522/522, Gameplay PlayMode 125/125, Player EditMode 88/88, BoosterEffectViewTests 21/21. Reviewed revision recomputed at cs:17 (scratchpad slice/reviewed_revision_head.py; removed outline files hash as missing). AC-1, AC-2 and AC-16 stay UNPROVEN; the D-001 files are still not checked in.
- 2026-09-28T04:05Z — Hub report triaged — in_progress — CONFIRMED_REUSE D-001 without reopening: the live-Editor full run failed with the pre-fix assertion text, which the reviewed bytes no longer contain; the single run passed. NEW_DEFECT D-002 (AC-9 FAIL in the throttled live Editor). The PlayMode count question is answered with no defect. The PanelSettings log comes from no Booster test. Attempt 3 starts.
- 2026-09-28T04:30Z — D-002 verified — waiting — CONFIRMED_ROOT_CAUSE: the real-time Finish deadline (at most 0.8 s) ends the bounded Finish before any frame inside the game-time intake window when one frame is that long, which is the DEC-93 timeout. Red and green on a forced 1 s frame in the isolated cs:17 copy; Gameplay PlayMode 125/125 with the clean fix. Only the test changed (BoardPresenterBoosterTests.cs). Reviewed revision recomputed at cs:17, with other sessions' pending edits and folder moves read at cs:17 (scratchpad slice/reviewed_revision_at.py). Attempt 3 of 3 used. AC-1, AC-2 and AC-16 stay UNPROVEN; nothing is checked in.
- 2026-09-28T07:55Z — Reopened by user feedback — in_progress — task-bug routed the user's order "vaccum cleaner tam süpürgenin ucunu hedef almalı vfx ve block pozisyonu olarak ... bunları düzelt" (the outline half went to TASK-20260925-025854248-9383f38c-held-outline-exit-fragments). NEW_DEFECT D-003 (AC-2, suction particles beside the hose) and NEW_DEFECT D-004 (AC-9, pulled blocks vanish over the board); both FIXED_UNVERIFIED in VacuumEffectView.cs, BoosterEffectView.cs and the RemoveColor branch of BoardPresenter.cs, with regression asserts in BoosterEffectViewTests and BoardPresenterBoosterTests. Attempt 4 runs on the user's direct fix order past the three-attempt limit. Lock held by Claude session [a36df4]; the Hammer session [6dd47ee8] edits the cast launch point in the same files by hunk agreement.
- 2026-09-28T08:30Z — D-003 and D-004 verified — waiting — isolated-copy PlayMode 36/36 and 25/25 on bytes equal to the live workspace, plus a real Game-scene capture; the verified revision covers the six Vacuum-slice files only (the full review set was not recomputed because the Hammer session's cast-anchor edits in the same files are still pending). AC-1, AC-2 and AC-16 stay UNPROVEN as before, so the task waits for the user. Nothing checked in. Lock released.
- 2026-09-28T08:50Z — Verification refreshed — waiting — the Hammer session [6dd47ee8] finished its cast-anchor edits (GetVacuumLaunch puts the canister in front of the near wall tops, so the nozzle is no longer hidden) and reported a windowed copy run of 59/59 across BoardPresenterBoosterTests, BoardPresenterBoosterAllocationTests, GameplayHudCompositionTests, BoosterEffectViewTests and GameSceneBoosterCompositionTests, which include the D-003/D-004 asserts; all four Vacuum hunks were confirmed present in the final bytes. D-003/D-004 verified revision moved to those bytes (owned-sha256:fd340842…ee0a). Lock taken and released by [a36df4].
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
- 2026-09-28T13:25Z — Post-closure user order applied and verified — closed (kept closed; no reopen, per the user's close order) — the user's "Intake pozisyonunu hedef alsın parçalar ve küçülerek ilerlesin ... magnet efekti ... intake pozisyonuna ... gereksiz booster modeli vaccumu sil ve prefab yap bu visual'ı effect içindeki ve yıldırımı düz çizgi ile değiştir": D-004 revised (the scale shares the position's InQuad, so size follows the remaining distance), D-003 revised (Magnet authored on the intake, camera-facing, straight suction lines, the template's world z -5 drift removed). Explicit targets from that order: the unused `Art/Models/Boosters/Vacuum.fbx` and its mesh prefab moved to the trash (both under Plastic, cs:2 and cs:6); `VacuumEffect.prefab`'s Visual became the nested prefab `Prefabs/Boosters/Models/Vacuum.prefab`; new `Art/Textures/Particles/suction_line.png` on `Magnet.mat` (`lightning.png` now unused, kept); `SharedArtAssets` updates only the Hammer and Rocket models. Docs: PROJECT.md Performance Rule 1 and DEC-152, Art-Asset-PRD-Mapping, shared-art README, Booster-Gameplay. Isolated copy with live-equal bytes: PlayMode 49/49, EditMode SharedArtTests 3/3. Nothing checked in. Lock taken and released by [a36df4].
- 2026-09-28T13:55Z — Post-closure user order applied and verified — closed (kept closed) — the user's "vakum esnasında bütün bloğun beyaz olmasına gerek yok outline'ın açılması yeterli": `BoardPresenter.StartVacuum` no longer writes white to the tapped block; every Vacuum target keeps its colour and shows only the white outline. Test renamed to `AVacuumOutlinesEveryMatchingRootAndDrawsItIntoTheIntake` (asserts the tapped block keeps its tint and both targets are outlined). PROJECT.md Booster-presentation bullet and Booster-Gameplay B-20 updated. Isolated copy PlayMode 28/28 (capture, BoardPresenterBoosterTests, AllocationTests, HeldOutlineTests). Nothing checked in. Lock taken and released by [a36df4].
<!-- TASK-LIFECYCLE:END -->
