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
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Storyboards;
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

        private ScheduledDelegate? scheduledTagUpdate;

        private WorkingBeatmap? working;

        public WorkingBeatmap? Working
        {
            get => working;
            set
            {
                if (working == value)
                    return;

                working = value;

                // 当外部传入 WorkingBeatmap 时触发订阅更新（尽量快速生效）
                scheduledTagUpdate?.Cancel();
                scheduledTagUpdate = Scheduler.AddDelayed(() =>
                {
                    if (!IsAlive) return;

                    updateSubscription();
                    scheduledTagUpdate = null;
                }, 0);
            }
        }

        private BeatmapInfo? beatmap;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap == null && value == null)
                    return;

                beatmap = value;

                // 取消上一次的防抖计划（若有），并在短延迟后执行订阅更新以减少滚动时的开销
                scheduledTagUpdate?.Cancel();
                scheduledTagUpdate = null;

                if (IsLoaded)
                {
                    scheduledTagUpdate = Scheduler.AddDelayed(() =>
                    {
                        updateSubscription();
                        scheduledTagUpdate = null;
                    }, 50);
                }
            }
        }

        // 直读故事版基本也不会有显著的内存问题。尽管AI在内存分析问题中，总是乱猜测说这样可能存在问题。
        private bool useSbFallBack => true;

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
            if (beatmap == null && working == null)
                return;

            updateTags();
        }

        private void updateTags()
        {
            tagFlow.Clear();

            beatmap ??= working?.BeatmapInfo;

            if (beatmap == null)
                return;

            // 先准备数据，统一全部释放，之后更新UI.
            var userTags = beatmap.Metadata.UserTags.Take(max_visible_tags);
            var tagInfo = getTagInfo(beatmap);

            if (tagInfo.HasVideo)
                tagFlow.Add(new IconTag(FontAwesome.Solid.Film, BeatmapsetsStrings.ShowInfoVideo));

            if (tagInfo.HasStoryboard)
                tagFlow.Add(new IconTag(FontAwesome.Solid.Image, BeatmapsetsStrings.ShowInfoStoryboard));

            foreach (string tag in userTags)
            {
                tagFlow.Add(new SimpleTag(tag)
                {
                    Action = () => songSelect?.Search($@"tag=""{tag}""!"),
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                // 取消防抖计划，避免延迟任务在组件已卸载时执行
                scheduledTagUpdate?.Cancel();
                scheduledTagUpdate = null;

                beatmap = null;
                working = null;

                lock (tag_info_cache_lock)
                {
                    tag_info_cache.Clear();
                }
            }
        }

        // TODO：故事版和背景识别不出来
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
            bool hasVideo = false;
            bool hasStoryboard = false;

            // 优先使用解析后轻量的数据：
            // 1) `WorkingBeatmap.Beatmap.UnhandledEventLines`（内存负担最小）
            // 2) `WorkingBeatmap.Storyboard`（作为回退）
            try
            {
                // 强制 refetch 以提升在 detached 模型下拿到完整数据的概率。
                working ??= beatmaps.GetWorkingBeatmap(beatmapInfo);

                // 先检查已解析的 beatmap 的 UnhandledEventLines（优先）
                try
                {
                    var bm = working?.Beatmap;

                    if (bm?.UnhandledEventLines != null)
                    {
                        foreach (string raw in bm.UnhandledEventLines)
                        {
                            if (string.IsNullOrWhiteSpace(raw))
                                continue;

                            string trimmed = raw.Trim();

                            if (trimmed.StartsWith("//", StringComparison.Ordinal))
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
                catch (Exception e)
                {
                    Logger.Error(e, $"Beatmap UnhandledEventLines check failed for beatmap {beatmapInfo}");
                }

                // 若仍未确定，则回退使用 Storyboard（可能有内存开销，作为最后手段）
                if (useSbFallBack && !(hasVideo && hasStoryboard))
                {
                    try
                    {
                        var sb = working?.Storyboard;

                        if (sb != null)
                        {
                            var elements = sb.Layers.SelectMany(l => l.Elements);

                            if (!hasVideo)
                                hasVideo = elements.Any(e => e is StoryboardVideo);

                            if (!hasStoryboard)
                                hasStoryboard = elements.Any(e => e is StoryboardAnimation || (e is StoryboardSprite && !(e is StoryboardVideo)));
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Storyboard check failed for beatmap {beatmapInfo}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Tag detection failed for beatmap {beatmapInfo}");
            }
            finally
            {
                working = null;
            }

            return new CachedTagInfo(beatmapInfo.Hash, hasVideo, hasStoryboard);
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
