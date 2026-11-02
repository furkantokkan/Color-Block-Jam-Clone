using ColorBlockJam.Editor.Levels.Validation;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>The 128-bit state key keeps sixteen 7-bit origin slots and sixteen exited bits apart.</summary>
    public sealed class LevelStateKeyTests
    {
        [Test]
        public void EverySlotFieldRoundTripsWithoutTouchingItsNeighbours()
        {
            LevelStateKey key = default;
            for (int block = 0; block < LevelCheckOptions.MaxBlocks; block++)
            {
                key = key.WithSlot(block, (block * 37 + 5) % 80);
            }

            for (int block = 0; block < LevelCheckOptions.MaxBlocks; block++)
            {
                Assert.That(key.GetSlot(block), Is.EqualTo((block * 37 + 5) % 80), "block " + block);
                Assert.That(key.IsExited(block), Is.False, "block " + block);
            }

            // Block 9's field straddles the two 64-bit words.
            LevelStateKey changed = key.WithSlot(9, LevelStateKey.MaxSlot);
            Assert.That(changed.GetSlot(9), Is.EqualTo(LevelStateKey.MaxSlot));
            Assert.That(changed.GetSlot(8), Is.EqualTo(key.GetSlot(8)));
            Assert.That(changed.GetSlot(10), Is.EqualTo(key.GetSlot(10)));
        }

        [Test]
        public void AnExitedBlockClearsItsSlotSoEqualPositionsGiveEqualKeys()
        {
            LevelStateKey first = default(LevelStateKey).WithSlot(15, 79).WithSlot(3, 12).WithExited(15);
            LevelStateKey second = default(LevelStateKey).WithSlot(15, 4).WithSlot(3, 12).WithExited(15);

            Assert.That(first.IsExited(15), Is.True);
            Assert.That(first.GetSlot(15), Is.Zero);
            Assert.That(first.GetSlot(3), Is.EqualTo(12));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }
    }
}
