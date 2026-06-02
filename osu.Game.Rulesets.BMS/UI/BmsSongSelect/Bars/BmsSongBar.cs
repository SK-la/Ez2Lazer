// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    public sealed class BmsSongBar : BmsSelectableBar
    {
        public BMSChartCache Chart { get; }

        public string PathKey => string.IsNullOrEmpty(Chart.Md5Hash)
            ? BmsPathKeys.ComputeChartPathKey(Chart.FullPath)
            : Chart.Md5Hash;

        public BmsSongBar(BMSChartCache chart)
        {
            Chart = chart;
            Title = string.IsNullOrWhiteSpace(chart.Title) ? chart.FileName : chart.Title;
            Subtitle = $"{chart.Artist} / Lv.{chart.PlayLevel} / {chart.KeyCount}K";
        }

        public override string Title { get; }

        public override string Subtitle { get; }
    }
}
