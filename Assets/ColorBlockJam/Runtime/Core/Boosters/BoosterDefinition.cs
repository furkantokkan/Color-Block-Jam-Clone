using ColorBlockJam.Runtime.Currencies;
using UnityEngine;
using UnityEngine.Serialization;

namespace ColorBlockJam.Runtime.Boosters
{
    /// <summary>
    /// Authored, read-only booster configuration. Runtime stock and effect state
    /// belong to the save service and level session, respectively.
    /// </summary>
    public abstract class BoosterDefinition : ScriptableObject
    {
        [SerializeField] private BoosterId m_id;
        [SerializeField] private string m_displayName;
        [SerializeField, TextArea] private string m_description;
        [SerializeField] private Sprite m_icon;
        [SerializeField] private GameObject m_effectPrefab;
        [SerializeField] private BoosterUseContext m_useContexts;
        [SerializeField] private BoosterTargetKind m_targetKind;
        [SerializeField, Range(0, 9)] private int m_initialCharges = 1;
        [FormerlySerializedAs("m_purchaseCoinPrice")]
        [SerializeField, Min(0)] private int m_purchasePrice = 10;
        [Tooltip("The currency the purchase price is paid in (design 3.2). Required when the price is above 0.")]
        [SerializeField] private CurrencyDefinition m_purchaseCurrency;
        [SerializeField, Min(1)] private int m_purchaseAmount = 1;

        public BoosterId Id => m_id;
        public string DisplayName => m_displayName;
        public string Description => m_description;
        public Sprite Icon => m_icon;
        public GameObject EffectPrefab => m_effectPrefab;
        public BoosterUseContext UseContexts => m_useContexts;
        public BoosterTargetKind TargetKind => m_targetKind;
        public int InitialCharges => m_initialCharges;
        public int PurchasePrice => m_purchasePrice;
        public CurrencyDefinition PurchaseCurrency => m_purchaseCurrency;
        public int PurchaseAmount => m_purchaseAmount;

        public virtual bool Validate(out string error)
        {
            if (m_id == BoosterId.None || string.IsNullOrWhiteSpace(m_displayName))
            {
                error = "Booster ID and display name are required.";
                return false;
            }

            if (m_useContexts == BoosterUseContext.None || m_initialCharges < 0
                || m_initialCharges > 9 || m_purchasePrice < 0
                || m_purchaseAmount < 1)
            {
                error = $"{m_id} has invalid availability or economy values.";
                return false;
            }

            if (m_purchasePrice > 0 && m_purchaseCurrency == null)
            {
                error = $"{m_id} has a purchase price of {m_purchasePrice} but no purchase currency. "
                        + "Assign the currency the price is paid in.";
                return false;
            }

            if (m_icon == null)
            {
                error = $"{m_id} is missing an icon.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
