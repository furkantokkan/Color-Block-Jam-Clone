# Path resolution and execution decisions

Recorded 2026-09-28T12:35Z by the implementer session "Booster sistemi dinamik UI [5bcc6f]", before the first
write, as the contract's stop condition requires ("The folder restructure moved an owned path: re-resolve the new
path and record it before writing").

## Implementer

The contract names session "Booster gameplay flow [f1ed7e]". That session is no longer live (ListAgents,
2026-09-28T12:20Z). The user asked this session to check and start the task, so it takes the implementer and
decision-owner role with the same authority. No scope, risk or ownership changes.

## Owned paths after DEC-157

| Contract path | Current path |
| --- | --- |
| `Runtime/Boosters/*` | `Runtime/Core/Boosters/*` |
| `Gameplay/Boosters/**` | `Runtime/Gameplay/Boosters/**` |
| `Gameplay/Session/LevelSession.cs` | `Runtime/Gameplay/Session/LevelSession.cs` |
| `Gameplay/UI/*.cs` (listed files) | `Runtime/Gameplay/UI/*.cs` |
| `Gameplay/UI/GameplayHud.uxml`, `.uss` | `UI/Game/GameplayHud.uxml`, `.uss` |
| `Gameplay/UI/BoosterButton.uxml` (new) | `UI/Game/BoosterButton.uxml` (DEC-157: UXML lives under `UI/<Screen>`) |
| `Gameplay/Board/BoosterCasts.cs`, `BoardPresenter.cs` | `Runtime/Gameplay/Board/...` |
| `Gameplay/Composition/GameSceneInstaller.cs`, `GameAttemptLoader.cs` | `Runtime/Gameplay/Composition/...` |
| `Hub/PreLevel/*.cs` | `Runtime/Hub/PreLevel/*.cs` |
| `Hub/UI/PreLevel.uxml`, `.uss` | `UI/Hub/PreLevel.uxml`, `.uss` |
| `Tests/Gameplay/**` | `Tests/Gameplay/**` (unchanged) |
| `Tests/Hub/EditMode/*`, `Tests/Hub/PlayMode/HubLaunchTests.cs` | unchanged |
| `Tests/PlayMode/BoosterEffectViewTests.cs` | `Tests/Presentation/PlayMode/BoosterEffectViewTests.cs` |

Forbidden paths after DEC-157:

- `Assets/ColorBlockJam/UI/**` meant the shared popup assembly `ColorBlockJam.UI`. Its scripts are now under
  `Runtime/UI/**`, and its UXML/USS under `UI/Purchase/**`, `UI/Settings/**`, `UI/Common/**` and `UI/Themes/**`.
  Those stay forbidden. The owned `UI/Game/GameplayHud.*`, `UI/Game/BoosterButton.uxml` and `UI/Hub/PreLevel.*` are
  writable, as the table above maps them.
- `Gameplay/Hud/**` → `Runtime/Gameplay/Hud/**`; `Domain/**` → `Runtime/Domain/**`; `Player/**` →
  `Runtime/Player/**`; `Presentation/**` → `Runtime/Presentation/**`. All stay forbidden.
- `Runtime/Boosters/BoosterDefinition.cs` → `Runtime/Core/Boosters/BoosterDefinition.cs`, still forbidden
  (currency-amount-price).

## User decisions (AskUserQuestion, 2026-09-28T12:28Z)

1. Bar layout when a bar outgrows one row: wrap to rows. The HUD bar holds at most 4 buttons per row (8 → 2 × 4). The
   pre-level popup holds 3 selectors per row. The button size stays the same; with more than 4 in-game boosters the
   board area shrinks by one row.
2. The four serialized targets under Ownership are approved: the two new layout assets, the two layout references in
   `BoosterCatalog.asset`, and the targeting instruction and behaviour kind in `Rocket.asset`, `Hammer.asset` and
   `Vacuum.asset`.
3. Start now; the files other sessions are editing right now (`BoardPresenter.cs`, `BoosterCasts.cs`, last written
   12:21–12:22) are changed last, after their writers finish.

## Execution environment

- Isolated copy: `%TEMP%\cbj-boosterbars-5bcc6f` (robocopy /MIR at 12:32, Library included, Bee excluded).
- Base snapshot for the guarded copy-back: `%TEMP%\cbj-boosterbars-5bcc6f-base`. At copy-back, a live file equal to
  its base is replaced; a live file that changed since is merged three-way (`git merge-file`), never overwritten.

## Contract gaps found at the check (2026-09-28T12:25Z)

- AC-7's "a new definition asset of an existing behaviour kind ... with no C# change" cannot hold in full: stock is
  saved per `BoosterId` name (`PlayerSnapshot`, DEC-129), and `Player/**` and save migrations are forbidden. A new
  booster therefore needs one appended `BoosterId` value; everything else about it is data.
- `HubSceneInstaller.cs` and `ProjectScopeInstaller.cs` are outside the write scope, so no app-wide availability
  binding is possible. The Game scene binds one availability; the pre-level presenter builds its own from the
  catalog (its `[Inject]` constructor). A remote-config override that must reach both scenes needs a later
  project-scope binding.
