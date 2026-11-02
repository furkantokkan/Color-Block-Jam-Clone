using Cysharp.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Runtime.Startup
{
    /// <summary>
    /// Draws the startup loading bar and its percentage from the Init scene's UI document; it holds no flow logic and
    /// runs no loop of its own (DEC-140).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class StartupLoadingView : MonoBehaviour
    {
        private const string k_fillName = "startup-loading-fill";
        private const string k_percentName = "startup-loading-percent";

        private VisualElement m_fill;
        private Label m_percent;
        private int m_shownPercent = -1;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            m_fill = root.Q(k_fillName);
            m_percent = root.Q<Label>(k_percentName);
            if (m_fill == null || m_percent == null)
            {
                Debug.LogError("Startup loading view: the UXML lacks the bar fill or the percentage label.", this);
            }

            m_shownPercent = -1;
            SetProgress(0f);
        }

        private void OnDisable()
        {
            m_fill = null;
            m_percent = null;
        }

        /// <summary>Shows normalized progress; the label text changes only when the whole percentage changes.</summary>
        public void SetProgress(float progress)
        {
            if (m_fill == null || m_percent == null)
            {
                return;
            }

            float value = Mathf.Clamp01(progress);
            m_fill.style.width = Length.Percent(value * 100f);
            int percent = Mathf.FloorToInt(value * 100f);
            if (percent == m_shownPercent)
            {
                return;
            }

            m_shownPercent = percent;
            m_percent.text = ZString.Concat(percent, "%");
        }
    }
}
