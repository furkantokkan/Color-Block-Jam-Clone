using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// Reads and writes a level database manifest, formatVersion 1 (DEC-196), without file or asset access: the
    /// database name and, per pack, its file and level IDs in play order. Strict like the level JSON reader: unknown or
    /// repeated properties, type metadata and any other version are refused, and a failed read returns no manifest
    /// (docs/Level-Data-Format.md).
    /// </summary>
    public static class LevelManifestJson
    {
        public const int FormatVersion = 1;

        private const string k_formatVersionProperty = "formatVersion";

        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None
        };

        private static readonly string s_versionError =
            $"Expected level manifest formatVersion {FormatVersion} with a name and a packs array.";

        public static bool TryRead(string json, out LevelManifest manifest, out string error)
        {
            manifest = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The manifest file is empty.";
                return false;
            }

            ManifestDto document;
            try
            {
                JToken token = JToken.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (!(token is JObject root) || LevelDatabaseJson.ContainsMetadata(token))
                {
                    error = "The manifest must be a JSON object without type metadata.";
                    return false;
                }

                // A level JSON or another version is refused before its fields are read, so the message names the format.
                if (!IsCurrentVersion(root))
                {
                    error = s_versionError;
                    return false;
                }

                document = JsonConvert.DeserializeObject<ManifestDto>(json, s_jsonSettings);
            }
            catch (JsonException exception)
            {
                error = $"Invalid level manifest JSON: {exception.Message}";
                return false;
            }

            if (document == null || document.packs == null)
            {
                error = s_versionError;
                return false;
            }

            LevelManifestPack[] packs = new LevelManifestPack[document.packs.Length];
            for (int index = 0; index < packs.Length; index++)
            {
                PackDto pack = document.packs[index];
                if (pack == null)
                {
                    error = $"Pack {index + 1} of the manifest is empty.";
                    return false;
                }

                packs[index] = new LevelManifestPack(pack.file, pack.levels);
            }

            return LevelManifest.TryCreate(document.name, packs, out manifest, out error);
        }

        public static bool TryWrite(LevelManifest manifest, out string json, out string error)
        {
            json = null;
            if (manifest == null)
            {
                error = "The manifest is missing.";
                return false;
            }

            if (manifest.Name == null)
            {
                error = "A single-file level JSON has no manifest to write.";
                return false;
            }

            ManifestDto document = new ManifestDto
            {
                formatVersion = FormatVersion,
                name = manifest.Name,
                packs = new PackDto[manifest.PackCount]
            };
            for (int packIndex = 0; packIndex < manifest.PackCount; packIndex++)
            {
                LevelPackInfo pack = manifest.GetPackAt(packIndex);
                string[] levelIds = new string[pack.LevelCount];
                for (int index = 0; index < levelIds.Length; index++)
                {
                    levelIds[index] = manifest.GetLevelIdAt(pack.FirstLevelIndex + index);
                }

                document.packs[packIndex] = new PackDto { file = pack.File, levels = levelIds };
            }

            json = JsonConvert.SerializeObject(document, Formatting.Indented, s_jsonSettings);
            error = null;
            return true;
        }

        // A formatVersion of exactly 1; a large integer, a float or a string is another version.
        private static bool IsCurrentVersion(JObject root)
        {
            return root.TryGetValue(k_formatVersionProperty, out JToken version)
                && version is JValue value && value.Value is long number && number == FormatVersion;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class ManifestDto
        {
            [JsonProperty("formatVersion", Required = Required.Always)] public int formatVersion;
            [JsonProperty("name", Required = Required.Always)] public string name;
            [JsonProperty("packs", Required = Required.Always)] public PackDto[] packs;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class PackDto
        {
            [JsonProperty("file", Required = Required.Always)] public string file;
            [JsonProperty("levels", Required = Required.Always)] public string[] levels;
        }
    }
}
