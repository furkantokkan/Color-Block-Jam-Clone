using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;

namespace ColorBlockJam.Player
{
    /// <summary>Buys booster stock with the booster's price currency as one saved snapshot (AC-6.7).</summary>
    public interface IBoosterShop
    {
        /// <summary>True while any player-data write is pending; presenters disable Buy while it is true.</summary>
        IReadOnlyReactiveProperty<bool> IsSaving { get; }

        Task<BoosterPurchaseResult> PurchaseAsync(BoosterDefinition booster, CancellationToken cancellationToken);
    }
}
