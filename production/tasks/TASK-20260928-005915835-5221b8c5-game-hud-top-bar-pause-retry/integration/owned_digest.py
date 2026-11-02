"""Computes this task's reviewed-revision owned digest (reviewed-evidence-identity.md).

Usage: python owned_digest.py <repo root> <plastic changeset number>
The owned set is every file under the task's owned paths (contract Ownership, final post-move paths),
excluding the task folder itself (its lifecycle block and notes are lifecycle records, not reviewed bytes).
"""
import hashlib
import os
import sys

OWNED_TREES = [
    "Assets/ColorBlockJam/Runtime/Gameplay/Hud",
    "Assets/ColorBlockJam/UI/Game/TopBar",
    "Assets/ColorBlockJam/UI/Game/Overlays",
    "Assets/ColorBlockJam/Tests/Gameplay/EditMode/Hud",
    "Assets/ColorBlockJam/Tests/Gameplay/PlayMode/Hud",
]
OWNED_FILES = [
    "Assets/ColorBlockJam/Runtime/Gameplay/Hud.meta",
    "Assets/ColorBlockJam/UI/Game/TopBar.meta",
    "Assets/ColorBlockJam/UI/Game/Overlays.meta",
    "Assets/ColorBlockJam/Tests/Gameplay/EditMode/Hud.meta",
    "Assets/ColorBlockJam/Tests/Gameplay/PlayMode/Hud.meta",
    "Assets/ColorBlockJam/Runtime/Gameplay/Session/IGameplayBackFallback.cs",
    "Assets/ColorBlockJam/Runtime/Gameplay/Session/IGameplayBackFallback.cs.meta",
    "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml",
    "Assets/ColorBlockJam/UI/Game/GameplayHud.uss",
    "Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameSceneInstaller.cs",
    "Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameAttemptLoader.cs",
    "Assets/ColorBlockJam/Runtime/Gameplay/Composition/ApplicationPauseRelay.cs",
    "Assets/ColorBlockJam/Runtime/Gameplay/Session/DragInputRouter.cs",
    "Assets/ColorBlockJam/Art/UI/Panels/bg_home_coin_holder.png.meta",
    "PROJECT.md",
    "docs/Game-Hud.md",
    "docs/UI-Asset-Import.md",
    "docs/Color-Block-Jam-Visual-References.md",
    "docs/media/color-block-jam/user-references/21-game-hud-top-bar.webp",
]


def collect(root):
    paths = set(OWNED_FILES)
    for tree in OWNED_TREES:
        for base, _, files in os.walk(os.path.join(root, tree)):
            for name in files:
                full = os.path.join(base, name)
                paths.add(os.path.relpath(full, root).replace("\\", "/"))
    return sorted(paths)


def main():
    root, changeset = sys.argv[1], sys.argv[2]
    digest = hashlib.sha256()
    paths = collect(root)
    for rel in paths:
        digest.update(rel.encode("utf-8"))
        digest.update(b"\0")
        full = os.path.join(root, rel)
        if os.path.isfile(full):
            with open(full, "rb") as handle:
                digest.update(handle.read())
        else:
            digest.update(b"<missing>")
        digest.update(b"\0")
    print(f"vcs:plastic:cs:{changeset}@ColorBlockJamCase2026@efurk64@unity+owned-sha256:{digest.hexdigest()}")
    print(f"files={len(paths)}", file=sys.stderr)


if __name__ == "__main__":
    main()
