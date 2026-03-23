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
using osu.Game.Beatmaps;
using osu.Game.Graphics;
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

        private Storyboard? storyboard;
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
                storyboard = null;
                tagFlow.Clear();
                return;
            }

            storyboard = beatmap != null ? beatmaps.GetWorkingBeatmap(beatmap).Storyboard : null;
            updateTags();
        }

        private void updateTags()
        {
            tagFlow.Clear();

            if (beatmap?.BeatmapSet == null || storyboard == null)
                return;

            bool hasVideo = storyboard.GetLayer("Video").Elements.Any(e => e is StoryboardVideo);

            if (hasVideo)
                tagFlow.Add(new IconTag(FontAwesome.Solid.Film, BeatmapsetsStrings.ShowInfoVideo));

            bool hasStoryboard = storyboard.Layers
                                           .SelectMany(l => l.Elements)
                                           .Any(e => e is StoryboardSprite && e is not StoryboardVideo);

            if (hasStoryboard)
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
            {
                beatmap = null;
                storyboard = null;
            }

            base.Dispose(isDisposing);
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
