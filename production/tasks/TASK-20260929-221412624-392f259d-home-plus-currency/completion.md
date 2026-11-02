# Home plus currency routing

- Changed `DummyCurrencyReward` to bind the authored life and coin holders by stable currency ID. Life grants +1 life only; coin grants +100 coins only. This supersedes the combined dummy reward recorded in PROJECT.md DEC-161.
- Preserved save-owner clamping, commit-before-publish, one pending grant, and teardown cancellation. No layout, serialized asset, package, save-schema or player-service changes.
- Files: reward implementation, installer comment, seven focused EditMode cases and generated test `.meta`.
- Verification: production and test Roslyn checks COMPILE_OK; exact Unity 2022.3.62f2 Editor/project readback; EditMode job `561a4aff521240dc8fa384a0274307c1` and shared request `cdc6e3902302` PASS 7/7; independent source/test review PASS.
- Review set includes the four source/test files plus Home.uxml, CurrencyHolder.uxml and currency catalog/life/coin assets. Owned digest uses sorted forward-slash paths, NUL, raw bytes, NUL.
- No device build or checkin. Task is ready for explicit closure.
