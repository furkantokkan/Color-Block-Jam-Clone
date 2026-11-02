# Existing-kit brick decision

- The background Blender proof ran against the unchanged supplied WallAndDoor.fbx. Axis-fit error was 0.000174; output is retained in `out/evidence.json` and the four `*_close.png`/`*_game.png` pairs.
- Root and an independent geometry reviewer rejected both composed candidates for the supplied reference. The two-column candidate has a visible 0.0471-unit centre trench and a 0.0114 cap transition. The single-corner candidate pinches at its inner join, with a 0.0112 step and incomplete sampled edge coverage.
- A full-width Wall core removes the middle trench but does not match the paired cap cross-section. Covering the transition with overlapping coplanar pieces would conceal the defect without producing a clean reusable brick. No further overlap-based iteration is selected.
- Decision: prefer a small Blender-authored end cap compatible with the existing Wall profile, preserving the original supplied FBX. This is conditional on the pending explicit asset/PRD-exception approval; no production asset is authorized or changed by this note.
- The proof checks composition only. It rebuilds smoothing and uses Blender Workbench lighting, so it is not Unity shader, colour, performance or final Game evidence. The mock floor is not a proposed floor change.
- Gate motion proceeds separately through the retained implementation patch and independent verification. No source, serialized asset, Editor state or VCS state was changed by the geometry proof.
