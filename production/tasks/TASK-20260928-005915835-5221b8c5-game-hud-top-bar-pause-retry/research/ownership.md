## Ownership, conflicts and duplicates for the Game-scene top bar, Pause and Retry

**Short answer:**
- **No task covers this work yet.** No contract or handoff covers the in-game top bar, the pause popup, the Retry button, the in-game coin display or navigation from Game back to Hub. The closed Hub Settings task left the pause menu and the Retry button to "Game-scene HUD work".
- **Booster owns the files this work needs.** Its contract gives it the Game scene, the Gameplay session/input/composition files, `Gameplay/UI/**`, the Gameplay asmdef, `PROJECT.md` and the PRD. Its lock is live. Its handoff has already given this session the `Gameplay/Hud/**` folder and a later window to edit shared binding lines.
- **Merge risk.** Game.unity, `GameSceneInstaller.cs`, `LevelSession.cs` and the Gameplay asmdef all differ between the shared workspace and the Booster copy. The copy's `PROJECT.md` also reuses DEC-142 to DEC-145, which the shared file already uses.

Nothing was written outside the scratchpad.

Path shorthand: `$R` = `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026`, `$C` = `C:/Users/e-fur/AppData/Local/Temp/cbj-booster-f1ed7e`. A task named by its short hash (for example `54655902`) means `$R/production/tasks/TASK-…-<hash>-…/contract.md`.

### 1. Open tasks that touch the requested paths

40 of the 71 task folders are not closed. The table lists every open task whose Allowed or Forbidden paths name one of the requested paths. The line numbers point into that task's `contract.md`.

