using System;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;

namespace ColorBlockJam.Player
{
    /// <summary>
    /// Calculates player economy changes from a snapshot and authored values. It owns no state, clock or persistence:
    /// the save service validates registered IDs, calls these rules under its write lock, and commits one revision.
    /// </summary>
    internal static class PlayerEconomyRules
    {
        internal const int MaxBoosterStock = 99;

        internal static PlayerSnapshot RecordWin(PlayerSnapshot snapshot, int wonLevelIndex,
            CurrencyDefinition currency, int reward)
        {
            snapshot.TryGetCurrency(currency.Id, out int balance);
            int amount = balance > currency.MaxAmount - reward ? currency.MaxAmount : balance + reward;
            int levelIndex = Math.Max(snapshot.CurrentLevelIndex, wonLevelIndex + 1);
            return snapshot.WithCurrency(currency.Id, amount).WithCurrentLevelIndex(levelIndex);
        }

        internal static PlayerSnapshot AddCurrency(PlayerSnapshot snapshot, CurrencyDefinition currency, int amount,
            CurrencyCatalog currencies, long nowUtcSeconds)
        {
            PlayerSnapshot current = WithRefills(snapshot, currencies, nowUtcSeconds);
            current.TryGetCurrency(currency.Id, out int balance);
            int next = balance > currency.MaxAmount - amount ? currency.MaxAmount : balance + amount;
            return next == balance
                ? current
                : WithRefills(current.WithCurrency(currency.Id, next), currencies, nowUtcSeconds);
        }

        internal static PlayerSnapshot TakeCurrency(PlayerSnapshot snapshot, CurrencyDefinition currency, int amount,
            CurrencyCatalog currencies, long nowUtcSeconds)
        {
            PlayerSnapshot current = WithRefills(snapshot, currencies, nowUtcSeconds);
            current.TryGetCurrency(currency.Id, out int balance);
            int next = Math.Max(0, balance - amount);
            return next == balance
                ? current
                : WithRefills(current.WithCurrency(currency.Id, next), currencies, nowUtcSeconds);
        }

        internal static BoosterPurchaseResult Purchase(PlayerSnapshot snapshot, BoosterDefinition booster,
            out PlayerSnapshot next)
        {
            next = null;
            CurrencyDefinition currency = booster.PurchaseCurrency;
            int price = booster.PurchasePrice;
            if (currency is null || price <= 0 || booster.PurchaseAmount < 1 || currency.Id == null)
            {
                return BoosterPurchaseResult.InvalidOffer;
            }

            snapshot.TryGetCurrency(currency.Id, out int balance);
            if (balance < price)
            {
                return BoosterPurchaseResult.InsufficientFunds;
            }

            snapshot.TryGetBoosterStock(booster.Id, out int stock);
            if (stock > MaxBoosterStock - booster.PurchaseAmount)
            {
                return BoosterPurchaseResult.StockFull;
            }

            next = snapshot.WithCurrency(currency.Id, balance - price)
                .WithBoosterStock(booster.Id, stock + booster.PurchaseAmount);
            return BoosterPurchaseResult.Purchased;
        }

        internal static BoosterConsumeResult Consume(PlayerSnapshot committed, BoosterId[] ids, out PlayerSnapshot next)
        {
            next = null;
            for (int index = 0; index < ids.Length; index++)
            {
                if (!committed.TryGetBoosterStock(ids[index], out int stock) || stock < 1)
                {
                    return BoosterConsumeResult.InsufficientStock;
                }
            }

            PlayerSnapshot snapshot = committed;
            for (int index = 0; index < ids.Length; index++)
            {
                snapshot.TryGetBoosterStock(ids[index], out int stock);
                snapshot = snapshot.WithBoosterStock(ids[index], stock - 1);
            }

            next = snapshot;
            return BoosterConsumeResult.Consumed;
        }

        // Return the original snapshot when no balance or timer changes, so callers can skip a no-op save.
        internal static PlayerSnapshot WithRefills(PlayerSnapshot snapshot, CurrencyCatalog currencies, long nowUtcSeconds)
        {
            for (int index = 0; index < currencies.Count; index++)
            {
                CurrencyDefinition currency = currencies.GetAt(index);
                if (currency is null || currency.Id == null || !currency.HasTimedRefill
                    || !snapshot.TryGetCurrency(currency.Id, out int amount))
                {
                    continue;
                }

                bool hasStart = snapshot.TryGetRefillStart(currency.Id, out long start);
                RefillState state = GetRefillState(snapshot, currency, nowUtcSeconds);
                if (state.Amount != amount)
                {
                    snapshot = snapshot.WithCurrency(currency.Id, state.Amount);
                }

                if (state.IsRefilling != hasStart || (hasStart && state.StartUtcSeconds != start))
                {
                    snapshot = snapshot.WithRefillStart(currency.Id, state.IsRefilling ? state.StartUtcSeconds : (long?)null);
                }
            }

            return snapshot;
        }

        internal static RefillState GetRefillState(PlayerSnapshot snapshot, CurrencyDefinition currency, long nowUtcSeconds)
        {
            snapshot.TryGetCurrency(currency.Id, out int amount);
            bool hasStart = snapshot.TryGetRefillStart(currency.Id, out long start);
            return CurrencyRefill.Advance(amount, currency.MaxAmount, currency.RefillSeconds, hasStart, start,
                nowUtcSeconds);
        }
    }
}
