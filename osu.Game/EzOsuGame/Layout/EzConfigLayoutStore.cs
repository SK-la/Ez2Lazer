// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Serialization;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Rulesets;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Theme JSON under <c>layout</c> / <c>layout-*</c>. <see cref="UseSet"/> rebinds this directory.
    /// </summary>
    public sealed class EzConfigLayoutStore
    {
        private static readonly JsonSerializerSettings json_settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = new List<JsonConverter> { new Vector2Converter() },
        };

        private const int playfield_save_debounce_ms = 200;

        private readonly Storage gameStorage;
        private readonly Lock playfieldSaveLock = new Lock();
        private Storage storage = null!;
        private int lastPlayfieldSave;
        private bool suppressPlayfieldSave;

        public string LayoutSetName { get; private set; }

        public ManiaPlayfieldLayoutConfig PlayfieldLayout { get; }

        public event Action? SetChanged;

        public EzConfigLayoutStore(Storage gameStorage)
        {
            this.gameStorage = gameStorage;
            LayoutSetName = EzModifyPath.LAYOUT_SET_DEFAULT;
            bind(LayoutSetName);
            PlayfieldLayout = new ManiaPlayfieldLayoutConfig();
            reloadPlayfield();
            PlayfieldLayout.StageX.BindValueChanged(_ => queuePlayfieldSave());
            PlayfieldLayout.StageY.BindValueChanged(_ => queuePlayfieldSave());
        }

        /// <summary>
        /// Points this store at <c>EzResources/Config/{layoutSetName}</c> and reloads subscribers.
        /// </summary>
        public void UseSet(string layoutSetName)
        {
            if (LayoutSetName == layoutSetName)
                return;

            LayoutSetName = layoutSetName;
            bind(layoutSetName);
            reloadPlayfield();
            SetChanged?.Invoke();
        }

        public SkinLayoutInfo Load(string layoutFile)
        {
            string? json = readText(layoutFile);

            if (string.IsNullOrEmpty(json))
                return new SkinLayoutInfo();

            try
            {
                return JsonConvert.DeserializeObject<SkinLayoutInfo>(json, json_settings) ?? new SkinLayoutInfo();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load Ez layout {layoutFile}");
                return new SkinLayoutInfo();
            }
        }

        public void Save(EzLayoutContainer container)
        {
            var layout = Load(container.LayoutFile);
            layout.Update(container.Ruleset, ((ISerialisableDrawableContainer)container).CreateSerialisedInfo().ToArray());
            Save(container.LayoutFile, layout);
        }

        public void Save(string layoutFile, SkinLayoutInfo layout) =>
            writeText(layoutFile, JsonConvert.SerializeObject(layout, json_settings));

        public void Reset(string layoutFile)
        {
            if (storage.Exists(layoutFile))
                storage.Delete(layoutFile);
        }

        public ManiaPlayfieldLayout LoadManiaPlayfieldLayout()
        {
            string? json = readText(ManiaPlayfieldLayout.FILENAME);

            if (string.IsNullOrEmpty(json))
                return new ManiaPlayfieldLayout();

            try
            {
                return ManiaPlayfieldLayout.Parse(json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load Ez mania playfield layout {ManiaPlayfieldLayout.FILENAME}");
                return new ManiaPlayfieldLayout();
            }
        }

        public void SaveManiaPlayfieldLayout(ManiaPlayfieldLayout layout) =>
            writeText(ManiaPlayfieldLayout.FILENAME, layout.Serialize());

        private void reloadPlayfield()
        {
            suppressPlayfieldSave = true;

            try
            {
                PlayfieldLayout.ApplySaved(LoadManiaPlayfieldLayout());
            }
            finally
            {
                suppressPlayfieldSave = false;
            }
        }

        private void queuePlayfieldSave()
        {
            if (suppressPlayfieldSave)
                return;

            int current = Interlocked.Increment(ref lastPlayfieldSave);

            Task.Delay(playfield_save_debounce_ms).ContinueWith(_ =>
            {
                if (current != lastPlayfieldSave)
                    return;

                lock (playfieldSaveLock)
                {
                    var layout = LoadManiaPlayfieldLayout();
                    layout.Stage.X = PlayfieldLayout.StageXFraction;
                    layout.Stage.Y = PlayfieldLayout.StageYFraction;
                    SaveManiaPlayfieldLayout(layout);
                }
            });
        }

        public void Reset(string layoutFile, RulesetInfo? ruleset)
        {
            var layout = Load(layoutFile);
            layout.Reset(ruleset);

            if (layout.DrawableInfo.Count == 0)
                Reset(layoutFile);
            else
                Save(layoutFile, layout);
        }

        public Container CreateComponentsContainer(string layoutFile, RulesetInfo? ruleset)
        {
            var layout = Load(layoutFile);

            if (!layout.TryGetDrawableInfo(ruleset, out var drawableInfos) || drawableInfos.Length == 0)
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

        private void bind(string layoutSetName) =>
            storage = gameStorage.GetStorageForDirectory(EzModifyPath.LayoutSetPath(layoutSetName));

        private string? readText(string filename)
        {
            if (!storage.Exists(filename))
                return null;

            try
            {
                using (var stream = storage.GetStream(filename))
                {
                    if (stream == null)
                        return null;

                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                        return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to read Ez layout {filename}");
                return null;
            }
        }

        private void writeText(string filename, string contents)
        {
            using (var stream = storage.CreateFileSafely(filename))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
                writer.Write(contents);
        }
    }
}
