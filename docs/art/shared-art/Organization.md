# Shared art organization

Owner: current Codex session, scoped to the moves below, their `.meta` files,
Editor/test/source-bake path references and current art documentation.
No scene, package, gameplay rule or FBX geometry changes. Unity AssetDatabase
moves preserve GUIDs and serialized bindings; no manual YAML edits.

| Asset | Canonical location under `Assets/ColorBlockJam` |
|---|---|
| Shared opaque material | `Art/Materials/Shared/MAT_Gameplay.mat` |
| Shared palette atlas | `Art/Textures/Atlases/T_GameplayPalette.png` |
| Vacuum sprite material | `Art/Materials/Effects/MAT_VacuumSprite.mat` |
| Vacuum animation atlas | `Art/Textures/Atlases/VacuumSuctionAtlas.png` |
| Hammer, RocketPurple, Vacuum model prefabs | `Prefabs/Boosters/Models/` |
| HammerEffect, RocketPurpleEffect, VacuumEffect | `Prefabs/Boosters/Effects/` |
| Booster FBX sources | `Art/Models/Boosters/` (unchanged) |

The shared palette is already a 128 x 32 RGB24 texture atlas, not a UI
SpriteAtlas. Its eight lower-row swatches preserve the Blender UV layout;
the upper white row supports owner-resolved GamePalette tints on blocks,
walls, gates, ground and arrows. No UV, pixel or import-format changes are
needed for this organization. Transparent Vacuum frames remain a separate
sprite atlas/material because they use different rendering behavior.

The unreferenced `MAT_BoosterPalette.mat` is removed after a Unity dependency
scan. Existing historical audit JSON files retain their original paths;
current authoring tools and mapping documentation use the canonical paths.

Verification (2026-09-25):
- All ten moves preserved GUIDs: [move audit](organization-moves.json).
- Booster prefab scan: 20 objects, no missing scripts or broken serialized
  references; the open scene remained clean: [reference audit](organization-references.json).
- SharedArtTests: 3/3 passed, including shared material bindings, atlas colors
  and import settings, and tint/clip/pool reset: [EditMode](organization-editmode.json).
- BoosterEffectViewTests: 12/12 passed, including contact, motion, suspension,
  cleanup and Vacuum frame bindings: [PlayMode](organization-playmode.json).
- Active import/preview tools, tests, PROJECT.md, README and current art/PRD
  mappings use the new paths. No material rebuild or animation rebake was needed.
