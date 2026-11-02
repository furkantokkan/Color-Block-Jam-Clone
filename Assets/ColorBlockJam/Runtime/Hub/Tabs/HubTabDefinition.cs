using System;
using UnityEngine;

namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// Authored, read-only description of one Home tab: which slot it drives, what it shows, when it is open and
    /// which menu it opens. One entry of <see cref="HubNavigationCatalog"/>; the player's state lives elsewhere.
    /// </summary>
    [Serializable]
    public sealed class HubTabDefinition
    {
        public const int MaxIdLength = 32;

        /// <summary>Home.uxml names the slot for tab "shop" <c>tab-shop</c>.</summary>
        public const string SlotNamePrefix = "tab-";

        [Tooltip("Stable ID, for example shop. Home.uxml names its slot 'tab-<id>'. Lowercase letters, digits and '-'.")]
        [SerializeField] private string m_id;
        [Tooltip("Text under the icon while the tab is open. Leave empty for no label.")]
        [SerializeField] private string m_label;
        [Tooltip("The tab's own icon, shown while open and, dimmed, under the lock.")]
        [SerializeField] private Sprite m_icon;
        [SerializeField] private HubTabAvailability m_availability;
        [Tooltip("Level Gated only: the tab opens when the player reaches this level.")]
        [SerializeField, Min(1)] private int m_unlockLevel = 1;
        [SerializeField] private HubTabLockedLook m_lockedLook;
        [Tooltip("While locked, show 'Lvl N' (Level Gated) or the label (Locked) under the lock.")]
        [SerializeField] private bool m_showLabelWhileLocked;
        [Tooltip("Menu opened on press, registered with HubNavigator.RegisterMenu under this ID. Empty: press feedback only.")]
        [SerializeField] private string m_menuId;

        /// <summary>Unity creates entries through this constructor when a designer adds one in the Inspector.</summary>
        public HubTabDefinition()
        {
        }

        public HubTabDefinition(string id, string label, Sprite icon, HubTabAvailability availability, int unlockLevel,
            HubTabLockedLook lockedLook, bool showLabelWhileLocked, string menuId)
        {
            m_id = id;
            m_label = label;
            m_icon = icon;
            m_availability = availability;
            m_unlockLevel = unlockLevel;
            m_lockedLook = lockedLook;
            m_showLabelWhileLocked = showLabelWhileLocked;
            m_menuId = menuId;
        }

        public string Id => m_id;
        public string Label => m_label ?? string.Empty;
        public Sprite Icon => m_icon;
        public HubTabAvailability Availability => m_availability;
        public int UnlockLevel => m_unlockLevel;
        public HubTabLockedLook LockedLook => m_lockedLook;
        public bool ShowLabelWhileLocked => m_showLabelWhileLocked;
        public string MenuId => m_menuId ?? string.Empty;
        public bool OpensMenu => !string.IsNullOrEmpty(m_menuId);

        /// <summary>True when the ID is 1 to 32 lowercase letters, digits or '-'; menu IDs follow the same rule.</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength)
            {
                return false;
            }

            for (int index = 0; index < id.Length; index++)
            {
                char character = id[index];
                bool allowed = (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9')
                               || character == '-';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        public bool Validate(out string error)
        {
            if (!IsValidId(m_id))
            {
                error = $"Tab '{m_id}' needs an ID of 1 to {MaxIdLength} lowercase letters, digits or '-'.";
                return false;
            }

            if (m_icon == null)
            {
                error = $"Tab '{m_id}' needs an icon.";
                return false;
            }

            if (!Enum.IsDefined(typeof(HubTabAvailability), m_availability))
            {
                error = $"Tab '{m_id}' has an unknown availability.";
                return false;
            }

            if (m_availability == HubTabAvailability.LevelGated && m_unlockLevel < 1)
            {
                error = $"Tab '{m_id}' is Level Gated and needs an unlock level of 1 or more.";
                return false;
            }

            if (!Enum.IsDefined(typeof(HubTabLockedLook), m_lockedLook))
            {
                error = $"Tab '{m_id}' has an unknown locked look.";
                return false;
            }

            if (OpensMenu && !IsValidId(m_menuId))
            {
                error = $"Tab '{m_id}' names menu '{m_menuId}'; a menu ID is 1 to {MaxIdLength} lowercase letters, "
                        + "digits or '-', or empty for no menu.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
