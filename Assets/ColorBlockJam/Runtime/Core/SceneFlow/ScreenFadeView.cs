using System;
using System.Threading;
using System.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Draws the black screen fade over every scene change (DEC-198): one full-screen element of the project scope's
    /// UI document, whose opacity one view-owned PrimeTween animates in unscaled time. Its panel sorts above every
    /// other panel, loading screen included; while the fade shows it blocks their presses, and hidden it neither draws
    /// nor picks.
    /// </summary>
    /// <remarks>
    /// Owner: this component on the persistent ProjectContext; the scene transition calls it. Stop points: each fade
    /// ends by itself, the caller's token stops it where it is, and <see cref="OnDisable"/> stops it and hides the
    /// black. The tween targets this view, so <c>Tween.GetTweensCount(view)</c> is 0 once it stopped. No Update.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)] // UIDocument creates its tree before this view hides the authoring preview.
    public sealed class ScreenFadeView : MonoBehaviour, IScreenFade
    {
        /// <summary>Seconds from clear to black; the old scene keeps running under the fade.</summary>
        public const float FadeOutSeconds = 0.25f;

        /// <summary>Seconds from black to clear, once the new scene has settled.</summary>
        public const float FadeInSeconds = 0.3f;

        private const string k_fadeName = "screen-fade";

        // PrimeTween rejects shorter durations; a smaller change is written at once.
        private const float k_minFadeSeconds = 0.001f;

        // Cached once: the tween's value callback, which captures nothing.
        private static readonly Action<ScreenFadeView, float> s_apply = (view, opacity) => view.Apply(opacity);

        private VisualElement m_fade;
        private Tween m_tween;
        private float m_opacity;
        private bool m_isShown;

        public bool IsVisible => m_isShown;

        /// <summary>The opacity last written, 0 (clear) to 1 (black); for tests and captures.</summary>
        public float Opacity => m_opacity;

        public Task FadeOutAsync(CancellationToken cancellationToken)
        {
            return FadeAsync(1f, FadeOutSeconds, Ease.InQuad, cancellationToken);
        }

        public Task FadeInAsync(CancellationToken cancellationToken)
        {
            return FadeAsync(0f, FadeInSeconds, Ease.OutQuad, cancellationToken);
        }

        private void OnEnable()
        {
            m_fade = GetComponent<UIDocument>().rootVisualElement.Q(k_fadeName);
            if (m_fade == null)
            {
                Debug.LogError($"Screen fade: the UI document has no '{k_fadeName}' element; scene changes run without a fade.",
                    this);
                return;
            }

            Apply(0f);
            Hide();
        }

        private void OnDisable()
        {
            m_tween.Stop();
            if (m_fade != null)
            {
                Apply(0f);
                Hide();
            }

            m_fade = null;
        }

        // Owner: the caller, which awaits the returned task; the tween itself belongs to this view. Stops: the fade's
        // end, the token (PrimeTween stops the tween on its next update and the await throws) or OnDisable.
        private async Task FadeAsync(float target, float seconds, Ease ease, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_fade == null)
            {
                return;
            }

            m_tween.Stop();
            Show();
            float duration = seconds * Mathf.Abs(target - m_opacity);
            if (duration < k_minFadeSeconds)
            {
                Apply(target);
            }
            else
            {
                m_tween = Tween.Custom(this, m_opacity, target, duration, s_apply, ease, useUnscaledTime: true)
                    .SetCancellationToken(cancellationToken);
                await m_tween;
            }

            // Disabled while the fade ran: OnDisable has already hidden it.
            if (m_fade != null && m_opacity <= 0f)
            {
                Hide();
            }
        }

        private void Apply(float opacity)
        {
            m_opacity = opacity;
            m_fade.style.opacity = opacity;
        }

        private void Show()
        {
            m_fade.style.display = DisplayStyle.Flex;
            m_isShown = true;
        }

        private void Hide()
        {
            m_fade.style.display = DisplayStyle.None;
            m_isShown = false;
        }
    }
}
