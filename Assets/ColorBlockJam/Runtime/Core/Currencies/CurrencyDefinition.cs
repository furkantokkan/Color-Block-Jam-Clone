using UnityEngine;

namespace ColorBlockJam.Runtime.Currencies
{
    /// <summary>
    /// Authored, read-only description of one currency: its stable save key, name, icon, starting amount, maximum
    /// Home display and optional timed or paid refill (Lives, DEC-174). Balances belong to the player save service; a new currency needs only a new asset listed
    /// in the currency catalog (Hub-And-Player-Data-Design 3.3).
    /// </summary>
    [CreateAssetMenu(
        fileName = "Currency",
        menuName = "Color Block Jam/Currencies/Currency Definition")]
    public sealed class CurrencyDefinition : ScriptableObject
    {
        public const int MaxIdLength = 32;

        [Tooltip("Stable save key, for example coin. Lowercase letters, digits and '-'. Never rename a shipped ID.")]
        [SerializeField] private string m_id;
        [SerializeField] private string m_displayName;
        [SerializeField] private Sprite m_icon;
        [Tooltip("Granted once, the first time a save lacks this currency.")]
        [SerializeField, Min(0)] private int m_startingAmount;
        [Tooltip("Upper bound for every balance change.")]
        [SerializeField, Min(1)] private int m_maxAmount = 1;
        [SerializeField] private CurrencyDisplay m_homeDisplay;
        [Tooltip("Holder text while the display is Count On Icon and the balance is at the maximum, for example Full.")]
        [SerializeField] private string m_fullLabel;
        [SerializeField] private bool m_showOnHome = true;
        [Tooltip("Seconds of wall-clock time that add one unit while the balance is below the maximum, also while "
                 + "the app is closed (Lives: 1800). 0 turns the timed refill off.")]
        [SerializeField, Min(0)] private int m_refillSeconds;
        [Tooltip("Price of one refill to the maximum, paid in Refill Price Currency (Lives: 1 coin). 0 offers no refill.")]
        [SerializeField, Min(0)] private int m_refillPrice;
        [SerializeField] private CurrencyDefinition m_refillPriceCurrency;

        public string Id => m_id;
        public string DisplayName => m_displayName;
        public Sprite Icon => m_icon;
        public int StartingAmount => m_startingAmount;
        public int MaxAmount => m_maxAmount;
        public CurrencyDisplay HomeDisplay => m_homeDisplay;
        public string FullLabel => m_fullLabel;
        public bool ShowOnHome => m_showOnHome;
        public int RefillSeconds => m_refillSeconds;
        public int RefillPrice => m_refillPrice;
        public CurrencyDefinition RefillPriceCurrency => m_refillPriceCurrency;

        /// <summary>True when the balance grows by one unit every <see cref="RefillSeconds"/> up to the maximum.</summary>
        public bool HasTimedRefill => m_refillSeconds > 0;

        /// <summary>True when the balance can be refilled to the maximum for <see cref="RefillPrice"/>.</summary>
        public bool HasRefillOffer => m_refillPrice > 0 && m_refillPriceCurrency != null && m_refillPriceCurrency != this;

        /// <summary>True when the ID is 1 to 32 lowercase letters, digits or '-'.</summary>
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
            string label = string.IsNullOrWhiteSpace(m_displayName) ? name : m_displayName;
            if (!IsValidId(m_id))
            {
                error = $"Currency '{label}' needs an ID of 1 to {MaxIdLength} lowercase letters, digits or '-' "
                        + $"(found '{m_id}').";
                return false;
            }

            if (string.IsNullOrWhiteSpace(m_displayName))
            {
                error = $"Currency '{m_id}' needs a display name.";
                return false;
            }

            if (m_icon == null)
            {
                error = $"Currency '{m_displayName}' needs an icon.";
                return false;
            }

            if (m_startingAmount < 0)
            {
                error = $"Currency '{m_displayName}' cannot start below 0.";
                return false;
            }

            if (m_maxAmount < 1 || m_maxAmount < m_startingAmount)
            {
                error = $"Currency '{m_displayName}' needs a maximum of at least 1 and at least its starting amount "
                        + $"({m_startingAmount}).";
                return false;
            }

            if (m_homeDisplay != CurrencyDisplay.Amount && m_homeDisplay != CurrencyDisplay.CountOnIcon)
            {
                error = $"Currency '{m_displayName}' has an unknown Home display.";
                return false;
            }

            if (m_refillSeconds < 0 || m_refillPrice < 0)
            {
                error = $"Currency '{m_displayName}' cannot have a negative refill time or price.";
                return false;
            }

            if (m_refillPrice > 0 && (m_refillPriceCurrency == null || m_refillPriceCurrency == this))
            {
                error = $"Currency '{m_displayName}' has a refill price but needs another currency to pay it in.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
