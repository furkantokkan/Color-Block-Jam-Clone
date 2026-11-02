using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>
    /// Treats a press as UI when the top pickable element of any active runtime UI Toolkit panel lies
    /// under it (AC-3.13). It runs only on pointer-down, never per frame; whether Pick allocates is
    /// still UNPROVEN on device.
    /// </summary>
    public sealed class UiToolkitPointerFilter : IUiPointerFilter
    {
        private readonly UIDocument[] m_documents;

        public UiToolkitPointerFilter(UIDocument[] documents)
        {
            if (documents == null)
            {
                throw new ArgumentNullException(nameof(documents));
            }

            m_documents = (UIDocument[])documents.Clone();
        }

        public bool IsOverUi(Vector2 screenPosition)
        {
            // Screen y points up and panel y points down, so flip before converting.
            Vector2 flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            for (int i = 0; i < m_documents.Length; i++)
            {
                UIDocument document = m_documents[i];
                if (document == null || !document.isActiveAndEnabled)
                {
                    continue;
                }

                VisualElement root = document.rootVisualElement;
                IPanel panel = root != null ? root.panel : null;
                if (panel == null)
                {
                    continue;
                }

                VisualElement picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped));
                if (picked != null && picked.pickingMode != PickingMode.Ignore)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
