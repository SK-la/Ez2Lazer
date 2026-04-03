// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Extensions;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// 用于在难度卡底部显示标签的组件，包括用户标签、视频标签、故事版标签
    /// </summary>
    public partial class EzTagDisplay : CompositeDrawable
    {
        private const int max_visible_tags = 10;
        private const float tag_corner_radius = 3;

        private static readonly Dictionary<Guid, CachedTagInfo> tag_info_cache = new Dictionary<Guid, CachedTagInfo>();
        private static readonly object tag_info_cache_lock = new object();

        private readonly FillFlowContainer tagFlow;

        private BeatmapInfo? beatmap;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                beatmap = value;

                if (IsLoaded)
                    updateSubscription();
            }
        }

        private bool tagDisplayEnabled => true;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        public EzTagDisplay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = tagFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(3, 0),
            };
        }

        private void updateSubscription()
        {
            if (!tagDisplayEnabled)
            {
                tagFlow.Clear();
                return;
            }

            updateTags();
        }

        private void updateTags()
        {
            tagFlow.Clear();

            if (beatmap?.BeatmapSet == null)
                return;

            var tagInfo = getTagInfo(beatmap);

            if (tagInfo.HasVideo)
                tagFlow.Add(new IconTag(FontAwesome.Solid.Film, BeatmapsetsStrings.ShowInfoVideo));

            if (tagInfo.HasStoryboard)
                tagFlow.Add(new IconTag(FontAwesome.Solid.Image, BeatmapsetsStrings.ShowInfoStoryboard));

            // 添加用户标签（不受图标数量影响）
            var userTags = beatmap.Metadata.UserTags.Take(max_visible_tags);

            foreach (string tag in userTags)
            {
                tagFlow.Add(new SimpleTag(tag)
                {
                    Action = () => songSelect?.Search($@"tag=""{tag}""!"),
                });
            }

            // 如果没有任何标签，显示提示
            // if (!tagFlow.Any())
            // {
            //     tagFlow.Add(new SimpleTag("No tags") { Alpha = 0.5f });
            // }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                beatmap = null;

            base.Dispose(isDisposing);
        }

        private CachedTagInfo getTagInfo(BeatmapInfo beatmapInfo)
        {
            lock (tag_info_cache_lock)
            {
                if (tag_info_cache.TryGetValue(beatmapInfo.ID, out var cachedTagInfo) && cachedTagInfo.BeatmapHash == beatmapInfo.Hash)
                    return cachedTagInfo;
            }

            var detectedTagInfo = detectTagInfo(beatmapInfo);

            lock (tag_info_cache_lock)
                tag_info_cache[beatmapInfo.ID] = detectedTagInfo;

            return detectedTagInfo;
        }

        private CachedTagInfo detectTagInfo(BeatmapInfo beatmapInfo)
        {
            var beatmapSet = beatmapInfo.BeatmapSet;

            if (beatmapSet == null || string.IsNullOrEmpty(beatmapInfo.Path))
                return new CachedTagInfo(beatmapInfo.Hash, false, false);

            string? beatmapFilePath = beatmapSet.GetPathForFile(beatmapInfo.Path);

            if (string.IsNullOrEmpty(beatmapFilePath))
                return new CachedTagInfo(beatmapInfo.Hash, false, false);

            bool hasVideo = false;
            bool hasStoryboard = false;

            try
            {
                var workingBeatmap = beatmaps.GetWorkingBeatmap(beatmapInfo);

                scanEventFile(workingBeatmap.GetStream(beatmapFilePath), ref hasVideo, ref hasStoryboard);

                if (!hasVideo || !hasStoryboard)
                {
                    string? storyboardFilePath = beatmapSet.GetPathForFile(getMainStoryboardFilename(beatmapInfo.Metadata));

                    if (!string.IsNullOrEmpty(storyboardFilePath))
                        scanEventFile(workingBeatmap.GetStream(storyboardFilePath), ref hasVideo, ref hasStoryboard);
                }
            }
            catch
            {
            }

            return new CachedTagInfo(beatmapInfo.Hash, hasVideo, hasStoryboard);
        }

        private static void scanEventFile(Stream stream, ref bool hasVideo, ref bool hasStoryboard)
        {
            if (stream == null)
                return;

            using (stream)
            using (var reader = new StreamReader(stream))
            {
                bool inEventsSection = false;

                while (reader.ReadLine() is string line)
                {
                    string trimmed = line.Trim();

                    if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
                    {
                        if (inEventsSection)
                            break;

                        inEventsSection = trimmed.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!inEventsSection)
                        continue;

                    int commentIndex = trimmed.IndexOf("//", StringComparison.Ordinal);

                    if (commentIndex >= 0)
                        trimmed = trimmed[..commentIndex].TrimEnd();

                    if (trimmed.Length == 0)
                        continue;

                    string eventType = trimmed.Split(',')[0].Trim();

                    if (!hasVideo && matchesEventType(eventType, LegacyEventType.Video, "Video"))
                        hasVideo = true;

                    if (!hasStoryboard && (matchesEventType(eventType, LegacyEventType.Sprite, "Sprite") || matchesEventType(eventType, LegacyEventType.Animation, "Animation")))
                        hasStoryboard = true;

                    if (hasVideo && hasStoryboard)
                        break;
                }
            }
        }

        private static string getMainStoryboardFilename(IBeatmapMetadataInfo metadata)
        {
            string baseFilename = (metadata.Artist.Length > 0 ? metadata.Artist + @" - " + metadata.Title : Path.GetFileNameWithoutExtension(metadata.AudioFile))
                                  + (metadata.Author.Username.Length > 0 ? @" (" + metadata.Author.Username + @")" : string.Empty)
                                  + @".osb";
            return baseFilename.GetValidFilename();
        }

        private static bool matchesEventType(string value, LegacyEventType eventType, string eventName)
            => value.Equals(eventName, StringComparison.OrdinalIgnoreCase) || value == ((int)eventType).ToString();

        private readonly struct CachedTagInfo
        {
            public readonly string BeatmapHash;
            public readonly bool HasVideo;
            public readonly bool HasStoryboard;

            public CachedTagInfo(string beatmapHash, bool hasVideo, bool hasStoryboard)
            {
                BeatmapHash = beatmapHash;
                HasVideo = hasVideo;
                HasStoryboard = hasStoryboard;
            }
        }

        private partial class IconTag : CompositeDrawable, IHasTooltip
        {
            private readonly IconUsage icon;
            public LocalisableString TooltipText { get; }

            public IconTag(IconUsage icon, LocalisableString tooltipText)
            {
                this.icon = icon;
                TooltipText = tooltipText;

                AutoSizeAxes = Axes.Both;
                CornerRadius = tag_corner_radius;
                Masking = true;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                InternalChild = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background3,
                        },
                        new SpriteIcon
                        {
                            Icon = icon,
                            Size = new Vector2(10),
                            Colour = colourProvider.Content2,
                            Margin = new MarginPadding { Horizontal = 2, Vertical = 1 },
                        }
                    }
                };
            }
        }

        private partial class SimpleTag : OsuClickableContainer
        {
            private readonly string tagText;

            public SimpleTag(string text)
            {
                tagText = text;
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                CornerRadius = tag_corner_radius;
                Masking = true;

                Child = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background3,
                        },
                        new OsuSpriteText
                        {
                            Text = tagText,
                            Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                            Colour = colourProvider.Content2,
                            Margin = new MarginPadding { Horizontal = 4, Vertical = 1 },
                        }
                    }
                };
            }
        }
    }
}