| Task (state / phase, lines) | Owns (Allowed) | Forbids |
|---|---|---|
| **54655902 booster-gameplay-flow** (in_progress / "implementation (isolated copy)", 241-242) | `Gameplay/Boosters/**` 57, **`Gameplay/UI/**` 59**, **`Gameplay/Session/*.cs` 62**, `Gameplay/Board/*.cs`, `Gameplay/Effects/*.cs`, **`Gameplay/Input/*.cs` 64**, **`Composition/GameAttemptLoader.cs` 65, `GameSceneInstaller.cs` 66**, **Gameplay asmdef 67** (Player ref, plus the UI ref added 174), `Player/PlayerSaveService.cs`, `PlayerDataInstaller.cs`, `IBoosterInventory.cs`, `IBoosterConsumer*`, `BoosterConsumeResult*` 78-84, `Tests/Gameplay/{EditMode,PlayMode}/**` 85-86, **`Scenes/Game.unity` 93**, `Art/UI/Icons/Gameplay/target.png(.meta)`, `Panels/bg_gameplay_time_holder.png.meta`, `Buttons/Btn_booster_normal.png.meta` 95-99, **`PROJECT.md` 104, PRD 105** | `Hub/**` 115, `Player/Storage/**`, `PlayerSnapshot.cs` 124-125, "Every scene, ScriptableObject, material, importer or assembly definition not explicitly listed" 126. Uses `UI/Purchase/**` read-only (174) and `Data/UI/BoardOverlayPanelSettings.asset` read-only (170). |
| **78356740 hub-pre-level-launch** (in_progress / implementation, 131-132) | `Hub/PreLevel`, `Purchase`, `Launch/**` 29-34, `Hub/UI/PreLevel.*` 35-38, **`UI/Purchase/**` 39-40**, `Tests/UI/**` 41, `Hub/UI/Hub.uxml` 42, `HubSceneInstaller.cs` 43, `HubScreen.cs` 44, **`UI/Common/Common.uss` 45**, `GameAttemptLoader.cs` 48, `LevelLaunchResolution.cs` 49-50, `GameSceneInstaller.cs` (m_levelIndex tooltip only) 52, `Tests/Hub/**` 53, `PROJECT.md` 59 | `Player/**` 64, `Gameplay/Session/**` 67, `Gameplay/Board/**` 68, `Scenes/**` 70, other `Art/UI/**` 72 |
| **9383f38c held-outline-exit-fragments** (in_progress / verification, 127-128) | `Gameplay/Board/Outline/**` 44, `BoardPresenter.cs` 46, `GameSceneInstaller.cs` 47, `Game.unity` 56, `PROJECT.md` 58 | `Gameplay/Session/IBoardPresenter.cs` 71 |
| **945d78fe ice-visibility** (waiting / "control released for user-directed Claude takeover", 187-188) | `Session/EffectAttemptHookSource.cs` 52-53, **`Session/DragInputRouter.cs` 54**, `IBlockedDragFeedback` 55-56, `GameAttemptLoader.cs` 57, `GameSceneInstaller.cs` 58, `BoardPresenter.cs` 59, **`Data/UI/BoardOverlayPanelSettings.asset(.meta)` 113-114**, `Game.unity` 115 | `LevelEditor.unity` 126, **`Session/LevelSession.cs` 127** |
| a88e25db block-drag-gate-exit (in_progress / verification, 211-212) | **`Gameplay/**` 84**, `Tests/Gameplay/**` 85, `PROJECT.md` 90, `Game.unity` 91 | other scenes 107-110; out of scope "Pause, result and pre-level UI and HUD." 180 |
| a9370ca5 board-look (waiting / "approved exact-target repair queued after owners", 242-243) | `Game.unity` 79, `Hub.unity` 80, `LevelEditor.unity` 81, `PROJECT.md` 84, PRD 87 | Init/Loading scenes 109-110 |
| dea430a7 play-area-environment (in_progress / verification, 157-158) | `Game.unity` 46, `PROJECT.md` 52, PRD 53 | other scenes 75-78 |
| a3ffdc40 arrow-block-gameplay (in_progress / planning; waiting on Booster, 178-179, 186) | `EffectAttemptHookSource` 69-70, `GameSceneInstaller.cs` (hook-source binding) 71, `BoardPresenter.cs`, `Tests/Gameplay/EditMode/**` 75, `PROJECT.md` 88 | `Scenes/**` 94 |
| 3d1b0a34 barrier-rule (ready / planning, 128-129) | `Gameplay/Levels/**` 62, `Gameplay/Effects/**` 63, `GameSceneInstaller.cs` 64, `Tests/Gameplay/**` 66, `PROJECT.md` 68 | `Gameplay/Session/**` 76, `Scenes/**` 79 |
| 11b501c1 gate-exit-fragments (in_progress / verification, 126-127) | `Gameplay/Board/ExitFragmentPlacement.cs` 49-50, `BoardPresenter.cs` (fragment lines only) 51, `PROJECT.md` (DEC-131 only) 60 | `Scenes/**` 65, `Composition/**` 70, `Session/**` 71, `Effects/**` 72, `Levels/**` 73 |
| 47831bb7 runtime-shapes-from-json (in_progress / verification, 188-189) | `Tests/Gameplay/**` (fixtures only) 95, `PROJECT.md` 112 | `Scenes/**` 120 |
| c8da893 solid-obstacle-art (ready_to_close, 118-119) | `GameAttemptLoader.cs` 49, `PROJECT.md` 60 | `Scenes/**` 65 |
| 0e93d47c home-tab-bar-locked-ends (in_progress / implementation, 97-98) | `Hub/UI/Home.uxml` 29, `Home.uss` 30, `HomeView.cs` 31, `PROJECT.md` 35 | `Art/UI/**` 40, `UI/**` 41, `Hub/Composition/**` 42, `Player/**` 46, `Gameplay/**` 47, `Scenes/**` 48 |
| f5013490 home-currency-holders-authored (in_progress / implementation, 104-105) | `Hub/UI/Home.uxml`, `Home.uss`, `CurrencyHolder.uxml`, `Hub/Home/HomeView`, `IHomeView`, `HomePresenter.cs` 34-39, `HubScreen.cs` 40, `PROJECT.md` 44 | `Art/UI/**` 49, `Player/**` 51, `UI/**` 53, `Gameplay/**` 54, `Scenes/**` 55 |
| 2949c75f currency-amount-price (ready / planning, queued after Booster, 118-119, 127) | `Player/IWallet.cs` 32, `Player/PlayerSaveService.cs` 33, `PROJECT.md` 51 | `Hub/**` 60, `UI/**` 61, `Gameplay/**` 62, `Scenes/**` 63 |
| 95397edf level-editor-validate-level (in_progress / verification, 169-170) | `PROJECT.md` 70 | `Gameplay/**` 76, `Scenes/**` 79, `Tests/Gameplay/**` 81, PRD 84 |
| d6a25edf vacuum-suction (ready_to_close, 102-103) | `PROJECT.md` 43 | `Assets/Scenes/**` 47 |
| 88e64a0d board-kit (waiting, 183-184) | PRD 50, `PROJECT.md` 52 | `Scenes/**` 58 |
| 2f31f26b remote-level-json (ready, 118-119) | `PROJECT.md` 50 | `Scenes/**` 61 |
| f7bfcf6a level-json-v2-board-objects (in_progress, 175-176) | `PROJECT.md` 102 | none |
| 08cc082b barrier-board-view (in_progress, 116-117) | `PROJECT.md` 54 | `Scenes/**` 62 |
| 9ac18e21 draft-survives-reload (in_progress, 113-114) | `PROJECT.md` (DEC-119 sentence) 49 | none |
| 2196c1d1 pen-edit-templates (in_progress, 137-138) | `PROJECT.md` (DEC-125 row) 67 | `Gameplay/**` 73, `Scenes/**` 78 |
| 515eb50b template-delete-choices (in_progress, 198-199) | `PROJECT.md` (DEC-126 row) 60 | `Gameplay/**` 66, `Scenes/**` 71 |

