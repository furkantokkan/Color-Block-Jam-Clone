using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Verifies the shipped currency and booster assets and the designer-language validation of currency catalogs,
    /// currency definitions and booster prices (player-data-currencies AC-1, AC-11).
    /// </summary>
    public sealed class CurrencyCatalogTests
    {
        private const string k_catalogPath = "Assets/ColorBlockJam/Data/Currencies/CurrencyCatalog.asset";
        private const string k_coinPath = "Assets/ColorBlockJam/Data/Currencies/Coin.asset";
        private const string k_boosterCatalogPath = "Assets/ColorBlockJam/Data/Boosters/BoosterCatalog.asset";

        private static readonly string[] s_boosterPaths =
        {
            "Assets/ColorBlockJam/Data/Boosters/Definitions/ExtraTime.asset",
            "Assets/ColorBlockJam/Data/Boosters/Definitions/Hammer.asset",
            "Assets/ColorBlockJam/Data/Boosters/Definitions/Rocket.asset",
            "Assets/ColorBlockJam/Data/Boosters/Definitions/StartingRocket.asset", // DEC-167
            "Assets/ColorBlockJam/Data/Boosters/Definitions/TimeFreeze.asset",
            "Assets/ColorBlockJam/Data/Boosters/Definitions/Vacuum.asset"
        };

        private PlayerTestFixtures m_fixtures;

        [SetUp]
        public void SetUp()
        {
            m_fixtures = new PlayerTestFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            m_fixtures.Dispose();
        }

        [Test]
        public void TheShippedCatalogListsLifeThenCoinAndValidates()
        {
            CurrencyCatalog catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<CurrencyCatalog>(k_catalogPath);
            Assert.That(catalog, Is.Not.Null, k_catalogPath);
            Assert.That(catalog.Validate(out string error), Is.True, error);
            Assert.That(catalog.Count, Is.EqualTo(2));

            CurrencyDefinition life = catalog.GetAt(0);
            Assert.That(life.Id, Is.EqualTo("life"));
            Assert.That(life.StartingAmount, Is.EqualTo(5));
            Assert.That(life.MaxAmount, Is.EqualTo(5));
            Assert.That(life.HomeDisplay, Is.EqualTo(CurrencyDisplay.CountOnIcon));
            Assert.That(life.FullLabel, Is.EqualTo("Full"));
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(life.Icon),
                Is.EqualTo("Assets/ColorBlockJam/Art/UI/Supplied/Icons/Currency/ic_heart.png"));

            CurrencyDefinition coin = catalog.GetAt(1);
            Assert.That(coin.Id, Is.EqualTo("coin"));
            Assert.That(coin.StartingAmount, Is.EqualTo(100)); // DEC-161
            Assert.That(coin.MaxAmount, Is.EqualTo(999999999));
            Assert.That(coin.HomeDisplay, Is.EqualTo(CurrencyDisplay.Amount));
            Assert.That(coin.FullLabel, Is.Null.Or.Empty);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(coin.Icon),
                Is.EqualTo("Assets/ColorBlockJam/Art/UI/Supplied/Icons/Currency/ic_coin_single.png"));
            Assert.That(catalog.TryGet("coin", out CurrencyDefinition found) && found == coin, Is.True);
        }

        [Test]
        public void EveryShippedBoosterPaysTenCoinsForOneChargeAndTheCatalogValidates()
        {
            CurrencyDefinition coin = UnityEditor.AssetDatabase.LoadAssetAtPath<CurrencyDefinition>(k_coinPath);
            Assert.That(coin, Is.Not.Null, k_coinPath);
            for (int index = 0; index < s_boosterPaths.Length; index++)
            {
                BoosterDefinition booster = UnityEditor.AssetDatabase.LoadAssetAtPath<BoosterDefinition>(s_boosterPaths[index]);
                Assert.That(booster, Is.Not.Null, s_boosterPaths[index]);
                Assert.That(booster.PurchasePrice, Is.EqualTo(10), booster.name);
                Assert.That(booster.PurchaseAmount, Is.EqualTo(1), booster.name);
                Assert.That(booster.PurchaseCurrency, Is.SameAs(coin), booster.name);
            }

            BoosterCatalog boosters = UnityEditor.AssetDatabase.LoadAssetAtPath<BoosterCatalog>(k_boosterCatalogPath);
            Assert.That(boosters.Validate(out string error), Is.True, error);
        }

        [Test]
        public void ABoosterWithAPriceAndNoCurrencyFailsValidation()
        {
            BoosterDefinition booster = m_fixtures.CreateBooster(BoosterId.ExtraTime, 1, 10, null);

            Assert.That(booster.Validate(out string error), Is.False);
            Assert.That(error, Does.Contain("no purchase currency"));
        }

        [Test]
        public void AFreeBoosterNeedsNoCurrency()
        {
            BoosterDefinition booster = m_fixtures.CreateBooster(BoosterId.ExtraTime, 1, 0, null);

            Assert.That(booster.Validate(out string error), Is.True, error);
        }

        [Test]
        public void AValidCatalogPassesAndFindsItsCurrencies()
        {
            CurrencyCatalog catalog = m_fixtures.CreateCatalog(
                m_fixtures.CreateCurrency("life", "Lives", 5, 5, CurrencyDisplay.CountOnIcon),
                m_fixtures.CreateCurrency("coin", "Coins", 0, 999999999),
                m_fixtures.CreateCurrency("gem-2", "Gems", 3, 100));

            Assert.That(catalog.Validate(out string error), Is.True, error);
            Assert.That(catalog.TryGet("gem-2", out CurrencyDefinition gem), Is.True);
            Assert.That(gem.StartingAmount, Is.EqualTo(3));
            Assert.That(catalog.TryGet("missing", out _), Is.False);
        }

        [Test]
        public void ADuplicateIdFails()
        {
            CurrencyCatalog catalog = m_fixtures.CreateCatalog(
                m_fixtures.CreateCurrency("coin", "Coins", 0, 10),
                m_fixtures.CreateCurrency("coin", "Gold", 0, 10));

            AssertInvalid(catalog, "used by both 'Coins' and 'Gold'");
        }

        [TestCase("Coin")]
        [TestCase("coin!")]
        [TestCase("")]
        [TestCase("a-very-long-currency-identifier-x")]
        public void AnInvalidIdFails(string id)
        {
            CurrencyCatalog catalog = m_fixtures.CreateCatalog(m_fixtures.CreateCurrency(id, "Coins", 0, 10));

            AssertInvalid(catalog, "needs an ID of 1 to 32 lowercase letters, digits or '-'");
        }

        [Test]
        public void AStartAboveTheMaximumFails()
        {
            CurrencyCatalog catalog = m_fixtures.CreateCatalog(m_fixtures.CreateCurrency("life", "Lives", 6, 5));

            AssertInvalid(catalog, "at least its starting amount");
        }

        [Test]
        public void AMissingIconFails()
        {
            CurrencyCatalog catalog = m_fixtures.CreateCatalog(
                m_fixtures.CreateCurrency("coin", "Coins", 0, 10, CurrencyDisplay.Amount, false));

            AssertInvalid(catalog, "Currency 'Coins' needs an icon.");
        }

        [Test]
        public void ANullEntryFails()
        {
            CurrencyCatalog catalog = m_fixtures.CreateCatalog(m_fixtures.CreateCurrency("coin", "Coins", 0, 10), null);

            AssertInvalid(catalog, "Currency catalog entry 2 is empty");
        }

        [Test]
        public void AnEmptyCatalogFails()
        {
            AssertInvalid(m_fixtures.CreateCatalog(), "lists no currencies");
        }

        private static void AssertInvalid(CurrencyCatalog catalog, string expected)
        {
            Assert.That(catalog.Validate(out string error), Is.False);
            Assert.That(error, Does.Contain(expected));
        }
    }
}
