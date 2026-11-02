using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Player
{
    /// <summary>
    /// One immutable, versioned copy of everything the player owns: revision, saved level index, currency balances
    /// by currency ID, booster stock by BoosterId name, the Settings toggles, and for each timed-refill currency below
    /// its maximum the UTC second its next unit started refilling (DEC-174). Keys this build does not know are
    /// kept so they are written back unchanged. Every With method returns a new snapshot (design 4.1).
    /// </summary>
    public sealed class PlayerSnapshot
    {
        private static readonly IReadOnlyDictionary<string, int> s_noValues =
            new Dictionary<string, int>(0, StringComparer.Ordinal);
        private static readonly IReadOnlyDictionary<string, long> s_noTimes =
            new Dictionary<string, long>(0, StringComparer.Ordinal);

        private readonly Dictionary<string, int> m_currencies;
        private readonly Dictionary<string, int> m_boosterStock;
        private readonly Dictionary<string, long> m_refillStarts;

        /// <param name="refillStarts">UTC Unix seconds by currency ID at which the next unit started refilling.</param>
        public PlayerSnapshot(long revision, int currentLevelIndex, IReadOnlyDictionary<string, int> currencies,
            IReadOnlyDictionary<string, int> boosterStock, PlayerSettings settings,
            IReadOnlyDictionary<string, long> refillStarts = null)
        {
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), revision, "The revision cannot be negative.");
            }

            if (currentLevelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentLevelIndex), currentLevelIndex,
                    "The level index cannot be negative.");
            }

            Revision = revision;
            CurrentLevelIndex = currentLevelIndex;
            m_currencies = Copy(currencies ?? s_noValues, nameof(currencies));
            m_boosterStock = Copy(boosterStock ?? s_noValues, nameof(boosterStock));
            m_refillStarts = Copy(refillStarts ?? s_noTimes, nameof(refillStarts));
            Settings = settings;
        }

        /// <summary>A snapshot that was never saved: revision 0, level 0, no balances or stock, toggles on.</summary>
        public static PlayerSnapshot Empty { get; } = new PlayerSnapshot(0, 0, null, null, PlayerSettings.Default);

        public long Revision { get; }
        public int CurrentLevelIndex { get; }
        public IReadOnlyDictionary<string, int> Currencies => m_currencies;
        public IReadOnlyDictionary<string, int> BoosterStock => m_boosterStock;
        public IReadOnlyDictionary<string, long> RefillStarts => m_refillStarts;
        public PlayerSettings Settings { get; }

        /// <summary>The JSON key a booster's stock is saved under: its BoosterId name.</summary>
        public static string GetBoosterKey(BoosterId id)
        {
            return id.ToString();
        }

        public bool TryGetCurrency(string currencyId, out int amount)
        {
            return m_currencies.TryGetValue(currencyId, out amount);
        }

        public bool TryGetBoosterStock(BoosterId id, out int stock)
        {
            return m_boosterStock.TryGetValue(GetBoosterKey(id), out stock);
        }

        public bool TryGetRefillStart(string currencyId, out long startUtcSeconds)
        {
            return m_refillStarts.TryGetValue(currencyId, out startUtcSeconds);
        }

        public PlayerSnapshot WithRevision(long revision)
        {
            return new PlayerSnapshot(revision, CurrentLevelIndex, m_currencies, m_boosterStock, Settings, m_refillStarts);
        }

        public PlayerSnapshot WithCurrentLevelIndex(int levelIndex)
        {
            return new PlayerSnapshot(Revision, levelIndex, m_currencies, m_boosterStock, Settings, m_refillStarts);
        }

        public PlayerSnapshot WithCurrency(string currencyId, int amount)
        {
            Dictionary<string, int> currencies = new Dictionary<string, int>(m_currencies, StringComparer.Ordinal)
            {
                [currencyId] = amount
            };
            return new PlayerSnapshot(Revision, CurrentLevelIndex, currencies, m_boosterStock, Settings, m_refillStarts);
        }

        public PlayerSnapshot WithBoosterStock(BoosterId id, int stock)
        {
            Dictionary<string, int> boosterStock = new Dictionary<string, int>(m_boosterStock, StringComparer.Ordinal)
            {
                [GetBoosterKey(id)] = stock
            };
            return new PlayerSnapshot(Revision, CurrentLevelIndex, m_currencies, boosterStock, Settings, m_refillStarts);
        }

        public PlayerSnapshot WithSettings(PlayerSettings settings)
        {
            return new PlayerSnapshot(Revision, CurrentLevelIndex, m_currencies, m_boosterStock, settings, m_refillStarts);
        }

        /// <summary>Sets the refill start of <paramref name="currencyId"/>, or removes it when null.</summary>
        public PlayerSnapshot WithRefillStart(string currencyId, long? startUtcSeconds)
        {
            Dictionary<string, long> refillStarts = new Dictionary<string, long>(m_refillStarts, StringComparer.Ordinal);
            if (startUtcSeconds.HasValue)
            {
                refillStarts[currencyId] = startUtcSeconds.Value;
            }
            else if (!refillStarts.Remove(currencyId))
            {
                return this;
            }

            return new PlayerSnapshot(Revision, CurrentLevelIndex, m_currencies, m_boosterStock, Settings, refillStarts);
        }

        private static Dictionary<string, int> Copy(IReadOnlyDictionary<string, int> source, string parameter)
        {
            Dictionary<string, int> copy = new Dictionary<string, int>(source.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value < 0)
                {
                    throw new ArgumentException("Keys must be nonempty and values 0 or more.", parameter);
                }

                copy.Add(pair.Key, pair.Value);
            }

            return copy;
        }

        private static Dictionary<string, long> Copy(IReadOnlyDictionary<string, long> source, string parameter)
        {
            Dictionary<string, long> copy = new Dictionary<string, long>(source.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value < 0)
                {
                    throw new ArgumentException("Keys must be nonempty and values 0 or more.", parameter);
                }

                copy.Add(pair.Key, pair.Value);
            }

            return copy;
        }
    }
}
