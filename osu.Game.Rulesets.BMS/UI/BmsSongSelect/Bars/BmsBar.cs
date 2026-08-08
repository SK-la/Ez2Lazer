// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    public readonly record struct BmsBarPageCursor(BmsChartPageCursor? IndexCursor, string? FilterCursor);

    public sealed record BmsBarPage(IReadOnlyList<BmsBar> Bars, BmsBarPageCursor? NextCursor);

    public abstract class BmsBar
    {
        public abstract string Title { get; }

        public virtual string Subtitle => string.Empty;

        public virtual bool IsSelectable => false;

        public virtual bool IsDirectory => false;
    }

    public abstract class BmsDirectoryBar : BmsBar
    {
        public override bool IsDirectory => true;

        public abstract bool IsSortable { get; }

        public abstract IReadOnlyList<BmsBar> GetChildren(BmsBarContext context);

        public virtual BmsBarPage? GetPage(BmsBarContext context, BmsBarPageCursor? after, int limit) => null;

        public virtual BmsChartSummary? GetRandom(BmsBarContext context, IReadOnlyDictionary<string, JsonElement>? filter) => null;
    }

    public abstract class BmsSelectableBar : BmsBar
    {
        public override bool IsSelectable => true;
    }
}
