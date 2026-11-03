// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering
{
    public sealed class BmsRajaFolderDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sql")]
        public string? Sql { get; set; }

        [JsonPropertyName("folder")]
        public List<BmsRajaFolderDefinition>? Folder { get; set; }

        [JsonPropertyName("showall")]
        public bool ShowAll { get; set; }
    }

    public sealed class BmsRajaRandomDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("filter")]
        public Dictionary<string, JsonElement>? Filter { get; set; }
    }

    public static class BmsFolderConfigLoader
    {
        public static IReadOnlyList<BmsRajaFolderDefinition> LoadFolderDefinitions()
        {
            string json = readEmbedded("osu.Game.Rulesets.BMS.Resources.Raja.folder.default.json");
            return JsonSerializer.Deserialize<List<BmsRajaFolderDefinition>>(json) ?? new List<BmsRajaFolderDefinition>();
        }

        public static IReadOnlyList<BmsRajaRandomDefinition> LoadRandomDefinitions()
        {
            string json = readEmbedded("osu.Game.Rulesets.BMS.Resources.Raja.random.default.json");
            return JsonSerializer.Deserialize<List<BmsRajaRandomDefinition>>(json) ?? new List<BmsRajaRandomDefinition>();
        }

        public static IReadOnlyList<BmsBar> BuildRootCommandBars(BmsBarContext context)
        {
            return BuildBars(LoadFolderDefinitions(), context);
        }

        public static IReadOnlyList<BmsBar> BuildBars(IReadOnlyList<BmsRajaFolderDefinition> definitions, BmsBarContext context)
        {
            var bars = new List<BmsBar>();

            foreach (var def in definitions)
            {
                if (def.Folder != null && def.Folder.Count > 0)
                    bars.Add(new BmsContainerBar(def.Name, def.Folder));
                else if (!string.IsNullOrWhiteSpace(def.Sql))
                    bars.Add(new BmsCommandBar(def.Name, def.Sql, def.ShowAll));
            }

            return bars;
        }

        private static string readEmbedded(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName)
                               ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
