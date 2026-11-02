using System;
using System.Collections.Generic;

namespace ColorBlockJam.Hub.Navigation
{
    /// <summary>
    /// Keeps the Hub's single overlay stack: opening pushes and shows, closing pops and hides, and only
    /// the top overlay can close through Back (design 6.4, AC-2.6). Settings, the pre-level popup and the purchase
    /// popup open by <see cref="HubOverlayId"/>; tab menus open by string ID on the same stack (DEC-169).
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle). Stops: <see cref="Dispose"/> at Hub unload. Opening an
    /// unregistered overlay or menu is a normal state, so it logs nothing; the tab presenter reports a tab whose
    /// menu is missing. Menus take string IDs so a new menu needs only a <see cref="RegisterMenu"/> call from the
    /// composition root, with no enum or navigator change.
    /// </remarks>
    public sealed class HubNavigator : IHubNavigator, IHubMenuNavigator, IDisposable
    {
        // One slot per HubOverlayId value; update together with the enum.
        private const int k_overlayCount = 3;

        private readonly IHubOverlay[] m_overlays = new IHubOverlay[k_overlayCount];
        private readonly Dictionary<string, IHubOverlay> m_menus = new Dictionary<string, IHubOverlay>(StringComparer.Ordinal);
        private readonly List<IHubOverlay> m_stack = new List<IHubOverlay>(k_overlayCount);

        public int OpenCount => m_stack.Count;

        /// <summary>Registers the overlay that <paramref name="id"/> opens; one overlay per id.</summary>
        public void Register(HubOverlayId id, IHubOverlay overlay)
        {
            if (overlay == null)
            {
                throw new ArgumentNullException(nameof(overlay));
            }

            int index = ToIndex(id);
            if (m_overlays[index] != null && !ReferenceEquals(m_overlays[index], overlay))
            {
                throw new InvalidOperationException($"Hub navigator: the {id} overlay is already registered.");
            }

            m_overlays[index] = overlay;
        }

        /// <summary>Removes the overlay's registration, hiding it first when it is open.</summary>
        public void Unregister(HubOverlayId id)
        {
            int index = ToIndex(id);
            IHubOverlay overlay = m_overlays[index];
            if (overlay == null)
            {
                return;
            }

            if (m_stack.Remove(overlay))
            {
                overlay.Hide();
            }

            m_overlays[index] = null;
        }

        /// <summary>Registers the menu a tab opens by naming <paramref name="menuId"/>; one menu per ID.</summary>
        public void RegisterMenu(string menuId, IHubOverlay menu)
        {
            if (string.IsNullOrEmpty(menuId))
            {
                throw new ArgumentException("Hub navigator: a menu needs an ID.", nameof(menuId));
            }

            if (menu == null)
            {
                throw new ArgumentNullException(nameof(menu));
            }

            if (m_menus.TryGetValue(menuId, out IHubOverlay registered) && !ReferenceEquals(registered, menu))
            {
                throw new InvalidOperationException($"Hub navigator: the '{menuId}' menu is already registered.");
            }

            m_menus[menuId] = menu;
        }

        /// <summary>Removes the menu's registration, hiding it first when it is open.</summary>
        public void UnregisterMenu(string menuId)
        {
            if (menuId == null || !m_menus.TryGetValue(menuId, out IHubOverlay menu))
            {
                return;
            }

            if (m_stack.Remove(menu))
            {
                menu.Hide();
            }

            m_menus.Remove(menuId);
        }

        public bool HasMenu(string menuId)
        {
            return menuId != null && m_menus.ContainsKey(menuId);
        }

        public bool OpenMenu(string menuId)
        {
            return menuId != null && m_menus.TryGetValue(menuId, out IHubOverlay menu) && Push(menu);
        }

        public bool OpenSettings()
        {
            return Open(HubOverlayId.Settings);
        }

        public bool OpenPreLevel()
        {
            return Open(HubOverlayId.PreLevel);
        }

        public bool Open(HubOverlayId id)
        {
            return Push(m_overlays[ToIndex(id)]);
        }

        public bool Close(HubOverlayId id)
        {
            IHubOverlay overlay = m_overlays[ToIndex(id)];
            int top = m_stack.Count - 1;
            if (overlay == null || top < 0 || !ReferenceEquals(m_stack[top], overlay))
            {
                return false;
            }

            return CloseTop();
        }

        public bool CloseTop()
        {
            int top = m_stack.Count - 1;
            if (top < 0)
            {
                return false;
            }

            IHubOverlay overlay = m_stack[top];
            m_stack.RemoveAt(top);
            overlay.Hide();
            return true;
        }

        public bool IsOpen(HubOverlayId id)
        {
            IHubOverlay overlay = m_overlays[ToIndex(id)];
            return overlay != null && m_stack.Contains(overlay);
        }

        public void Dispose()
        {
            m_stack.Clear();
            Array.Clear(m_overlays, 0, m_overlays.Length);
            m_menus.Clear();
        }

        /// <summary>Pushes and shows <paramref name="overlay"/>; false when it is missing or already open.</summary>
        private bool Push(IHubOverlay overlay)
        {
            if (overlay == null || m_stack.Contains(overlay))
            {
                return false;
            }

            m_stack.Add(overlay);
            overlay.Show();
            return true;
        }

        private static int ToIndex(HubOverlayId id)
        {
            int index = (int)id;
            if (index < 0 || index >= k_overlayCount)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            return index;
        }
    }
}
