// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 用于在难度卡底部显示标签的组件，包括用户标签、视频标签、故事版标签。
    /// Video/Storyboard 标记由 <see cref="BeatmapUpdater"/> 写入 <see cref="BeatmapInfo"/>，此处只读 Realm 字段。
    /// </summary>
    public partial class EzDisplayTag : CompositeDrawable
    {
        private const int max_visible_tags = 10;
        private const float tag_corner_radius = 3;

        private readonly FillFlowContainer tagFlow;

        private ScheduledDelegate? scheduledTagUpdate;

        private BeatmapInfo? beatmap;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap != null && beatmap.Equals(value))
                    return;

                beatmap = value;
                scheduleTagUpdate();
            }
        }

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

        private void scheduleTagUpdate()
        {
            scheduledTagUpdate?.Cancel();
            scheduledTagUpdate = null;

            if (!IsLoaded)
                return;

            scheduledTagUpdate = Scheduler.AddDelayed(() =>
            {
                updateTags();
                scheduledTagUpdate = null;
            }, beatmap != null ? 50 : 0);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            scheduleTagUpdate();
        }

        private void updateTags()
        {
            tagFlow.Clear();

            if (beatmap == null)
                return;

            var userTags = beatmap.Metadata.UserTags.Take(max_visible_tags);

            if (beatmap.HasVideo == true)
                tagFlow.Add(new IconTag(FontAwesome.Solid.Film, BeatmapsetsStrings.ShowInfoVideo));

            if (beatmap.HasStoryboard == true)
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
                scheduledTagUpdate?.Cancel();
                scheduledTagUpdate = null;
                beatmap = null;
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

            private OverlayColourProvider colourProvider = null!;
            private Box background = null!;
            private OsuSpriteText label = null!;

            public SimpleTag(string text)
            {
                tagText = text;
                AutoSizeAxes = Axes.Both;
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!e.CurrentState.Keyboard.Keys.IsPressed(Key.LAlt))
                    return false;

                return base.OnClick(e);
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!e.CurrentState.Keyboard.Keys.IsPressed(Key.LAlt))
                    return false;

                background.FadeColour(colourProvider.Highlight1, 200, Easing.OutQuint);
                label.FadeColour(colourProvider.Content1, 200, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(colourProvider.Background3, 200, Easing.OutQuint);
                label.FadeColour(colourProvider.Content2, 200, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                this.colourProvider = colourProvider;

                CornerRadius = tag_corner_radius;
                Masking = true;

                Child = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background3,
                        },
                        label = new OsuSpriteText
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
