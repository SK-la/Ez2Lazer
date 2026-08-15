// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Serialization;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Loads and saves Ez client layout JSON under <see cref="EzModifyPath.CONFIG_LAYOUT_PATH"/>.
    /// Independent of skin files and Realm skin storage.
    /// </summary>
    public class EzLayoutStore
    {
        private static readonly JsonSerializerSettings json_settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter> { new Vector2Converter() },
        };

        private readonly Storage storage;

        public EzLayoutStore(Storage gameStorage)
        {
            storage = gameStorage.GetStorageForDirectory(EzModifyPath.CONFIG_LAYOUT_PATH);
        }

        public SkinLayoutInfo Load(GlobalSkinnableContainers target)
        {
            string filename = getFilename(target);

            if (!storage.Exists(filename))
                return new SkinLayoutInfo();

            try
            {
                using (var stream = storage.GetStream(filename))
                {
                    if (stream == null)
                        return new SkinLayoutInfo();

                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        return JsonConvert.DeserializeObject<SkinLayoutInfo>(json, json_settings) ?? new SkinLayoutInfo();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load Ez layout {filename}");
                return new SkinLayoutInfo();
            }
        }

        public void Save(SkinnableContainer container)
        {
            var layout = Load(container.Lookup.Lookup);
            layout.Update(container.Lookup.Ruleset, ((ISerialisableDrawableContainer)container).CreateSerialisedInfo().ToArray());
            Save(container.Lookup.Lookup, layout);
        }

        public void Save(GlobalSkinnableContainers target, SkinLayoutInfo layout)
        {
            string filename = getFilename(target);
            string json = JsonConvert.SerializeObject(layout, json_settings);

            using (var stream = storage.CreateFileSafely(filename))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
                writer.Write(json);
        }

        public void Reset(GlobalSkinnableContainers target)
        {
            string filename = getFilename(target);

            if (storage.Exists(filename))
                storage.Delete(filename);
        }

        public void Reset(GlobalSkinnableContainerLookup lookup)
        {
            var layout = Load(lookup.Lookup);
            layout.Reset(lookup.Ruleset);

            if (layout.DrawableInfo.Count == 0)
                Reset(lookup.Lookup);
            else
                Save(lookup.Lookup, layout);
        }

        public Container CreateComponentsContainer(GlobalSkinnableContainerLookup lookup)
        {
            var layout = Load(lookup.Lookup);

            if (!layout.TryGetDrawableInfo(lookup.Ruleset, out var drawableInfos) || drawableInfos.Length == 0)
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.Both,
                };
            }

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                ChildrenEnumerable = drawableInfos.Select(i => i.CreateInstance())
            };
        }

        private static string getFilename(GlobalSkinnableContainers target) => target switch
        {
            GlobalSkinnableContainers.SongSelect => @"SongSelect.json",
            GlobalSkinnableContainers.MainHUDComponents => @"Gameplay.json",
            _ => $"{target}.json",
        };
    }
}
