// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    public sealed class BmsSongBar : BmsSelectableBar
    {
        public BmsChartSummary Summary { get; }

        public Guid BeatmapId => Summary.BeatmapId;

        public string PathKey => Summary.PathKey;

        public BmsSongBar(BmsChartSummary summary)
        {
            Summary = summary;
            Title = string.IsNullOrWhiteSpace(summary.Title) ? summary.FileName : summary.Title;
            Subtitle = $"{summary.Artist} / Lv.{summary.PlayLevel} / {summary.KeyCount}K";
        }

        public override string Title { get; }

        public override string Subtitle { get; }
    }
}
