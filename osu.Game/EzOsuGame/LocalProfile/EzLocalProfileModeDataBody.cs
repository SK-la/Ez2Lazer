// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Career mode-specific distribution: line charts, horizontal bar columns, and ruleset blocks.
    /// </summary>
    public partial class EzLocalProfileModeDataBody : FillFlowContainer
    {
        private readonly EzLocalProfileSnapshot snapshot;
        private readonly int rulesetId;

        public EzLocalProfileModeDataBody(EzLocalProfileSnapshot snapshot, int rulesetId)
        {
            this.snapshot = snapshot;
            this.rulesetId = rulesetId;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 16);
        }

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            var ruleset = rulesets.GetRuleset(rulesetId);
            bool showXxy = rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID
                           || (ruleset != null && EzXxyStarRatingSupport.SupportsRuleset(ruleset));

            var lineSection = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
            };

            addLineChart(lineSection, EzSettingsProfile.LOCAL_PROFILE_STAR_PLAY_LINE, createStarLineChart());
            if (showXxy)
                addLineChart(lineSection, EzSettingsProfile.LOCAL_PROFILE_XXY_PLAY_LINE, createXxyLineChart());

            if (rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID)
                addLineChart(lineSection, EzSettingsProfile.LOCAL_PROFILE_MANIA_AVG_KPS_LINE, createManiaKpsLineChart());

            Add(lineSection);

            var barColumns = new List<Drawable>();

            barColumns.Add(createBarColumn(
                EzSettingsProfile.LOCAL_PROFILE_SECTION_STARS,
                EzLocalProfileBucketBars.FromStarPlayCounts(snapshot.StarPlayCounts.Where(s => s.RulesetId == rulesetId))));

            if (showXxy)
            {
                barColumns.Add(createBarColumn(
                    EzSettingsProfile.LOCAL_PROFILE_XXY_PLAY_DISTRIBUTION,
                    EzLocalProfileBucketBars.FromXxyPlayCounts(snapshot.XxyPlayCounts.Where(s => s.RulesetId == rulesetId))));
            }

            if (rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID)
            {
                barColumns.Add(createBarColumn(
                    EzSettingsProfile.LOCAL_PROFILE_MANIA_PLAYS_BY_KEY,
                    new EzLocalProfileManiaKeyPlayBars(snapshot.ManiaKeyStats.ToList())));
            }

            Add(new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                ColumnDimensions = barColumns.Select(_ => new Dimension(GridSizeMode.Distributed)).ToArray(),
                Content = new[] { barColumns.ToArray() },
            });

            if (rulesetId == EzLocalProfileConstants.MANIA_RULESET_ID)
                Add(createManiaExpandableBlock());

            if (rulesetId == EzLocalProfileConstants.OSU_RULESET_ID)
                Add(new EzLocalProfileStdAffinityBlock(snapshot));
        }

        private Drawable createManiaExpandableBlock()
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
            };

            var keyStats = snapshot.ManiaKeyStats.OrderBy(k => k.KeyCount).ToList();

            if (keyStats.Count == 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return flow;
            }

            foreach (var key in keyStats)
            {
                var columns = snapshot.ManiaColumnStats
                                      .Where(c => c.KeyCount == key.KeyCount)
                                      .OrderBy(c => c.ColumnIndex)
                                      .ToList();
                flow.Add(new EzLocalProfileExpandableRow(key, columns));
            }

            return flow;
        }

        private EzLocalProfileLabeledLineChart? createStarLineChart()
        {
            var list = snapshot.StarPlayCounts.Where(s => s.RulesetId == rulesetId).OrderBy(s => s.StarBucket).ToList();
            if (list.Count == 0)
                return null;

            return new EzLocalProfileLabeledLineChart(
                list.Select(s => (float)s.Count).ToArray(),
                list.Select(s => $"{s.StarBucket}★").ToArray());
        }

        private EzLocalProfileLabeledLineChart? createXxyLineChart()
        {
            var list = snapshot.XxyPlayCounts.Where(s => s.RulesetId == rulesetId).OrderBy(s => s.StarBucket).ToList();
            if (list.Count == 0)
                return null;

            return new EzLocalProfileLabeledLineChart(
                list.Select(s => (float)s.Count).ToArray(),
                list.Select(s => $"{s.StarBucket}xxy").ToArray());
        }

        private EzLocalProfileLabeledLineChart? createManiaKpsLineChart()
        {
            var ordered = snapshot.ManiaKeyStats.OrderBy(k => k.KeyCount).ToList();
            if (ordered.Count == 0)
                return null;

            return new EzLocalProfileLabeledLineChart(
                ordered.Select(k => (float)k.AvgKps).ToArray(),
                ordered.Select(k => $"{k.KeyCount}K").ToArray());
        }

        private static void addLineChart(FillFlowContainer parent, LocalisableString title, EzLocalProfileLabeledLineChart? chart)
        {
            if (chart == null)
                return;

            parent.Add(new OsuSpriteText
            {
                Text = title,
                Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
            });
            parent.Add(chart);
        }

        private static Drawable createBarColumn(LocalisableString title, Drawable body) =>
            new EzLocalProfileChartCard(title, body);
    }
}
