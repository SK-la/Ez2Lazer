// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileBeatmapPerformance : CompositeDrawable
    {
        private const float card_spacing = 8;

        private readonly FillFlowContainer cardsFlow;

        private static readonly MetricDefinition[] metrics =
        {
            new MetricDefinition(
                EzSettingsStrings.LOCAL_PROFILE_BEATMAP_PERF_PP,
                row => row.PpResolved > 0 ? row.PpResolved : null,
                EzLocalProfileFormat.FormatPp,
                "pp"),
            new MetricDefinition(
                EzSettingsStrings.LOCAL_PROFILE_BEATMAP_PERF_ACC,
                row => row.Accuracy,
                v => $"{v * 100:0.00}%",
                string.Empty),
            new MetricDefinition(
                EzSettingsStrings.LOCAL_PROFILE_BEATMAP_PERF_OFF,
                row => row.AvgAbsOffsetMs,
                v => $"{v:0} ms",
                string.Empty),
        };

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
            cardsFlow.Clear();

            if (current == null)
            {
                Hide();
                return;
            }

            var peers = EzLocalProfileScoreDrillQuery.PeersOnSameBeatmap(current, allScores);

            foreach (var metric in metrics)
                cardsFlow.Add(createCard(metric, current, peers));

            Show();
            distributeCardWidths();
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            distributeCardWidths();
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
