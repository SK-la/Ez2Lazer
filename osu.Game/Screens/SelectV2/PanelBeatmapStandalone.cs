// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Buffers;
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
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.LAsEzExtensions.UserInterface;
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
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        #region Ez功能

        [Resolved]
        private EzBeatmapManiaAnalysisCache maniaAnalysisCache { get; set; } = null!;

        private EzDisplayKpsGraph ezDisplayKpsGraph = null!;
        private EzKpsDisplay ezKpsDisplay = null!;
        private EzKpcDisplay ezKpcDisplay = null!;
        private EzDisplayXxySR displayXxySR = null!;
        private Bindable<KpcDisplayMode> kpcMode = null!;

        private IBindable<ManiaBeatmapAnalysisResult>? maniaAnalysisBindable;
        private CancellationTokenSource? maniaAnalysisCancellationSource;

        private bool maniaWasVisible;

        private int keyCount;
        private string? scratchText;

        private const int mania_ui_update_throttle_ms = 15;

        #endregion

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
        private void load(Ez2ConfigManager ezConfig)
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
                                        ezKpsDisplay = new EzKpsDisplay
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                        },
                                        Empty(),
                                        ezDisplayKpsGraph = new EzDisplayKpsGraph
                                        {
                                            Size = new Vector2(300, 20),
                                            Blending = BlendingParameters.Mixture,
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                        },
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
                                        displayXxySR = new EzDisplayXxySR
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        spreadDisplay = new SpreadDisplay
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                        },
                                        ezKpcDisplay = new EzKpcDisplay
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                    },
                                }
                            }
                        }
                    }
                }
            };

            kpcMode = ezConfig.GetBindable<KpcDisplayMode>(Ez2Setting.KpcDisplayMode);
            ezKpcDisplay.KpcDisplayModeBindable.BindTo(kpcMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ =>
            {
                resetManiaAnalysisDisplay();
                updateKeyCount();
            });

            mods.BindValueChanged(_ =>
            {
                updateKeyCount();
            }, true);

            Selected.BindValueChanged(s =>
            {
                Expanded.Value = s.NewValue;
                spreadDisplay.Enabled.Value = s.NewValue;
            }, true);

            // kpcMode.BindValueChanged(_ => maniaWasVisible = false);
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

            computeManiaAnalysis();
            computeStarRating();
            spreadDisplay.Beatmap.Value = beatmap;
            updateKeyCount();
        }

        private void resetManiaAnalysisDisplay()
        {
            if (ruleset.Value.OnlineID == 3)
            {
                ezKpcDisplay.Show();
                displayXxySR.Show();
            }
            else
            {
                ezKpcDisplay.Hide();
                displayXxySR.Hide();
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

            // Ez功能
            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisBindable = null;
        }

        private void updateKPS((double averageKps, double maxKps, List<double> kpsList) result, Dictionary<int, int>? columnCounts, Dictionary<int, int>? holdNoteCounts)
        {
            if (Item == null || Item.IsVisible != true)
                return;

            var (averageKps, maxKps, kpsList) = result;

            ezKpsDisplay.SetKps(averageKps, maxKps);

            if (kpsList.Count > 0)
            {
                ezDisplayKpsGraph.SetPoints(kpsList);
            }

            if (columnCounts != null)
            {
                // Forward raw counts to EzKpcDisplay. Debounce, checksum and pooling
                // are handled internally by EzKpcDisplay to avoid duplication.
                ezKpcDisplay.UpdateColumnCounts(columnCounts, holdNoteCounts, keyCount);
            }
        }

        private void computeManiaAnalysis()
        {
            // Defer creating the heavy bindable until this panel becomes visible.
            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisCancellationSource = null;
            maniaAnalysisBindable = null;
        }

        private void requestManiaAnalysisBindable()
        {
            if (maniaAnalysisBindable != null || Item == null)
                return;

            maniaAnalysisCancellationSource?.Cancel();
            maniaAnalysisCancellationSource = new CancellationTokenSource();

            maniaAnalysisBindable = maniaAnalysisCache.GetBindableAnalysis(beatmap, maniaAnalysisCancellationSource.Token, computationDelay: SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            // Delay initial callback; we'll pull the current value when panel becomes visible.
            maniaAnalysisBindable.BindValueChanged(result =>
            {
                scratchText = result.NewValue.ScratchText;
                Schedule(updateKeyCount);
                if (Item?.IsVisible == true)
                    updateKPS((result.NewValue.AverageKps, result.NewValue.MaxKps, result.NewValue.KpsList), result.NewValue.ColumnCounts, result.NewValue.HoldNoteCounts);

                if (result.NewValue.XxySr != null)
                    displayXxySR.Current.Value = result.NewValue.XxySr;
            }, false);
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

                maniaAnalysisCancellationSource?.Cancel();
                maniaAnalysisCancellationSource = null;
            }

            // If we just became visible, pull latest mania analysis value to initialise UI.
            if (!maniaWasVisible && Item?.IsVisible == true)
            {
                maniaWasVisible = true;
                requestManiaAnalysisBindable();
            }

            if (maniaWasVisible && Item?.IsVisible != true)
                maniaWasVisible = false;

            // Dirty hack to make sure we don't take up spacing in parent fill flow when not displaying a rank.
            // I can't find a better way to do this.
            mainFill.Margin = new MarginPadding { Left = 1 / starRatingDisplay.Scale.X * (localRank.HasRank ? 0 : -3) };

            var diffColour = starRatingDisplay.DisplayedDifficultyColour;

            AccentColour = diffColour;
            spreadDisplay.Current.Colour = diffColour;

            backgroundBorder.Colour = diffColour;
            difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;
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
                keyCount = legacyRuleset.GetKeyCount(beatmap, mods.Value);

                keyCountText.Alpha = 1;
                keyCountText.Text = scratchText ?? $"[{keyCount}K] ";
                keyCountText.Colour = Colour4.LightPink.ToLinear();
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
