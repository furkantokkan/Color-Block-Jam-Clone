using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;

namespace ColorBlockJam.Player
{
    /// <summary>Reads committed booster stock by booster ID.</summary>
    public interface IBoosterInventory
    {
        /// <summary>The committed stock of a catalog booster; throws for a booster the catalog lacks.</summary>
        IReadOnlyReactiveProperty<int> GetStock(BoosterId id);
    }
}
