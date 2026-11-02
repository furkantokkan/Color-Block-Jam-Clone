using ColorBlockJam.Runtime.Startup;
using NUnit.Framework;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the startup bar mapping (DEC-140, DEC-199): the project wait starts at 0, never goes back and never
    /// claims completion; the Hub load fills from wherever the wait stopped up to <see cref="StartupProgress.HubLoadEnd"/>;
    /// Hub's build of Home and its prepared level then eases toward, and never reaches, the full bar.
    /// </summary>
    public sealed class StartupProgressTests
    {
        private static readonly float[] s_waitTimes = { 0f, 0.016f, 0.1f, 0.5f, 1f, 2f, 5f, 10f, 60f, 3600f };

        [Test]
        public void ForProjectWait_StartsAtZero()
        {
            Assert.That(StartupProgress.ForProjectWait(0f), Is.EqualTo(0f));
            Assert.That(StartupProgress.ForProjectWait(-1f), Is.EqualTo(0f));
        }

        [Test]
        public void ForProjectWait_NeverDecreasesAndNeverReachesOne()
        {
            float previous = 0f;
            foreach (float seconds in s_waitTimes)
            {
                float value = StartupProgress.ForProjectWait(seconds);
                Assert.That(value, Is.GreaterThanOrEqualTo(previous), $"went back at {seconds} s");
                Assert.That(value, Is.LessThan(1f), $"claimed completion at {seconds} s");
                previous = value;
            }
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(0f, 1f, StartupProgress.HubLoadEnd)]
        [TestCase(0.6f, 0f, 0.6f)]
        [TestCase(0.6f, 0.5f, 0.725f)]
        [TestCase(0.6f, 1f, StartupProgress.HubLoadEnd)]
        public void ForHubLoad_FillsFromTheReachedValueToTheHubLoadEnd(float reached, float hubLoad, float expected)
        {
            Assert.That(StartupProgress.ForHubLoad(reached, hubLoad), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void ForHubLoad_ClampsOutOfRangeInputs()
        {
            Assert.That(StartupProgress.ForHubLoad(0.5f, -1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(StartupProgress.ForHubLoad(0.5f, 2f), Is.EqualTo(StartupProgress.HubLoadEnd).Within(1e-5f));
            Assert.That(StartupProgress.ForHubLoad(2f, 0f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ForSceneBuild_StartsAtTheReachedValue()
        {
            Assert.That(StartupProgress.ForSceneBuild(StartupProgress.HubLoadEnd, 0f),
                Is.EqualTo(StartupProgress.HubLoadEnd).Within(1e-5f));
            Assert.That(StartupProgress.ForSceneBuild(0.5f, -1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(StartupProgress.ForSceneBuild(2f, 0f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ForSceneBuild_NeverDecreasesAndNeverReachesOne()
        {
            float previous = StartupProgress.HubLoadEnd;
            foreach (float seconds in s_waitTimes)
            {
                float value = StartupProgress.ForSceneBuild(StartupProgress.HubLoadEnd, seconds);
                Assert.That(value, Is.GreaterThanOrEqualTo(previous), $"went back at {seconds} s");
                Assert.That(value, Is.LessThan(1f), $"claimed completion at {seconds} s");
                previous = value;
            }
        }
    }
}
