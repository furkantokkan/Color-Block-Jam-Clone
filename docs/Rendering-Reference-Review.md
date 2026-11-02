# Rendering reference review

Date: 2026-09-24. Decision: PRD DEC-96, refining DEC-58 and NFR-P5.
Scope: source/material inspection and implementation direction. No shader,
material, package, scene or pipeline asset was imported or changed by this review.

## Decision

Use Pixel Flow's **shared material and cached MaterialPropertyBlock (MPB)
pattern**, adapted into a small target-owned renderer. Use CBJ Template for the
**block/gate/wall lighting, rounded specular look and opaque ice treatment**.
Keep Unity 2022.3.62f2 / URP 14.0.12, the supplied modular meshes, Onity,
PrimeTween and UI Toolkit.

The block shader uses direct palette color through an explicitly
GPU-instanced property. **2026-09-25 update:** the user's shared-art request adds
one 128 x 32 swatch atlas and instanced UV scale/offset to this shader. Booster
meshes sample their eight original swatches; board/environment meshes sample
white and keep their dynamic tint. All opaque art now references `MAT_Gameplay`.
The original mesh UVs are unchanged. See the [implementation and comparison](art/shared-art/README.md).
This is the implementation choice, not a measured frame-time win:
compare it with a small prebuilt per-color material palette under NFR-P5 before
retaining the final rendering path. Ship one path, not two runtime frameworks.

## Evidence and reuse boundary

Source roots are `C:/Users/e-fur/Documents/Repos/Pixel Flow Clone` and
`C:/Users/e-fur/Documents/Repos/CBJ Template`. References below point to the
files inspected, rather than an assumed feature inferred from a screenshot.

| Source | Observed implementation | Decision |
| --- | --- | --- |
| Pixel Flow [ColorAtlasPicker.shader](<C:/Users/e-fur/Documents/Repos/Pixel Flow Clone/Assets/Core/Modules/Color Atlas/Shaders/ColorAtlasPicker.shader:43>) and [shared material](<C:/Users/e-fur/Documents/Repos/Pixel Flow Clone/Assets/Materials/Shared/PixelFlowAtlasShared.mat:11>) | Real gameplay prefabs use the shared material. Instancing variants and per-instance atlas/outline fields exist. The project is Unity 6000.0.62f1 / URP 17.0.4. | Adapt the shared-material and instanced-property design in URP 14; do not transfer newer serialized assets or the source render pipeline. |
| Pixel Flow [AtlasColorController.cs](<C:/Users/e-fur/Documents/Repos/Pixel Flow Clone/Assets/Core/Modules/Color Atlas/Scripts/AtlasColorController.cs:381>) | Reuses one MPB and cached property IDs, but application also reads `sharedMaterials`, looks up components and constructs a debug string. `LateUpdate` polls for changes. | Retain cached IDs/MPB and change-only application; cache renderer slots once, apply one complete state, and remove polling and per-application allocation. Do not copy the controller and its TMP/domain dependencies. |
| Pixel Flow [outline/main passes](<C:/Users/e-fur/Documents/Repos/Pixel Flow Clone/Assets/Core/Modules/Color Atlas/Shaders/ColorAtlasPicker.shader:30>) | Inverted-hull outline in world units; disabled outlines clip fragments. Main pass uses a fixed light direction and a 16-by-16 atlas. No ShadowCaster, DepthOnly or DepthNormals passes. | Neither a ready whole-block outline nor a drop-in shader for the target's shadows/SSAO. Clipping a disabled outline does not remove its submitted pass cost. |
| Template [LevelBlockBehavior.cs](<C:/Users/e-fur/Documents/Repos/CBJ Template/Assets/Project Files/Game/Scripts/Level System/LevelBlockBehavior.cs:48>) and [Block.shader](<C:/Users/e-fur/Documents/Repos/CBJ Template/Assets/Project Files/Game/Shaders/Watermelon Shaders/Shaders/Block.shader:110>) | Blocks/gates use palette materials with `WMelon/Block`; borders use `WMelon/Universal`. Same Editor/URP version as this case. Shader includes many unrelated lighting/foliage/wind variants. | Reference for appearance and required URP passes; build only the features the case uses. No Watermelon framework or shader-library import. |
| Template [base block material](<C:/Users/e-fur/Documents/Repos/CBJ Template/Assets/Project Files/Game/Materials/Colors/Base Block Material.mat:26>) and [ShaderHelper.cs](<C:/Users/e-fur/Documents/Repos/CBJ Template/Assets/Project Files/Game/Shaders/Watermelon Shaders/Scripts/ShaderHelper.cs:13>) | Normal block materials disable the outline pass. Outline helpers have no C# call sites; outlined materials are used by lock/key effects. | Do not describe this as an existing held-block outline implementation. The user's white whole-footprint silhouette remains the requirement. |
| Template [BlockClip.cs](<C:/Users/e-fur/Documents/Repos/CBJ Template/Assets/Project Files/Game/Scripts/Other/BlockClip.cs:19>) and [BlockLitPass.hlsl](<C:/Users/e-fur/Documents/Repos/CBJ Template/Assets/Project Files/Game/Shaders/Watermelon Shaders/Shaders/Includes/Universal/BlockLitPass.hlsl:17>) | Exit clipping writes an MPB, then clears all overrides. Clip uniforms are outside the material CBUFFER, and clipping is in the forward pass only. | Reuse the idea of clipping at the gate plane, with unified property ownership and matching clipping in color, depth, normals, shadow and any outline pass. Otherwise tint can disappear or invisible geometry can leave shadows/occlusion. |
| Template [Ice Shader.shadergraph](<C:/Users/e-fur/Documents/Repos/CBJ Template/Assets/Project Files/Game/Shaders/Ice Shader.shadergraph:596>) | URP Lit, opaque, texture/color/Fresnel/emission/smoothness; no scene-color/depth refraction nodes found. Ice behavior obtains unique materials and uses Watermelon/TMP behavior. | Reproduce the visual technique using the case's `icetexture.jpg` and shared Shader Graph overlay. Do not copy material cloning or behavior dependencies. |

