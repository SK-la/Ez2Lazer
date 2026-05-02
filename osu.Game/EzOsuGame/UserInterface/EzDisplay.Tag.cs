// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 用于在难度卡底部显示标签的组件，包括用户标签、视频标签、故事版标签
    /// </summary>
    public partial class EzDisplayTag : CompositeDrawable
    {
        private const int max_visible_tags = 10;
        private const float tag_corner_radius = 3;

        private readonly FillFlowContainer tagFlow;

        private ScheduledDelegate? scheduledTagUpdate;
        private EzBeatmapTagSummary? tagSummary;

        public EzBeatmapTagSummary? TagSummary
        {
            get => tagSummary;
            set
            {
                if (tagSummary == value)
                    return;

                tagSummary = value;

                scheduledTagUpdate?.Cancel();
                scheduledTagUpdate = Scheduler.AddDelayed(() =>
                {
                    if (!IsAlive)
                        return;

                    updateSubscription();
                    scheduledTagUpdate = null;
                }, 0);
            }
        }

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

        public EzDisplayTag()
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
            EzBeatmapTagSummary resolvedTagSummary = tagSummary ?? getFallbackTagSummary(beatmap);

            if (resolvedTagSummary.HasVideo)
                tagFlow.Add(new IconTag(FontAwesome.Solid.Film, BeatmapsetsStrings.ShowInfoVideo));

            if (resolvedTagSummary.HasStoryboard)
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
                tagSummary = null;
            }
        }

        private EzBeatmapTagSummary getFallbackTagSummary(BeatmapInfo beatmapInfo)
        {
            WorkingBeatmap fallbackWorking = working ?? beatmaps.GetWorkingBeatmap(beatmapInfo);
            return EzBeatmapTagParser.Parse(fallbackWorking);
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
