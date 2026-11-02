using ColorBlockJam.Editor.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the progress line a background Validate Level shows in the validation panel and the status line
    /// (DEC-175): the phase, the stored positions against a position budget when there is one, the elapsed seconds
    /// against the time budget, and how to stop the run.
    /// </summary>
    public sealed class LevelValidationProgressTests
    {
        [Test]
        public void TheCheckNamesItsPositionsAndTimeBudget()
        {
            Assert.That(LevelValidationFlow.FormatProgress(false, 338036, int.MaxValue, 1.24, 15.0),
                Is.EqualTo("Checked 338,036 positions in 1.2 of 15 s. Cancel stops the check."));
        }

        [Test]
        public void TheListingNamesItsPositionBudgetWhenItHasOne()
        {
            Assert.That(LevelValidationFlow.FormatProgress(true, 12345, 1000000, 0.46, 10.0),
                Is.EqualTo(LevelValidationFlow.k_listingProgressPrefix
                           + "12,345 of 1,000,000 positions in 0.5 of 10 s. Cancel stops the listing."));
        }
    }
}
