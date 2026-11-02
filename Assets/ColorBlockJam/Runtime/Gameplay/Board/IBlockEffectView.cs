using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Gameplay.Effects;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>Scene-owned effect presentation, driven only by the existing board presenter lifecycle.</summary>
    public interface IBlockEffectView : IDisposable
    {
        string TypeId { get; }
        bool TryPrepare(BlockEffectContext context, IBlockEffectLevel metadata, out string error);
        void BindAttempt(BlockEffectAttempt attempt, IBlockEffectState state);
        void Attach(int block, Transform root);
        void Sync(IBoardReader board);
        void Reproject();
        void SetClipPlane(int block, Vector4 plane, bool enabled);
        void SetPaused(bool paused);
        void StopTweens();
        void Detach(int block);
        void ReturnAll();
    }
}
