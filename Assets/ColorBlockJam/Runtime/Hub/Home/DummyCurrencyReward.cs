using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Currencies;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Hub.Home
{
    /// <summary>
    /// Case-only placeholder for the Home currency holders' plus buttons: life adds +1 life, coin adds +100 coins
    /// through the save owner. There is no shop or IAP in the case scope, so this stands in for one.
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle). <see cref="Attach"/>/<see cref="Detach"/> follow HubScreen's
    /// bind/unbind; <see cref="Dispose"/> at Hub unload cancels a pending grant. One grant runs at a time, so a
    /// rapid double press cannot start a second write while the first is saving.
    /// </remarks>
    public sealed class DummyCurrencyReward : IDisposable
    {
        private const string k_plusButtonName = "plus-button";
        private const string k_holderNamePrefix = "currency-holder-";
        private const int k_lifeAmount = 1;
        private const int k_coinAmount = 100;

        private readonly ICurrencyGrant m_grant;
        private readonly CurrencyCatalog m_catalog;
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private readonly Action m_onLifeClicked;
        private readonly Action m_onCoinClicked;
        private Button m_lifeButton;
        private Button m_coinButton;

        private Task m_pending = Task.CompletedTask;
        private bool m_disposed;

        public DummyCurrencyReward(ICurrencyGrant grant, CurrencyCatalog catalog)
        {
            m_grant = grant ?? throw new ArgumentNullException(nameof(grant));
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_onLifeClicked = OnLifeClicked;
            m_onCoinClicked = OnCoinClicked;
        }

        /// <summary>Hooks the authored life and coin holders by their stable currency IDs.</summary>
        public void Attach(VisualElement home)
        {
            Detach();
            if (m_disposed)
            {
                return;
            }

            m_lifeButton = home.Q<VisualElement>(k_holderNamePrefix + CurrencyIds.Life)?.Q<Button>(k_plusButtonName);
            m_coinButton = home.Q<VisualElement>(k_holderNamePrefix + CurrencyIds.Coin)?.Q<Button>(k_plusButtonName);
            if (m_lifeButton != null)
            {
                m_lifeButton.clicked += m_onLifeClicked;
            }

            if (m_coinButton != null)
            {
                m_coinButton.clicked += m_onCoinClicked;
            }
        }

        public void Detach()
        {
            if (m_lifeButton != null)
            {
                m_lifeButton.clicked -= m_onLifeClicked;
            }

            if (m_coinButton != null)
            {
                m_coinButton.clicked -= m_onCoinClicked;
            }

            m_lifeButton = null;
            m_coinButton = null;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            Detach();
            m_lifetime.Cancel();
            m_lifetime.Dispose();
        }

        private void OnLifeClicked()
        {
            Grant(CurrencyIds.Life, k_lifeAmount);
        }

        private void OnCoinClicked()
        {
            Grant(CurrencyIds.Coin, k_coinAmount);
        }

        private void Grant(string currencyId, int amount)
        {
            if (m_disposed || !m_pending.IsCompleted)
            {
                return;
            }

            m_pending = AddAsync(currencyId, amount, m_lifetime.Token);
        }

        private async Task AddAsync(string currencyId, int amount, CancellationToken cancellationToken)
        {
            if (!m_catalog.TryGet(currencyId, out CurrencyDefinition currency))
            {
                Debug.LogError("Dummy currency reward: the currency catalog has no '" + currencyId + "'.");
                return;
            }

            // A full balance (life at its maximum of 5) clamps and saves nothing.
            await m_grant.AddAsync(currency, amount, cancellationToken);
        }
    }
}
