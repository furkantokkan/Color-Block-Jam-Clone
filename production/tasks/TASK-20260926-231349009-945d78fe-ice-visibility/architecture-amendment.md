# Shared block effects — implementation amendment

Status: approved by the user's direct shared-architecture instruction and "Ortak altyapıyı ekle ve uygula (önerilen)" on 2026-09-27 local time; no production writes yet.

The Arrow owner released its claim and restored its partial gameplay changes to the pre-task baseline. Its handoff proposes that Ice establishes the shared effect seams first, followed by Arrow. The original Ice plan assumed the completed Arrow hook source; that source is now absent. This amendment makes the new ordering concrete without adding serialized targets.

## Design

- Build immutable `BlockEffectContext` metadata for the exact `AttemptLevel` on Home in `GameAttemptLoader`. Modules read typed authored effects there; no JSON or authored-data reads occur in Playing.
- `EffectAttemptHookSource` iterates registered modules. Each returns fresh attempt state and registers Domain hooks. Publish the completed `BlockEffectAttempt` only after all modules succeed. Restart reuses metadata and pools, and creates fresh counters.
- `BoardPresenter` forwards its existing lifecycle to ordered `IBlockEffectView` instances. Include prepare, bind-attempt, attach, sync, reproject, clip, detach, pause, stop-tweens, return and dispose. Bind the new attempt before every Place, including same-level Restart.
- Domain events mutate counters; presentation reads them during the existing Sync. No Domain-to-view callbacks or new loop.
- Add canvas layers and preview adapters within their current Editor assemblies. They use Runtime/Presentation types, never Gameplay references. Frost draws below foreground badges and labels. Project counters explicitly after camera framing.
- Preserve current Arrow presentation and the existing authored-data marker overload. Arrow gameplay is still unsupported in the restored baseline; the following Arrow task owns its runtime flag and combined Arrow+Ice gameplay acceptance. Test generic hook composition here without claiming Arrow gameplay is enabled.
- Keep all six Ice outcomes, five approved protected asset targets, package/scene restrictions, and the independent verifier handoff. No new package, assembly, prefab or project-setting target.

## Additional source ownership

Paths below are relative to `Assets/ColorBlockJam`, with matching `.meta` files for new scripts:

- `Gameplay/Effects/BlockEffectContext.cs`, `BlockEffectContextSource.cs`, `BlockEffectAttempt.cs`, `IBlockEffectModule.cs`, `IBlockEffectLevel.cs`, `IBlockEffectState.cs`.
- `Gameplay/Board/IBlockEffectView.cs`, `BlockEffectViewContext.cs`.
- `Gameplay/Session/EffectAttemptHookSource.cs.meta` (the script was already approved but now needs creation).
- `Editor/Levels/Canvas/ILevelCanvasEffectLayer.cs`, `IceCanvasEffectLayer.cs`.
- `Editor/LevelPreview/ILevelPreviewEffect.cs`, `IcePreviewEffect.cs`.
- `Tests/Gameplay/EditMode/Gameplay/BlockEffectCompositionTests.cs`.
- `Tests/EditMode/LevelCanvasEffectLayerTests.cs`.
- `Tests/LevelPreview/LevelPreviewEffectAdapterTests.cs`.
- Repository document `docs/Block-Effects-Architecture.md` to record the final shared contracts for the Arrow successor.

The implementer owns these files sequentially with the existing Ice paths. The root owns lifecycle records. The verifier follows a stable implementation handoff and does not fix production code.