The remaining open tasks only forbid `Scenes/**` and/or `Gameplay/**`:
- 8e54a900 (141, 145)
- f4a07e8a (95)
- 9e5cbf34 (87, 92)
- 45507296 (62)
- e7f5beb8 (61)
- 22e06638 (68, 71)
- 5858b722 (87)
- 306ba760 (101, 104)
- 2df1d5a7 (64, plus `PROJECT.md` forbidden at 72)
- a65fbdb1 (64, 69)
- 224a7aef (76)
- a3ad6ee3 (62, 65)

Three open tasks name none of the requested paths: 93892dee cbj-level-import, b289bba3 level-canvas-v2-tools (forbids `Scenes/**` at 46) and 141dd370 resize-keeps-edge-voids.

**Paths with no open owner:**
- `UI/Settings/**`: its owner c5020d50 closed at 2026-09-27T14:13Z (c5020d50 lines 125, 136).
- `Gameplay/Hud/**`: no contract lists it (see section 3).
- The `Art/UI` sprites a top bar would use, other than the three Booster metas and `target.png`.

### 2. Live locks in `$R/Library/ccgs/task-lifecycle-locks`

Each live lock has a matching `.lock/owner.json` and `.lock.claim`.

| Task | Session | Acquired (UTC) |
|---|---|---|
| booster-gameplay-flow | "Booster gameplay flow [f1ed7e]" (token 31b8ef92…) | 2026-09-27T13:44:47Z |
| hub-pre-level-launch | "Hub sahnesini oluşturma [5c52bd]" | 2026-09-27T15:37:05Z |
| home-tab-bar-locked-ends | [5c52bd] | 2026-09-27T20:28:23Z |
| home-currency-holders-authored | [5c52bd] | 2026-09-27T21:59:04Z |
| held-outline-exit-fragments | "Taşıdığım objede beyaz outline [1f28c6]" | 2026-09-28T00:10:49Z |

The paths for each lock are the Allowed paths in section 1.

Three more folders there are audit records, not locks: `runtime-shapes-from-json.audit.367809b8…` (codex `/root/scope_fix`, 2026-09-26) and two `level-editor-architecture.lock.audit.*` folders. Other `Library/ccgs` content is ice/arrow baselines and a parked copy (`parked/a3ffdc40`).

### 3. Duplicate search

No existing or planned task covers the top bar, the pause popup, Retry, the in-game coin display or navigation from Game back to Hub. The relevant lines:

**Hub Settings (c5020d50, closed) left the pause host to the Game scene:**
- Line 20: the popup "is a shared component that the in-game pause menu (reference 20) can reuse with its HOME button shown."
- Lines 23-24: the HOME button is `Btn_popup_green` with `home_icon` and "stays hidden unless a host shows it."
- Line 65 forbids `Assets/ColorBlockJam/Gameplay/**` "(the pause menu and HUD restart button belong to the Game-scene HUD work)".
- Line 99 puts "the in-game pause menu itself, the HUD restart button" out of scope.
- AC-8 (line 89) and defect D-002 (VERIFIED, line 166) prove that a host can show HOME and that the popup with HOME fits at 1080 x 1920.

**Booster (54655902) mentions no top-bar pieces.** It never names a level badge, pause button, retry button or coin holder. It does touch the timer, pause and Back:
- Line 21: "Gameplay still binds InactiveCountdown"
- Line 31: the Time Freeze main-countdown rule
- Line 33: "Close, same-button tap, empty-space tap or Back cancels without spending"
- Line 182 (AC-4): pause, saving and background hold the clocks
- Line 189 (AC-11): Home/Restart discard a pending effect without refund
- Line 207: "full game menus/result screen design" is out of scope

**Booster's handoff defines this session's lane.** In `…54655902-booster-gameplay-flow/implementation-handoff.md`:
- Line 16: session "Oyun sahnesi UI hazırlama [4effc5]" (Game-scene top bar, Pause, Retry) was told the following.
- Line 17: "Its layout goes in the carve-out Gameplay/Hud/**, and its template keeps Booster's timer element names."
- Line 18: "the router is the only Back reader while Playing (targeting cancel, then that session's pause fallback). UI Toolkit cancel handles Back while Paused."
- Line 19: "Restart and GoHome are illegal from Playing (Pause(User) first), and navigation is refused while IsBoosterWritePending."
- Line 20: "a window for its binding lines in GameSceneInstaller, Game.unity, DragInputRouter and GameplayHud.uxml after Booster's copy-back, READY_TO_CLOSE and checkin, earlier on request."
- Neither workspace has a `Gameplay/Hud` folder yet.

