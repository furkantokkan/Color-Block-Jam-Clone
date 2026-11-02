using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorBlockJam.Player.Storage
{
    /// <summary>
    /// Maps a player snapshot to its versioned JSON (format 1, design 4.2) and back with explicit Newtonsoft
    /// settings and TypeNameHandling.None. Currency and booster keys this build does not know are read and written
    /// back unchanged; a newer formatVersion is refused with PlayerSnapshotVersionException, and any malformed,
    /// negative or non-numeric content with InvalidDataException.
    /// </summary>
    public static class PlayerSnapshotJson
    {
        public const int FormatVersion = 1;

        private const int k_maxJsonCharacters = 1024 * 1024;
        private const int k_maxEntries = 256;
        private const int k_maxKeyLength = 64;

        private const string k_formatVersion = "formatVersion";
        private const string k_revision = "revision";
        private const string k_currentLevelIndex = "currentLevelIndex";
        private const string k_currencies = "currencies";
        private const string k_boosterStock = "boosterStock";
        private const string k_settings = "settings";
        private const string k_currencyRefillStarts = "currencyRefillStarts";

        private static readonly string[] s_knownFields =
        {
            k_formatVersion, k_revision, k_currentLevelIndex, k_currencies, k_boosterStock, k_settings,
            k_currencyRefillStarts
        };

        private static readonly string[] s_settingFields = { "music", "sound", "vibration" };

        private static readonly JsonSerializerSettings s_settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            MaxDepth = 8
        };

        public static PlayerSnapshot Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > k_maxJsonCharacters)
            {
                throw new InvalidDataException("The player save must be nonempty and at most 1 MiB.");
            }

            JObject root = Parse(json);
            JToken version = root[k_formatVersion];
            RequireType(version, JTokenType.Integer, k_formatVersion);
            long formatVersion = version.Value<long>();
            if (formatVersion > FormatVersion)
            {
                throw new PlayerSnapshotVersionException(formatVersion);
            }

            if (formatVersion != FormatVersion)
            {
                throw new InvalidDataException($"Unsupported player save format version {formatVersion}.");
            }

            ValidateFields(root);
            try
            {
                SnapshotDto document = root.ToObject<SnapshotDto>(JsonSerializer.Create(s_settings));
                PlayerSettings settings = new PlayerSettings(
                    document.settings.music ?? true, document.settings.sound ?? true, document.settings.vibration ?? true);
                return new PlayerSnapshot(document.revision, document.currentLevelIndex, document.currencies,
                    document.boosterStock, settings, document.currencyRefillStarts);
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                throw new InvalidDataException("Invalid player save JSON: " + exception.Message, exception);
            }
        }

        public static string Write(PlayerSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            SnapshotDto document = new SnapshotDto
            {
                formatVersion = FormatVersion,
                revision = snapshot.Revision,
                currentLevelIndex = snapshot.CurrentLevelIndex,
                currencies = new Dictionary<string, int>(snapshot.Currencies.Count, StringComparer.Ordinal),
                boosterStock = new Dictionary<string, int>(snapshot.BoosterStock.Count, StringComparer.Ordinal),
                settings = new SettingsDto
                {
                    music = snapshot.Settings.Music,
                    sound = snapshot.Settings.Sound,
                    vibration = snapshot.Settings.Vibration
                }
            };

            foreach (KeyValuePair<string, int> pair in snapshot.Currencies)
            {
                document.currencies.Add(pair.Key, pair.Value);
            }

            foreach (KeyValuePair<string, int> pair in snapshot.BoosterStock)
            {
                document.boosterStock.Add(pair.Key, pair.Value);
            }

            // Written only while a timed currency is refilling, so a save without one keeps its earlier shape.
            if (snapshot.RefillStarts.Count > 0)
            {
                document.currencyRefillStarts = new Dictionary<string, long>(snapshot.RefillStarts.Count, StringComparer.Ordinal);
                foreach (KeyValuePair<string, long> pair in snapshot.RefillStarts)
                {
                    document.currencyRefillStarts.Add(pair.Key, pair.Value);
                }
            }

            return JsonConvert.SerializeObject(document, Formatting.Indented, s_settings) + "\n";
        }

        private static JObject Parse(string json)
        {
            try
            {
                using (StringReader text = new StringReader(json))
                using (JsonTextReader reader = new JsonTextReader(text))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.MaxDepth = 8;
                    JToken token = JToken.ReadFrom(reader,
                        new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    while (reader.Read())
                    {
                        if (reader.TokenType != JsonToken.Comment)
                        {
                            throw new InvalidDataException("The player save has content after its root object.");
                        }
                    }

                    if (!(token is JObject root))
                    {
                        throw new InvalidDataException("The player save root must be a JSON object.");
                    }

                    return root;
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid player save JSON: " + exception.Message, exception);
            }
        }

        private static void ValidateFields(JObject root)
        {
            foreach (JProperty property in root.Properties())
            {
                // Unknown top-level fields are ignored, but a case alias of a known field is refused: Json.NET
                // matches members without case sensitivity and would read it as the known field.
                for (int index = 0; index < s_knownFields.Length; index++)
                {
                    string known = s_knownFields[index];
                    if (!string.Equals(property.Name, known, StringComparison.Ordinal)
                        && string.Equals(property.Name, known, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Field names are case-sensitive: " + property.Name);
                    }
                }
            }

            RequireRange(root[k_revision], k_revision, long.MaxValue);
            RequireRange(root[k_currentLevelIndex], k_currentLevelIndex, int.MaxValue);
            ValidateCounts(root[k_currencies], k_currencies);
            ValidateCounts(root[k_boosterStock], k_boosterStock);

            // Optional (DEC-174): a save written before timed refills has no such field.
            JToken refillStarts = root[k_currencyRefillStarts];
            if (refillStarts != null)
            {
                ValidateCounts(refillStarts, k_currencyRefillStarts, long.MaxValue);
            }

            JToken settings = root[k_settings];
            RequireType(settings, JTokenType.Object, k_settings);
            foreach (JProperty property in ((JObject)settings).Properties())
            {
                for (int index = 0; index < s_settingFields.Length; index++)
                {
                    string known = s_settingFields[index];
                    if (string.Equals(property.Name, known, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.Equals(property.Name, known, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException("Field names are case-sensitive: " + property.Name);
                        }

                        RequireType(property.Value, JTokenType.Boolean, known);
                    }
                }
            }
        }

        private static void ValidateCounts(JToken token, string field, long maximum = int.MaxValue)
        {
            RequireType(token, JTokenType.Object, field);
            JObject values = (JObject)token;
            if (values.Count > k_maxEntries)
            {
                throw new InvalidDataException($"The player save lists more than {k_maxEntries} {field}.");
            }

            foreach (JProperty property in values.Properties())
            {
                if (property.Name.Length == 0 || property.Name.Length > k_maxKeyLength)
                {
                    throw new InvalidDataException($"Every {field} key must be 1 to {k_maxKeyLength} characters.");
                }

                RequireRange(property.Value, field + "." + property.Name, maximum);
            }
        }

        private static void RequireRange(JToken token, string field, long maximum)
        {
            RequireType(token, JTokenType.Integer, field);
            object raw = ((JValue)token).Value;
            if (!(raw is long value) || value < 0 || value > maximum)
            {
                throw new InvalidDataException($"The player save field {field} must be a whole number from 0 to {maximum}.");
            }
        }

        private static void RequireType(JToken token, JTokenType expected, string field)
        {
            if (token == null || token.Type != expected)
            {
                throw new InvalidDataException($"The player save field {field} is missing or not {expected}.");
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class SnapshotDto
        {
            [JsonProperty(Order = 0)] public int formatVersion;
            [JsonProperty(Order = 1)] public long revision;
            [JsonProperty(Order = 2)] public int currentLevelIndex;
            [JsonProperty(Order = 3)] public Dictionary<string, int> currencies;
            [JsonProperty(Order = 4)] public Dictionary<string, int> boosterStock;
            [JsonProperty(Order = 5)] public SettingsDto settings;

            [JsonProperty(Order = 6, NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, long> currencyRefillStarts;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class SettingsDto
        {
            [JsonProperty(Order = 0)] public bool? music;
            [JsonProperty(Order = 1)] public bool? sound;
            [JsonProperty(Order = 2)] public bool? vibration;
        }
    }
}
