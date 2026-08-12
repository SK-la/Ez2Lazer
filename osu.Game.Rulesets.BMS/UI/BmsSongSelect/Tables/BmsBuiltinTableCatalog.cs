// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables
{
    public sealed class BmsBuiltinTableCatalogEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("group")]
        public string Group { get; set; } = string.Empty;
    }

    public static class BmsBuiltinTableCatalog
    {
        private const string resource_name = "osu.Game.Rulesets.BMS.Resources.Tables.builtin-catalog.json";

        private static readonly JsonSerializerOptions json_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static IReadOnlyList<BmsBuiltinTableCatalogEntry> Load()
        {
            var assembly = typeof(BmsBuiltinTableCatalog).Assembly;
            using var stream = assembly.GetManifestResourceStream(resource_name)
                               ?? throw new InvalidOperationException($"Missing embedded resource: {resource_name}");
            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<List<BmsBuiltinTableCatalogEntry>>(json, json_options)
                   ?? new List<BmsBuiltinTableCatalogEntry>();
        }
    }

    public readonly record struct BmsBuiltinTableSyncResult(int Succeeded, int Failed, int Skipped);
}
