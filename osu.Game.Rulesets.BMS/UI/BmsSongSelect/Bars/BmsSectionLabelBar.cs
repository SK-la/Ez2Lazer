// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars
{
    /// <summary>
    /// Non-interactive section header in the bar list (e.g. "曲库", "过滤").
    /// </summary>
    public sealed class BmsSectionLabelBar : BmsBar
    {
        public BmsSectionLabelBar(string title) => Title = title;

        public override string Title { get; }
    }
}
