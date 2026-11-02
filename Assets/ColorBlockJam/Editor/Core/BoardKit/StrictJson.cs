using System;
using Newtonsoft.Json.Linq;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Shared strict-JSON checks (duplicate-property rejection, exact type and case-sensitive field names) for the
    /// editor-only piece library reader and the one-time JSON migrations (DEC-193).
    /// </summary>
    internal static class StrictJson
    {
        public static JToken Parse(string json) =>
            JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });

        public static void RequireType(JToken token, JTokenType expected, string what)
        {
            if (token == null || token.Type != expected)
                throw new ArgumentException(Capitalize(what) + " has a missing field or an incorrect value type.");
        }

        public static void RequireOnlyProperties(JToken token, string what, params string[] allowedNames)
        {
            RequireType(token, JTokenType.Object, what);
            foreach (JProperty property in ((JObject)token).Properties())
            {
                // Json.NET matches members without case sensitivity; reject aliases before deserialization.
                if (Array.IndexOf(allowedNames, property.Name) < 0)
                    throw new ArgumentException("Unknown " + what + " field: " + property.Name + ". Field names are case-sensitive.");
            }
        }

        private static string Capitalize(string what) =>
            what.Length == 0 ? what : char.ToUpperInvariant(what[0]) + what.Substring(1);
    }
}
