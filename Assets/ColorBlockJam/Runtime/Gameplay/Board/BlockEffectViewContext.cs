using System;
using ColorBlockJam.Presentation.Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>Stable scene presentation dependencies; no authored data or mutable attempt state.</summary>
    public sealed class BlockEffectViewContext
    {
        public BlockEffectViewContext(BoardArt art, Transform boardRoot, Camera camera, UIDocument document)
        {
            Art = art ?? throw new ArgumentNullException(nameof(art));
            BoardRoot = boardRoot != null ? boardRoot : throw new ArgumentNullException(nameof(boardRoot));
            Camera = camera;
            Document = document;
        }
        public BoardArt Art { get; }
        public Transform BoardRoot { get; }
        public Camera Camera { get; }
        public UIDocument Document { get; }
    }
}
