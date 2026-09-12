// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Profile Insights (Mania Ez / Track): Key Split / Mod / BPM / PP + newest/oldest top plays.
    /// </summary>
    public partial class EzLocalProfileInsightsBody : FillFlowContainer
    {
        private readonly string username;
        private readonly Bindable<EzLocalProfileDrillScoreRow?>? selectDrillScore;
        private readonly IReadOnlyList<EzLocalProfileDrillScoreRow>? preloadedDrillScores;

        private enum DetailKind
        {
            None,
            KeyPp,
            Mods,
            Bpm,
            Pp,
        }

        private DetailKind openDetail = DetailKind.None;
        private FillFlowContainer summaryFlow = null!;
        private Container detailContainer = null!;
        private FillFlowContainer playCardsFlow = null!;
        private OsuSpriteText emptyHint = null!;
        private Container loadingHost = null!;
        private EzLocalProfileInsights? insights;
        private CancellationTokenSource? rebuildCts;

        [Resolved]
        private EzLocalProfileService profileService { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public EzLocalProfileInsightsBody(
            string username,
            Bindable<EzLocalProfileDrillScoreRow?>? selectDrillScore = null,
            IReadOnlyList<EzLocalProfileDrillScoreRow>? preloadedDrillScores = null)
        {
            this.username = username;
            this.selectDrillScore = selectDrillScore;
            this.preloadedDrillScores = preloadedDrillScores;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 12);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                emptyHint = new OsuSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.GetFont(size: 14),
                    Alpha = 0,
                },
                loadingHost = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 64,
                    Child = new LoadingSpinner
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        State = { Value = Visibility.Visible },
                    },
                },
                summaryFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(10),
                },
                detailContainer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                playCardsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(10),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuild();
        }

        protected override void Dispose(bool isDisposing)
        {
            rebuildCts?.Cancel();
            rebuildCts?.Dispose();
            rebuildCts = null;
            base.Dispose(isDisposing);
        }

        private void rebuild()
        {
            rebuildCts?.Cancel();
            rebuildCts?.Dispose();
            rebuildCts = new CancellationTokenSource();
            var token = rebuildCts.Token;

            summaryFlow.Clear();
            detailContainer.Clear();
            playCardsFlow.Clear();
            openDetail = DetailKind.None;
            emptyHint.Hide();
            loadingHost.Show();

            var localProfileService = profileService;
            string localUsername = username;
            var localPreloaded = preloadedDrillScores;
            var localRealm = realm;
            var localBeatmaps = beatmapManager;
            var localRulesets = rulesets;

            Task.Run(() =>
            {
                var rows = localPreloaded
                           ?? localProfileService.LoadDrillScores(EzLocalProfileConstants.MANIA_RULESET_ID, localUsername);
                var plays = EzLocalProfileInsightScoreBuilder.Build(rows, localBeatmaps, localRulesets, localRealm);
                return EzLocalProfileInsightsCalculator.Calculate(plays);
            }, token).ContinueWith(task => Schedule(() =>
            {
                if (token.IsCancellationRequested || task.IsCanceled)
                    return;

                loadingHost.Hide();

                if (task.IsFaulted)
                {
                    emptyHint.Text = EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_EMPTY;
                    emptyHint.Show();
                    return;
                }

                insights = task.GetResultSafely();
                applyInsightsUi(insights);
            }), token);
        }

        private void applyInsightsUi(EzLocalProfileInsights insights)
        {
            if (insights.SampleSize == 0)
            {
                emptyHint.Text = EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_EMPTY;
                emptyHint.Show();
                return;
            }

            emptyHint.Hide();

            summaryFlow.Add(new InsightChip(
                EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_KEY_SPLIT,
                formatKeySplitSummary(insights),
                () => toggleDetail(DetailKind.KeyPp)));

            summaryFlow.Add(new InsightChip(
                EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_MOST_USED_MOD,
                formatMostUsedMod(insights),
                () => toggleDetail(DetailKind.Mods)));

            summaryFlow.Add(new InsightChip(
                EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_MEDIAN_BPM,
                formatMedianBpm(insights),
                () => toggleDetail(DetailKind.Bpm)));

            summaryFlow.Add(new InsightChip(
                EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_PP_RANGE,
                formatPpRange(insights),
                () => toggleDetail(DetailKind.Pp)));

            if (insights.NewestTopPlay != null)
            {
                playCardsFlow.Add(createTopPlayCard(
                    EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_NEWEST_TOP,
                    insights.NewestTopPlay));
            }

            if (insights.OldestTopPlay != null)
            {
                playCardsFlow.Add(createTopPlayCard(
                    EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_OLDEST_TOP,
                    insights.OldestTopPlay));
            }
        }

        private void selectPlay(EzLocalProfileInsightPlay play)
        {
            selectDrillScore?.Value = play.Row;
        }

        private EzLocalProfileScoreNarrowCard createTopPlayCard(LocalisableString caption, EzLocalProfileInsightPlay play)
        {
            return new EzLocalProfileScoreNarrowCard(
                play.Row,
                EzLocalProfileDrillMods.Resolve(play.Row, rulesets),
                () => selectPlay(play),
                caption)
            {
                Width = 320,
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.Y,
            };
        }

        private void toggleDetail(DetailKind kind)
        {
            openDetail = openDetail == kind ? DetailKind.None : kind;
            refreshDetail();
        }

        private void refreshDetail()
        {
            detailContainer.Clear();

            if (insights == null || openDetail == DetailKind.None)
                return;

            Drawable body = openDetail switch
            {
                DetailKind.KeyPp => createKeyPpDetail(insights),
                DetailKind.Mods => createModDetail(insights),
                DetailKind.Bpm => createBpmDetail(insights),
                DetailKind.Pp => createPpDetail(insights),
                _ => new Container { RelativeSizeAxes = Axes.X, Height = 0 },
            };

            detailContainer.Child = new EzLocalProfileChartCard(detailTitle(openDetail), body);
        }

        private static LocalisableString detailTitle(DetailKind kind) => kind switch
        {
            DetailKind.KeyPp => (LocalisableString)EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_KEY_PP,
            DetailKind.Mods => (LocalisableString)EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_MOD_USAGE,
            DetailKind.Bpm => (LocalisableString)EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_BPM_BREAKDOWN,
            DetailKind.Pp => (LocalisableString)EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_PP_DISTRIBUTION,
            _ => (LocalisableString)string.Empty,
        };

        private static Drawable createKeyPpDetail(EzLocalProfileInsights data)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };

            if (data.KeyPp.Count == 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 13),
                });
                return flow;
            }

            double max = data.KeyPp.Max(k => k.WeightedPp);

            foreach (var bucket in data.KeyPp)
            {
                float ratio = max <= 0 ? 0 : (float)(bucket.WeightedPp / max);
                flow.Add(new LabeledBarRow(
                    $"{bucket.KeyCount}K",
                    $"{EzLocalProfileFormat.FormatPp(bucket.WeightedPp)}pp · {bucket.Count}",
                    ratio));
            }

            if (data.KeyPpConverts > 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_CONVERTS_EXCLUDED.Format(data.KeyPpConverts),
                    Font = OsuFont.GetFont(size: 12),
                });
            }

            return flow;
        }

        private static Drawable createModDetail(EzLocalProfileInsights data)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };

            if (data.ModBreakdown.Count == 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_NO_MODS,
                    Font = OsuFont.GetFont(size: 13),
                });
                return flow;
            }

            int max = data.ModBreakdown.Max(m => m.Count);

            foreach (var mod in data.ModBreakdown)
            {
                float ratio = max <= 0 ? 0 : (float)mod.Count / max;
                double pct = mod.Total <= 0 ? 0 : 100.0 * mod.Count / mod.Total;
                flow.Add(new LabeledBarRow(
                    mod.Label,
                    $"{mod.Count} ({pct.ToString("0.0", CultureInfo.InvariantCulture)}%)",
                    ratio));
            }

            return flow;
        }

        private static Drawable createBpmDetail(EzLocalProfileInsights data)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };

            if (data.BpmRange != null)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_BPM_RANGE.Format(
                        data.BpmRange.Min.ToString("0", CultureInfo.InvariantCulture),
                        data.BpmRange.Max.ToString("0", CultureInfo.InvariantCulture)),
                    Font = OsuFont.GetFont(size: 13),
                });
            }

            if (data.BpmByKeyMode.Count == 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 13),
                });
                return flow;
            }

            double max = data.BpmByKeyMode.Max(b => b.Median);

            foreach (var row in data.BpmByKeyMode)
            {
                float ratio = max <= 0 ? 0 : (float)(row.Median / max);
                flow.Add(new LabeledBarRow(
                    $"{row.KeyCount}K",
                    $"{row.Median.ToString("0", CultureInfo.InvariantCulture)} BPM · {row.Count}",
                    ratio));
            }

            return flow;
        }

        private static Drawable createPpDetail(EzLocalProfileInsights data)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };

            if (data.PpDistribution.Count == 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 13),
                });
                return flow;
            }

            int max = data.PpDistribution.Max(b => b.Count);

            foreach (var bucket in data.PpDistribution)
            {
                float ratio = max <= 0 ? 0 : (float)bucket.Count / max;
                string label = formatPpBucketLabel(bucket);
                flow.Add(new LabeledBarRow(label, bucket.Count.ToString("N0"), ratio));
            }

            if (data.PpCumulative.Count > 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Margin = new MarginPadding { Top = 8 },
                    Text = EzSettingsProfile.LOCAL_PROFILE_INSIGHTS_PP_CUMULATIVE,
                    Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                });

                int cumMax = data.PpCumulative.Max(r => r.Count);

                foreach (var row in data.PpCumulative.Take(12))
                {
                    float ratio = cumMax <= 0 ? 0 : (float)row.Count / cumMax;
                    flow.Add(new LabeledBarRow(
                        $"≥{row.Threshold}",
                        $"{row.Count}/{row.Total}",
                        ratio));
                }
            }

            return flow;
        }

        private static string formatPpBucketLabel(EzLocalProfilePpDistributionBucket bucket)
        {
            if (bucket.Min is null)
                return $"<{bucket.Max + 1}";
            if (bucket.Max is null)
                return $"≥{bucket.Min}";

            return $"{bucket.Min}–{bucket.Max}";
        }

        private static string formatKeySplitSummary(EzLocalProfileInsights data)
        {
            if (data.KeySplit.Count == 0)
                return "—";

            var top = data.KeySplit[0];
            double pct = data.SampleSize <= 0 ? 0 : 100.0 * top.Count / data.SampleSize;
            return $"{top.KeyCount}K {pct.ToString("0", CultureInfo.InvariantCulture)}%";
        }

        private static string formatMostUsedMod(EzLocalProfileInsights data)
        {
            if (data.MostUsedMod == null)
                return "NM";

            double pct = data.MostUsedMod.Total <= 0 ? 0 : 100.0 * data.MostUsedMod.Count / data.MostUsedMod.Total;
            return $"{data.MostUsedMod.Label} {pct.ToString("0", CultureInfo.InvariantCulture)}%";
        }

        private static string formatMedianBpm(EzLocalProfileInsights data)
        {
            if (data.MedianBpm is not double bpm)
                return "—";

            if (data.BpmRange != null)
            {
                return $"{bpm.ToString("0", CultureInfo.InvariantCulture)} ({data.BpmRange.Min.ToString("0", CultureInfo.InvariantCulture)}–{data.BpmRange.Max.ToString("0", CultureInfo.InvariantCulture)})";
            }

            return bpm.ToString("0", CultureInfo.InvariantCulture);
        }

        private static string formatPpRange(EzLocalProfileInsights data)
        {
            if (data.PpRange == null)
                return "—";

            return $"{EzLocalProfileFormat.FormatPp(data.PpRange.Top)}–{EzLocalProfileFormat.FormatPp(data.PpRange.Bottom)}";
        }

        private partial class InsightChip : OsuClickableContainer
        {
            private readonly LocalisableString title;
            private readonly string value;
            private EzLocalProfileHoverBox background = null!;

            public InsightChip(LocalisableString title, string value, Action action)
            {
                this.title = title;
                this.value = value;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 8;
                Action = action;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                background = new EzLocalProfileHoverBox();
                background.Configure(colours);

                Children = new Drawable[]
                {
                    background,
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Horizontal = 14, Vertical = 10 },
                        Spacing = new Vector2(0, 2),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = title,
                                Font = OsuFont.GetFont(size: 11, weight: FontWeight.SemiBold),
                                Colour = colours.Content2,
                            },
                            new OsuSpriteText
                            {
                                Text = value,
                                Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold),
                                Colour = EzLocalProfileColours.Numeric(colours),
                            },
                        }
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.Refresh(true);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.Refresh(false);
                base.OnHoverLost(e);
            }
        }

        private partial class LabeledBarRow : Container
        {
            private readonly OsuSpriteText labelText;
            private readonly OsuSpriteText valueLabel;

            public LabeledBarRow(string label, string valueText, float ratio)
            {
                RelativeSizeAxes = Axes.X;
                Height = 22;
                Padding = new MarginPadding { Horizontal = 4 };

                labelText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = label,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                    Width = 72,
                };
                valueLabel = new OsuSpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Text = valueText,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                };

                Children = new Drawable[]
                {
                    labelText,
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 80, Right = 96 },
                        Child = new EzLocalProfileRoundedBar(ratio),
                    },
                    valueLabel,
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                labelText.Colour = EzLocalProfileColours.Plays(colours);
                valueLabel.Colour = EzLocalProfileColours.Numeric(colours);
            }
        }
    }
}
