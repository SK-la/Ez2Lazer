// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.IO.Serialization;
using osu.Framework.Platform;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Import/export for per-skin HUD layout JSON (<c>{GlobalSkinnableContainers}.json</c>).
    /// </summary>
    public static class EzSkinLayoutIO
    {
        private static readonly JsonSerializerSettings json_settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter> { new Vector2Converter() },
        };

        /// <summary>
        /// Exports the active target's layout from <paramref name="skin"/> to <c>{exportRoot}/layout-layers/{lookup}.json</c>.
        /// </summary>
        public static string ExportLayerJson(SkinnableContainer target, Skin skin, Storage exportRoot)
        {
            skin.UpdateDrawableTarget(target);

            if (!skin.LayoutInfos.TryGetValue(target.Lookup.Lookup, out var layout))
                layout = new SkinLayoutInfo();

            string fileName = $"{target.Lookup.Lookup}.json";
            string relativeDest = $"layout-layers/{fileName}";
            string json = JsonConvert.SerializeObject(layout, json_settings);

            using (var stream = exportRoot.GetStorageForDirectory("layout-layers").CreateFileSafely(fileName))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
                writer.Write(json);

            return exportRoot.GetFullPath(relativeDest);
        }

        /// <summary>
        /// Replaces one skin layout file in memory, reloads <paramref name="target"/>, and writes through <paramref name="skins"/>.
        /// </summary>
        public static void ImportLayerJson(SkinnableContainer target, SkinManager skins, string sourceFile)
        {
            string json = File.ReadAllText(sourceFile);

            var layout = JsonConvert.DeserializeObject<SkinLayoutInfo>(json, json_settings)
                         ?? throw new InvalidDataException("Layout JSON was empty or invalid.");

            skins.EnsureMutableSkin();
            var skin = skins.CurrentSkin.Value;
            skin.LayoutInfos[target.Lookup.Lookup] = layout;
            target.Reload();
            skins.Save(skin);
        }
    }
}
