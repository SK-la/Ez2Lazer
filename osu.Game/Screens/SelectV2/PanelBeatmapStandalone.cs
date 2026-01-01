// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.Screens.SelectV2
{
    public partial class PanelBeatmapStandalone : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.6f;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private CancellationTokenSource? kpsCalculationCancellationSource;
        private ScheduledDelegate? scheduledKpsCalculation;
        private string? cachedScratchText;
        private ManiaKpsDisplay maniaKpsDisplay = null!;
        private ManiaKpcDisplay maniaKpcDisplay = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;

        private PanelSetBackground beatmapBackground = null!;
        private ScheduledDelegate? scheduledBackgroundRetrieval;

        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private PanelUpdateBeatmapButton updateButton = null!;
        private BeatmapSetOnlineStatusPill statusPill = null!;

        private ConstrainedIconContainer difficultyIcon = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private SpreadDisplay spreadDisplay = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText keyCountText = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer mainFill = null!;

        private Box backgroundBorder = null!;

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        public PanelBeatmapStandalone()
        {
            PanelXOffset = 20;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Height = HEIGHT;

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(12),
                Margin = new MarginPadding { Left = 4f, Right = 3f },
                Colour = colourProvider.Background5,
            };

            Background = backgroundBorder = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };

            Content.Children = new Drawable[]
            {
                beatmapBackground = new PanelSetBackground(),
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Spacing = new Vector2(5),
                    Margin = new MarginPadding { Left = 6.5f },
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        localRank = new PanelLocalRankDisplay
                        {
                            Scale = new Vector2(0.8f),
                            Origin = Anchor.CentreLeft,
                            Anchor = Anchor.CentreLeft,
                        },
                        mainFill = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Bottom = 4.8f },
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                titleText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Heading2.With(typeface: Typeface.TorusAlternate, weight: FontWeight.Bold),
                                },
                                artistText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                    Padding = new MarginPadding { Top = -2 },
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Top = 2, Bottom = 2 },
                                    Children = new Drawable[]
                                    {
                                        statusPill = new BeatmapSetOnlineStatusPill
                                        {
                                            Animated = false,
                                            Origin = Anchor.BottomLeft,
                                            Anchor = Anchor.BottomLeft,
                                            TextSize = OsuFont.Style.Caption2.Size,
                                            Margin = new MarginPadding { Right = 4f },
                                        },
                                        updateButton = new PanelUpdateBeatmapButton
                                        {
                                            Scale = new Vector2(0.8f),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 4f, Bottom = -1f },
                                        },
                                        keyCountText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Alpha = 0,
                                        },
                                        difficultyText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 3f },
                                        },
                                        authorText = new OsuSpriteText
                                        {
                                            Colour = colourProvider.Content2,
                                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        },
                                        maniaKpsDisplay = new ManiaKpsDisplay(),
                                    }
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        starRatingDisplay = new StarRatingDisplay(default, StarRatingDisplaySize.Small, animated: true)
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        spreadDisplay = new SpreadDisplay
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Enabled = { BindTarget = Selected }
                                        },
                                        new OsuSpriteText
                                        {
                                            Text = "[Notes] ",
                                            Font = OsuFont.GetFont(size: 14),
                                            Colour = Colour4.GhostWhite,
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        },
                                        maniaKpcDisplay = new ManiaKpcDisplay(),
                                    },
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ =>
            {
                cachedScratchText = null;
                if (Item != null)
                    updateCalculationsAsync(beatmap);
                updateKeyCount();
            });

            mods.BindValueChanged(_ =>
            {
                cachedScratchText = null;
                if (Item != null)
                    updateCalculationsAsync(beatmap);
                updateKeyCount();
            }, true);

            Selected.BindValueChanged(s => Expanded.Value = s.NewValue, true);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            var beatmapSet = beatmap.BeatmapSet!;

            scheduledBackgroundRetrieval = Scheduler.AddDelayed(b => beatmapBackground.Beatmap = beatmaps.GetWorkingBeatmap(b), beatmap, 50);

            titleText.Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title);
            artistText.Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist);
            updateButton.BeatmapSet = beatmapSet;
            statusPill.Status = beatmap.Status;

            difficultyIcon.Icon = beatmap.Ruleset.CreateInstance().CreateIcon();
            difficultyIcon.Show();

            localRank.Beatmap = beatmap;
            difficultyText.Text = beatmap.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            cachedScratchText = null;
            updateCalculationsAsync(beatmap);
            computeStarRating();
            spreadDisplay.Beatmap.Value = beatmap;
            updateKeyCount();
        }

        private void updateCalculationsAsync(BeatmapInfo beatmapInfo)
        {
            if (ruleset.Value.OnlineID != 3)
                return;

            kpsCalculationCancellationSource?.Cancel();
            kpsCalculationCancellationSource = new CancellationTokenSource();
            var cancellationToken = kpsCalculationCancellationSource.Token;
            scheduledKpsCalculation?.Cancel();

            // 注意：面板来自 DrawablePool，快速拖动滚动条时会被频繁回收/复用。
            // 任何延迟执行的回调都不能再依赖 Item/beatmap 这类由当前面板状态决定的属性，否则会空引用崩溃。
            // 因此这里对 ruleset/mods 做快照，后续只使用快照数据。
            var rulesetSnapshot = ruleset.Value;
            var modsSnapshot = mods.Value.ToArray();

            scheduledKpsCalculation = Scheduler.AddDelayed(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                ILegacyRuleset legacyRuleset = (ILegacyRuleset)rulesetSnapshot.CreateInstance();
                int keyCount = legacyRuleset.GetKeyCount(beatmapInfo, modsSnapshot);

                string cacheKey = ManiaBeatmapAnalysisCache.CreateCacheKey(beatmapInfo, rulesetSnapshot, modsSnapshot);

                if (ManiaBeatmapAnalysisCache.TryGet(cacheKey, out var cached))
                {
                    cachedScratchText = cached.ScratchText;
                    updateUI((cached.AverageKps, cached.MaxKps, cached.KpsList), cached.ColumnCounts);
                    updateKeyCount();
                    return;
                }

                startKpsCalculationAsync(beatmapInfo, rulesetSnapshot, modsSnapshot, keyCount, cancellationToken);
            }, 120);
        }

        private async void startKpsCalculationAsync(BeatmapInfo beatmapInfo, RulesetInfo ruleset, Mod[] mods, int keyCount, CancellationToken cancellationToken)
        {
            try
            {
                var result = await ManiaBeatmapAnalysisCache.GetOrComputeAsync(
                        beatmapManager,
                        beatmapInfo,
                        ruleset,
                        mods,
                    keyCount)
                    .ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                    return;

                Schedule(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    cachedScratchText = result.ScratchText;
                    updateUI((result.AverageKps, result.MaxKps, result.KpsList), result.ColumnCounts);
                    updateKeyCount();
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        private void updateUI((double averageKps, double maxKps, List<double> kpsList) result, Dictionary<int, int>? columnCounts)
        {
            var (averageKps, maxKps, _) = result;

            maniaKpsDisplay.SetKps(averageKps, maxKps);

            if (columnCounts != null && columnCounts.Any())
            {
                maniaKpcDisplay.UpdateColumnCounts(columnCounts);
            }
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scheduledBackgroundRetrieval?.Cancel();
            scheduledBackgroundRetrieval = null;
            beatmapBackground.Beatmap = null;
            updateButton.BeatmapSet = null;
            localRank.Beatmap = null;
            starDifficultyBindable = null;
            spreadDisplay.Beatmap.Value = null;

            starDifficultyCancellationSource?.Cancel();
            kpsCalculationCancellationSource?.Cancel();
            scheduledKpsCalculation?.Cancel();
            scheduledKpsCalculation = null;
            cachedScratchText = null;
        }

        private void computeStarRating()
        {
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                spreadDisplay.StarDifficulty.Value = starDifficulty.NewValue;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (Item?.IsVisible != true)
            {
                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource = null;
            }

            // Dirty hack to make sure we don't take up spacing in parent fill flow when not displaying a rank.
            // I can't find a better way to do this.
            mainFill.Margin = new MarginPadding { Left = 1 / starRatingDisplay.Scale.X * (localRank.HasRank ? 0 : -3) };

            var diffColour = starRatingDisplay.DisplayedDifficultyColour;

            AccentColour = diffColour;
            spreadDisplay.Current.Colour = diffColour;

            backgroundBorder.Colour = diffColour;
            difficultyIcon.Colour = starRatingDisplay.DisplayedStars.Value > OsuColour.STAR_DIFFICULTY_DEFINED_COLOUR_CUTOFF ? colours.Orange1 : colourProvider.Background5;
        }

        private void updateKeyCount()
        {
            if (Item == null)
                return;

            if (ruleset.Value.OnlineID == 3)
            {
                // Account for mania differences locally for now.
                // Eventually this should be handled in a more modular way, allowing rulesets to add more information to the panel.
                ILegacyRuleset legacyRuleset = (ILegacyRuleset)ruleset.Value.CreateInstance();
                int keyCount = legacyRuleset.GetKeyCount(beatmap, mods.Value);

                keyCountText.Text = cachedScratchText ?? $"[{keyCount}K] ";

                keyCountText.Alpha = 1;
            }
            else
                keyCountText.Alpha = 0;

            computeStarRating();
        }

        public override MenuItem[] ContextMenuItems
        {
            get
            {
                if (Item == null)
                    return Array.Empty<MenuItem>();

                List<MenuItem> items = new List<MenuItem>();

                if (songSelect != null)
                    items.AddRange(songSelect.GetForwardActions(beatmap));

                return items.ToArray();
            }
        }
    }
}
