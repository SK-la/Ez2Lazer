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
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Screens;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// A skinnable sprite that always loads from EzResources/Modify via <see cref="EzResourceStore"/>.
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

        [SettingSource("Use Beatmap BPM")]
        public Bindable<bool> UseBeatmapBPM { get; } = new Bindable<bool>(false);

        [SettingSource("Beat Division (1/n)")]
        public BindableNumber<int> BeatDivision { get; } = new BindableNumber<int>(4)
        {
            MinValue = 1,
            MaxValue = 32,
            Precision = 1
        };

        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private EzResourceStore resource { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IBindable<IReadOnlyList<Mod>>? mods { get; set; }

        // Only available while playing, which is where the chart's timing points can be read from. The HUD itself is
        // clocked in real time, so the song time has to be obtained via the frame-stable clock as well.
        [Resolved(canBeNull: true)]
        private GameplayState? gameplayState { get; set; }

        [Resolved(canBeNull: true)]
        private IFrameStableClock? frameStableClock { get; set; }

        private Drawable? currentDrawable;
        private TextureAnimation? currentAnimation;

        // Frames are baked once at this length; every playback speed influence is applied on top of it.
        private const double base_frame_length = 1000.0 / 60.0;

        // Beat length (ms per beat) of the selected beatmap, and the rate multiplier of the selected mods.
        private double beatLength;
        private float modRate = 1;

        // A dynamic speed mod changes the rate while playing, so it reports the live value itself.
        private ILinkedDynamicSpeedHUD? dynamicSpeedMod;

        private ModSettingChangeTracker? modSettingTracker;

        // Accumulated playback position, in animation time.
        private double playbackTime;

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

            // Beat-synced playback follows the selected beatmap and the selected mods, so both are tracked directly
            // (no extra copies) and only reflected in the private beat length / rate values.
            beatmap.BindValueChanged(_ => updateBeatLength(), true);

            mods?.BindValueChanged(selectedMods =>
            {
                updateModSettingTracker(selectedMods.NewValue);
                updateModRate();
            }, true);

            dynamicSpeedMod = gameplayState?.Mods.OfType<ILinkedDynamicSpeedHUD>().FirstOrDefault();

            TextureScale.BindValueChanged(_ => applyVisualSettings(), true);
            AccentColour.BindValueChanged(_ => applyVisualSettings(), true);
            Blend.BindValueChanged(_ => applyVisualSettings(), true);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            scheduleReload();
        }

        protected override void Update()
        {
            base.Update();

            if (currentAnimation == null)
                return;

            double duration = currentAnimation.Duration;

            if (duration <= 0)
                return;

            // Playback is driven here instead of by the framework's own advance (see createAnimatedDrawable), so
            // every speed influence only has to end up in getPlaybackSpeed().
            playbackTime = Math.Max(0, playbackTime + Clock.ElapsedFrameTime * getPlaybackSpeed());
            currentAnimation.PlaybackPosition = playbackTime % duration;
        }

        // 1 means one reference frame per base frame length; FPS, beat division and mod rate all feed into it.
        private double getPlaybackSpeed()
        {
            if (UseBeatmapBPM.Value)
            {
                double currentBeatLength = getCurrentBeatLength();

                if (currentBeatLength > 0)
                    return base_frame_length * BeatDivision.Value * getRate() / currentBeatLength;
            }

            return FPS.Value * base_frame_length / 1000;
        }

        private double getCurrentBeatLength()
        {
            // While playing, follow the chart's timing points so the animation tracks every BPM section.
            if (gameplayState != null && frameStableClock != null)
            {
                double liveBeatLength = gameplayState.Beatmap.ControlPointInfo.TimingPointAt(frameStableClock.CurrentTime).BeatLength;

                if (liveBeatLength > 0)
                    return liveBeatLength;
            }

            return beatLength;
        }

        private float getRate()
        {
            ILinkedDynamicSpeedHUD? liveMod = dynamicSpeedMod;
            return liveMod != null ? (float)liveMod.SpeedChange.Value : modRate;
        }

        private void scheduleReload() => Schedule(reloadDrawable);

        private void reloadDrawable()
        {
            string spriteName = SpriteName.Value?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(spriteName))
            {
                ClearInternal();
                currentDrawable = null;
                currentAnimation = null;
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
            currentAnimation = newDrawable as TextureAnimation;
            playbackTime = 0;

            AddInternal(newDrawable);
            applyVisualSettings();
        }

        private void updateBeatLength()
        {
            double bpm = beatmap.Value.BeatmapInfo.BPM;
            beatLength = bpm > 0 ? 60000 / bpm : 0;
        }

        private void updateModRate() => modRate = EzModRate.Resolve(mods?.Value);

        // Only rate-changing mods can affect playback speed, so nothing else needs to be watched.
        private void updateModSettingTracker(IReadOnlyList<Mod> selectedMods)
        {
            modSettingTracker?.Dispose();
            modSettingTracker = new ModSettingChangeTracker(selectedMods.Where(mod => mod is IApplicableToRate));
            modSettingTracker.SettingChanged += _ => updateModRate();
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
                DefaultFrameLength = base_frame_length,

                // Playback speed is driven by the owning drawable instead, so that it can change at any time
                // without having to clear and re-add every frame.
                IsPlaying = false,
            };

            for (int i = 0; i < max_animation_frames; i++)
            {
                int frameIndex = start + i;
                string frameSuffix = frameIndex.ToString($"D{width}");
                Texture? texture = resource.Get($"{baseLookup}{frameSuffix}", EzTextureUsage.AnimationSafe);
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

            Texture? texture = resource.Get(lookup, EzTextureUsage.AnimationSafe)
                               ?? resource.Get(baseLookup, EzTextureUsage.AnimationSafe);
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

        /// <summary>
        /// Ensures <paramref name="current"/> is present in <paramref name="items"/> before assigning to a Dropdown,
        /// so <c>ensureItemSelectionIsValid</c> cannot rewrite the bound setting on settings-panel rebuild.
        /// </summary>
        private static void ensureCurrentInItems(Bindable<string> current, List<string> items, bool normaliseAsPath)
        {
            string value = normaliseAsPath
                ? normaliseModifyPath(current.Value)
                : (current.Value?.Trim() ?? string.Empty);

            int existing = items.FindIndex(i => string.Equals(i, value, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
            {
                if (current.Value != items[existing])
                    current.Value = items[existing];
                return;
            }

            if (value.Length > 0)
            {
                items.Insert(0, value);
                if (current.Value != value)
                    current.Value = value;
                return;
            }

            if (items.Count == 0)
                items.Add(string.Empty);

            current.Value = items[0];
        }

        public partial class ModifyPathSelectorControl : SettingsDropdown<string>
        {
            [Resolved]
            private Storage storage { get; set; } = null!;

            private EzHUDSpritePlus source = null!;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                source = (EzHUDSpritePlus)SettingSourceObject;
                refreshItems();
            }

            private void refreshItems()
            {
                var list = new List<string>();

                try
                {
                    string modifyRoot = storage.GetFullPath("EzResources/Modify");

                    if (Directory.Exists(modifyRoot))
                    {
                        list.AddRange(Directory.GetDirectories(modifyRoot, "*", SearchOption.AllDirectories)
                                               .Select(path => Path.GetRelativePath(modifyRoot, path))
                                               .Select(normaliseModifyPath)
                                               .Where(path => path.Length > 0)
                                               .Distinct(StringComparer.OrdinalIgnoreCase)
                                               .OrderBy(path => path.Count(c => c == '/'))
                                               .ThenBy(path => path, StringComparer.OrdinalIgnoreCase));
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                ensureCurrentInItems(source.ModifyPath, list, normaliseAsPath: true);
                Items = list;
            }
        }

        public partial class ModifySpriteSelectorControl : SettingsDropdown<string>
        {
            [Resolved]
            private Storage storage { get; set; } = null!;

            private EzHUDSpritePlus source = null!;
            private static readonly Regex selector_frame_template_regex = new Regex(@"^\{(0{1,3})\}$", RegexOptions.Compiled);

            protected override void LoadComplete()
            {
                base.LoadComplete();

                source = (EzHUDSpritePlus)SettingSourceObject;
                refreshItems();
                source.ModifyPath.BindValueChanged(_ => refreshItems());
                source.FrameTemplate.BindValueChanged(_ => refreshItems());
            }

            private void refreshItems()
            {
                var list = new List<string>();

                try
                {
                    string path = source.ModifyPath.Value?.Trim() ?? string.Empty;
                    string fullDir = storage.GetFullPath(buildModifyDirectory(path));

                    if (Directory.Exists(fullDir))
                    {
                        string[] rawFileNames = Directory.GetFiles(fullDir)
                                                         .Where(file => SupportedExtensions.IMAGE_EXTENSIONS.Contains(Path.GetExtension(file).ToLowerInvariant()))
                                                         .Select(file => Path.GetFileNameWithoutExtension(file))
                                                         .Where(name => !string.IsNullOrEmpty(name))
                                                         .ToArray();

                        var rawFileNameSet = new HashSet<string>(rawFileNames, StringComparer.OrdinalIgnoreCase);

                        list.AddRange(rawFileNames.Select(name => resolveDisplayName(name, rawFileNameSet))
                                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                                  .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                ensureCurrentInItems(source.SpriteName, list, normaliseAsPath: false);
                Items = list;
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
