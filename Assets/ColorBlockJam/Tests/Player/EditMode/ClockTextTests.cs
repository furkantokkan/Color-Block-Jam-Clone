using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Proves the shared "mm:ss" clock format (RULE-21), used by every clock and countdown.
    /// </summary>
    public sealed class ClockTextTests
    {
        [TestCase(0, "00:00")]
        [TestCase(5, "00:05")]
        [TestCase(59, "00:59")]
        [TestCase(60, "01:00")]
        [TestCase(65, "01:05")]
        [TestCase(600, "10:00")]
        [TestCase(-1, "00:00")]
        public void Format_MatchesMmSs(long seconds, string expected)
        {
            Assert.That(ClockText.Format(seconds), Is.EqualTo(expected));
        }
    }
}