**The top timer already exists in the Booster copy.** `$C/Assets/ColorBlockJam/Gameplay/UI/GameplayHud.uxml` lines 19-31 hold `top-area`, `top-content`, `timer-band`, `timer-holder`, `timer-icon`, `timer-label` and `freeze-counter`. The copy's `LevelSession.cs` exposes `IsBoosterWritePending` (113), `Pause` (195), `Resume` (221), `Restart` (237) and `GoHome` (264).

**Other mentions (no competing owner):**
- a88e25db line 56: the "interim `NoPauseOverlay` auto-resumes until a pause UI exists."
- a88e25db `design/final-design-2.md` line 140: "Pause/result popups and HUD … | UI tasks | `IPauseOverlay`, session operations".
- a88e25db `design/final-design.md` line 422: "`GoHome()` … Scene navigation is the Hub task's."
- 78356740 line 23 keeps "the `NoPauseOverlay` foreground-resume interim of DEC-116" unchanged.
- 945d78fe line 158 puts "full HUD" out of scope.
- a9370ca5 line 206 puts "A HUD safe-area framing fit" out of scope.
- 43e35c51 line 79 (closed): "gameplay HUD layout (no HUD screen exists yet…)".
- Every Hub Home "top bar" hit is about Hub Home only: 461b4f9c D-007/D-008, 0e93d47c and f5013490.

### 4. DEC numbers

- **Shared `$R/PROJECT.md`:** highest is **DEC-145** (line 677, held outline). DEC-140 to DEC-145 are at lines 672-677.
- **Copy `$C/PROJECT.md`:** highest is **DEC-147** (line 664). The copy has no DEC-137 to DEC-141 rows. Its Booster rows use **DEC-142 to DEC-147** (lines 659-664, including "Gameplay HUD host…" DEC-146 and the in-game purchase popup DEC-147). DEC-142 to DEC-145 collide with the shared rows.
- **Reservations:**
  - DEC-138 was reserved for the pre-level task and is now used: handoff line 11; 0e93d47c line 63; `c1e67965/verification/environment-review-v6.md` line 37.
  - Booster's handoff lines 12 and 15 say its rows "start after the highest live number at write time" and that it must "renumber Booster's DEC rows" on integration. That means six pending rows, not a fixed range.
  - 95397edf line 70 says "DEC-143 or the next free number"; it used DEC-144.
  - No contract or handoff reserves DEC-146 or above.

### 5. Shared workspace vs Booster copy (sha256)

| File | Shared `$R` | Copy `$C` | Result |
|---|---|---|---|
| `Assets/ColorBlockJam/Scenes/Game.unity` | c55a492d76fa4922283cb03a70533357e623a2144d0881ce30bd5696e5840211 (708 lines, 2026-09-28 02:29) | 1dadf03e4b00acb90659c28aeaaba19123f2a7c967860e3078c4fa8779e85f06 (851 lines, 09-27 23:57) | **DIFF** |
| `Gameplay/Composition/GameSceneInstaller.cs` | b750ad0c1f1f496159ce4dda31f8772af4e62d97d083d97742a5faed9013b0ed (113 lines) | 31d32122db59ba752cb3ecc0e3cbf3d4e7ec8e6bc5b0aeed4c1624af334941fb (167 lines) | **DIFF** |
| `Gameplay/Session/LevelSession.cs` | cc4bad6e178be990ba5a7eb9e797d7e7ef0ccb7980c676d934bb9ee0ce1507f9 (416 lines) | 14c1c748bdcc7df344ddf6aafdd957ab85e47b1384f1a0401bbaf0a80b41911e (807 lines) | **DIFF** |
| `Gameplay/ColorBlockJam.Gameplay.asmdef` | 311bfdd4d6bf8307234c7f90d3ca68d3435933872528df9fe8a7463518689935 | 358ee61f6be78e58dc20e37915bcdfceab35631d5cae0a157a42509568edbc46 | **DIFF**: the copy adds `"ColorBlockJam.Player"` and `"ColorBlockJam.UI"` |

- **The copy is missing held-outline work.** The shared `Game.unity` line 623 has `m_outlineMaterial`; the copy's `Game.unity` does not. The copy's `GameSceneInstaller.cs` has no outline code either.
- **The shared files were changed by other tasks after Booster copied them.** The shared `GameSceneInstaller.cs` hash b750ad0c matches the held-outline hash in the handoff (line 15). `DragInputRouter.cs`, `GameAttemptLoader.cs` and `BoardPresenter.cs` also differ between the two trees.
- **These are unchanged in both trees:** `Game.unity.meta` and `ColorBlockJam.Gameplay.asmdef.meta`.

Scratch files from this search are in `C:/Users/e-fur/AppData/Local/Temp/claude/C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026/ae392a45-3c89-43ec-bc78-9b52ff9e8c2f/scratchpad/` (`own.txt`, `dup.txt`, `own.awk`).