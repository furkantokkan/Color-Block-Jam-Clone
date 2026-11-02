# TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry — Game HUD top bar, pause menu and Retry confirmation

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry
- Supersedes: none
- Title: Game HUD top bar, pause menu and Retry confirmation
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity; development in the isolated copy %TEMP%/cbj-hud-4effc5 (never checked in)
- Base revision: Plastic cs:14 plus pending changes (about 3057), including Booster's uncommitted copy-back; copy baseline = Booster copy after its held-outline merge, 2026-09-28T00:38Z (manifest `hud-copy-baseline.sha256` in the session scratchpad)
- Task kind: UI
- Area: Game-scene HUD top bar, in-game pause menu, Retry confirmation, Game-to-Hub navigation
- Workflow: trio

## Objective and baseline

- Observable objective: In the Game scene the player sees the reference top bar (Level badge, Time panel with the countdown, orange Retry, blue Pause, coin holder) and can pause into the Settings popup with HOME, resume, return to Hub, and restart the level through a confirmation popup.
- Current behavior/evidence: Booster's HUD (`Gameplay/UI/GameplayHud.uxml`) shows only a centred timer pill, the freeze counter, the targeting banner and the booster bar. No Level label, Retry, Pause or coin display exists. `NoPauseOverlay` auto-resumes on foreground (DEC-116 interim). No Game-to-Hub navigation exists (`LevelSession.GoHome` only sets state). `Restart()`/`GoHome()` are illegal from Playing in the Domain transition table. Back only cancels targeting. The shared Settings popup already supports a HOME button (DEC-136). Research: `research/*.md`; design: `design.md`.
- Expected behavior: As `design.md` sections 1-8 and the acceptance scenarios below.
- Behavior that must remain unchanged: Booster's booster bar, targeting, banner content, purchase popup, opening hold, timer arithmetic and prebuilt timer strings; `SessionTransitions` and `LevelSession` semantics; Hub Settings popup look and behaviour on Hub; direct Play of Game; drag/gate/exit, ice, arrow, barrier; save rules (no refund/refill on Restart); 0 B per Playing tick; HUD-less scenes keep today's auto-resume.

## Ownership

