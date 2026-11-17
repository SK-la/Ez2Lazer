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
        private readonly Dictionary<string, SkinLayoutInfo> pendingLayouts = new Dictionary<string, SkinLayoutInfo>();
        private Storage storage = null!;
        private int lastPlayfieldSave;
        private bool suppressPlayfieldSave;

        public string LayoutSetName { get; private set; }

        public bool IsClientRoot { get; }

        public ManiaPlayfieldLayoutConfig PlayfieldLayout { get; }

        public event Action? SetChanged;

        public EzConfigLayoutStore(Storage gameStorage)
            : this(gameStorage, clientRoot: false)
        {
        }

        /// <summary>
        /// JSON under <see cref="EzModifyPath.CONFIG_ROOT_PATH"/>. <see cref="UseSet"/> is a no-op.
        /// </summary>
        public static EzConfigLayoutStore CreateClientRoot(Storage gameStorage) =>
            new EzConfigLayoutStore(gameStorage, clientRoot: true);

        private EzConfigLayoutStore(Storage gameStorage, bool clientRoot)
        {
            this.gameStorage = gameStorage;
            IsClientRoot = clientRoot;
            PlayfieldLayout = new ManiaPlayfieldLayoutConfig();

            if (clientRoot)
            {
                LayoutSetName = string.Empty;
                storage = gameStorage.GetStorageForDirectory(EzModifyPath.CONFIG_ROOT_PATH);
                return;
            }

            LayoutSetName = EzModifyPath.LAYOUT_SET_DEFAULT;
            bind(LayoutSetName);
            reloadPlayfield();
            PlayfieldLayout.StageX.BindValueChanged(_ => queuePlayfieldSave());
            PlayfieldLayout.StageY.BindValueChanged(_ => queuePlayfieldSave());
        }

        /// <summary>
        /// Points this store at <c>EzResources/Config/{layoutSetName}</c> and reloads subscribers.
        /// </summary>
        public void UseSet(string layoutSetName)
        {
            if (IsClientRoot)
                return;

            if (LayoutSetName == layoutSetName)
                return;

            LayoutSetName = layoutSetName;
            DiscardPending();
            bind(layoutSetName);
            reloadPlayfield();
            SetChanged?.Invoke();
        }

        public SkinLayoutInfo Load(string layoutFile)
        {
            if (pendingLayouts.TryGetValue(layoutFile, out var pending))
                return cloneLayout(pending);

            string? json = readText(layoutFile);

            if (string.IsNullOrEmpty(json))
                return new SkinLayoutInfo();

            json = EzLegacySkinFolders.StripUtf8Bom(json);

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

        /// <summary>
        /// Keeps the live container in the session cache. Does not write JSON until <see cref="FlushPending"/>.
        /// </summary>
        public void Capture(EzLayoutContainer container) =>
            Capture(container.LayoutFile, container.Ruleset, ((ISerialisableDrawableContainer)container).CreateSerialisedInfo().ToArray());

        public void Capture(string layoutFile, RulesetInfo? ruleset, SerialisedDrawableInfo[] components)
        {
            var layout = Load(layoutFile);
            layout.Update(ruleset, components);
            pendingLayouts[layoutFile] = layout;
        }

        /// <summary>
        /// Whether any captured layout serialises differently from the on-disk JSON.
        /// </summary>
        public bool HasUnflushedChanges()
        {
            foreach ((string file, SkinLayoutInfo layout) in pendingLayouts)
            {
                string json = JsonConvert.SerializeObject(layout, json_settings);

                if (readText(file) != json)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Writes captured layouts that differ from disk. Returns whether any file changed.
        /// </summary>
        public bool FlushPending(string? layoutFile = null)
        {
            bool written = false;
            string[] files = layoutFile == null
                ? pendingLayouts.Keys.ToArray()
                : pendingLayouts.ContainsKey(layoutFile)
                    ? new[] { layoutFile }
                    : Array.Empty<string>();

            foreach (string file in files)
            {
                if (!pendingLayouts.TryGetValue(file, out var layout))
                    continue;

                string json = JsonConvert.SerializeObject(layout, json_settings);

                if (readText(file) != json)
                {
                    writeText(file, json);
                    written = true;
                }

                pendingLayouts.Remove(file);
            }

            return written;
        }

        public void DiscardPending(string? layoutFile = null)
        {
            if (layoutFile == null)
                pendingLayouts.Clear();
            else
                pendingLayouts.Remove(layoutFile);
        }

        public void Save(string layoutFile, SkinLayoutInfo layout)
        {
            pendingLayouts.Remove(layoutFile);
            writeText(layoutFile, JsonConvert.SerializeObject(layout, json_settings));
        }

        public void Reset(string layoutFile)
        {
            pendingLayouts.Remove(layoutFile);

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

        private static SkinLayoutInfo cloneLayout(SkinLayoutInfo layout)
        {
            string json = JsonConvert.SerializeObject(layout, json_settings);
            return JsonConvert.DeserializeObject<SkinLayoutInfo>(json, json_settings) ?? new SkinLayoutInfo();
        }

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
            if (readText(filename) == contents)
                return;

            using (var stream = storage.CreateFileSafely(filename))
            using (var writer = new StreamWriter(stream, EzLegacySkinFolders.UTF8_NO_BOM))
                writer.Write(contents);
        }
    }
}
