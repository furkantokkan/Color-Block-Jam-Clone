using ColorBlockJam.Runtime.Currencies;
using Onity.Reactive;

namespace ColorBlockJam.Player
{
    /// <summary>Reads committed currency balances; a balance changes only after a successful save (AC-6.4).</summary>
    public interface IWallet
    {
        /// <summary>The committed balance of a catalog currency; throws for a currency the catalog lacks.</summary>
        IReadOnlyReactiveProperty<int> GetBalance(CurrencyDefinition currency);

        /// <summary>True when the committed balance covers the amount.</summary>
        bool CanAfford(CurrencyDefinition currency, int amount);
    }
}
