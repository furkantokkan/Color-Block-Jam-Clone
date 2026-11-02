using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Currencies;

namespace ColorBlockJam.Player
{
    /// <summary>Adds currency through the save owner; the balance changes only after the write commits (DEC-92).</summary>
    public interface ICurrencyGrant
    {
        /// <summary>Adds <paramref name="amount"/>, clamped to the currency's maximum, as one snapshot and one revision.</summary>
        Task<PlayerSaveResult> AddAsync(CurrencyDefinition currency, int amount, CancellationToken cancellationToken);
    }
}
