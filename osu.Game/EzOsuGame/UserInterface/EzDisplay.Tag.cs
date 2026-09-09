// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Skills;
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
    /// 难度卡底部标签行：左侧可选技能段位，其后为用户标签 / 视频 / 故事版。
    /// Video/Storyboard 标记由 <see cref="BeatmapUpdater"/> 写入 <see cref="BeatmapInfo"/>，此处只读 Realm 字段。
    /// </summary>
    public partial class EzDisplayTag : CompositeDrawable
    {
        private const int max_visible_tags = 10;
        private const float tag_corner_radius = 3;

        private readonly FillFlowContainer rootFlow;
        private readonly FillFlowContainer tagFlow;
        private readonly EzDisplaySkillsDan skillsDan;

        private ScheduledDelegate? scheduledTagUpdate;
        private int chartDanRequestId;

        private bool altHighlightActive;

        private BeatmapInfo? beatmap;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap != null && beatmap.Equals(value))
                    return;

                beatmap = value;
                chartDanRequestId++;
                scheduleTagUpdate();
            }
        }

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved(canBeNull: true)]
        private EzSkillProvider? skillProvider { get; set; }

        public EzDisplayTag()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            skillsDan = new EzDisplaySkillsDan
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                PreferDanImage = true,
                Alpha = 0,
                BypassAutoSizeAxes = Axes.Both,
            };

            tagFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(3, 0),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };

            // skillsDan is always the leftmost child; visibility via Alpha / BypassAutoSizeAxes.
            InternalChild = rootFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
                Children = new Drawable[]
                {
                    skillsDan,
                    tagFlow,
                },
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
            EzDisplayTagAltHighlight.ActiveChanged += onAltHighlightChanged;
            onAltHighlightChanged(EzDisplayTagAltHighlight.Active);
        }

        private void onAltHighlightChanged(bool active)
        {
            if (altHighlightActive == active)
                return;

            altHighlightActive = active;

            foreach (var tag in tagFlow.Children)
            {
                if (tag is SimpleTag simpleTag)
                    simpleTag.SetAltHighlight(altHighlightActive);
                else if (tag is IconTag iconTag)
                    iconTag.SetAltHighlight(altHighlightActive);
            }
        }

        private void updateTags()
        {
            tagFlow.Clear();

            if (beatmap == null)
            {
                hideSkillsDan();
                return;
            }

            var cached = skillProvider?.TryGetCachedChartDan(beatmap);

            if (cached != null)
            {
                showSkillsDan(cached);
            }
            else if (skillProvider != null)
            {
                hideSkillsDan();
                requestChartDanCompute(beatmap);
            }
            else
            {
                hideSkillsDan();
            }

            var userTags = beatmap.Metadata.UserTags.Take(max_visible_tags);

            if (beatmap.HasVideo == true)
            {
                tagFlow.Add(new IconTag(FontAwesome.Solid.Film, BeatmapsetsStrings.ShowInfoVideo)
                {
                    Action = () => songSelect?.AddToSearch(@"video:true"),
                });
            }

            if (beatmap.HasStoryboard == true)
            {
                tagFlow.Add(new IconTag(FontAwesome.Solid.Image, BeatmapsetsStrings.ShowInfoStoryboard)
                {
                    Action = () => songSelect?.AddToSearch(@"storyboard:true"),
                });
            }

            foreach (string tag in userTags)
            {
                var simpleTag = new SimpleTag(tag)
                {
                    Action = () => songSelect?.AddToSearch($@"tag=""{tag}""!"),
                };
                tagFlow.Add(simpleTag);
                simpleTag.SetAltHighlight(altHighlightActive);
            }
        }

        private void showSkillsDan(EzChartDanVerdict verdict)
        {
            skillsDan.BypassAutoSizeAxes = Axes.None;
            skillsDan.SetFrom(verdict);
            skillsDan.Show();
        }

        private void hideSkillsDan()
        {
            skillsDan.Clear();
            skillsDan.Hide();
            skillsDan.BypassAutoSizeAxes = Axes.Both;
        }

        private void requestChartDanCompute(BeatmapInfo target)
        {
            int requestId = ++chartDanRequestId;
            var provider = skillProvider;
            // Detach so MinaCalc can run off the update thread without touching live Realm.
            var detached = target.Detach();

            Task.Run(() =>
            {
                try
                {
                    return provider?.TryGetChartDan(detached);
                }
                catch
                {
                    return null;
                }
            }).ContinueWith(t => Schedule(() =>
            {
                if (requestId != chartDanRequestId || beatmap == null || !string.Equals(beatmap.Hash, detached.Hash, StringComparison.Ordinal))
                    return;

                if (t.Status == TaskStatus.RanToCompletion && t.GetResultSafely() is EzChartDanVerdict verdict)
                    showSkillsDan(verdict);
                else
                    hideSkillsDan();
            }));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                EzDisplayTagAltHighlight.ActiveChanged -= onAltHighlightChanged;
                scheduledTagUpdate?.Cancel();
                scheduledTagUpdate = null;
                chartDanRequestId++;
                beatmap = null;
            }
        }

        private partial class IconTag : CompositeDrawable, IHasTooltip
        {
            public Action? Action { get; set; }

            private readonly IconUsage icon;
            public LocalisableString TooltipText { get; }

            private OverlayColourProvider colourProvider = null!;
            private Box background = null!;
            private SpriteIcon spriteIcon = null!;
            private bool altHighlightActive;

            public IconTag(IconUsage icon, LocalisableString tooltipText)
            {
                this.icon = icon;
                TooltipText = tooltipText;

                AutoSizeAxes = Axes.Both;
                CornerRadius = tag_corner_radius;
                Masking = true;
            }

            public void SetAltHighlight(bool active)
            {
                if (altHighlightActive == active)
                    return;

                altHighlightActive = active;
                refreshColours();
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!e.CurrentState.Keyboard.Keys.IsPressed(Key.LAlt))
                    return false;

                Action?.Invoke();
                return true;
            }

            protected override bool OnHover(HoverEvent e) => altHighlightActive && base.OnHover(e);

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!altHighlightActive)
                    refreshColours();

                base.OnHoverLost(e);
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                this.colourProvider = colourProvider;

                InternalChild = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background3,
                        },
                        spriteIcon = new SpriteIcon
                        {
                            Icon = icon,
                            Size = new Vector2(10),
                            Colour = colourProvider.Content2,
                            Margin = new MarginPadding { Horizontal = 2, Vertical = 1 },
                        }
                    }
                };
            }

            private void refreshColours()
            {
                if (!IsLoaded)
                    return;

                background.FadeColour(altHighlightActive ? colourProvider.Highlight1 : colourProvider.Background3, 200, Easing.OutQuint);
                spriteIcon.FadeColour(altHighlightActive ? colourProvider.Content1 : colourProvider.Content2, 200, Easing.OutQuint);
            }
        }

        private partial class SimpleTag : OsuClickableContainer
        {
            private readonly string tagText;

            private OverlayColourProvider colourProvider = null!;
            private Box background = null!;
            private OsuSpriteText label = null!;
            private bool altHighlightActive;

            public SimpleTag(string text)
            {
                tagText = text;
                AutoSizeAxes = Axes.Both;
            }

            public void SetAltHighlight(bool active)
            {
                if (altHighlightActive == active)
                    return;

                altHighlightActive = active;
                refreshColours();
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!e.CurrentState.Keyboard.Keys.IsPressed(Key.LAlt))
                    return false;

                return base.OnClick(e);
            }

            protected override bool OnHover(HoverEvent e) => altHighlightActive && base.OnHover(e);

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!altHighlightActive)
                    refreshColours();

                base.OnHoverLost(e);
            }

            private void refreshColours()
            {
                if (!IsLoaded)
                    return;

                background.FadeColour(altHighlightActive ? colourProvider.Highlight1 : colourProvider.Background3, 200, Easing.OutQuint);
                label.FadeColour(altHighlightActive ? colourProvider.Content1 : colourProvider.Content2, 200, Easing.OutQuint);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                refreshColours();
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