## Target implementation rules

1. **One property owner per renderer/material slot.** Cache renderer references,
   eligible slots, static shader IDs and the reusable MPB at load. Compose tint,
   gate clipping and relevant effect values before one application. Independent
   effects submit state to this owner; they never clear or overwrite another
   effect's MPB. Pool release resets all owned values; reuse cannot inherit tint,
   clipping or selection. No `.material` getter, `new Material`, renderer
   discovery, `sharedMaterials` array reads or debug interpolation in play.
2. **Update only when state changes.** Tint changes on spawn/recolor. Gate clipping
   and animated effects may change during their owned presentation, driven by the
   session/PrimeTween lifecycle. This does not introduce renderer `Update` or
   `LateUpdate` loops. Suspension, teardown and bounded Won Finish keep their
   existing rules.
3. **Instancing must be real.** Declare every per-renderer MPB field as instanced
   in each applicable shader pass; pass instance IDs correctly and verify the
   resulting draw events. Use consistent `UnityPerMaterial` layouts for shared
   values. Same material alone does not merge different meshes or render passes.
   Enabling GPU Instancing on stock URP Simple Lit does not by itself make
   `_BaseColor` a classic MeshRenderer instanced property; its DOTS declarations
   do not establish that behavior.
4. **Match the target renderer.** Android/iOS default to Balanced, which enables
   SRP Batcher and has active SSAO in
   [URP-Balanced-Renderer.asset](../Assets/ColorBlockJam/Settings/URP-Balanced-Renderer.asset).
   The Editor was using High Fidelity during inspection. The adapted shader must
   provide the forward lighting, depth/normals and shadow behavior the target
   uses; verify gate clipping consistently in those passes. Do not disable SSAO
   or copy a source quality profile to make an incomplete port appear correct.
5. **Selection is one white silhouette of the resolved footprint.** Use a
   dedicated pooled footprint-outline visual, with no internal modular seams,
   and stable apparent thickness at both portrait ratios. Reuse preallocated
   storage when Rocket changes the footprint. Enable it only for selection;
   clear it on release/cancel/state change/pool return. The source per-mesh
   inverted hull is not assumed to satisfy concave, multi-cell silhouettes.
6. **Keep distinct effects distinct.** Block Ice retains its shared Shader Graph
   overlay, counter-driven cracks and UI Toolkit label. Time Freeze retains its
   separate screen-edge Shader Graph frost and 10-second indicator. Inspect the
   generated graph passes before claiming MPB instancing support. Use the
   supplied `icetexture.jpg`; no atlas replacement is needed. The user's
   2026-09-25 extension replaces the separate opaque booster material bindings
   with `MAT_Gameplay`. The transparent baked Vacuum keeps `MAT_VacuumSprite`
   because it needs alpha blending and already contains offline lighting.

MPB renderers leave the SRP Batcher path; SRP Batcher reduces CPU preparation
work and does not mean all geometry becomes one draw. Non-instanced properties
in an MPB can prevent GPU instancing. These are why the two material approaches
need an actual comparison, not an assumption based on material count.
[Unity 2022.3 SRP Batcher](https://docs.unity3d.com/2022.3/Documentation/Manual/SRPBatcher.html),
[instanced shader properties](https://docs.unity3d.com/2022.3/Documentation/Manual/gpu-instancing-shader.html).
Unity copies the submitted MPB, allowing the owner to reuse its cached block;
an MPB cannot change render state.
[MaterialPropertyBlock API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MaterialPropertyBlock.html).

## Verification and completion note

- **Completed:** read-only source/prefab/material inspection of both references;
  exact target Editor/project verification; checked URP versions, quality assets
  and active target SRP Batcher. Recorded DEC-96, updated the linked performance
  requirements, and reconciled the older reuse/quality decisions with DEC-94.
- **Not implemented or measured here:** the target has no gameplay renderer yet.
  No shader compilation/port, device rendering comparison, instancing proof or
  FPS/GC improvement is claimed. Existing imported art and settings are intact.
- **Implementation verification:** compare A (shared material, explicitly
  instanced MPB values) and B (prebuilt color palette, no MPB on ordinary block
  renderers, SRP Batcher) on the same level 5, camera, quality, lighting,
  passes and device. First compare the steady board, then the same exit/effect
  sequence. B may still need owned MPB overrides on actively clipping/effected
  renderers; include those batching breaks and costs instead of disabling the
  effect or calling the whole sequence SRP-batched. Record batches, SetPass,
  instancing/batch-break reasons,
  main/render-thread time, GPU time when available, GC and material count. Prefer
  lower device frame cost while meeting NFR-P4; retain one implementation.
- **Visual/device checks:** white concave outlines without seams at 1080x1920
  and 1080x2400; Rocket footprint changes; exit color/depth/shadow consistency;
  Ice plus selection/clipping; pause/reuse/teardown reset. Confirm Android
  IL2CPP ARM64 on Vulkan and OpenGLES3 using the retained Balanced profile.
