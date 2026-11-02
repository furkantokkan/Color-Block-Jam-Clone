using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Names a level's level pieces (DEC-193): for each key a block of the level uses that the piece library lacks,
    /// the name its Blocks tile shows and Save Level writes into the level's pieces table, plus the small table edits
    /// that keep a name with its piece. It only reads the levels it is given and returns new ones.
    /// </summary>
    internal static class LevelPieceNames
    {
        /// <summary>
        /// The level's pieces table as Save Level writes it: one entry per key a block uses and the catalog (the piece
        /// library with its pending template footprints) lacks, in block order. A key takes the level's own name for
        /// it, else the first name another level of the database gives it ("use the existing one"), else the name this
        /// session gave the piece, else a new "&lt;Level Name&gt; Piece n" that no library, database or session name
        /// uses, ignoring case. Names for other keys are left out. While the catalog cannot be read, the level keeps
        /// its own table, since no key can be told from a template.
        /// </summary>
        public static LevelPieceName[] Resolve(LevelData level, IReadOnlyList<LevelData> otherLevels,
            LevelShapeCatalog library, Func<ShapeKey, string> sessionName, IEnumerable<string> reservedNames)
        {
            if (level == null)
            {
                return Array.Empty<LevelPieceName>();
            }

            if (library == null || library.Error != null)
            {
                return LevelRecordCopy.GetPieceNames(level);
            }

            HashSet<string> taken = GetTakenNames(level, otherLevels, library, reservedNames);
            List<LevelPieceName> names = new List<LevelPieceName>();
            for (int index = 0; index < level.BlockCount; index++)
            {
                ShapeKey key = level.GetBlockAt(index)?.Key ?? default;
                if (!key.IsValid || library.IndexOf(key) >= 0 || IndexOf(names, key) >= 0)
                {
                    continue;
                }

                string name = GetUsable(Find(level, key)) ?? GetUsable(Find(otherLevels, key))
                              ?? GetUsable(sessionName?.Invoke(key));
                if (name == null)
                {
                    name = LevelNamedPiece.GetDefaultName(level.Name, taken.Contains);
                    taken.Add(name);
                }

                names.Add(new LevelPieceName(key, name));
            }

            return names.ToArray();
        }

        /// <summary>The name the level's own pieces table gives this key, or null.</summary>
        public static string Find(LevelData level, ShapeKey key)
        {
            for (int index = 0; key.IsValid && level != null && index < level.PieceNameCount; index++)
            {
                LevelPieceName piece = level.GetPieceNameAt(index);
                if (piece != null && piece.Key == key)
                {
                    return piece.Name;
                }
            }

            return null;
        }

        /// <summary>The first name these levels' pieces tables give this key, in level order, or null.</summary>
        public static string Find(IReadOnlyList<LevelData> levels, ShapeKey key)
        {
            for (int index = 0; levels != null && index < levels.Count; index++)
            {
                string name = Find(levels[index], key);
                if (name != null)
                {
                    return name;
                }
            }

            return null;
        }

        /// <summary>
        /// The level whose table names <paramref name="to"/> as it named <paramref name="from"/>, once no block uses
        /// <paramref name="from"/> any more: an edit moved the piece's blocks to a new footprint, so its name follows
        /// them. <paramref name="to"/> keeps a name of its own ("use the existing one"); the default key, for a new
        /// footprint that is a template, takes none. The same instance comes back when nothing changes.
        /// </summary>
        public static LevelData CarryName(LevelData level, ShapeKey from, ShapeKey to)
        {
            string name = Find(level, from);
            if (name == null || from == to || LevelBlockQueries.HasBlock(level, from))
            {
                return level;
            }

            List<LevelPieceName> names = new List<LevelPieceName>(LevelRecordCopy.GetPieceNames(level));
            names.RemoveAt(IndexOf(names, from));
            if (to.IsValid && IndexOf(names, to) < 0)
            {
                names.Add(new LevelPieceName(to, name));
            }

            return LevelRecordCopy.WithPieceNames(level, names.ToArray());
        }

        /// <summary>The level whose table names this key <paramref name="name"/>, added or replaced.</summary>
        public static LevelData SetName(LevelData level, ShapeKey key, string name)
        {
            List<LevelPieceName> names = new List<LevelPieceName>(LevelRecordCopy.GetPieceNames(level));
            int index = IndexOf(names, key);
            if (index >= 0 && string.Equals(names[index].Name, name, StringComparison.Ordinal))
            {
                return level;
            }

            if (index >= 0)
            {
                names[index] = new LevelPieceName(key, name);
            }
            else
            {
                names.Add(new LevelPieceName(key, name));
            }

            return LevelRecordCopy.WithPieceNames(level, names.ToArray());
        }

        // Every name a new default must not repeat: the templates', the database's tables' and the session's.
        private static HashSet<string> GetTakenNames(LevelData level, IReadOnlyList<LevelData> otherLevels,
            LevelShapeCatalog library, IEnumerable<string> reservedNames)
        {
            HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < library.Count; index++)
            {
                taken.Add(library.Get(index).DisplayName);
            }

            AddNames(level, taken);
            for (int index = 0; otherLevels != null && index < otherLevels.Count; index++)
            {
                AddNames(otherLevels[index], taken);
            }

            if (reservedNames != null)
            {
                foreach (string name in reservedNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        taken.Add(name);
                    }
                }
            }

            return taken;
        }

        private static void AddNames(LevelData level, HashSet<string> names)
        {
            for (int index = 0; level != null && index < level.PieceNameCount; index++)
            {
                string name = level.GetPieceNameAt(index)?.Name;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }
        }

        /// <summary>True for a name LevelData.Validate accepts in a pieces table: 1 to 128 characters, not blank.</summary>
        public static bool CanName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length <= LevelPieceName.MaxNameLength;
        }

        // A name Validate accepts, or null.
        private static string GetUsable(string name)
        {
            return CanName(name) ? name : null;
        }

        private static int IndexOf(List<LevelPieceName> names, ShapeKey key)
        {
            for (int index = 0; index < names.Count; index++)
            {
                if (names[index] != null && names[index].Key == key)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
