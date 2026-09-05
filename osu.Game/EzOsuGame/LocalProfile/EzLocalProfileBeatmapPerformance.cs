// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileBeatmapPerformance : CompositeDrawable
    {
        private const float card_spacing = 8;

        private readonly FillFlowContainer cardsFlow;
        private readonly Dictionary<Guid, double?> resolvedOffsets = new Dictionary<Guid, double?>();
        private CancellationTokenSource? offsetLoadCts;
        private EzLocalProfileDrillScoreRow? pendingCurrent;
        private IReadOnlyList<EzLocalProfileDrillScoreRow> pendingAllScores = Array.Empty<EzLocalProfileDrillScoreRow>();

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IEzReplaySession replaySession { get; set; } = null!;

        public EzLocalProfileBeatmapPerformance()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = cardsFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(card_spacing, 0),
            };
        }

        public void Update(EzLocalProfileDrillScoreRow? current, IReadOnlyList<EzLocalProfileDrillScoreRow> allScores)
        {
            offsetLoadCts?.Cancel();
            offsetLoadCts = null;
            pendingCurrent = current;
            pendingAllScores = allScores;

            rebuildCards();

            if (current == null)
                return;

            var peers = EzLocalProfileScoreDrillQuery.PeersOnSameBeatmap(current, allScores);
            var missing = peers.Where(row => resolveOffset(row) == null).Select(row => row.ScoreId).Distinct().ToList();

            if (missing.Count == 0)
                return;

            var localCancellation = offsetLoadCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                foreach (var scoreId in missing)
                {
                    localCancellation.Token.ThrowIfCancellationRequested();

                    double? offset = await EzLocalProfileHitEventResolver.ResolveAvgAbsOffsetMsAsync(
                        scoreId,
                        realm,
                        scoreManager,
                        beatmapManager,
                        replaySession,
                        localCancellation.Token).ConfigureAwait(false);

                    resolvedOffsets[scoreId] = offset;
                }
            }, localCancellation.Token).ContinueWith(task => Schedule(() =>
            {
                if (task.IsCanceled || pendingCurrent == null)
                    return;

                rebuildCards();
            }), localCancellation.Token);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            distributeCardWidths();
        }

        private void rebuildCards()
        {
            cardsFlow.Clear();

            var current = pendingCurrent;

            if (current == null)
            {
                Hide();
                return;
            }

            var peers = EzLocalProfileScoreDrillQuery.PeersOnSameBeatmap(current, pendingAllScores);

            foreach (var metric in createMetrics())
                cardsFlow.Add(createCard(metric, current, peers));

            Show();
            distributeCardWidths();
        }

        private IEnumerable<MetricDefinition> createMetrics()
        {
            yield return new MetricDefinition(
                EzSettingsProfile.LOCAL_PROFILE_BEATMAP_PERF_PP,
                row => row.PpResolved > 0 ? row.PpResolved : null,
                EzLocalProfileFormat.FormatPp,
                "pp");

            yield return new MetricDefinition(
                EzSettingsProfile.LOCAL_PROFILE_BEATMAP_PERF_ACC,
                row => row.Accuracy,
                v => $"{v * 100:0.00}%",
                string.Empty);

            yield return new MetricDefinition(
                EzSettingsProfile.LOCAL_PROFILE_BEATMAP_PERF_OFFSET,
                resolveOffset,
                v => $"{v:0} ms",
                string.Empty);
        }

        private double? resolveOffset(EzLocalProfileDrillScoreRow row)
        {
            if (row.AvgAbsOffsetMs is double stored)
                return stored;

            return resolvedOffsets.GetValueOrDefault(row.ScoreId);
        }

        private void distributeCardWidths()
        {
            if (cardsFlow.Count == 0)
                return;

            float availableWidth = cardsFlow.DrawWidth > 0 ? cardsFlow.DrawWidth : DrawWidth;

            if (availableWidth <= 0)
                return;

            float cardWidth = (availableWidth - card_spacing * (cardsFlow.Count - 1)) / cardsFlow.Count;

            foreach (var card in cardsFlow.Children)
            {
                card.RelativeSizeAxes = Axes.None;
                card.Width = cardWidth;
            }
        }

        private static EzLocalProfileScrollFlagCard createCard(MetricDefinition metric, EzLocalProfileDrillScoreRow current, IReadOnlyList<EzLocalProfileDrillScoreRow> peers)
        {
            var peerValues = peers
                             .Select(metric.SelectValue)
                             .Where(v => v != null)
                             .Select(v => v!.Value)
                             .ToList();

            string headerValue = formatValue(metric, metric.SelectValue(current));
            string maxValue = peerValues.Count > 0 ? formatValue(metric, peerValues.Max()) : "—";
            string minValue = peerValues.Count > 0 ? formatValue(metric, peerValues.Min()) : "—";
            string avgValue = peerValues.Count > 0 ? formatValue(metric, peerValues.Average()) : "—";

            return new EzLocalProfileScrollFlagCard(metric.Title, headerValue, maxValue, minValue, avgValue);
        }

        private static string formatValue(MetricDefinition metric, double? value)
        {
            if (value is not double resolved)
                return "—";

            string suffix = string.IsNullOrEmpty(metric.Suffix) ? string.Empty : $" {metric.Suffix}";
            return $"{metric.Format(resolved)}{suffix}";
        }

        private readonly struct MetricDefinition
        {
            public LocalisableString Title { get; }
            public Func<EzLocalProfileDrillScoreRow, double?> SelectValue { get; }
            public Func<double, string> Format { get; }
            public string Suffix { get; }

            public MetricDefinition(LocalisableString title, Func<EzLocalProfileDrillScoreRow, double?> selectValue, Func<double, string> format, string suffix)
            {
                Title = title;
                SelectValue = selectValue;
                Format = format;
                Suffix = suffix;
            }
        }
    }
}
