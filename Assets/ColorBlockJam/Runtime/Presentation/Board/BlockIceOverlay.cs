using System;
using PrimeTween;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// One frozen block's Ice look (DEC-184): the shared Ice material shown as a second material on the block's own
    /// tiles, with a crack amount that follows the remaining count. The Ice pass expands each surface slightly and
    /// reads the tile's own clip plane, so no second object is spawned and a recomposed footprint keeps the ice. Its
    /// owner attaches it on Home or when a Rocket part spawns and detaches it on thaw or teardown. Owner of its thaw
    /// and punch tweens; stop point: StopTweens and Detach.
    /// </summary>
    public sealed class BlockIceOverlay
    {
        private const float k_thawSeconds = 0.18f;
        private const float k_feedbackSeconds = 0.18f;
        private readonly Material m_material;
        private readonly Color m_tint;
        private readonly float m_opacity;
        private ModularBlockView m_view;
        private Tween m_thaw;
        private Tween m_feedback;
        private Transform m_feedbackTarget;
        private Vector3 m_feedbackOrigin;
        private float m_cracks;

        /// <summary>A look that reads its tint and opacity from the shared Ice material.</summary>
        public BlockIceOverlay(Material material)
        {
            m_material = material != null ? material : throw new ArgumentNullException(nameof(material));
            m_tint = material.GetColor(IceShader.TintId);
            m_opacity = material.GetFloat(IceShader.OpacityId);
        }

        /// <summary>The block view that shows this ice, or null while detached.</summary>
        public ModularBlockView View => m_view;
        public bool IsAttached => m_view != null;
        public bool IsThawing { get; private set; }
        public bool ThawFinished => IsThawing && !m_thaw.isAlive;

        /// <summary>Shows the ice over view's tiles with the crack amount of remaining out of initial.</summary>
        public void Attach(ModularBlockView view, int remaining, int initial)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (m_view != null) throw new InvalidOperationException("Detach the Ice from its block first.");
            m_view = view;
            SetCount(remaining, initial);
        }

        public void SetCount(int remaining, int initial)
        {
            m_cracks = initial > 0 ? 1f - (float)remaining / initial : 0f;
            Apply(m_opacity);
        }

        /// <summary>Starts the fade that ends the ice; Sync applies it and the owner detaches once it finished.</summary>
        public void Thaw()
        {
            if (!Application.isPlaying || IsThawing || m_view == null) return;
            StopTweens();
            IsThawing = true;
            m_thaw = Tween.Delay(m_view, k_thawSeconds);
        }

        public void Sync()
        {
            if (!IsThawing || !m_thaw.isAlive) return;
            Apply(m_opacity * (1f - m_thaw.elapsedTimeTotal / k_thawSeconds));
        }

        /// <summary>
        /// Punches the block root, which carries its iced tiles; a frozen block cannot be dragged or exit, so the
        /// presenter writes no position to this root while the punch runs.
        /// </summary>
        public void Shake()
        {
            if (!Application.isPlaying || m_view == null || IsThawing) return;
            StopTweens();
            m_feedbackTarget = m_view.transform;
            m_feedbackOrigin = m_feedbackTarget.localPosition;
            m_feedback = Tween.PunchLocalPosition(m_feedbackTarget, new Vector3(0.045f, 0f, 0.015f), k_feedbackSeconds, 18f);
        }

        public void SetPaused(bool paused)
        {
            if (m_thaw.isAlive) m_thaw.isPaused = paused;
            if (m_feedback.isAlive) m_feedback.isPaused = paused;
        }

        public void StopTweens()
        {
            if (m_thaw.isAlive) m_thaw.Stop();
            StopFeedback();
        }

        /// <summary>Stops an unfinished punch and puts the block root back where it started; the thaw keeps running.</summary>
        public void StopFeedback()
        {
            if (m_feedback.isAlive)
            {
                m_feedback.Stop();
                // An interrupted punch returns the block root to where it started.
                if (m_feedbackTarget != null) m_feedbackTarget.localPosition = m_feedbackOrigin;
            }
            m_feedbackTarget = null;
        }

        /// <summary>Removes the ice from the block's tiles and resets this look for the next block.</summary>
        public void Detach()
        {
            StopTweens();
            IsThawing = false;
            m_thaw = default;
            m_cracks = 0f;
            // A root a scene unload destroyed has no tiles left to clear.
            if (m_view != null) m_view.ClearIce();
            m_view = null;
        }

        private void Apply(float opacity)
        {
            if (m_view != null) m_view.SetIce(m_material, m_tint, opacity, m_cracks);
        }
    }
}
