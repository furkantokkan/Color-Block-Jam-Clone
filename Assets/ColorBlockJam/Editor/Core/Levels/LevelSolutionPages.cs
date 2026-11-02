using System.Globalization;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Splits the Level Editor's listed solutions into pages of five rows: which entry each row slot shows, whether the
    /// pager shows and which of its arrows can move. It holds only counts, so it needs no window.
    /// </summary>
    internal sealed class LevelSolutionPages
    {
        /// <summary>Rows per page; the validation panel has this many row slots.</summary>
        internal const int k_pageSize = 5;

        /// <summary>Listed entries; 0 when the panel shows no list.</summary>
        internal int Count { get; private set; }

        /// <summary>The shown page, from 0.</summary>
        internal int Page { get; private set; }

        /// <summary>At least one page, so an empty list reads as one empty page.</summary>
        internal int PageCount => Count <= k_pageSize ? 1 : (Count + k_pageSize - 1) / k_pageSize;

        /// <summary>The pager shows only when every entry does not fit on one page.</summary>
        internal bool ShowsPager => Count > k_pageSize;

        internal bool CanGoBack => Page > 0;

        internal bool CanGoNext => Page < PageCount - 1;

        /// <summary>The pager's reading between its arrows, for example "2 / 4".</summary>
        internal string PageText => (Page + 1).ToString(CultureInfo.InvariantCulture) + " / " +
                                    PageCount.ToString(CultureInfo.InvariantCulture);

        /// <summary>A new list shows its first page.</summary>
        internal void Reset(int count)
        {
            Count = count > 0 ? count : 0;
            Page = 0;
        }

        /// <summary>Moves one page on; false at the last page.</summary>
        internal bool Next()
        {
            if (!CanGoNext)
            {
                return false;
            }

            Page++;
            return true;
        }

        /// <summary>Moves one page back; false at the first page.</summary>
        internal bool Previous()
        {
            if (!CanGoBack)
            {
                return false;
            }

            Page--;
            return true;
        }

        /// <summary>The entry a row slot shows on the current page, or -1 when that slot is empty.</summary>
        internal int GetEntryIndex(int slot)
        {
            if (slot < 0 || slot >= k_pageSize)
            {
                return -1;
            }

            int entry = Page * k_pageSize + slot;
            return entry < Count ? entry : -1;
        }
    }
}