- Owner/writer role: this session is the decision and lifecycle owner and orchestrator; one implementer writes the copy, then an independent verifier; a bugfixer only on verified failure; the owner performs the integration window and the checkin.
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Gameplay/Hud/**
  - Assets/ColorBlockJam/Runtime/Gameplay/Hud.meta
  - Assets/ColorBlockJam/Gameplay/Hud/** (pre-move equivalent; used only in the isolated copy or if the reorg has not run)
  - Assets/ColorBlockJam/Gameplay/Hud.meta (pre-move equivalent)
  - Assets/ColorBlockJam/UI/Game/TopBar/**
  - Assets/ColorBlockJam/UI/Game/TopBar.meta
  - Assets/ColorBlockJam/UI/Game/Overlays/**
  - Assets/ColorBlockJam/UI/Game/Overlays.meta
  - Assets/ColorBlockJam/UI/Game.meta (only if the reorg did not create it)
  - Assets/ColorBlockJam/Runtime/Gameplay/Session/IGameplayBackFallback.cs (new; pre-move Gameplay/Session/)
  - Assets/ColorBlockJam/Runtime/Gameplay/Session/IGameplayBackFallback.cs.meta
  - Assets/ColorBlockJam/UI/Game/GameplayHud.uxml (integration hunk 1 only; pre-move Gameplay/UI/GameplayHud.uxml)
  - Assets/ColorBlockJam/UI/Game/GameplayHud.uss (integration hunk 2 only; pre-move Gameplay/UI/GameplayHud.uss)
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameSceneInstaller.cs (integration hunk 3 only)
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameAttemptLoader.cs (integration hunk 4 only)
  - Assets/ColorBlockJam/Runtime/Gameplay/Session/DragInputRouter.cs (integration hunk 5 only)
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/ApplicationPauseRelay.cs (integration hunk 6 only)
  - Assets/ColorBlockJam/Art/UI/Panels/bg_home_coin_holder.png.meta
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/Hud/**
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/Hud.meta
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/Hud/**
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/Hud.meta
  - PROJECT.md (new DEC rows only)
  - docs/Game-Hud.md
  - docs/UI-Asset-Import.md (coin-holder row and Btn_levelBg_normal use only)
  - docs/Color-Block-Jam-Visual-References.md (new reference 21 and the Retry-popup text entry only)
  - docs/media/color-block-jam/user-references/21-game-hud-top-bar.png
  - docs/media/color-block-jam/user-references/21-game-hud-top-bar.png.meta (only if Unity creates one; docs is outside Assets, so none expected)
  - production/tasks/TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Domain/** and Assets/ColorBlockJam/Runtime/Domain/**
  - Assets/ColorBlockJam/Runtime/Gameplay/Session/LevelSession.cs (and pre-move Gameplay/Session/LevelSession.cs)
  - Every other Booster-owned file: Gameplay/UI/*.cs, Gameplay/Boosters/**, Gameplay/Board/**, Gameplay/Input/**, Gameplay/Effects/** (pre- and post-move)
  - Assets/ColorBlockJam/UI/Settings/** (shared popup; read-only reuse)
  - Assets/ColorBlockJam/UI/Purchase/** and Assets/ColorBlockJam/UI/Common/**
  - Assets/ColorBlockJam/Hub/**, Runtime/Hub/**, UI/Hub/**, Player/**, Runtime/Player/**
  - Assets/ColorBlockJam/Scenes/** (no scene edit expected; stop and ask if one becomes necessary)
  - Assets/ColorBlockJam/Data/** (panel settings, catalogs, levels, scene flow)
  - Assets/ColorBlockJam/Art/UI/Atlases/** and every sprite importer except bg_home_coin_holder.png.meta
  - Any shared-workspace write before Booster's checkin/release and the reorg's "restructure done" message
  - Writing into %TEMP%/cbj-booster-f1ed7e (Booster's copy)
  - New packages, assemblies, save-format changes, engine upgrades; Time.timeScale pauses; Update methods, coroutines, async void, static mutable state or static events in new code
  - Checkin before all required verification and the visual verdict pass; shelve, push, build or publishing; task closure without the lifecycle gates
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Art/UI/Panels/bg_home_coin_holder.png.meta
    Sole writer: this task's integration owner, through the live Editor TextureImporter API (border 24/24/25/24 as recorded in docs/UI-Asset-Import.md, spriteMeshType Full Rect), no other importer field
    Matching `.meta` owner: this task; GUID e1e3b04b16d42ee4d9e4c6ce5f4ac7c6 preserved
- Other active writers/worktrees checked: live locks at 2026-09-28T00:40Z: booster-gameplay-flow ([f1ed7e], owns every integration file), hub-pre-level-launch, home-tab-bar-locked-ends and home-currency-holders-authored ([5c52bd]), held-outline (released at 00:47Z). Folder-reorg session [7d9d8c] will move Gameplay/ -> Runtime/Gameplay/ and GameplayHud.uxml -> UI/Game/ after Hub and Booster check in, then open this task's window. No task covers the top bar, pause menu, Retry or Game-to-Hub navigation (duplicate search in `research/ownership.md`).
- Execution dependency: TASK-20260927-004448215-54655902-booster-gameplay-flow stable handoff with its checkin and lock release, then the reorg session's "restructure done" message, before any shared-workspace write. Development and verification in the isolated copy proceed now.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a running level at 1080 x 1920 and 1080 x 2400, the top bar shows the Level badge and Time panel (`Btn_levelBg_normal`), "Level N", the clock and "Time", the m:ss countdown in the `bg_gameplay_time_holder` field, Retry (`Btn_square_retry` + `ic_restart`) above Pause (`Btn_settings_normal` + `ic_pause`), and the coin holder (`bg_home_coin_holder`, `Coin_Single`, compact amount, green plus) under them, inside the safe area, within 966 units wide, without overlapping the board, and resembling `references/ref-hud-top-bar-level29.png` per the chief visual validator.
2. Given a Hub launch, direct Play or Level Editor Test Level, "Level N" shows the 1-based position of the attempt's level in the active level database before Play starts, and three-digit numbers fit.
3. Given the countdown and Time Freeze, Booster's timer text and freeze counter keep updating through the preserved element names, the freeze counter shows under the Time field only while frozen, and the text changes only when the shown second changes; the Playing tick still allocates 0 B.
4. Given a coin balance, the holder shows `CurrencyText.FormatCompact` of the IWallet balance, updates only when it changes, and the plus gives press feedback only (no Shop, no pause).
5. Given Playing (including the opening hold), when Pause is pressed, the session enters Paused(User) through Suspend and then the Settings popup shows with HOME; toggles persist through the save service; placeholder buttons give feedback only; Close, the scrim or Back resume with board, timer and tweens exactly as before; while a booster write is pending Close is disabled and nothing resumes.
6. Given Playing with no targeting, Back opens the pause menu; with targeting, Back only cancels targeting; with our menu or confirmation open, Back resumes; with Booster's purchase popup open, Booster's behaviour is unchanged; one Back press is handled once.
7. Given backgrounding during Playing, Paused(Saving) or Paused(Purchase), on return the pause menu is visible, no time was lost, the session never auto-resumes behind it, and Close resumes; a focus change without backgrounding opens nothing.
8. Given the pause menu, HOME tears the attempt down (discarding a committed unapplied booster without refund) and loads Hub through the Onity scene flow with no loading screen; a second press does nothing; HOME is disabled while a booster write is pending; during Test Level HOME ends Play Mode.
9. Given Playing (not in the opening hold, no pending write), Retry pauses the session and shows the confirmation popup (header "Level N", heart with "-1", "You will lose 1 life!", green Retry, close button); its Retry restarts the level from LevelData exactly once (no refund, no refill, no starting selection, lives unchanged) and play resumes; Close, the scrim or Back resume without restarting; Retry is disabled in the opening hold, while a write is pending and in Won.
10. Given Failed or Won with no result popup, Pause opens the menu without a session pause so HOME works; in Failed, Retry opens the confirmation and restarts; closing the menu or confirmation only hides it.
11. Given a booster targeting mode, the top bar is hidden and Booster's info banner sits at the top (reference 02); after cancel or use the top bar returns.
12. Given presses on the top-bar buttons or an open overlay, the board never starts a drag; presses on empty top-bar areas still reach the board; Booster's HUD layout tests (names, Ignore/Position rules) still pass.
13. Given pause, resume, restart, Home, scene unload or disposal, every new subscription, input action, UI callback, scheduled item and cancellation source stops at its named stop point; no Update, coroutine, async void, static event or Time.timeScale pause is added; HUD-less scenes still bind NoPauseOverlay.
14. Given final review, only the allowed paths changed, `bg_home_coin_holder.png.meta` keeps its GUID and changes only border and mesh type, the DEC rows and docs are concise, and Booster's, Hub's and the existing Gameplay suites pass.

## References and constraints

- Story/GDD/ADR/design/log/image: user request 2026-09-28 in session [4effc5] with four screenshots (`references/`), the Retry popup screenshot (described in `design.md` section 6), user answers (Retry confirmation, banner per reference 02, targets and checkin approved); `design.md`; PROJECT.md DEC-7/13/14/16/18/31/45/85/92/93/116/127/134/135/136/140/142/150/151; docs/Color-Block-Jam-PRD.md AC-3.x, AC-4.x, AC-6.x, RULE-21/24, NFR-P2/P6/P7; docs/Color-Block-Jam-Visual-References.md 02, 19, 20; docs/Hub-And-Player-Data-Design.md; docs/Booster-Gameplay.md.
- Unity version/platform: 2022.3.62f2; Android and iOS portrait, 1080 x 1920 and 20:9.
- Package/architecture constraints: UI Toolkit only; Onity DI/reactive; Input System code-built actions; ZString/prebuilt strings for per-change text; plain C# presenters with thin views; one Gameplay assembly; Booster element names preserved.
- Explicit out of scope: result (win/fail) popups, lives mechanics (the life copy is placeholder), Shop, Next Level, camera framing changes, Settings popup redesign, panel-settings changes, iPad layout, new art.

## Verification and risk

- Focused test path/command: new fixtures under Tests/Gameplay/EditMode/Hud and Tests/Gameplay/PlayMode/Hud in the existing Gameplay test assemblies; then Booster's GameplayHud*/Gameplay suites and Hub SceneFlow smoke tests once; run in the isolated copy with `Unity.exe -batchmode -runTests -projectPath <copy> -logFile <copy>/x.log` (windowed for UI captures), later focused live-Editor runs after integration.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn compile check per change in the copy; clean Unity compile in the copy; clean live-Editor compile and Console after integration.
- EditMode: flow rules (design section 4-5), top-bar presenter (level number, coin formatting, enabled matrix), navigator guard.
- PlayMode: real Game composition: elements once, timer updates, Pause/Resume, Retry confirmation/Restart, Escape via the Input System test fixture, targeting hides the bar, pointer veto, cleanup.
- Manual/built-player/profiler/device evidence: captures at 1080 x 1920 and 1080 x 2400 (idle, frozen, targeting, pause menu, Retry confirmation) reviewed by the Opus 5.5 chief visual validator; device/IL2CPP checks remain release checks and are not claimed.
- Risk: high
- Approval already granted (exact target only): user answer 2026-09-28 "Onayla + checkin (Recommended)" approves `bg_home_coin_holder.png.meta` (border and Full Rect), the integration hunks in `design.md` section 8 (GameplayHud.uxml/.uss, GameSceneInstaller, GameAttemptLoader, DragInputRouter, ApplicationPauseRelay), and a scoped Plastic checkin after verification. Retry uses a confirmation popup ("Onay popup'ı sor"); the banner follows reference 02 ("Referans 02 gibi"). Booster [f1ed7e] granted the integration window after its checkin; the reorg [7d9d8c] opens it after its move.
- Commit/checkin constraint: allowed-on-explicit-invocation (granted above: scoped checkin of this task's files, matching metas and task docs only, after all required verification and the visual verdict pass; nonempty English comment; no AI attribution)
- Current invocation permission: implement and orchestrate in the isolated copy now; integrate in the granted window; perform the scoped checkin; no build, publishing or closure without the lifecycle gates
- Handoff destination: implement-task TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry via agent-orchestration -> independent verifier -> task-cycle only on defects -> integration window -> scoped checkin

## Stop conditions

- A required change falls outside the allowed paths (for example LevelSession, the Domain table, a scene, panel settings or another importer): stop and ask.
- Booster has not checked in or still holds a lock on an integration file, or the reorg has not reported "restructure done": keep WAITING_FOR_OWNER for shared writes; continue copy work.
- A live file's hash differs from the reviewed base at integration in a way that conflicts with a hunk: three-way merge; never overwrite another owner's lines.
- The top bar cannot fit 966 units with readable sizes, or the board clashes with the bar at 1080 x 2400: report with captures before changing camera or panel assets.
- Exact Unity Editor identity is unproven for a live operation; preserve unsaved scene work and do not select another project.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:d732c426ae2e2e1e32c220f2e40418f2942991a89d061982e348bd50b9d2098b
- Evidence fingerprint: none
- Reviewed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9
- Attempt count: 1
- Last feedback: 2026-09-28 user: "üst taraf referansla aynı olmamış burayı düzeltiğinden emin ol" (before-capture of the old timer pill); addressed by the top bar and the chief visual APPROVE.
- Blocker: AC-8's Test Level clause (HOME ends Play Mode during Level Editor Test Level) has code review only; the checkin waits for the user's decision in the folder-reorg session, because the pending move can only be checked in recursively with every session's pending work under Runtime/, Editor/, Tests/ and UI/ (likely one coordinated combined checkin).
- Next action: None
- Closed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | Captures 1080x1920 and 1080x2400 (110/70 inset) + chief visual validator (Opus) CHIEF VERDICT: APPROVE (verification/captures) + PlayMode GameHudTopBarTests layout | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-2 | PASS | EditMode AttemptLevelNumberTests (non-first and Test-Level-style IDs) + PlayMode AHubLaunchRequestShowsTheLaunchedLevelsNumber ("Level 2") | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-3 | PASS | PlayMode ARealTimeFreezeShowsItsCounterBelowTheTimerAndHoldsTheMainTimer + Booster HUD/Game suites (regression 104/104); no tick work added (two code reviews) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-4 | PASS | EditMode TopBarPresenterTests (compact text only on change) + PlayMode PressingTheCoinPlusKeepsPlayingAndOpensNothing | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-5 | PASS | EditMode GameMenuFlowTests (pending write) + PlayMode pause menu, toggle persistence, placeholder, Close keeps the time | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-6 | PASS | EditMode flow Back rows + PlayMode GameHudBackInputTests (Escape via Input System fixture) + background-over-purchase one-handler test | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-7 | PASS | EditMode background/foreground rows + PlayMode through the real ApplicationPauseRelay from Playing, Paused(User) and Paused(Purchase) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-8 | UNPROVEN | PlayMode HomeFromThePauseMenuTearsDownOnceAndOpensHubWithNoLoadingScene + EditMode GameExitNavigatorTests pass; the Test Level clause (HOME ends Play Mode) has code review only and needs a manual check or user acceptance | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-9 | PASS | EditMode confirm lock/refusal + PlayMode Retry confirmation restart once with stock and coins unchanged + captures (chief APPROVE) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-10 | PASS | EditMode GameMenuFlowTests Won/Failed rows (menu without pause, Failed confirmation, Won ignored) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-11 | PASS | PlayMode targeting hides the bar and returns it (deterministic stock) + targeting captures (chief APPROVE) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-12 | PASS | PlayMode pointer-veto tests + Booster GameplayHudLayoutTests in the regression run | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-13 | PASS | EditMode/PlayMode disposal cases + two independent code reviews (no blocker; HUD-less NoPauseOverlay/UnavailablePurchaseHost branch kept) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |
| AC-14 | PASS | Owner diff review (allowed paths only; coin-holder meta: border and mesh type lines only, GUID kept) + post-move suites: EditMode Hud 113/113, PlayMode Hud 20/20, PlayMode regression 104/104, EditMode Gameplay/UI/Hub/Player 871/871; copy full suites EditMode 1976 passed / 0 failed, PlayMode 233/233 | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3efc1ed42e10a71f5bd128d4b5031d1d9ea960120cffadbc05381d62b334e5d9 |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-28T01:00Z — Created — planning — contract authored from the user request, four screenshots, the Retry popup screenshot, three user answers, five read-only research maps and a completeness critique; Booster [f1ed7e] and reorg [7d9d8c] coordination recorded.
- 2026-09-28T01:05Z — Started — in_progress — lifecycle lock acquired by session [4effc5] (Library/ccgs/task-lifecycle-locks); Unity preflight READY WITH WARNINGS (CLI 1.0.0-beta.11, 2022.3.62f2 pre-6 MCP gate, file-only; live Editor identity not claimed); implementation dispatched to the isolated copy.
- 2026-09-28T02:5xZ — Implementation round 1 — in_progress — copy only: 31 new files, 6 copy-local integration hunks; Roslyn COMPILE_OK; Unity copy runs EditMode Hud 84/84, PlayMode Hud 13/13, Gameplay EditMode 622/622, HUD/Game/Booster/SceneFlow PlayMode 72/72. Independent verification: chief visual validator (Opus) APPROVE WITH CHANGES (C1 text weight, C2 frozen tint); code review no blockers, 1 major (targeting hides clocks vs DEC-150: kept per user choice, superseding DEC row to be recorded) and 5 minors; AC audit gaps recorded. Captures in verification/captures. Fix round 1 dispatched.
- 2026-09-28T03:4xZ — Fix round 1 — in_progress — copy only: C1 heavy text pairs, C2 timer frost, review minors A-D, deterministic stock setup, new tests (real Time Freeze, relay background from Playing/User/Purchase, coin plus, pause toggles/placeholder, Hub launch level number, HOME to Hub). Full copy suites: EditMode 1976 passed / 0 failed (1977 total), PlayMode 233/233 (capture fixtures excluded). Recapture: chief visual validator CHIEF VERDICT: APPROVE. Delta review: no blockers; two minors (no-menu resume only on the foreground return; back-layer resync on attach) fixed by the owner; focused reruns EditMode Hud 113/113, PlayMode Hud 20/20. Integration patches staged in integration/ (base = cs:16/live; installer needs a 3-way merge over cs:17's outline removal). Waiting for the reorg's "restructure done" before shared writes.
- 2026-09-28T04:1xZ — Integration rehearsal — in_progress — fresh copy of the live workspace (cs:17 + pending, pre-move) with the task's 91 files and hunks: five hunks applied cleanly, GameSceneInstaller 3-way merged cleanly over cs:17's outline removal (integration/merged/). COMPILE_OK; EditMode Hud 113/113, PlayMode Hud 20/20, PlayMode regression (GameplayHud|GameScene|Booster|SceneFlow|InGamePurchase|HeldBlock|Highlight) 86/86, Gameplay EditMode 635/635. No shared write yet.
- 2026-09-28T04:0xZ — Integration window — in_progress — after the reorg's "restructure done" (DEC-157, move pending as CH+MV): 83 task files copied to Runtime/Gameplay/Hud/**, UI/Game/TopBar/**, UI/Game/Overlays/**, Tests/Gameplay/{EditMode,PlayMode}/Hud/** plus IGameplayBackFallback.cs (no GUID collisions); hunks applied after live-hash rechecks (GameplayHud.uxml 3-way over the moved Style path, GameSceneInstaller from integration/merged, four others equal to base); bg_home_coin_holder.png.meta through the live TextureImporter (border 24/24/25/24, Full Rect; only those two lines changed, GUID kept); PROJECT.md DEC-153..155 plus the reference count; docs/Game-Hud.md, UI-Asset-Import rows, Visual-References 02/03/12 notes and refs 21/22 (21 stored as the original webp). Live proof: MCP instance ColorBlockJamCase2026@99077e4df1ecfaa4 read back root and 2022.3.62f2; Roslyn COMPILE_OK; Unity compile clean (no error CS after the reload); live EditMode Hud 113/113. Live PlayMode could not initialize in the unfocused Editor (180 s); its temp InitTestScene was removed and Init reopened; PlayMode runs moved to a post-move mirror copy. Haptics [a39f41] and Arrow [5c3ff984] hold their installer/router/uxml edits until this task's checkin.
- 2026-09-28T04:3xZ — Post-move verification — in_progress — mirror of the post-move workspace (07:13; the Arrow session's then mid-edit BoardPresenterArrowMarkerTests.cs removed in the copy only): EditMode Hud 113/113, PlayMode Hud 20/20, PlayMode regression (GameplayHud|GameScene|Booster|SceneFlow|InGamePurchase|HeldBlock|Highlight|HubScene|HubLaunch) 104/104, EditMode Gameplay/UI/Hub/Player 871/871. Reviewed revision computed with integration/owned_digest.py over 102 owned files; AC-1..7 and AC-9..14 PASS, AC-8 UNPROVEN (Test Level clause).
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
