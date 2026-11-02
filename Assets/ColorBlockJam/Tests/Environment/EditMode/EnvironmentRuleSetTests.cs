using System;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Environment
{
    public sealed class EnvironmentRuleSetTests
    {
        internal const string RuleSetPath = "Assets/ColorBlockJam/Data/Environment/EnvironmentRuleSet.asset";

        private const EnvironmentNeighbor k_any = EnvironmentNeighbor.Any;
        private const EnvironmentNeighbor k_floor = EnvironmentNeighbor.Floor;
        private const EnvironmentNeighbor k_void = EnvironmentNeighbor.Void;

        private EnvironmentRuleSet m_ruleSet;
        private EnvironmentRuleSet m_temporary;

        [SetUp]
        public void SetUp()
        {
            m_ruleSet = AssetDatabase.LoadAssetAtPath<EnvironmentRuleSet>(RuleSetPath);
            Assert.That(m_ruleSet, Is.Not.Null, RuleSetPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_temporary != null)
            {
                Object.DestroyImmediate(m_temporary);
            }
        }

        [Test]
        public void MigratedRuleSetKeepsTheThreeRotatedWallRulesInOrder()
        {
            Assert.That(m_ruleSet.Validate(out string error), Is.True, error);
            Assert.That(m_ruleSet.RuleCount, Is.EqualTo(3));
            AssertRule(0, "Corner_1", k_any, k_floor, k_any, k_any, k_floor, k_any, k_any, k_any);
            AssertRule(1, "Wall", k_any, k_void, k_any, k_any, k_floor, k_any, k_any, k_any);
            AssertRule(2, "Corner_1", k_any, k_any, k_any, k_void, k_any, k_floor, k_void, k_any);
            Assert.That(AssetDatabase.GetAssetPath(m_ruleSet.GetRule(1).Output),
                Is.EqualTo("Assets/ColorBlockJam/Prefabs/Board/Pieces/Wall.prefab"));
        }

        [Test]
        public void NeighbourOffsetsFollowTheRuleTileOrder()
        {
            Vector2Int[] expected =
            {
                new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(-1, 0),
                new Vector2Int(1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1)
            };

            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(EnvironmentRuleSet.GetNeighborOffset(index), Is.EqualTo(expected[index]));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => EnvironmentRuleSet.GetNeighborOffset(8));
        }

        [Test]
        public void ValidateRejectsMissingOutputsUnknownValuesAndAllAnyRules()
        {
            BoardPieceView wall = m_ruleSet.GetRule(1).Output;

            m_temporary = CreateRuleSet(wall, true, k_any, k_floor, k_any, k_any, k_any, k_any, k_any, k_any);
            Assert.That(m_temporary.Validate(out string error), Is.True, error);

            SetRule(m_temporary, null, true, k_any, k_floor, k_any, k_any, k_any, k_any, k_any, k_any);
            Assert.That(m_temporary.Validate(out error), Is.False);
            StringAssert.Contains("output", error);

            SetRule(m_temporary, wall, true, k_any, k_any, k_any, k_any, k_any, k_any, k_any, k_any);
            Assert.That(m_temporary.Validate(out error), Is.False);
            StringAssert.Contains("Floor or Void", error);

            SetRule(m_temporary, wall, true, (EnvironmentNeighbor)7, k_floor, k_any, k_any, k_any, k_any, k_any, k_any);
            Assert.That(m_temporary.Validate(out error), Is.False);
            StringAssert.Contains("unknown", error);

            SerializedObject serialized = new SerializedObject(m_temporary);
            serialized.FindProperty("m_rules").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(m_temporary.Validate(out error), Is.False);
            Assert.Throws<ArgumentException>(() => new EnvironmentRuleMatcher(m_temporary));
        }

        [Test]
        public void MatcherTurnsRulesClockwiseInRuleTileOrder()
        {
            EnvironmentRuleMatcher matcher = new EnvironmentRuleMatcher(m_ruleSet);
            matcher.Reset(new RectInt(-1, -1, 3, 3));
            // Floor to the right and below: the inner corner rule (Upper and Right floor) turned 90 degrees.
            matcher.SetFloor(1, 0);
            matcher.SetFloor(0, -1);

            Assert.That(matcher.TryMatch(0, 0, out int rule, out int angle), Is.True);
            Assert.That(rule, Is.Zero);
            Assert.That(angle, Is.EqualTo(90));
            Assert.That(matcher.GetOutput(rule), Is.SameAs(m_ruleSet.GetRule(0).Output));
        }

        [Test]
        public void MatcherTreatsQuadrantsOutsideItsMaskAsVoid()
        {
            EnvironmentRuleMatcher matcher = new EnvironmentRuleMatcher(m_ruleSet);
            matcher.Reset(new RectInt(0, 0, 2, 1));
            matcher.SetFloor(1, 0);

            Assert.That(matcher.IsFloor(1, 0), Is.True);
            Assert.That(matcher.IsFloor(-5, 3), Is.False);
            // Straight wall: void above, floor to the right, and the upper quadrant lies outside the mask.
            Assert.That(matcher.TryMatch(0, 0, out int rule, out int angle), Is.True);
            Assert.That(rule, Is.EqualTo(1));
            Assert.That(angle, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => matcher.SetFloor(2, 0));

            matcher.Reset(new RectInt(0, 0, 2, 1));
            Assert.That(matcher.IsFloor(1, 0), Is.False, "Reset clears the reused mask.");
            Assert.That(matcher.TryMatch(0, 0, out rule, out _), Is.False);
            Assert.That(rule, Is.EqualTo(-1));
        }

        [Test]
        public void UnrotatedRulesAreOnlyTriedAtZeroDegrees()
        {
            BoardPieceView wall = m_ruleSet.GetRule(1).Output;
            m_temporary = CreateRuleSet(wall, false, k_any, k_floor, k_any, k_any, k_any, k_any, k_any, k_any);
            EnvironmentRuleMatcher matcher = new EnvironmentRuleMatcher(m_temporary);
            matcher.Reset(new RectInt(-1, -1, 3, 3));
            matcher.SetFloor(1, 0);

            Assert.That(matcher.IsMatch(0, 0, 0, 90), Is.True);
            Assert.That(matcher.TryMatch(0, 0, out _, out _), Is.False);
        }

        [Test]
        public void MatchingAPreparedMaskAllocatesZeroBytes()
        {
            EnvironmentRuleMatcher matcher = new EnvironmentRuleMatcher(m_ruleSet);
            RectInt bounds = new RectInt(-4, -4, 12, 12);
            matcher.Reset(bounds);
            matcher.SetFloor(2, 2);
            matcher.TryMatch(1, 2, out _, out _);

            // GC.GetAllocatedBytesForCurrentThread reads 0 in the 2022.3 Editor; the Recorder-based
            // constraint below is the one that actually counts Mono allocations (AD-27).
            Assert.That(() => { }, Is.Not.AllocatingGCMemory(), "warms the recorder");
            Assert.That(() =>
            {
                matcher.Reset(bounds);
                matcher.SetFloor(2, 2);
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    for (int x = bounds.xMin; x < bounds.xMax; x++)
                    {
                        matcher.TryMatch(x, y, out _, out _);
                    }
                }
            }, Is.Not.AllocatingGCMemory());
        }

        internal static EnvironmentRuleSet CreateRuleSet(
            BoardPieceView output, bool isRotated, params EnvironmentNeighbor[] neighbors)
        {
            EnvironmentRuleSet ruleSet = ScriptableObject.CreateInstance<EnvironmentRuleSet>();
            SerializedObject serialized = new SerializedObject(ruleSet);
            serialized.FindProperty("m_rules").arraySize = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetRule(ruleSet, output, isRotated, neighbors);
            return ruleSet;
        }

        private static void SetRule(
            EnvironmentRuleSet ruleSet, BoardPieceView output, bool isRotated, params EnvironmentNeighbor[] neighbors)
        {
            SerializedObject serialized = new SerializedObject(ruleSet);
            SerializedProperty rule = serialized.FindProperty("m_rules").GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("m_output").objectReferenceValue = output;
            rule.FindPropertyRelative("m_isRotated").boolValue = isRotated;
            SerializedProperty values = rule.FindPropertyRelative("m_neighbors");
            values.arraySize = neighbors.Length;
            for (int index = 0; index < neighbors.Length; index++)
            {
                values.GetArrayElementAtIndex(index).intValue = (int)neighbors[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void AssertRule(int index, string output, params EnvironmentNeighbor[] neighbors)
        {
            EnvironmentRuleSet.Rule rule = m_ruleSet.GetRule(index);
            Assert.That(rule.Output.name, Is.EqualTo(output), $"rule {index} output");
            Assert.That(rule.IsRotated, Is.True, $"rule {index} rotated");
            for (int neighbor = 0; neighbor < neighbors.Length; neighbor++)
            {
                Assert.That(rule.GetNeighbor(neighbor), Is.EqualTo(neighbors[neighbor]), $"rule {index} neighbour {neighbor}");
            }
        }
    }
}
