// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Merged career summary: PP, duration, keys, KPS, and score count on one row; grades below.
    /// </summary>
    public partial class EzLocalProfileCareerBody : FillFlowContainer
    {
        public EzLocalProfileCareerBody(EzLocalProfileRulesetStats rulesetStats, IEnumerable<EzLocalProfileGradeCount> gradeCounts)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 12);

            Add(new EzLocalProfileCareerSummaryRow(rulesetStats));
            Add(new EzLocalProfileGradeRow(gradeCounts));
        }
    }
}
