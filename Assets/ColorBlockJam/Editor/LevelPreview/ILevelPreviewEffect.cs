using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.LevelPreview
{
    /// <summary>
    /// LevelPreviewHost's extension contract for one preview effect adapter (Ice, Arrow). The host calls its
    /// members in this order for a build: Prepare once for the whole mapped level, then Attach once per block,
    /// then Reproject once after framing the camera; ReturnAll releases every attached visual before a rebuild
    /// reuses the pools, and Dispose releases the pools themselves. An adapter that can refuse a level before
    /// the scene changes also implements ILevelPreviewEffectCheck, which the host calls before Prepare.
    /// </summary>
    internal interface ILevelPreviewEffect : IDisposable
    {
        /// <summary>Readies this adapter's pools and per-block state for the whole mapped level.</summary>
        void Prepare(LevelData level, IReadOnlyList<LevelPreviewBlock> blocks, BoardArt art, Transform storage, Camera camera);

        /// <summary>Attaches this adapter's visual to one block's root, after Prepare.</summary>
        void Attach(int block, Transform root);

        /// <summary>Re-projects any screen-space visual onto the framed camera, after every block is attached.</summary>
        void Reproject();

        /// <summary>Releases every attached visual back to its pool, before the host recycles the block roots.</summary>
        void ReturnAll();
    }

    /// <summary>
    /// An optional check a preview effect adapter runs on the mapped level before the host changes the scene, so a
    /// level it cannot show (for example a missing prefab or material) is rejected with the last valid preview
    /// left untouched.
    /// </summary>
    internal interface ILevelPreviewEffectCheck
    {
        bool TryCheck(LevelData level, IReadOnlyList<LevelPreviewBlock> blocks, BoardArt art, out string error);
    }
}
