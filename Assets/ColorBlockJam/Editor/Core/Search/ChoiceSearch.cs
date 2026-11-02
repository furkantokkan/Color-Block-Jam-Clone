using System;
using System.Collections.Generic;

namespace ColorBlockJam.Editor.Search
{
    /// <summary>
    /// Filters dropdown choices by a typed query, keeping matches in list order.
    /// </summary>
    internal static class ChoiceSearch
    {
        /// <summary>
        /// Writes the indices of the choices that match <paramref name="query"/> into
        /// <paramref name="matches"/>, in list order. An empty query keeps every choice.
        /// </summary>
        public static void Filter(IReadOnlyList<string> choices, string query, List<int> matches)
        {
            matches.Clear();
            string trimmed = query == null ? string.Empty : query.Trim();
            for (int index = 0; index < choices.Count; index++)
            {
                if (trimmed.Length == 0 || Matches(choices[index], trimmed))
                {
                    matches.Add(index);
                }
            }
        }

        /// <summary>
        /// True when <paramref name="choice"/> contains the trimmed, non-empty <paramref name="query"/>.
        /// </summary>
        internal static bool Matches(string choice, string query)
        {
            return choice.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
