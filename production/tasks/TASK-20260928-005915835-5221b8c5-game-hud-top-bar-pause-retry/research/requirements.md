# HUD top bar, Pause and Retry: requirements map

**Path legend.** `R:` means `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026/` (the shared workspace). `C:` means `C:/Users/e-fur/AppData/Local/Temp/cbj-booster-f1ed7e/` (the booster session's copy). `PRD` is `R:docs/Color-Block-Jam-PRD.md`, `VR` is `R:docs/Color-Block-Jam-Visual-References.md`, `UAI` is `R:docs/UI-Asset-Import.md`, `HUB` is `R:docs/Hub-And-Player-Data-Design.md`, and `BG` is `C:docs/Booster-Gameplay.md`.

**Which docs differ.** Five docs have different text in the copy: the PRD, the visual references, UI-Asset-Import, the Hub design and Booster-Gameplay. `UI-Asset-Mapping.md` is the same in both. The copy holds the newer booster rules. The repo holds the newer atlas and Hub rules.

---

## 1. Requirements

**Top bar content and art**
1. The PRD's HUD top lists pause (`ic_pause`), "Level N", the timer (`ic_time` on `bg_gameplay_time_holder`) and the coin balance (PRD:300). The Restart button moved onto the HUD later: DEC-135 (R:PROJECT.md:667), AC-4.4 (PRD:333) and reference 20 (VR:285-289, which shows the level badge, the time holder and an orange restart button at top right).
2. HUD asset row: `bg_gameplay_time_holder`, `ic_time`, `ic_pause`, `Coin_Single` and the booster icons (PRD:174). `Btn_square_retry` and `ic_restart` appear only in the Popups row (PRD:176).
3. The HUD timer uses `ic_time`; `Clock` and `Clock_Freeze` are for other contexts (DEC-47, PRD:961; OQ-3 is decided, PRD:1233).
4. All runtime UI uses UI Toolkit, with no UGUI or TextMeshPro (R:AGENTS.md:127; R:PROJECT.md:372; DEC-16, PRD:930 "Panel Settings at 1080 x 1920").

**Timer**

5. The timer counts down and stops the level at zero (AC-3.5, PRD:309). It starts from the authored value plus any Extra Time, in `m:ss`. The text changes only when the displayed second changes (AC-3.6, PRD:310; RULE-20 and RULE-21, PRD:243-244, which round up). On fail it holds at `0:00` (AC-3.7, PRD:311). It is frozen while Paused (AC-3.8, PRD:312).
6. The timer uses a table of prebuilt `m:ss` strings and updates its text only when the second changes (R:AGENTS.md:161; R:PROJECT.md:271-272).
7. Time Freeze shows an iced timer holder plus a separate 10-to-0 counter while the main timer holds (VR:89-93; PRD:563). In the copy, the targeting banner sits **below** the timer band (BG:51, B-22).

**Level label**

8. The HUD shows "Level N" (PRD:300). Home's rule is `"Level " + (GetPlayableLevelIndex(saved, count) + 1)`, updated only when the value changes (HUB:545-547, 207-210). **No rule for the Game HUD's level number was found.**

**Coins**

9. The HUD coin balance equals the save service balance (AC-3.9, PRD:313). There is one reactive value and no stored copy (AC-6.4, PRD:391). Failed, Restart, Pause, Resume and Home leave the balance unchanged (AC-6.3, PRD:390).
10. The HUD reads the balance from `IWallet.GetBalance` (HUB:260). Amounts use `CurrencyText.FormatCompact`, which gives "1.52k"-style text, and HUB names the Game HUD as a user (HUB:153-157, 557-558).
11. Coin `ic_plus` buttons "remain locked/placeholders" (RULE-24, PRD:249). On Home both plus buttons give press feedback only (AC-1.6, PRD:281; HUB:563).

**Pause**

12. The pause button moves Playing to Paused through Suspend, then opens the pause menu. The menu is the shared Settings popup plus a Home button. Its close button resumes. Restart is the HUD button (AC-4.4, PRD:333; DEC-135; DEC-13 is superseded, PRD:927).
13. The pause popup opens only after Suspend (R:PROJECT.md:113-114). Suspend has three steps (R:PROJECT.md:116-120). `Time.timeScale` is never used to pause (R:PROJECT.md:169; RULE-22, PRD:245).
14. While Paused the board takes no input and tweens are paused, not stopped (AC-4.5, PRD:334). Resume restores board, timer and tweens exactly (AC-4.6, PRD:335; V-E6, PRD:1088).
15. Pausing while targeting cancels targeting and spends nothing (AC-BST-0.2, PRD:574; PRD:555).
16. `Paused(Saving)` uses the same Suspend steps but never opens the pause popup (R:PROJECT.md:122-126).

**Pause popup (DEC-134/135/136)**

17. The popup is the `ColorBlockJam.UI` Settings popup (R:PROJECT.md:666-668; HUB:43, 57-62, 573-630).
    - The presenter exposes `SetHomeVisible`, `CloseRequested` and `HomeRequested` (R:Assets/ColorBlockJam/UI/Settings/SettingsPopupPresenter.cs:62,65,175).
    - In the Game host, `CloseRequested` maps to Resume and `HomeRequested` maps to teardown and a return to Hub (HUB:629-630).
    - Toggles commit after Suspend, outside any tick (VR:287).
    - Placeholder buttons give feedback only (AC-2.4, PRD:295). Toggles persist (AC-2.5, PRD:296).
18. Popup and HUD presses never start a board drag (AC-3.13, PRD:317). A second tap during a transition does nothing (AC-4.8, PRD:337).

**Home from pause**

19. HOME is `Btn_popup_green` + `home_icon` (VR:287). The element already exists in the shared popup, hidden on Hub (HUB:595).
20. Home runs teardown and shows committed balances (AC-4.7, PRD:336; transition table Paused→Home, PRD:374; Rule 8, R:PROJECT.md:173-177).
21. Home from a later pause discards a committed but unapplied booster intent without refund. Navigation is blocked while a write is unresolved (AC-6.13, PRD:400; BG:42, B-13).
22. Moving between Hub and Game shows no loading screen (DEC-140, R:PROJECT.md:672).

**Retry / Restart**

23. The HUD restart button is `Btn_square_retry` + `ic_restart` (VR:287). The fail popup keeps its own Restart and Home (AC-4.3, PRD:332).
24. Restart returns the level to its start state (AC-5.3, PRD:353). Everything is rebuilt from level data; committed coins and stock stay (AC-5.3a, PRD:354).
    - Restart never refills or refunds (R:PROJECT.md:173-177; AC-BST-0.3, PRD:575).
    - It starts with no starting-booster selection (AC-5.7, PRD:358).
    - The environment survives a Restart of the same level (R:PROJECT.md:512-513).
    - Restart to Playing takes 0.5 s or less (NFR-P7, PRD:775). Soak target is 10 restarts (NFR-P10, PRD:778; NFR-L10, PRD:792).
    - Restart is refused while a booster write is unresolved (BG:42).

**Android Back**

25. In Playing, Back acts as Pause. On the ordinary pause popup it resumes. On fail/win popups it acts as Home. Saving blocks it (AC-4.11, PRD:340).
26. Back also cancels targeting (AC-BST-0.5, PRD:577; BG:41, B-12) and closes the purchase popup, which resumes play (PRD:372; BG:61).
27. Back's tests are V-E25 (PRD:1107) and V-D4 (PRD:1152). Hub's pattern is a code-built `<Keyboard>/escape` action (HUB:438-441).

**Backgrounding**

28. Backgrounding during Playing enters Paused through Suspend. On return the pause popup shows and no time is lost (AC-3.12, PRD:316; DEC-31, PRD:945).
29. This retires the interim `NoPauseOverlay` foreground resume (DEC-116, R:PROJECT.md:648; `R:docs/Gameplay-Drag-And-Gate.md:128`). The copy's AC-10 stays incomplete until that happens (BG:146-147).

**Safe area and layout**

30. The layout must hold at 1080×1920 and 1080×2400 inside `Screen.safeArea` (NFR-4, PRD:757; AC-1.4a, PRD:279; R:PROJECT.md:299). An 8×10 board must not touch the HUD (AC-3.11, PRD:315).
31. One reusable safe-area root (DEC-18, PRD:932). The Game HUD reuses `Presentation/UI/SafeAreaPadding` (HUB:42, 443-456). The popup host pads `settings-safe-area` (HUB:585, 627).

**Feedback, lifecycle and performance**

32. Press feedback is one shared USS class, `.cbj-pressable` (DEC-14, PRD:928; HUB:461-464). UI animation uses USS transitions owned by the view (DEC-45, PRD:959; PRD:346).
33. The Playing tick allocates 0 B, HUD included (NFR-P2, PRD:770; R:PROJECT.md:526).
34. Every subscription is disposed (R:PROJECT.md:159).
35. The HUD makes 5 UI draws or fewer from two atlases (NFR-P6, PRD:774).
36. The pause popup opens only after Suspend (NFR-L2, PRD:784).
37. Manual and device checks: V-M4 (1118), V-M6 (1120), V-M7 (1121), V-M8 (1122).

**Lives**

38. PRD Gameplay scope includes "the life display as a placeholder" (PRD:73; REQ-G7, PRD:113, which maps to Home's AC-1.7).

---

## 2. Sprites for the top bar and pause popup

GUIDs are verified against each `.png.meta`. Atlas membership is verified in `R:Assets/ColorBlockJam/Art/UI/Atlases/*.spriteatlasv2`.

| Element | Sprite (`Art/UI/…`) | GUID | Class / border (UAI) | Border applied? | Atlas |
|---|---|---|---|---|---|
| Level badge + wide bar | **No HUD mapping in the docs.** Visual candidate: `Buttons/Btn_levelBg_normal.png` (883×325, a circular badge fused to a bar). Mapped as a Home Level action (UI-Asset-Mapping:31) and "reserved" (UAI:119, 393-396); geometry caveats at UAI:183-201 | `34af3ba7e2012fc4dbc8e764b5de5bb2` | nine-slice L,B,R,T 323,131,75,119 (USS 323,119,75,131) | No (.meta:52 is 0) | HomeSettings (atlas line 16) |
| Timer pill | `Panels/bg_gameplay_time_holder.png` (151×115; UI-Asset-Mapping:19) | `d1532cdaa44f9744f98209ebfb479b7d` | nine-slice. Repo: 37,40,37,39, measured only (UAI:116). Copy: **50,50,50,50** (C:docs/UI-Asset-Import.md:110; BG:56, B-27) | Repo: no (.meta:52 is 0, compression Normal .meta:75). Copy: yes (.meta:52 is 50, compression None) | HudPopups (line 32) |
| Clock icon | `Icons/Gameplay/ic_time.png` (512×527; UI-Asset-Mapping:20) | `195b567d34a82944f8b385a5ad6a0d9c` | fixed (UAI:140) | n/a | HudPopups (31) |
| Retry back | `Buttons/Btn_square_retry.png` (512×531; UI-Asset-Mapping:46) | `3ee174f6f558e5d44baa212d55d4c3ab` | fixed (UAI:128) | n/a | HudPopups (23) |
| Retry glyph | `Icons/Gameplay/ic_restart.png` (350×350) | `ae45d62ab6946fc47a3a2736a5e30046` | fixed (UAI:139) | n/a | HudPopups (30) |
| Pause glyph | `Icons/Gameplay/ic_pause.png` (128×132) | `6b78223fff4898542bfe5025d6ac7d2f` | fixed (UAI:138) | n/a | HudPopups (29) |
| Pause back (blue square) | **Not documented.** Visual candidate: `Buttons/Btn_settings_normal.png` (134×139). Mapped as "Settings row control" (UI-Asset-Mapping:49); used for Home route nodes (UAI:127) | `90307f3a3ca4e2c4e9775c1553334fd7` | fixed | n/a | **HomeSettings** (18) |
| Coin pill | **No HUD mapping.** Candidates: `Panels/bg_home_coin_holder.png` (101×84, light blue, matches references 04 and 14), and `Panels/bg_home_resource_holder.png` (Home's beige pill). Both mapped as Home top-bar holders (UI-Asset-Mapping:28) | `e1e3b04b16d42ee4d9e4c6ce5f4ac7c6` / `3b06c5352b926ef409afcc444fd73fc9` | coin holder: nine-slice 24,24,25,24 (UAI:114). Resource holder: nine-slice 40,52,40,45 (UAI:115) | Coin holder: no. Resource holder: yes | **HomeSettings** (36 / 37) |
| Coin icon | `Icons/Currency/Coin_Single.png` (UI-Asset-Mapping:47) | `f6855176a87142c4981ea278b281d4e8` | fixed (UAI:134) | n/a | **HomeSettings** (20) |
| Plus (green) | `Buttons/Btn_circle_green_small.png` + `Icons/Currency/ic_plus.png` | `5d010ec8ca79d2f42a93707c7eda82f6` / `ecc7d7841af7798408efd540539bcc26` | fixed / fixed (UAI:126, 136) | n/a | **HomeSettings** (15 / 22) |
| Pause HOME button | `Buttons/Btn_popup_green.png` | `88ed48e414904bc47a1b1e2c4b972442` | nine-slice 69,128,70,129 (USS 69,129,70,128), `-unity-slice-scale: 0.63px` (UAI:122) | Yes | HudPopups (19) |
| HOME glyph | `Icons/Navigation/home_icon.png` (153×167) | `efc9c1ff96029f64eb32f44d83596a42` | fixed (UAI:145) | n/a | **HomeSettings** (27) |
| Popup shell | `bg_popup`, `bg_popup_header`, `bg_popup_info_holder`, `Popup_Close_Button`, `Btn_popup_blue` (UI-Asset-Mapping:43-45) | `58167b54…`, `baeb46b4…`, `3edfb6bd…`, `aa686243…`, `cdf10c6f…` | applied (UAI:110-112, 121, 129) | Yes | HudPopups. The popup's rows, toggle and icons come from HomeSettings (UAI:280-289) |

---

## 3. Conflicts

1. **HUD Retry pressed during Playing has no valid transition.**
   - Playing may go only to Paused, Won or Failed (R:PROJECT.md:19-20). The PRD table allows Restart only from Paused (PRD:373) and Failed (PRD:380).
   - The code agrees: `R:Assets/ColorBlockJam/Domain/Session/SessionTransitions.cs:46-62` (same at C: 46-64) rejects Restart and Home from Playing.
   - But DEC-135, AC-4.4 and reference 20 put Restart on the HUD, which is only pressable in Playing because the pause scrim covers it.
2. **The PRD's HUD lists are out of date.** 7.3 Content (PRD:300) and the HUD asset row (PRD:174) omit Restart.
3. **Superseded text remains in the PRD.**
   - AF-3 (PRD:186) still names a "`Btn_popup_green` with a Resume label".
   - V-M6 (PRD:1120) still tests "a pause (Resume, Restart, Home)".
   - AF-1 (PRD:184) still points to DEC-24's grey OFF tint.
4. **Two Home button styles.** DEC-41 (PRD:955, still Pending) and AC-4.3 (PRD:332) use `Btn_popup_blue` + text for Home. DEC-135 uses `Btn_popup_green` + `home_icon` for the pause HOME.
5. **Timer text and layout.**
   - RULE-21 and AGENTS.md specify `m:ss`, but references 04 and 14 show `01:35` (`mm:ss`).
   - The references show a "Time" caption with `ic_time` above a dark pill. The copy puts `ic_time` inside the holder (C:Assets/ColorBlockJam/Gameplay/UI/GameplayHud.uxml:21-25; GameplayHud.uss:74-98).
6. **`bg_gameplay_time_holder` border and compression differ.** The repo records 37/40/37/39 (measured only), 0 on the importer and Normal compression. The copy has 50/50/50/50 on the importer and no compression (B-27).
7. **DEC number collision.**
   - The repo uses DEC-142 to 145 for atlases, currency holders, Validate Level and the held outline (R:PROJECT.md:674-677).
   - The copy uses DEC-142 to 147 for booster rules (C:PROJECT.md:659-664), and its PRD and visual-reference notes cite DEC-142, 143 and 146 with the booster meanings.
   - The repo's next free number, DEC-146, is already taken in the copy.
8. **Back precedence is undefined.** AC-4.11 says Back pauses in Playing; AC-BST-0.5 and B-12 say Back cancels targeting. In the copy, the Back action lives on the gameplay input map, which is disabled while Paused. The pause popup therefore needs its own Back action, as the purchase host does (BG:61, B-32).
9. **The panel differs from the one the popup was tested on.**
   - DEC-16 and Hub's panel settings use 1080×1920 with Expand (R:PROJECT.md:664).
   - The copy's HUD uses `BoardOverlayPanelSettings` with match 0.5, which is about 966 units wide at 20:9 (C:…/GameplayHud.uss:3-4; BG:50).
   - The Settings popup is 926 wide and 1886 high with HOME shown, and was checked only on Hub's panel (HUB:588, 603-605).
10. **Atlas split versus the HUD draw budget.**
    - DEC-142 puts every sprite Home shows into HomeSettings, on the assumption that Home is always on screen (UAI:292-298).
    - In the Game scene, the coin pill, coin icon, plus button, the likely pause backing, `Btn_levelBg_normal` and `home_icon` would all pull in HomeSettings as well as both HudPopups pages (UAI:364-368). That puts NFR-P6's limit of 5 HUD draws at risk.
    - UAI:250 claims HudPopups holds the "pause and restart controls", but only the glyphs are there.
11. **Status of the coin plus button.** RULE-24 says "locked/placeholders". Home gives press feedback (AC-1.6). There is no HUD rule.
12. **0 B per frame versus timer text.** Performance Rule 5 and NFR-P2 require 0 B per frame. The copy measured that `Label.text` allocates on every shown-second change (BG:60, B-31).
13. **Won Finish interruption.** AC-4.12 (PRD:341) speaks of "an allowed Home/Restart interruption", but the tables have no Won→Restart transition (R:PROJECT.md:21; PRD:377-378).
14. **Lives in the HUD.** PRD:73 requires a life placeholder in Gameplay. The references and the target HUD show none.

---

## 4. Open questions for the user

1. **Retry:** should it ask for confirmation? No doc says. And how should Playing→Restart work: a direct new transition, or an internal Pause followed by Restart? Either way it needs a decision row (conflict 1).
2. **Retry and Pause availability** during these moments:
   - the opening hold: AC-5.7 holds "HUD input", but B-16 holds only booster input (BG:45);
   - targeting: should the press cancel targeting first?
   - `Paused(Saving)`: B-13 refuses Restart, and PROJECT.md:122 bars the popup;
   - Won Finish.
3. **Coin plus in game:** locked with no feedback, or press feedback only like Home? It must not open the Shop (RULE-24).
4. **Level number source:** is N the resolved database index + 1?
   - For a Hub launch the index is clamped (`R:Assets/ColorBlockJam/Gameplay/Composition/LevelLaunchResolution.cs:54-64`).
   - Test Level resolves by ID and returns index -1 (:67-70), so what should the label show then?
   - Direct Play uses the serialized index (:73-76).
   - After Next Level, the label must update.
5. **Timer format and layout:** keep `m:ss` (spec) or show `mm:ss` (references)? Should the "Time" caption and clock sit above the pill as in the references, or should the copy's icon-inside layout stay?
6. **Sprite choices:**
   - Should the level badge use `Btn_levelBg_normal` (reserved for Home)?
   - Should the pause backing use `Btn_settings_normal`?
   - Should the coin pill use `bg_home_coin_holder`?
   - Applying their borders means importer (`.meta`) writes, which are high risk. Should these sprites move into HudPopups (DEC-142, NFR-P6)?
7. **File ownership:** the top bar changes `GameplayHud.uxml`, `GameplayHud.uss` and `GameplayHudView`, plus the time-holder `.meta` (B-27). The booster session owns all of these, uncommitted. Which session writes them, and in what order?
8. **Pause scrim tap:** should it resume? Hub maps a scrim tap to `CloseRequested` (HUB:627), which would mean resume in game.
9. **Back precedence:** targeting cancel versus Pause (conflict 8). Who owns the Back action while the pause popup is open?
10. **HOME from pause to Hub:** which transition does it use? `LevelSession.GoHome` only sets the state: "scene navigation belongs to the Hub task" (C:Assets/ColorBlockJam/Gameplay/Session/LevelSession.cs:259-262). The Game→Hub route is not documented. Also, should `Btn_popup_blue` (DEC-41) be formally superseded?
11. **Panel for the top bar and pause popup:** `BoardOverlayPanelSettings` (match 0.5) or 1080×1920 Expand?
12. **Life placeholder** in the in-game HUD (PRD:73): required or not?
13. **DEC numbers** for the new HUD, Pause and Retry decisions, given the DEC-146/147 collision.