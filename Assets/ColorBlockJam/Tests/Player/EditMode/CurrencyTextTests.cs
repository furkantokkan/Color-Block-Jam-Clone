using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Proves the compact currency format of design 7 (AC-1.8), shared by every screen (DEC-137).
    /// </summary>
    public sealed class CurrencyTextTests
    {
        [TestCase(0, "0")]
        [TestCase(5, "5")]
        [TestCase(950, "950")]
        [TestCase(999, "999")]
        [TestCase(1000, "1k")]
        [TestCase(1005, "1k")]
        [TestCase(1050, "1.05k")]
        [TestCase(1500, "1.5k")]
        [TestCase(1520, "1.52k")]
        [TestCase(1999, "1.99k")]
        [TestCase(10000, "10k")]
        [TestCase(12500, "12.5k")]
        [TestCase(12599, "12.5k")]
        [TestCase(125000, "125k")]
        [TestCase(999999, "999k")]
        [TestCase(1000000, "1m")]
        [TestCase(1520000, "1.52m")]
        [TestCase(999999999, "999m")]
        [TestCase(int.MaxValue, "2.14b")]
        public void FormatCompact_MatchesDesign(int value, string expected)
        {
            Assert.That(CurrencyText.FormatCompact(value), Is.EqualTo(expected));
        }
    }
}
