using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// The levels of the shipped default database (Data/Json/Levels/Main), read from its manifest and packs as the Level
    /// Editor reads them (DEC-196), for tests that check every shipped level.
    /// </summary>
    internal static class ShippedLevels
    {
        internal const string ManifestPath = LevelDatabaseAddressables.DefaultManifestPath;

        internal static LevelDatabaseSnapshot ReadDatabase()
        {
            Assert.That(LevelDatabaseFiles.TryRead(ManifestPath, out LevelDatabaseSnapshot snapshot, out string error),
                Is.True, error);
            return snapshot;
        }

        internal static LevelData[] Read()
        {
            return ToArray(ReadDatabase());
        }

        /// <summary>The levels of the database the game plays: the one the project scope installer names (DEC-194).</summary>
        internal static bool TryReadActive(out LevelData[] levels, out string error)
        {
            levels = null;
            string path = LevelDatabaseAddressables.GetActivePath(LevelDatabaseAddressables.LoadProjectScope());
            if (!LevelDatabaseFiles.TryRead(path, out LevelDatabaseSnapshot snapshot, out error))
            {
                return false;
            }

            levels = ToArray(snapshot);
            return true;
        }

        private static LevelData[] ToArray(LevelDatabaseSnapshot snapshot)
        {
            LevelData[] levels = new LevelData[snapshot.Levels.Count];
            for (int index = 0; index < levels.Length; index++)
            {
                levels[index] = snapshot.Levels[index];
            }

            return levels;
        }
    }
}
