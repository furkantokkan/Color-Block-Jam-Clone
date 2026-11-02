using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Maps a CBJ level's integer BCT colour id to the project's PaletteColor. One editable entry per
    /// known BCT value; a BCT value with no entry fails the CBJ import naming that value (DEC-101).
    /// </summary>
    [CreateAssetMenu(menuName = "Color Block Jam/Levels/CBJ Color Table")]
    public sealed class CbjColorTable : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private int m_bct;
            [SerializeField] private PaletteColor m_color;

            public int Bct => m_bct;
            public PaletteColor Color => m_color;

            public Entry(int bct, PaletteColor color)
            {
                m_bct = bct;
                m_color = color;
            }
        }

        [SerializeField] private Entry[] m_entries = CreateDefaultEntries();

        public IReadOnlyList<Entry> Entries => m_entries;

        /// <summary>Replaces every entry; used by tests and programmatic table construction.</summary>
        internal void SetEntries(IReadOnlyList<Entry> entries)
        {
            m_entries = new Entry[entries?.Count ?? 0];
            for (int index = 0; index < m_entries.Length; index++)
            {
                m_entries[index] = entries[index];
            }
        }

        /// <summary>True with the mapped colour when bct has an entry; false when the table does not cover it.</summary>
        public bool TryGetColor(int bct, out PaletteColor color)
        {
            for (int index = 0; index < m_entries.Length; index++)
            {
                Entry entry = m_entries[index];
                if (entry != null && entry.Bct == bct)
                {
                    color = entry.Color;
                    return true;
                }
            }

            color = default;
            return false;
        }

        /// <summary>
        /// The CBJ Template's BlockColor order (BCT 0-10), the same order as the project's gate colours
        /// (BoardLayout.TryParseDoorColor). BCT 11 (the example's unmapped value) and any higher id are
        /// left uncovered until a designer confirms and adds them.
        /// </summary>
        public static Entry[] CreateDefaultEntries()
        {
            return new[]
            {
                new Entry(0, PaletteColor.Red),
                new Entry(1, PaletteColor.Orange),
                new Entry(2, PaletteColor.Yellow),
                new Entry(3, PaletteColor.LightGreen),
                new Entry(4, PaletteColor.DarkGreen),
                new Entry(5, PaletteColor.LightTurquoise),
                new Entry(6, PaletteColor.DarkTurquoise),
                new Entry(7, PaletteColor.LightBlue),
                new Entry(8, PaletteColor.Blue),
                new Entry(9, PaletteColor.Purple),
                new Entry(10, PaletteColor.Pink)
            };
        }
    }
}
