using ColorBlockJam.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.Environment
{
    /// <summary>
    /// Keeps the RuleTile equivalence proof after the RuleTile package was removed:
    /// the rule set and matcher must reproduce, for all 256 neighbour patterns at all
    /// four angles, the matches recorded from the old WallRuleTile on 2026-09-25.
    /// </summary>
    public sealed class EnvironmentRuleSetTableTests
    {
        // Index: neighbour pattern, bit n set when neighbour n (GetNeighborOffset order) is floor.
        // Value: bit (rule * 4 + angle / 90) set when the WallRuleTile rule matched at that angle.
        // Recorded with RuleTile.RuleMatches before removal; see the task's evidence.md.
        private static readonly int[] s_ruleTileMatches =
        {
            0, 512, 128, 128, 1024, 1536, 128, 128, 64, 64, 72, 72, 1088, 1088, 72, 72,
            16, 528, 129, 129, 16, 528, 129, 129, 80, 80, 73, 73, 80, 80, 73, 73,
            256, 768, 384, 384, 1280, 1792, 384, 384, 64, 64, 72, 72, 1088, 1088, 72, 72,
            272, 784, 385, 385, 272, 784, 385, 385, 80, 80, 73, 73, 80, 80, 73, 73,
            32, 544, 160, 160, 1056, 1568, 160, 160, 36, 36, 44, 44, 1060, 1060, 44, 44,
            18, 530, 131, 131, 18, 530, 131, 131, 22, 22, 15, 15, 22, 22, 15, 15,
            32, 544, 160, 160, 1056, 1568, 160, 160, 36, 36, 44, 44, 1060, 1060, 44, 44,
            18, 530, 131, 131, 18, 530, 131, 131, 22, 22, 15, 15, 22, 22, 15, 15,
            2048, 2560, 2176, 2176, 3072, 3584, 2176, 2176, 2112, 2112, 2120, 2120, 3136, 3136, 2120, 2120,
            16, 528, 129, 129, 16, 528, 129, 129, 80, 80, 73, 73, 80, 80, 73, 73,
            2304, 2816, 2432, 2432, 3328, 3840, 2432, 2432, 2112, 2112, 2120, 2120, 3136, 3136, 2120, 2120,
            272, 784, 385, 385, 272, 784, 385, 385, 80, 80, 73, 73, 80, 80, 73, 73,
            32, 544, 160, 160, 1056, 1568, 160, 160, 36, 36, 44, 44, 1060, 1060, 44, 44,
            18, 530, 131, 131, 18, 530, 131, 131, 22, 22, 15, 15, 22, 22, 15, 15,
            32, 544, 160, 160, 1056, 1568, 160, 160, 36, 36, 44, 44, 1060, 1060, 44, 44,
            18, 530, 131, 131, 18, 530, 131, 131, 22, 22, 15, 15, 22, 22, 15, 15
        };

        [Test]
        public void RuleSetReproducesTheRecordedRuleTileMatchesForEveryPatternAndAngle()
        {
            EnvironmentRuleSet ruleSet = AssetDatabase.LoadAssetAtPath<EnvironmentRuleSet>(EnvironmentRuleSetTests.RuleSetPath);
            Assert.That(ruleSet.RuleCount, Is.EqualTo(3), "The table was recorded for the three migrated rules.");
            Assert.That(s_ruleTileMatches.Length, Is.EqualTo(256));
            EnvironmentRuleMatcher matcher = new EnvironmentRuleMatcher(ruleSet);
            RectInt bounds = new RectInt(-1, -1, 3, 3);

            for (int pattern = 0; pattern < 256; pattern++)
            {
                matcher.Reset(bounds);
                for (int neighbor = 0; neighbor < EnvironmentRuleSet.NeighborCount; neighbor++)
                {
                    if ((pattern & (1 << neighbor)) != 0)
                    {
                        Vector2Int offset = EnvironmentRuleSet.GetNeighborOffset(neighbor);
                        matcher.SetFloor(offset.x, offset.y);
                    }
                }

                int matches = 0;
                for (int rule = 0; rule < ruleSet.RuleCount; rule++)
                {
                    for (int angle = 0; angle < 360; angle += EnvironmentRuleMatcher.RotationStep)
                    {
                        if (matcher.IsMatch(rule, 0, 0, angle))
                        {
                            matches |= 1 << (rule * 4 + angle / EnvironmentRuleMatcher.RotationStep);
                        }
                    }
                }

                int expected = s_ruleTileMatches[pattern];
                Assert.That(matches, Is.EqualTo(expected), $"pattern {pattern}");

                // RuleTile order: the first rule, then the first angle, wins.
                bool found = matcher.TryMatch(0, 0, out int ruleIndex, out int matchedAngle);
                Assert.That(found, Is.EqualTo(expected != 0), $"pattern {pattern}");
                if (found)
                {
                    int first = GetLowestBit(expected);
                    Assert.That(ruleIndex, Is.EqualTo(first / 4), $"pattern {pattern} rule");
                    Assert.That(matchedAngle, Is.EqualTo(first % 4 * EnvironmentRuleMatcher.RotationStep), $"pattern {pattern} angle");
                }
            }
        }

        private static int GetLowestBit(int value)
        {
            int bit = 0;
            while ((value & (1 << bit)) == 0)
            {
                bit++;
            }

            return bit;
        }
    }
}
