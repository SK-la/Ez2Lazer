// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Screens;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// A skinnable sprite that always loads from EzResources/Modify via <see cref="EzResourceProvider"/>.
    /// Supports both single-image and frame animation loading.
    /// </summary>
    public partial class EzHUDSpritePlus : CompositeDrawable, ISerialisableDrawable
    {
        private const string modify_root = "Modify";
        private const int max_animation_frames = 240;

        private static readonly Regex frame_template_regex = new Regex(@"^\{(0{1,3})\}$", RegexOptions.Compiled);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SPRITE_PLUS_PATH_LABEL), nameof(EzHUDStrings.SPRITE_PLUS_PATH_DESCRIPTION), SettingControlType = typeof(ModifyPathSelectorControl))]
        public Bindable<string> ModifyPath { get; } = new Bindable<string>("Tachie");

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.SpriteName), SettingControlType = typeof(ModifySpriteSelectorControl))]
        public Bindable<string> SpriteName { get; } = new Bindable<string>(string.Empty);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SPRITE_PLUS_FRAME_TEMPLATE_LABEL), nameof(EzHUDStrings.SPRITE_PLUS_FRAME_TEMPLATE_DESCRIPTION), SettingControlType = typeof(SettingsTextBox))]
        public Bindable<string> FrameTemplate { get; } = new Bindable<string>("{0}");

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.PLAYBACK_FPS_LABEL), nameof(EzHUDStrings.PLAYBACK_FPS_DESCRIPTION))]
        public BindableNumber<float> FPS { get; } = new BindableNumber<float>(60)
        {
            MinValue = 1,
            MaxValue = 240,
            Precision = 1f
        };

        [SettingSource("Scale")]
        public BindableNumber<float> TextureScale { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0.01f,
            MaxValue = 10f,
            Precision = 0.01f
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour), SettingControlType = typeof(EzSettingsColour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.HITRESULT_BLENDING_LABEL), nameof(EzHUDStrings.HITRESULT_BLENDING_DESCRIPTION))]
        public Bindable<BlendMode> Blend { get; } = new Bindable<BlendMode>(BlendMode.Mixture);

        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private EzResourceProvider resourceProvider { get; set; } = null!;

        private Drawable? currentDrawable;

        public EzHUDSpritePlus()
        {
            RelativeSizeAxes = Axes.None;
            AutoSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ModifyPath.BindValueChanged(_ => scheduleReload());
            SpriteName.BindValueChanged(_ => scheduleReload());
            FrameTemplate.BindValueChanged(_ => scheduleReload());
            FPS.BindValueChanged(_ => scheduleReload());
            TextureScale.BindValueChanged(_ => applyVisualSettings(), true);
            AccentColour.BindValueChanged(_ => applyVisualSettings(), true);
            Blend.BindValueChanged(_ => applyVisualSettings(), true);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            scheduleReload();
        }

        private void scheduleReload() => Schedule(reloadDrawable);

        private void reloadDrawable()
        {
            string spriteName = SpriteName.Value?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(spriteName))
            {
                ClearInternal();
                currentDrawable = null;
                return;
            }

            string baseLookup = buildBaseLookup(spriteName);
            Drawable? newDrawable = createAnimatedDrawable(baseLookup) ?? createSingleDrawable(baseLookup);

            // Keep the current drawable if a transient settings state cannot resolve a texture.
            // This avoids flickering/reset when dropdowns are rebuilding their item sources.
            if (newDrawable == null)
                return;

            ClearInternal();
            currentDrawable = newDrawable;
            AddInternal(newDrawable);
            applyVisualSettings();
        }

        private Drawable? createAnimatedDrawable(string baseLookup)
        {
            string template = FrameTemplate.Value?.Trim() ?? string.Empty;
            if (!tryParseAnimationTemplate(template, out int start, out int width))
                return null;

            var animation = new TextureAnimation
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Loop = true,
                DefaultFrameLength = 1000f / Math.Clamp(FPS.Value, 1f, 240f),
            };

            for (int i = 0; i < max_animation_frames; i++)
            {
                int frameIndex = start + i;
                string frameSuffix = frameIndex.ToString($"D{width}");
                Texture? texture = resourceProvider.Get($"{baseLookup}{frameSuffix}");
                if (texture == null)
                    break;

                animation.AddFrame(texture);
            }

            return animation.FrameCount > 0 ? animation : null;
        }

        private Drawable? createSingleDrawable(string baseLookup)
        {
            string template = FrameTemplate.Value?.Trim() ?? string.Empty;
            string lookup = baseLookup;

            if (!string.IsNullOrEmpty(template) && !template.Contains('{') && !template.Contains('}'))
                lookup += template;

            Texture? texture = resourceProvider.Get(lookup) ?? resourceProvider.Get(baseLookup);
            if (texture == null)
                return null;

            return new Sprite
            {
                Texture = texture,
            };
        }

        private void applyVisualSettings()
        {
            if (currentDrawable != null)
            {
                currentDrawable.Scale = new Vector2(TextureScale.Value);
                currentDrawable.Colour = AccentColour.Value;
                currentDrawable.Blending = getBlendingParameters(Blend.Value);
            }
        }

        private static BlendingParameters getBlendingParameters(BlendMode mode)
        {
            return mode switch
            {
                BlendMode.Inherit => BlendingParameters.Inherit,
                BlendMode.Mixture => BlendingParameters.Mixture,
                BlendMode.Additive => BlendingParameters.Additive,
                BlendMode.None => BlendingParameters.None,
                _ => BlendingParameters.Mixture,
            };
        }

        public enum BlendMode
        {
            Inherit,
            Mixture,
            Additive,
            None
        }

        private string buildBaseLookup(string spriteName)
        {
            string path = normaliseModifyPath(ModifyPath.Value);
            return string.IsNullOrEmpty(path) ? $"{modify_root}/{spriteName}" : $"{modify_root}/{path}/{spriteName}";
        }

        private static bool tryParseAnimationTemplate(string template, out int start, out int width)
        {
            start = 0;
            width = 1;

            Match match = frame_template_regex.Match(template);
            if (!match.Success)
                return false;

            string digits = match.Groups[1].Value;
            if (digits.Length == 0 || digits.Length > 3)
                return false;

            start = 0;
            width = digits.Length;
            return true;
        }

        private static string normaliseModifyPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.Trim()
                       .Replace('\\', '/')
                       .Trim('/');
        }

        public partial class ModifyPathSelectorControl : SettingsDropdown<string>
        {
            [Resolved]
            private Storage storage { get; set; } = null!;

            private EzHUDSpritePlus source = null!;
            private readonly BindableList<string> itemSource = new BindableList<string>();

            protected override void LoadComplete()
            {
                base.LoadComplete();

                source = (EzHUDSpritePlus)SettingSourceObject;
                ItemSource = itemSource;
                refreshItems();
            }

            private void refreshItems()
            {
                string modifyRoot = storage.GetFullPath("EzResources/Modify");

                IEnumerable<string> items = Enumerable.Empty<string>();

                if (Directory.Exists(modifyRoot))
                {
                    items = Directory.GetDirectories(modifyRoot, "*", SearchOption.AllDirectories)
                                     .Select(path => Path.GetRelativePath(modifyRoot, path))
                                     .Where(path => !string.IsNullOrWhiteSpace(path))
                                     .Select(path => path.Replace('/', '\\').Trim('\\'))
                                     .Where(path => !string.IsNullOrWhiteSpace(path))
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .OrderBy(path => path.Count(c => c == '\\'))
                                     .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);
                }

                string[] itemArray = items.ToArray();

                if (!itemSource.SequenceEqual(itemArray))
                {
                    itemSource.Clear();
                    itemSource.AddRange(itemArray);
                }

                if (itemArray.Length == 0)
                {
                    source.ModifyPath.Value = string.Empty;
                    return;
                }

                if (itemArray.All(i => !string.Equals(i, source.ModifyPath.Value, StringComparison.OrdinalIgnoreCase)))
                    source.ModifyPath.Value = itemArray[0];
            }
        }

        public partial class ModifySpriteSelectorControl : SettingsDropdown<string>
        {
            [Resolved]
            private Storage storage { get; set; } = null!;

            private EzHUDSpritePlus source = null!;
            private readonly BindableList<string> itemSource = new BindableList<string>();
            private static readonly Regex selector_frame_template_regex = new Regex(@"^\{(0{1,3})\}$", RegexOptions.Compiled);

            protected override void LoadComplete()
            {
                base.LoadComplete();

                source = (EzHUDSpritePlus)SettingSourceObject;
                ItemSource = itemSource;
                source.ModifyPath.BindValueChanged(_ => refreshItems(), true);
                source.FrameTemplate.BindValueChanged(_ => refreshItems());
            }

            private void refreshItems()
            {
                string path = source.ModifyPath.Value?.Trim() ?? string.Empty;
                string fullDir = storage.GetFullPath(buildModifyDirectory(path));

                IEnumerable<string> items = Enumerable.Empty<string>();

                if (Directory.Exists(fullDir))
                {
                    string[] rawFileNames = Directory.GetFiles(fullDir)
                                                     .Where(file => SupportedExtensions.IMAGE_EXTENSIONS.Contains(Path.GetExtension(file).ToLowerInvariant()))
                                                     .Select(file => Path.GetFileNameWithoutExtension(file))
                                                     .Where(name => !string.IsNullOrEmpty(name))
                                                     .ToArray();

                    var rawFileNameSet = new HashSet<string>(rawFileNames, StringComparer.OrdinalIgnoreCase);

                    items = rawFileNames.Select(name => resolveDisplayName(name, rawFileNameSet))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
                }

                string[] itemArray = items.ToArray();

                if (!itemSource.SequenceEqual(itemArray))
                {
                    itemSource.Clear();
                    itemSource.AddRange(itemArray);
                }

                if (itemArray.Length == 0)
                {
                    source.SpriteName.Value = string.Empty;
                    return;
                }

                if (itemArray.All(i => !string.Equals(i, source.SpriteName.Value, StringComparison.OrdinalIgnoreCase)))
                    source.SpriteName.Value = itemArray[0];
            }

            private static string buildModifyDirectory(string relativePath)
            {
                string normalised = normaliseModifyPath(relativePath);
                return string.IsNullOrEmpty(normalised)
                    ? "EzResources/Modify"
                    : $"EzResources/Modify/{normalised}";
            }

            private string resolveDisplayName(string fileNameWithoutExtension, HashSet<string> allNames)
            {
                string template = source.FrameTemplate.Value?.Trim() ?? string.Empty;
                if (!tryParseSelectorAnimationTemplate(template, out int start, out int width))
                    return fileNameWithoutExtension;

                if (fileNameWithoutExtension.Length < width)
                    return fileNameWithoutExtension;

                string suffix = fileNameWithoutExtension[^width..];
                if (!suffix.All(char.IsDigit))
                    return fileNameWithoutExtension;

                if (!int.TryParse(suffix, out int frame) || frame < start || frame > 239)
                    return fileNameWithoutExtension;

                string baseName = fileNameWithoutExtension[..^width];
                if (string.IsNullOrEmpty(baseName))
                    return fileNameWithoutExtension;

                // Avoid treating numeric IDs as animation frames.
                string previousFrame = frame > start ? $"{baseName}{(frame - 1).ToString($"D{width}")}" : string.Empty;
                string nextFrame = frame < 239 ? $"{baseName}{(frame + 1).ToString($"D{width}")}" : string.Empty;

                bool hasNeighbourFrame = (previousFrame.Length > 0 && allNames.Contains(previousFrame))
                                         || (nextFrame.Length > 0 && allNames.Contains(nextFrame));

                return hasNeighbourFrame ? baseName : fileNameWithoutExtension;
            }

            private static bool tryParseSelectorAnimationTemplate(string template, out int start, out int width)
            {
                start = 0;
                width = 1;

                Match match = selector_frame_template_regex.Match(template);
                if (!match.Success)
                    return false;

                string digits = match.Groups[1].Value;
                if (digits.Length == 0 || digits.Length > 3)
                    return false;

                start = 0;
                width = digits.Length;
                return true;
            }
        }
    }
}
