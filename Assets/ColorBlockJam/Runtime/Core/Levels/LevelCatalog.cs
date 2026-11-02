using System;
using System.Collections.Generic;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The levels of one level database in play order, handed out as copies so no caller can change them. The manifest
    /// gives the count and every level's index; a level's data is there while its pack is resident (DEC-196). The
    /// project scope binds one catalog and reads its manifest before Hub, and the Game scene loads the pack of the level
    /// it plays; the Level Editor and tests keep their own catalogs with every pack resident, so an authoring read never
    /// replaces the game's levels (DEC-192).
    /// </summary>
    public sealed class LevelCatalog
    {
        private LevelManifest m_manifest;

        // One entry per manifest pack, in manifest order; null while that pack is not resident.
        private LevelData[][] m_packs;

        /// <summary>A catalog read from text or from levels, with no manifest GUID.</summary>
        public LevelCatalog()
        {
        }

        /// <param name="jsonGuid">The Addressable GUID of the database manifest this catalog reads, or null when unknown.</param>
        public LevelCatalog(string jsonGuid)
        {
            JsonGuid = jsonGuid;
        }

        /// <summary>The Addressable GUID of the database manifest this catalog reads, or null for one read from text.</summary>
        public string JsonGuid { get; }

        /// <summary>The loaded manifest; throws until a manifest, a database or a single-file level JSON is loaded.</summary>
        public LevelManifest Manifest
        {
            get
            {
                ThrowIfNotLoaded();
                return m_manifest;
            }
        }

        public int Count => Manifest.Count;

        /// <summary>The play-order index of the level with <paramref name="id"/>, or -1 when the database has none.</summary>
        public int IndexOf(string id)
        {
            return Manifest.IndexOf(id);
        }

        /// <summary>
        /// Replaces everything with the manifest in <paramref name="json"/>; no pack is resident afterwards. A failed read
        /// leaves the catalog unloaded.
        /// </summary>
        public bool LoadManifestJson(string json, out string error)
        {
            Unload();
            if (!LevelManifestJson.TryRead(json, out LevelManifest manifest, out error))
            {
                return false;
            }

            m_manifest = manifest;
            m_packs = new LevelData[manifest.PackCount][];
            return true;
        }

        /// <summary>
        /// Reads one pack of the loaded manifest and makes it resident. Its levels must be exactly the ones the manifest
        /// lists for it, in order. A failed read changes nothing.
        /// </summary>
        public bool LoadPackJson(int packIndex, string json, LevelReadMode mode, out string error)
        {
            LevelManifest manifest = Manifest;
            if ((uint)packIndex >= (uint)manifest.PackCount)
            {
                throw new ArgumentOutOfRangeException(nameof(packIndex), packIndex,
                    $"The manifest has {manifest.PackCount} pack(s).");
            }

            if (!LevelDatabaseJson.TryRead(json, mode, out LevelData[] levels, out error)
                || !manifest.TryMatchPack(packIndex, levels, out error))
            {
                return false;
            }

            m_packs[packIndex] = levels;
            return true;
        }

        /// <summary>
        /// Replaces everything with a whole database whose levels were read already (the Level Editor's files), every
        /// pack resident. <paramref name="levels"/> must be the manifest's levels in play order; the catalog keeps them
        /// and hands out copies. A failed check leaves the catalog unloaded.
        /// </summary>
        public bool LoadLevels(LevelManifest manifest, IReadOnlyList<LevelData> levels, out string error)
        {
            Unload();
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            if (levels.Count != manifest.Count)
            {
                error = $"The database has {levels.Count} level(s), but its manifest lists {manifest.Count}.";
                return false;
            }

            LevelData[][] packs = new LevelData[manifest.PackCount][];
            for (int packIndex = 0; packIndex < packs.Length; packIndex++)
            {
                LevelPackInfo pack = manifest.GetPackAt(packIndex);
                LevelData[] packLevels = new LevelData[pack.LevelCount];
                for (int index = 0; index < packLevels.Length; index++)
                {
                    packLevels[index] = levels[pack.FirstLevelIndex + index];
                }

                if (!manifest.TryMatchPack(packIndex, packLevels, out error))
                {
                    return false;
                }

                packs[packIndex] = packLevels;
            }

            m_manifest = manifest;
            m_packs = packs;
            error = null;
            return true;
        }

        /// <summary>Authoring read of a single-file level JSON for tests and tools; the runtime gate is not applied.</summary>
        public bool LoadJson(string json, out string error)
        {
            return LoadJson(json, LevelReadMode.Authoring, out error);
        }

        /// <summary>
        /// Replaces everything with the levels of a single-file level JSON: one resident pack under a nameless manifest.
        /// A failed read leaves the catalog unloaded, so <see cref="Count"/> and <see cref="GetAt"/> throw until a read
        /// succeeds.
        /// </summary>
        public bool LoadJson(string json, LevelReadMode mode, out string error)
        {
            Unload();
            if (!LevelDatabaseJson.TryRead(json, mode, out LevelData[] levels, out error))
            {
                return false;
            }

            m_manifest = LevelManifest.Single(levels);
            m_packs = levels.Length == 0 ? Array.Empty<LevelData[]>() : new[] { levels };
            return true;
        }

        /// <summary>True when the pack of the level at <paramref name="index"/> is resident.</summary>
        public bool IsResident(int index)
        {
            return m_packs[Manifest.GetPackIndex(index)] != null;
        }

        /// <summary>
        /// Keeps only the pack at <paramref name="packIndex"/> resident; -1 releases every pack. The game keeps one pack.
        /// </summary>
        public void ReleasePacksExcept(int packIndex)
        {
            ThrowIfNotLoaded();
            for (int index = 0; index < m_packs.Length; index++)
            {
                if (index != packIndex)
                {
                    m_packs[index] = null;
                }
            }
        }

        /// <summary>A copy of the level at <paramref name="index"/>; throws when its pack is not resident.</summary>
        public LevelData GetAt(int index)
        {
            LevelManifest manifest = Manifest;
            int packIndex = manifest.GetPackIndex(index);
            LevelPackInfo pack = manifest.GetPackAt(packIndex);
            LevelData[] levels = m_packs[packIndex];
            if (levels == null)
            {
                throw new InvalidOperationException(
                    $"Level {manifest.GetLevelIdAt(index)} is in pack {pack.File}, which is not loaded.");
            }

            return levels[index - pack.FirstLevelIndex].Clone();
        }

        private void Unload()
        {
            m_manifest = null;
            m_packs = null;
        }

        private void ThrowIfNotLoaded()
        {
            if (m_manifest == null)
            {
                throw new InvalidOperationException("The level catalog has not been loaded.");
            }
        }
    }
}
