using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Level Editor's colour order: the 11 gate colours as a permutation, palette order by default. A Colors swatch
    /// badge shows each colour's position, and moving the draft from one order to another recolours it position by
    /// position (user decisions of 2026-09-26, TASK-20260926-145804549-da16f02f). Instances never change.
    /// </summary>
    internal sealed class LevelColorOrder
    {
        /// <summary>The ordered colours: the 11 gate colours below Wall.</summary>
        public const int k_count = LevelEditorToolState.k_colorCount;

        /// <summary>Palette order: Red first, Pink last.</summary>
        public static readonly LevelColorOrder Default = CreateDefault();

        private readonly PaletteColor[] m_colors;
        // The position of each colour, indexed by its int value.
        private readonly int[] m_positions;

        private LevelColorOrder(PaletteColor[] colors)
        {
            m_colors = colors;
            m_positions = new int[k_count];
            for (int position = 0; position < k_count; position++)
            {
                m_positions[(int)colors[position]] = position;
            }
        }

        /// <summary>True in palette order.</summary>
        public bool IsDefault
        {
            get
            {
                for (int position = 0; position < k_count; position++)
                {
                    if ((int)m_colors[position] != position)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// The order stored as int values, each gate colour exactly once. False for anything else, such as an empty
        /// array or a copy kept before the order existed; the caller then keeps palette order.
        /// </summary>
        public static bool TryCreate(IReadOnlyList<int> values, out LevelColorOrder order)
        {
            order = null;
            if (values == null || values.Count != k_count)
            {
                return false;
            }

            PaletteColor[] colors = new PaletteColor[k_count];
            bool[] seen = new bool[k_count];
            for (int position = 0; position < k_count; position++)
            {
                int value = values[position];
                if (value < 0 || value >= k_count || seen[value])
                {
                    return false;
                }

                seen[value] = true;
                colors[position] = (PaletteColor)value;
            }

            order = new LevelColorOrder(colors);
            return true;
        }

        /// <summary>The colour at a 0-based position.</summary>
        public PaletteColor GetColorAt(int position)
        {
            return m_colors[position];
        }

        /// <summary>The 0-based position of a gate colour.</summary>
        public int IndexOf(PaletteColor color)
        {
            return m_positions[(int)color];
        }

        /// <summary>
        /// The colour that <paramref name="color"/> becomes when a level moves from this order to
        /// <paramref name="target"/>: the target's colour at this order's position of it.
        /// </summary>
        public PaletteColor MapTo(LevelColorOrder target, PaletteColor color)
        {
            return target.m_colors[m_positions[(int)color]];
        }

        /// <summary>A uniformly random order (Fisher-Yates) drawn from <paramref name="random"/>.</summary>
        public LevelColorOrder Shuffle(Random random)
        {
            PaletteColor[] colors = (PaletteColor[])m_colors.Clone();
            for (int last = k_count - 1; last > 0; last--)
            {
                int pick = random.Next(last + 1);
                (colors[last], colors[pick]) = (colors[pick], colors[last]);
            }

            return new LevelColorOrder(colors);
        }

        /// <summary>This order with two colours trading places.</summary>
        public LevelColorOrder Swap(PaletteColor first, PaletteColor second)
        {
            PaletteColor[] colors = (PaletteColor[])m_colors.Clone();
            int firstPosition = m_positions[(int)first];
            int secondPosition = m_positions[(int)second];
            colors[firstPosition] = second;
            colors[secondPosition] = first;
            return new LevelColorOrder(colors);
        }

        /// <summary>The colours as int values, position by position, for the draft's serialized field.</summary>
        public int[] ToValues()
        {
            int[] values = new int[k_count];
            for (int position = 0; position < k_count; position++)
            {
                values[position] = (int)m_colors[position];
            }

            return values;
        }

        private static LevelColorOrder CreateDefault()
        {
            PaletteColor[] colors = new PaletteColor[k_count];
            for (int position = 0; position < k_count; position++)
            {
                colors[position] = (PaletteColor)position;
            }

            return new LevelColorOrder(colors);
        }
    }
}
