using System;
using System.Globalization;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Hud
{
    /// <summary>
    /// The attempt's 1-based level number and its prebuilt texts, shared by the top bar's Level badge, the Retry
    /// confirmation's title and the win result (AC-2). The attempt loader sets it once per level on Home: the launched
    /// number, which keeps counting past the last database level while the levels play again (DEC-173).
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle, HUD branch). Plain state with no subscription or work; the strings
    /// are built only when the number changes, never in a Playing tick (Gameplay has no ZString; HudText's pattern).
    /// </remarks>
    public sealed class AttemptLevelNumber
    {
        /// <summary>The number shown before any level is known, or for a Test Level outside the active database.</summary>
        public const int k_unknown = 0;

        private const string k_titlePrefix = "Level ";
        private const string k_unknownText = "-";
        private const string k_unknownTitle = "Level";

        /// <summary>The level's 1-based number, or <see cref="k_unknown"/>.</summary>
        public int Value { get; private set; } = k_unknown;

        /// <summary>The number as the badge shows it ("29", or "-" when unknown).</summary>
        public string NumberText { get; private set; } = k_unknownText;

        /// <summary>The popup title ("Level 29", or "Level" when unknown).</summary>
        public string Title { get; private set; } = k_unknownTitle;

        /// <summary>
        /// The 1-based position of the level with <paramref name="levelId"/> in <paramref name="levels"/>, or
        /// <see cref="k_unknown"/> when it is absent. One manifest lookup on Home, with no pack needed, for the Level
        /// Editor's Test Level, which knows only the ID (DEC-196).
        /// </summary>
        public static int Find(LevelCatalog levels, string levelId)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            if (string.IsNullOrWhiteSpace(levelId))
            {
                return k_unknown;
            }

            int index = levels.IndexOf(levelId);
            return index < 0 ? k_unknown : index + 1;
        }

        /// <summary>Stores the number and rebuilds its texts when it changed; a negative number counts as unknown.</summary>
        public void Set(int number)
        {
            if (number < 0)
            {
                number = k_unknown;
            }

            if (number == Value)
            {
                return;
            }

            Value = number;
            if (number == k_unknown)
            {
                NumberText = k_unknownText;
                Title = k_unknownTitle;
                return;
            }

            NumberText = number.ToString(CultureInfo.InvariantCulture);
            Title = string.Concat(k_titlePrefix, NumberText);
        }
    }
}
