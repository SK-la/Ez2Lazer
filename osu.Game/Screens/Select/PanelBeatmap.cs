// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class PanelBeatmap : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT + 26f;

        private StarCounter starCounter = null!;
        private ConstrainedIconContainer difficultyIcon = null!;
        private OsuSpriteText variantText = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer mainFill = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;

        private Box backgroundBorder = null!;
        private Box backgroundDifficultyTint = null!;

        private TrianglesV2 triangles = null!;

        private EzDisplayKpsGraph ezDisplayKpsGraph = null!;
        private EzDisplayKps ezDisplayKps = null!;
        private EzDisplayKpc ezDisplayKpc = null!;
        private EzDisplaySR displaySR = null!;
        private EzDisplayTag ezDisplayTag = null!;

        private IBindable<EzAnalysisResult>? ezAnalysisBindable;
        private CancellationTokenSource? ezAnalysisCancellationSource;
        private ScheduledDelegate? scheduledEzAnalysisUpdate;

        private bool ezAnalysisEnabled;
        private string? scratchText;
        private const int mania_ui_update_throttle_ms = 15;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private EzAnalysisCache ezAnalysisCache { get; set; } = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        public PanelBeatmap()
        {
            PanelXOffset = 60;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Height = HEIGHT;

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(9f),
                Margin = new MarginPadding { Left = 2.5f, Right = 1.5f },
                Colour = colourProvider.Background5,
            };

            Background = backgroundBorder = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };

            Content.Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4),
                },
                backgroundDifficultyTint = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                triangles = new TrianglesV2
                {
                    ScaleAdjust = 1.2f,
                    Thickness = 0.01f,
                    Velocity = 0.3f,
                    RelativeSizeAxes = Axes.Both,
                },
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
                            AutoSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Bottom = 3.5f },
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Bottom = 4 },
                                    Children = new Drawable[]
                                    {
                                        variantText = new OsuSpriteText
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
                                        }
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
                                        displaySR = new EzDisplaySR(EzManiaSummary.EMPTY, StarRatingDisplaySize.Small, animated: true)
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        starCounter = new StarCounter
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Scale = new Vector2(0.4f)
                                        },
                                        ezDisplayKpc = new EzDisplayKpc(),
                                    },
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    // Padding = new MarginPadding { Bottom = 2 },
                                    Children = new Drawable[]
                                    {
                                        ezDisplayKps = new EzDisplayKps
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        ezDisplayKpsGraph = new EzDisplayKpsGraph
                                        {
                                            Size = new Vector2(300, 15),
                                            Blending = BlendingParameters.Mixture,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                    }
                                },
                                ezDisplayTag = new EzDisplayTag
                                {
                                    Margin = new MarginPadding { Top = 2 },
                                    Alpha = 0.9f,
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

            ezConfig.BindWith(Ez2Setting.KpcDisplayMode, ezDisplayKpc.KpcDisplayModeBindable);

            bool ezAnalysisCacheEnabled = ezConfig.Get<bool>(Ez2Setting.EzAnalysisRecEnabled);
            bool ezAnalysisSqliteEnabled = ezConfig.Get<bool>(Ez2Setting.EzAnalysisSqliteEnabled);

            ruleset.BindValueChanged(_ =>
            {
                ezAnalysisEnabled = ezAnalysisCacheEnabled || ezAnalysisSqliteEnabled;

                resetEzDisplay();
                updateKeyCount();
            }, true);

            mods.BindValueChanged(_ =>
            {
                updateKeyCount();
            }, true);

            var xxySrFilterSetting = ezConfig.GetBindable<bool>(Ez2Setting.XxySRFilter);
            xxySrFilterSetting.BindValueChanged(value =>
            {
                starCounter.Icon = value.NewValue
                    ? FontAwesome.Solid.Moon
                    : FontAwesome.Solid.Star;
            }, true);
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            difficultyIcon.Icon = getRulesetIcon(beatmap.Ruleset);

            localRank.Beatmap = beatmap;
            difficultyText.Text = beatmap.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            computeStarRating();
            updateKeyCount();

            resetEzDisplay();
            ezDisplayTag.TagSummary = null;
            ezDisplayTag.Beatmap = beatmap;
            computeEzAnalysis();
        }

        private void resetEzDisplay()
        {
            if (ezAnalysisEnabled && ruleset.Value.OnlineID == 3)
            {
                displaySR.Show();
            }
            else
            {
                ezDisplayKpc.ManiaSummary = null;
                displaySR.Current.Value = EzManiaSummary.EMPTY;
                displaySR.Hide();
            }
        }

        private Drawable getRulesetIcon(RulesetInfo rulesetInfo)
        {
            var rulesetInstance = rulesets.GetRuleset(rulesetInfo.ShortName)?.CreateInstance();

            if (rulesetInstance is null)
                return new SpriteIcon { Icon = FontAwesome.Regular.QuestionCircle };

            return rulesetInstance.CreateIcon();
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            localRank.Beatmap = null;
            starDifficultyBindable = null;

            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource?.Dispose();
            starDifficultyCancellationSource = null;

            clearEzAnalysisBinding();
        }

        private void clearEzAnalysisBinding(bool resetDisplay = true)
        {
            scheduledEzAnalysisUpdate?.Cancel();
            scheduledEzAnalysisUpdate = null;

            ezAnalysisBindable?.UnbindAll();
            ezAnalysisBindable = null;

            ezAnalysisCancellationSource?.Cancel();
            ezAnalysisCancellationSource?.Dispose();
            ezAnalysisCancellationSource = null;

            if (!resetDisplay)
                return;

            ezDisplayTag.Beatmap = null;
            ezDisplayTag.TagSummary = null;
            scratchText = null;

            displaySR.Current.Value = EzManiaSummary.EMPTY;
            ezDisplayKpc.ManiaSummary = null;
        }

        private void updateKPS(EzAnalysisResult ezAnalysisResult)
        {
            double avgKPS = ezAnalysisResult.AverageKps;
            double maxKps = ezAnalysisResult.MaxKps;
            var kpsList = ezAnalysisResult.KpsList;
            ezDisplayKps.SetKps(ezAnalysisResult.Pp, avgKPS, maxKps);
            ezDisplayKpsGraph.SetPoints(kpsList);

            if (ezAnalysisEnabled && ezAnalysisResult.TagSummary != null)
                ezDisplayTag.TagSummary = ezAnalysisResult.TagSummary;

            if (ezAnalysisEnabled && ruleset.Value.OnlineID == 3)
            {
                var maniaSummary = ezAnalysisResult.ManiaSummary;
                var columnCounts = maniaSummary?.ColumnCounts ?? new Dictionary<int, int>();

                scratchText = EzBeatmapCalculator.GetScratchFromPrecomputed(columnCounts, maxKps, kpsList);
                updateKeyCount();
                ezDisplayKpc.ManiaSummary = maniaSummary;
                displaySR.Current.Value = maniaSummary ?? EzManiaSummary.EMPTY;
            }
        }

        private void computeEzAnalysis()
        {
            if (!ezAnalysisEnabled)
                return;

            ezAnalysisCancellationSource?.Cancel();
            ezAnalysisCancellationSource?.Dispose();
            ezAnalysisCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            ezAnalysisBindable = ezAnalysisCache.GetBindableAnalysis(beatmap, ezAnalysisCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            ezAnalysisBindable.BindValueChanged(result =>
            {
                scheduledEzAnalysisUpdate?.Cancel();
                scheduledEzAnalysisUpdate = Scheduler.AddDelayed(() =>
                {
                    updateKPS(result.NewValue);
                    scheduledEzAnalysisUpdate = null;
                }, mania_ui_update_throttle_ms);
            }, true);
        }

        private void computeStarRating()
        {
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource?.Dispose();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token, SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                starCounter.Current = (float)starDifficulty.NewValue.Stars;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (Item?.IsVisible != true)
            {
                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource?.Dispose();
                starDifficultyCancellationSource = null;

                ezAnalysisCancellationSource?.Cancel();
                ezAnalysisCancellationSource?.Dispose();
                ezAnalysisCancellationSource = null;
            }

            // Dirty hack to make sure we don't take up spacing in parent fill flow when not displaying a rank.
            // I can't find a better way to do this.
            mainFill.Margin = new MarginPadding { Left = 1 / starRatingDisplay.Scale.X * (localRank.HasRank ? 0 : -3) };

            var diffColour = starRatingDisplay.DisplayedDifficultyColour;

            if (AccentColour != diffColour)
            {
                AccentColour = diffColour;
                starCounter.Colour = diffColour;

                backgroundBorder.Colour = diffColour;
                backgroundDifficultyTint.Colour = ColourInfo.GradientHorizontal(diffColour.Opacity(0.25f), diffColour.Opacity(0f));

                triangles.Colour = ColourInfo.GradientVertical(diffColour.Opacity(0.25f), diffColour.Opacity(0f));
            }

            if (difficultyIcon.Colour != starRatingDisplay.DisplayedDifficultyTextColour)
            {
                difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;
            }
        }

        private void updateKeyCount()
        {
            if (Item == null)
                return;

            var rulesetInstance = ruleset.Value.CreateInstance();

            if (rulesetInstance.AvailableVariants.Count() > 1)
            {
                int variant = rulesetInstance.GetVariantForBeatmap(beatmap, mods.Value);
                var variantName = rulesetInstance.GetVariantName(variant);

                variantText.Alpha = 1;
                variantText.Text = scratchText ?? LocalisableString.Interpolate($"[{variantName}] ");
                variantText.Colour = Colour4.LightPink.ToLinear();
            }
            else
                variantText.Alpha = 0;
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
