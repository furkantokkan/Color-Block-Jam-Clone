using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor colour order and the level recolour it drives: palette order by default, shuffles and
    /// swaps stay permutations, and a change of order maps every gate colour reference (blocks, doors, board objects and
    /// palette colour effect keys) one to one while other values stay (TASK-20260926-145804549-da16f02f).
    /// </summary>
    public sealed class LevelColorOrderTests
    {
        private const string k_otherValue = "keep-7";

        [Test]
        public void DefaultOrderIsThePaletteOrder()
        {
            LevelColorOrder order = LevelColorOrder.Default;

            Assert.That(order.IsDefault, Is.True);
            for (int value = 0; value < LevelColorOrder.k_count; value++)
            {
                Assert.That(order.GetColorAt(value), Is.EqualTo((PaletteColor)value));
                Assert.That(order.IndexOf((PaletteColor)value), Is.EqualTo(value));
            }
        }

        [Test]
        public void ShuffleAndSwapKeepEachColourOnce([Values(1, 7, 42, 2026)] int seed)
        {
            LevelColorOrder shuffled = LevelColorOrder.Default.Shuffle(new System.Random(seed));
            AssertPermutation(shuffled);

            LevelColorOrder swapped = shuffled.Swap(PaletteColor.Red, PaletteColor.Pink);
            AssertPermutation(swapped);
            Assert.That(swapped.IndexOf(PaletteColor.Red), Is.EqualTo(shuffled.IndexOf(PaletteColor.Pink)));
            Assert.That(swapped.IndexOf(PaletteColor.Pink), Is.EqualTo(shuffled.IndexOf(PaletteColor.Red)));
        }

        [Test]
        public void TryCreateAcceptsOnlyAPermutationOfTheElevenColours()
        {
            int[] valid = LevelColorOrder.Default.Swap(PaletteColor.Red, PaletteColor.Blue).ToValues();
            Assert.That(LevelColorOrder.TryCreate(valid, out LevelColorOrder order), Is.True);
            Assert.That(order.ToValues(), Is.EqualTo(valid));

            Assert.That(LevelColorOrder.TryCreate(Array.Empty<int>(), out _), Is.False, "An empty copy reads as palette order.");
            Assert.That(LevelColorOrder.TryCreate(null, out _), Is.False);
            int[] duplicate = LevelColorOrder.Default.ToValues();
            duplicate[1] = duplicate[0];
            Assert.That(LevelColorOrder.TryCreate(duplicate, out _), Is.False);
            int[] wall = LevelColorOrder.Default.ToValues();
            wall[0] = (int)PaletteColor.Wall;
            Assert.That(LevelColorOrder.TryCreate(wall, out _), Is.False, "Wall is not a gate colour.");
        }

        [Test]
        public void RemapMapsEveryPaletteColourKeyOfEveryRegisteredEffect()
        {
            LevelColorOrder to = LevelColorOrder.Default.Swap(PaletteColor.Red, PaletteColor.Purple)
                .Swap(PaletteColor.Blue, PaletteColor.Yellow);
            List<LevelEffectData> effects = new List<LevelEffectData>();
            for (int index = 0; index < LevelEffectRegistry.DefinitionCount; index++)
            {
                LevelEffectDefinition definition = LevelEffectRegistry.GetDefinitionAt(index);
                LevelEffectParamData[] parameters = new LevelEffectParamData[definition.KeyCount];
                for (int keyIndex = 0; keyIndex < parameters.Length; keyIndex++)
                {
                    LevelEffectKey key = definition.GetKeyAt(keyIndex);
                    parameters[keyIndex] = new LevelEffectParamData(key.Name, !IsColorKey(key) ? k_otherValue
                        : key.IsList ? "Red,Blue,Pink" : "Red");
                }

                effects.Add(new LevelEffectData(definition.TypeId, parameters));
            }

            // The remap reads the registry for each effect, whether or not the effect suits its owner.
            LevelData level = CreateLevel(new[] { CreateBlock("b1", "Red", effects.ToArray()) },
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Blue", effects.ToArray()) },
                new[] { new LevelCellObjectData("o1", new[] { Vector2Int.one }, effects[0]) });

            LevelData result = LevelColorRemap.Apply(level, LevelColorOrder.Default, to);

            Assert.That(result.GetBlockAt(0).ColorId, Is.EqualTo("Purple"));
            Assert.That(result.GetDoorAt(0).ColorId, Is.EqualTo("Yellow"));
            int colorKeys = 0;
            for (int index = 0; index < LevelEffectRegistry.DefinitionCount; index++)
            {
                LevelEffectDefinition definition = LevelEffectRegistry.GetDefinitionAt(index);
                for (int keyIndex = 0; keyIndex < definition.KeyCount; keyIndex++)
                {
                    LevelEffectKey key = definition.GetKeyAt(keyIndex);
                    string expected = !IsColorKey(key) ? k_otherValue : key.IsList ? "Purple,Yellow,Pink" : "Purple";
                    colorKeys += IsColorKey(key) ? 1 : 0;
                    Assert.That(result.GetBlockAt(0).GetEffectAt(index).FindValue(key.Name), Is.EqualTo(expected),
                        $"Block effect {definition.TypeId} key {key.Name}");
                    Assert.That(result.GetDoorAt(0).GetEffectAt(index).FindValue(key.Name), Is.EqualTo(expected),
                        $"Door effect {definition.TypeId} key {key.Name}");
                }
            }

            Assert.That(colorKeys, Is.GreaterThan(0), "The registry has palette colour keys to map.");
            Assert.That(result.GetCellObjectAt(0).Effect.Type, Is.EqualTo(effects[0].Type));
            Assert.That(level.GetBlockAt(0).ColorId, Is.EqualTo("Red"), "The source level is not changed.");
        }

        [Test]
        public void RemapKeepsNonGateColoursAndUnknownEffects()
        {
            LevelEffectData unknown = new LevelEffectData("not-a-registered-effect",
                new[] { new LevelEffectParamData("colorId", "Red") });
            LevelData level = CreateLevel(
                new[] { CreateBlock("b1", "Wall", new[] { unknown }), CreateBlock("b2", "neutral", null) },
                Array.Empty<LevelDoorData>(), Array.Empty<LevelCellObjectData>());

            LevelData result = LevelColorRemap.Apply(level, LevelColorOrder.Default,
                LevelColorOrder.Default.Swap(PaletteColor.Red, PaletteColor.Purple));

            Assert.That(result.GetBlockAt(0).ColorId, Is.EqualTo("Wall"));
            Assert.That(result.GetBlockAt(1).ColorId, Is.EqualTo("neutral"));
            Assert.That(result.GetBlockAt(0).GetEffectAt(0).FindValue("colorId"), Is.EqualTo("Red"),
                "An effect the registry does not know keeps its values.");
        }

        [Test]
        public void ARemapAndItsInverseRestoreTheLevelAndKeepPairsMatched([Values(3, 11, 99)] int seed)
        {
            LevelData level = CreateLevel(
                new[] { CreateBlock("b1", "Red", null), CreateBlock("b2", "Blue", null) },
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red"),
                    new LevelDoorData("d2", BoardSide.Bottom, 0, 1, "Blue")
                },
                Array.Empty<LevelCellObjectData>());
            LevelColorOrder to = LevelColorOrder.Default.Shuffle(new System.Random(seed));

            LevelData shuffled = LevelColorRemap.Apply(level, LevelColorOrder.Default, to);
            LevelData restored = LevelColorRemap.Apply(shuffled, to, LevelColorOrder.Default);

            Assert.That(shuffled.GetBlockAt(0).ColorId, Is.EqualTo(shuffled.GetDoorAt(0).ColorId), "b1 keeps its gate.");
            Assert.That(shuffled.GetBlockAt(1).ColorId, Is.EqualTo(shuffled.GetDoorAt(1).ColorId), "b2 keeps its gate.");
            Assert.That(shuffled.GetBlockAt(0).ColorId, Is.Not.EqualTo(shuffled.GetBlockAt(1).ColorId));
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(level)));
        }

        [Test]
        public void ARemapKeepsTheValidationResultOfTheDatabaseLevels()
        {
            // The project scope installer names the active JSON; its levels are read from the file (DEC-192, DEC-194).
            if (!ShippedLevels.TryReadActive(out LevelData[] levels, out _) || levels.Length == 0)
            {
                Assert.Ignore("The active level database is missing or empty.");
            }

            LevelColorOrder to = LevelColorOrder.Default.Shuffle(new System.Random(2026));
            for (int index = 0; index < levels.Length; index++)
            {
                LevelData level = levels[index];
                bool valid = level.Validate(out _);
                LevelData recolored = LevelColorRemap.Apply(level, LevelColorOrder.Default, to);
                Assert.That(recolored.Validate(out string error), Is.EqualTo(valid), $"{level.Id}: {error}");
            }
        }

        [Test]
        public void ShuffleAlwaysMovesAColourTheLevelUses()
        {
            LevelData level = CreateLevel(new[] { CreateBlock("b1", "Red", null) }, Array.Empty<LevelDoorData>(),
                Array.Empty<LevelCellObjectData>());
            for (int seed = 0; seed < 200; seed++)
            {
                LevelColorOrder to = LevelColorRemap.Shuffle(level, LevelColorOrder.Default, new System.Random(seed));
                AssertPermutation(to);
                Assert.That(LevelColorOrder.Default.MapTo(to, PaletteColor.Red), Is.Not.EqualTo(PaletteColor.Red),
                    $"Seed {seed}");
            }
        }

        // Color Switch (fifth revision): the partner is a colour the level does not use while one exists, so only the
        // switched colour visibly changes; with every colour in use, any other colour.
        [Test]
        public void SwitchPartnerIsAnUnusedColourWhileOneExists()
        {
            LevelData level = CreateLevel(new[] { CreateBlock("b1", "Red", null), CreateBlock("b2", "Blue", null) },
                Array.Empty<LevelDoorData>(), Array.Empty<LevelCellObjectData>());
            bool[] picked = new bool[LevelColorOrder.k_count];
            for (int seed = 0; seed < 200; seed++)
            {
                PaletteColor partner = LevelColorRemap.PickSwitchPartner(level, PaletteColor.Red, new System.Random(seed));
                Assert.That(partner, Is.Not.EqualTo(PaletteColor.Red).And.Not.EqualTo(PaletteColor.Blue), $"Seed {seed}");
                picked[(int)partner] = true;
            }

            Assert.That(picked, Has.Exactly(LevelColorOrder.k_count - 2).True, "Every unused colour can be picked.");

            LevelBlockData[] all = new LevelBlockData[LevelColorOrder.k_count];
            for (int value = 0; value < all.Length; value++)
            {
                all[value] = CreateBlock("b" + value, ((PaletteColor)value).ToString(), null);
            }

            LevelData full = CreateLevel(all, Array.Empty<LevelDoorData>(), Array.Empty<LevelCellObjectData>());
            for (int seed = 0; seed < 50; seed++)
            {
                Assert.That(LevelColorRemap.PickSwitchPartner(full, PaletteColor.Red, new System.Random(seed)),
                    Is.Not.EqualTo(PaletteColor.Red), $"Seed {seed}");
            }
        }

        [Test]
        public void UsedColoursCoverBlocksDoorsAndEffectColours()
        {
            LevelEffectData path = new LevelEffectData("colorful-path",
                new[] { new LevelEffectParamData("colorId", "Pink") });
            LevelData level = CreateLevel(new[] { CreateBlock("b1", "Red", null) },
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Blue") },
                new[] { new LevelCellObjectData("o1", new[] { Vector2Int.one }, path) });

            bool[] used = LevelColorRemap.GetUsedColors(level);

            for (int value = 0; value < used.Length; value++)
            {
                PaletteColor color = (PaletteColor)value;
                bool expected = color == PaletteColor.Red || color == PaletteColor.Blue || color == PaletteColor.Pink;
                Assert.That(used[value], Is.EqualTo(expected), color.ToString());
            }
        }

        private static bool IsColorKey(LevelEffectKey key)
        {
            return key.Kind == LevelEffectValueKind.PaletteColor;
        }

        private static LevelBlockData CreateBlock(string id, string colorId, LevelEffectData[] effects)
        {
            return new LevelBlockData(id, colorId, Vector2Int.zero, new[] { Vector2Int.zero },
                effects ?? Array.Empty<LevelEffectData>());
        }

        private static LevelData CreateLevel(LevelBlockData[] blocks, LevelDoorData[] doors, LevelCellObjectData[] cellObjects)
        {
            return new LevelData("level-color-order", "Colour Order", 4, 4, 60, Array.Empty<Vector2Int>(), doors, blocks,
                cellObjects);
        }

        private static void AssertPermutation(LevelColorOrder order)
        {
            bool[] seen = new bool[LevelColorOrder.k_count];
            for (int position = 0; position < LevelColorOrder.k_count; position++)
            {
                int value = (int)order.GetColorAt(position);
                Assert.That(value, Is.InRange(0, LevelColorOrder.k_count - 1));
                Assert.That(seen[value], Is.False, $"{(PaletteColor)value} appears twice.");
                seen[value] = true;
                Assert.That(order.IndexOf((PaletteColor)value), Is.EqualTo(position));
            }
        }
    }
}
