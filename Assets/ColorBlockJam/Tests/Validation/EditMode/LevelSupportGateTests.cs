using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// AC-6: a mechanic without solver support or more than 16 blocks gives Not checked without a search,
    /// and every effect that gameplay implements has solver support.
    /// </summary>
    public sealed class LevelSupportGateTests
    {
        [Test]
        public void ArrowAndIceHaveSolverSupport()
        {
            Assert.That(LevelSolverSupport.IsSupported("arrow"), Is.True);
            Assert.That(LevelSolverSupport.IsSupported("ice"), Is.True);
            Assert.That(LevelSolverSupport.TryFindUnsupported(ValidationFixtures.ArrowBlockingColumn("Vertical"), out _),
                Is.False);
        }

        [Test]
        public void ABarrierIsNotCheckedWithoutASearch()
        {
            LevelEffectData barrier = new LevelEffectData("barrier", new LevelEffectParamData[0]);
            LevelCellObjectData[] objects = { new LevelCellObjectData("o1", new[] { new Vector2Int(3, 0) }, barrier) };
            LevelData level = ValidationLevelFixture.Create(new[]
            {
                "....",
                ".A..",
                "....",
                "...."
            }, new[] { ValidationLevelFixture.Door(BoardSide.Top, 0, 1, 'A') }, null, objects);

            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.NotChecked));
            Assert.That(result.Text, Is.EqualTo("Not checked: this level uses barriers, which the check does not cover yet."));
            Assert.That(result.VisitedPositions, Is.Zero);
        }

        [Test]
        public void MoreThanSixteenBlocksIsNotCheckedWithoutASearch()
        {
            LevelData level = ValidationFixtures.SeventeenBlocks();
            Assert.That(level.BlockCount, Is.EqualTo(17));

            LevelCheckResult result = LevelSolvabilityCheck.Run(level);

            Assert.That(result.Status, Is.EqualTo(LevelCheckStatus.NotChecked));
            Assert.That(result.Text, Is.EqualTo("Not checked: this level has 17 blocks and the check covers at most 16."));
            Assert.That(result.VisitedPositions, Is.Zero);
        }

        [Test]
        public void EveryRuntimeImplementedEffectHasSolverSupport()
        {
            for (int i = 0; i < LevelEffectRegistry.DefinitionCount; i++)
            {
                LevelEffectDefinition definition = LevelEffectRegistry.GetDefinitionAt(i);
                if (definition.IsRuntimeImplemented)
                {
                    Assert.That(LevelSolverSupport.IsSupported(definition.TypeId), Is.True,
                        definition.TypeId + " has gameplay rules but no solver support; add its solver hook and table entry.");
                }
            }
        }

        [Test]
        public void MechanicNamesAreDesignerWordsForBlocksGatesAndBoardObjects()
        {
            Assert.That(LevelDesignerText.GetMechanicName("arrow", LevelEffectTarget.Block), Is.EqualTo("Arrow blocks"));
            // No block effect other than Arrow and Ice and no gate effect exists (DEC-177); these unregistered
            // ids only exercise the wording rules for several words, "door" endings and the colour spelling.
            Assert.That(LevelDesignerText.GetMechanicName("sample-block", LevelEffectTarget.Block),
                Is.EqualTo("Sample Block blocks"));
            Assert.That(LevelDesignerText.GetMechanicName("sample-door", LevelEffectTarget.Door), Is.EqualTo("sample gates"));
            Assert.That(LevelDesignerText.GetMechanicName("color-sample", LevelEffectTarget.Door),
                Is.EqualTo("colour sample gates"));
            Assert.That(LevelDesignerText.GetMechanicName("barrier", LevelEffectTarget.CellObject), Is.EqualTo("barriers"));
            Assert.That(LevelDesignerText.GetMechanicName("colorful-path", LevelEffectTarget.CellObject),
                Is.EqualTo("colourful paths"));
        }
    }
}
