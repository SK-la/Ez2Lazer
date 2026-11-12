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
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.UI;
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
    /// 难度卡底部标签行：CSI 技能标签（首位）/ 视频 / 故事版 / 用户标签。
    /// Video/Storyboard 标记由 <see cref="BeatmapUpdater"/> 写入 <see cref="BeatmapInfo"/>，此处只读 Realm 字段。
    /// 技能标签平时读 <see cref="EzSkillProvider.GetChartSkillTagsForHashes"/> 的会话缓存（一次 Realm 批量读取），
    /// 只有被选中的卡再用 <see cref="EzSkillProvider.TryGetOrComputeChartSkillInfo"/> 复算一次。
    /// </summary>
    public partial class EzDisplayTag : CompositeDrawable
    {
        private const int max_visible_tags = 10;
        private const int max_skill_tags = 3;
        private const float tag_corner_radius = EzUIStyleV1.CORNER_RADIUS_CHIP;

        private readonly FillFlowContainer tagFlow;

        private ScheduledDelegate? scheduledTagUpdate;
        private int skillTagRequestId;

        private bool altHighlightActive;
        private bool selected;

        private BeatmapInfo? beatmap;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap != null && beatmap.Equals(value))
                    return;

                beatmap = value;
                skillTagRequestId++;
                scheduleTagUpdate();
            }
        }

        /// <summary>
        /// 选歌 carousel 的当前选中。未选中的卡只读会话缓存；选中时额外做一次 live 复算。
        /// </summary>
        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value)
                    return;

                selected = value;

                if (selected)
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

            tagFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(3, 0),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
                Children = new Drawable[]
                {
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
                else if (tag is SkillChipTag skillTag)
                    skillTag.SetAltHighlight(altHighlightActive);
            }
        }

        private void updateTags()
        {
            tagFlow.Clear();

            if (beatmap == null)
                return;

            int requestId = ++skillTagRequestId;
            var target = beatmap;
            var provider = skillProvider;
            string hash = target.Hash;

            // Icons / user tags first; skill chips replace the row once the session cache answers.
            rebuildTagFlow([]);

            if (provider == null || target.Ruleset.OnlineID != 3 || string.IsNullOrEmpty(hash))
                return;

            Task.Run(() => getSessionSkillPatterns(provider, hash)).ContinueWith(t =>
            {
                string[] cachedPatterns = t.Status == TaskStatus.RanToCompletion ? t.GetResultSafely() : [];

                Schedule(() =>
                {
                    if (!isCurrent(requestId, hash))
                        return;

                    rebuildTagFlow(cachedPatterns);

                    if (selected)
                        recheckLivePatterns(provider, target, requestId, hash, cachedPatterns);
                });
            });
        }

        /// <summary>
        /// Session dictionary warmed by one Realm pass over every stored ChartSkillInfo row; repeated calls are
        /// dictionary lookups, so a browsing carousel does not issue one Realm read per card.
        /// </summary>
        private static string[] getSessionSkillPatterns(EzSkillProvider provider, string hash)
        {
            try
            {
                return provider.GetChartSkillTagsForHashes([hash]).TryGetValue(hash, out string[]? tags) ? tags : [];
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Re-runs the chart skill pass for the selected card only; a chart whose row is already current answers
        /// from the provider's session caches without recomputing.
        /// </summary>
        private void recheckLivePatterns(EzSkillProvider provider, BeatmapInfo target, int requestId, string hash, string[] cachedPatterns)
        {
            var detached = target.Detach();

            Task.Run(() =>
            {
                try
                {
                    return provider.TryGetOrComputeChartSkillInfo(detached)?.Patterns;
                }
                catch
                {
                    return null;
                }
            }).ContinueWith(t =>
            {
                string[]? livePatterns = t.Status == TaskStatus.RanToCompletion ? t.GetResultSafely() : null;

                if (livePatterns == null || livePatterns.SequenceEqual(cachedPatterns))
                    return;

                Schedule(() =>
                {
                    if (!isCurrent(requestId, hash))
                        return;

                    rebuildTagFlow(livePatterns);
                });
            });
        }

        private bool isCurrent(int requestId, string hash)
            => requestId == skillTagRequestId
               && beatmap != null
               && string.Equals(beatmap.Hash, hash, StringComparison.Ordinal);

        private void rebuildTagFlow(string[] skillPatterns)
        {
            tagFlow.Clear();

            if (beatmap == null)
                return;

            int remaining = max_visible_tags;

            foreach (string patternId in skillPatterns.Take(max_skill_tags))
            {
                if (remaining <= 0)
                    break;

                if (!tryResolveSkillChip(patternId, out var chip, out string id))
                    continue;

                var skillTag = new SkillChipTag(chip)
                {
                    Action = () => songSelect?.AddToSearch($@"skill:{id}"),
                };
                tagFlow.Add(skillTag);
                skillTag.SetAltHighlight(altHighlightActive);
                remaining--;
            }

            if (beatmap.HasVideo == true && remaining > 0)
            {
                var icon = new IconTag(FontAwesome.Solid.Film, BeatmapsetsStrings.ShowInfoVideo)
                {
                    Action = () => songSelect?.AddToSearch(@"video:true"),
                };
                tagFlow.Add(icon);
                icon.SetAltHighlight(altHighlightActive);
                remaining--;
            }

            if (beatmap.HasStoryboard == true && remaining > 0)
            {
                var icon = new IconTag(FontAwesome.Solid.Image, BeatmapsetsStrings.ShowInfoStoryboard)
                {
                    Action = () => songSelect?.AddToSearch(@"storyboard:true"),
                };
                tagFlow.Add(icon);
                icon.SetAltHighlight(altHighlightActive);
                remaining--;
            }

            foreach (string tag in beatmap.Metadata.UserTags.Take(remaining))
            {
                var simpleTag = new SimpleTag(tag)
                {
                    Action = () => songSelect?.AddToSearch($@"tag=""{tag}""!"),
                };
                tagFlow.Add(simpleTag);
                simpleTag.SetAltHighlight(altHighlightActive);
            }
        }

        private static bool tryResolveSkillChip(string patternId, out EzSkillChip chip, out string id)
        {
            if (EzManiaRCAxisExtensions.TryParse(patternId, out var rc))
            {
                chip = rc.Chip();
                id = rc.ToId();
                return true;
            }

            if (EzManiaLNAxisExtensions.TryParse(patternId, out var ln))
            {
                chip = ln.Meta().Chip;
                id = ln.ToId();
                return true;
            }

            chip = new EzSkillChip(patternId, EzSkillChip.FALLBACK.AccentHex);
            id = patternId;
            return !string.IsNullOrWhiteSpace(patternId);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                EzDisplayTagAltHighlight.ActiveChanged -= onAltHighlightChanged;
                scheduledTagUpdate?.Cancel();
                scheduledTagUpdate = null;
                skillTagRequestId++;
                beatmap = null;
            }
        }

        private partial class SkillChipTag : OsuClickableContainer
        {
            private readonly EzSkillChip chip;

            private Box background = null!;
            private OsuSpriteText label = null!;
            private bool altHighlightActive;
            private Colour4 accent;
            private Colour4 idleBackground;
            private Colour4 idleForeground;

            public SkillChipTag(EzSkillChip chip)
            {
                this.chip = chip;
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

            protected override void LoadComplete()
            {
                base.LoadComplete();
                refreshColours();
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                accent = Colour4.FromHex(chip.AccentHex);
                idleBackground = accent.Opacity(0.18f);
                idleForeground = accent;

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
                            Colour = idleBackground,
                        },
                        label = new OsuSpriteText
                        {
                            Text = chip.Name,
                            Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold),
                            Colour = idleForeground,
                            Margin = new MarginPadding { Horizontal = 4, Vertical = 1 },
                        }
                    }
                };
            }

            private void refreshColours()
            {
                if (!IsLoaded)
                    return;

                background.FadeColour(altHighlightActive ? accent.Opacity(0.55f) : idleBackground, 200, Easing.OutQuint);
                label.FadeColour(altHighlightActive ? Colour4.White : idleForeground, 200, Easing.OutQuint);
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
