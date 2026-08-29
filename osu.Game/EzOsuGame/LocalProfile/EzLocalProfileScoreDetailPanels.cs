// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public readonly struct EzLocalProfileScoreDisplayData
    {
        public ScoreRank Rank { get; }
        public double? Pp { get; }
        public string AccuracyText { get; }
        public bool PerfectAccuracy { get; }
        public int MaxCombo { get; }
        public int MaxAchievableCombo { get; }
        public long TotalScore { get; }
        public string Username { get; }
        public Mod[] Mods { get; }

        private EzLocalProfileScoreDisplayData(
            ScoreRank rank,
            double? pp,
            string accuracyText,
            bool perfectAccuracy,
            int maxCombo,
            int maxAchievableCombo,
            long totalScore,
            string username,
            Mod[] mods)
        {
            Rank = rank;
            Pp = pp;
            AccuracyText = accuracyText;
            PerfectAccuracy = perfectAccuracy;
            MaxCombo = maxCombo;
            MaxAchievableCombo = maxAchievableCombo;
            TotalScore = totalScore;
            Username = username;
            Mods = mods;
        }

        public static EzLocalProfileScoreDisplayData? From(EzLocalProfileDrillScoreRow row, Mod[] mods)
        {
            return new EzLocalProfileScoreDisplayData(
                row.Rank,
                row.PpResolved > 0 ? row.PpResolved : null,
                $"{row.Accuracy * 100:0.00}%",
                row.Accuracy == 1,
                row.MaxCombo,
                row.MaxAchievableCombo,
                row.TotalScore,
                row.Username,
                mods);
        }
    }

    public partial class EzLocalProfileScoreBeatmapCard : CompositeDrawable
    {
        public const float HEIGHT = PanelBeatmapStandalone.HEIGHT;

        private PanelSetBackground beatmapBackground = null!;
        private UpdateableRank rankDisplay = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private BeatmapSetOnlineStatusPill statusPill = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private EzDisplayKpsGraph ezDisplayKpsGraph = null!;
        private EzDisplayKps ezDisplayKps = null!;
        private EzDisplayKpc ezDisplayKpc = null!;
        private EzDisplaySR displaySR = null!;
        private EzDisplayTag ezDisplayTag = null!;
        private Box accentStrip = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        public EzLocalProfileScoreBeatmapCard()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                beatmapBackground = new PanelSetBackground(),
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.3f),
                },
                accentStrip = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 4,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Padding = new MarginPadding { Left = 10.5f },
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5),
                        Children = new Drawable[]
                        {
                            rankDisplay = new UpdateableRank(animate: false)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(40, 20),
                                Scale = new Vector2(0.8f),
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                Child = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding { Bottom = 4.8f },
                                    Children = new Drawable[]
                                    {
                                        titleText = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = OsuFont.Style.Heading2.With(typeface: Typeface.TorusAlternate, weight: FontWeight.Bold),
                                        },
                                        artistText = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                            Padding = new MarginPadding { Top = -2 },
                                        },
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Horizontal,
                                            Padding = new MarginPadding { Top = 2, Bottom = 2 },
                                            Spacing = new Vector2(6),
                                            Children = new Drawable[]
                                            {
                                                statusPill = new BeatmapSetOnlineStatusPill
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Animated = false,
                                                    TextSize = OsuFont.Style.Caption2.Size,
                                                    Margin = new MarginPadding { Right = 4f },
                                                },
                                                difficultyText = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                                    Margin = new MarginPadding { Right = 3f },
                                                },
                                                authorText = new TruncatingSpriteText
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    RelativeSizeAxes = Axes.X,
                                                    Colour = colourProvider.Content2,
                                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                                },
                                            },
                                        },
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Horizontal,
                                            Padding = new MarginPadding { Top = 2, Bottom = 2 },
                                            Spacing = new Vector2(3),
                                            Children = new Drawable[]
                                            {
                                                starRatingDisplay = new StarRatingDisplay(default, StarRatingDisplaySize.Small, animated: true)
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Scale = new Vector2(0.875f),
                                                },
                                                displaySR = new EzDisplaySR(EzManiaSummary.EMPTY, StarRatingDisplaySize.Small, animated: true)
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Scale = new Vector2(0.875f),
                                                },
                                                ezDisplayKps = new EzDisplayKps
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Scale = new Vector2(0.875f),
                                                },
                                                ezDisplayKpc = new EzDisplayKpc
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                },
                                            },
                                        },
                                        ezDisplayTag = new EzDisplayTag
                                        {
                                            Margin = new MarginPadding { Top = 2 },
                                            Alpha = 0.9f,
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            // Drill detail column is narrow; keep KPS metrics numeric only.
            ezDisplayKpsGraph = new EzDisplayKpsGraph();
        }

        public void Update(EzLocalProfileDrillScoreRow row)
        {
            rankDisplay.Rank = row.Rank;
            rankDisplay.Alpha = 1;

            var beatmap = beatmapManager.QueryBeatmap(b => b.ID == row.BeatmapId);
            beatmapBackground.Beatmap = beatmap != null ? beatmapManager.GetWorkingBeatmap(beatmap) : null;

            titleText.Text = row.Title;
            artistText.Text = row.Artist;
            statusPill.Status = row.BeatmapStatus;
            difficultyText.Text = row.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(row.MapperUsername);

            starRatingDisplay.Current.Value = new StarDifficulty(row.StarRating, 0);
            accentStrip.Colour = starRatingDisplay.DisplayedDifficultyColour;

            bool showXxy = row.RulesetId == EzLocalProfileConstants.MANIA_RULESET_ID && row.XxyStarRating >= 0;
            var maniaSummary = row.ReadManiaSummary();

            if (showXxy)
            {
                displaySR.Show();
                displaySR.Current.Value = maniaSummary;
            }
            else
            {
                displaySR.Current.Value = EzManiaSummary.EMPTY;
                displaySR.Hide();
            }

            ezDisplayTag.Beatmap = beatmap;
            ezDisplayKps.SetPp(row.MapPerformancePoints > 0 ? row.MapPerformancePoints : null);

            var metrics = new EzSongSelectAnalysisDisplay.PanelMetrics(row.KpsAvg, row.KpsMax, row.ReadKpsList().ToArray(), maniaSummary);
            applyPanelKps(metrics);

            if (showXxy && maniaSummary.ColumnCounts.Count > 0)
            {
                ezDisplayKpc.ManiaSummary = maniaSummary;
                ezDisplayKpc.Show();
            }
            else
            {
                ezDisplayKpc.ManiaSummary = null;
                ezDisplayKpc.Hide();
            }
        }

        public void Clear()
        {
            rankDisplay.Rank = null;
            rankDisplay.Alpha = 0;
            beatmapBackground.Beatmap = null;
            ezDisplayTag.Beatmap = null;
        }

        private void applyPanelKps(in EzSongSelectAnalysisDisplay.PanelMetrics metrics)
        {
            ezDisplayKpsGraph.SetPoints(metrics.KpsList);
            ezDisplayKps.SetKpsMetrics(metrics);
        }
    }

    public partial class EzLocalProfileScoreDetailRow : CompositeDrawable
    {
        private TruncatingSpriteText usernameText = null!;
        private FillFlowContainer modsFlow = null!;

        public EzLocalProfileScoreDetailRow()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 8;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = 10, Vertical = 6 },
                    Spacing = new Vector2(0, 14),
                    Children = new Drawable[]
                    {
                        usernameText = new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Font = OsuFont.Style.Heading2,
                            Colour = colourProvider.Content1,
                        },
                        modsFlow = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(4, 0),
                        },
                    },
                },
            };
        }

        public void Update(EzLocalProfileScoreDisplayData? data)
        {
            if (data == null)
            {
                Alpha = 0;
                return;
            }

            Alpha = 1;
            usernameText.Text = data.Value.Username;

            modsFlow.Clear();

            foreach (var mod in data.Value.Mods.AsOrdered())
            {
                modsFlow.Add(new ModIcon(mod)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Scale = new Vector2(0.3f),
                    Height = ModIcon.MOD_ICON_SIZE.Y * 3 / 4f,
                });
            }

            modsFlow.Alpha = modsFlow.Count > 0 ? 1 : 0;
        }
    }

    public partial class EzLocalProfileScoreDetailColumn : CompositeDrawable
    {
        private readonly Bindable<EzLocalProfileDrillScoreRow?> scoreSource;
        private readonly IReadOnlyList<EzLocalProfileDrillScoreRow> allScores;

        private EzLocalProfileScoreBeatmapCard beatmapCard = null!;
        private EzLocalProfileScoreDetailRow scoreRow = null!;
        private EzLocalProfileScoreKeysRow keysRow = null!;
        private EzLocalProfileBeatmapPerformance beatmapPerformance = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        public EzLocalProfileScoreDetailColumn(Bindable<EzLocalProfileDrillScoreRow?> scoreSource, IReadOnlyList<EzLocalProfileDrillScoreRow> allScores)
        {
            this.scoreSource = scoreSource;
            this.allScores = allScores;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            // Match OsuTextBox inner text inset (LeftRightPadding 10 + margin) on the drill search row.
            Padding = new MarginPadding { Right = 10 };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Children = new Drawable[]
                {
                    beatmapCard = new EzLocalProfileScoreBeatmapCard(),
                    scoreRow = new EzLocalProfileScoreDetailRow(),
                    keysRow = new EzLocalProfileScoreKeysRow(),
                    beatmapPerformance = new EzLocalProfileBeatmapPerformance(),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            scoreSource.BindValueChanged(v => updateScore(v.NewValue), true);
        }

        private void updateScore(EzLocalProfileDrillScoreRow? row)
        {
            if (row == null)
            {
                this.FadeOut(200);
                beatmapCard.Clear();
                scoreRow.Update(null);
                keysRow.UpdateRow(null);
                beatmapPerformance.Update(null, allScores);
                return;
            }

            this.FadeIn(200);
            beatmapCard.Update(row);
            var displayData = EzLocalProfileScoreDisplayData.From(row, EzLocalProfileDrillMods.Resolve(row, rulesets));
            scoreRow.Update(displayData);
            keysRow.UpdateRow(row, displayData);
            beatmapPerformance.Update(row, allScores);
        }
    }
}
