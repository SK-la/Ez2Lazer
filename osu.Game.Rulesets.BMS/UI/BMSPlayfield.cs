// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Objects.Drawables;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// BMS playfield layout. Double-play stage gap (<see cref="BMSRulesetSetting.DpStageSpacing"/>) is implemented here on the BMS side
    /// (not mania <see cref="Mania.UI.Stage"/>) until/unless promoted upstream.
    /// Scratch lane width uses <see cref="BMSStageLayout.ScratchColumnIndices"/> when present; otherwise a legacy heuristic by column count.
    /// </summary>
    [Cached]
    public partial class BMSPlayfield : ScrollingPlayfield
    {
        public const float COLUMN_WIDTH = Mania.UI.Column.COLUMN_WIDTH;
        public const float SCRATCH_WIDTH = Mania.UI.Column.SPECIAL_COLUMN_WIDTH;

        /// <summary>
        /// Required for <see cref="Mania.UI.Column"/> load (match key count for mania skin / config).
        /// </summary>
        [Cached]
        public readonly StageDefinition StageDefinition;

        private readonly int totalColumns;
        private readonly Container<BMSColumn> columns;
        private readonly BindableDouble dpStageSpacing = new BindableDouble();

        public BMSPlayfield(BMSStageLayout layout)
        {
            var scratchSet = new HashSet<int>(layout.ScratchColumnIndices);
            totalColumns = layout.TotalColumns;
            StageDefinition = new StageDefinition(totalColumns);

            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChild = columns = new Container<BMSColumn>
            {
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
            };

            for (int i = 0; i < totalColumns; i++)
            {
                bool isScratch = scratchSet.Count > 0
                    ? scratchSet.Contains(i)
                    : legacyScratchHeuristic(i, totalColumns);

                float width = isScratch ? SCRATCH_WIDTH : COLUMN_WIDTH;

                columns.Add(new BMSColumn(i, isScratch)
                {
                    Width = width,
                    RelativeSizeAxes = Axes.Y,
                });
            }

            updateColumnLayout();
        }

        /// <summary>
        /// Fallback when beatmap does not mark scratch lanes (empty <see cref="BMSStageLayout.ScratchColumnIndices"/>).
        /// </summary>
        private static bool legacyScratchHeuristic(int columnIndex, int columnsTotal) =>
            columnsTotal switch
            {
                6 or 8 => columnIndex == 0,
                12 or 16 => columnIndex == 0 || columnIndex == columnsTotal / 2,
                _ => false,
            };

        [BackgroundDependencyLoader]
        private void load(BMSRulesetConfigManager config)
        {
            config.BindWith(BMSRulesetSetting.DpStageSpacing, dpStageSpacing);
            dpStageSpacing.BindValueChanged(_ => updateColumnLayout(), true);
        }

        private void updateColumnLayout()
        {
            float x = 0;
            int midpoint = totalColumns / 2;
            bool isDpLayout = totalColumns is 10 or 14 or 16 or 18;

            for (int i = 0; i < columns.Count; i++)
            {
                columns[i].X = x;
                x += columns[i].Width;

                if (isDpLayout && i == midpoint - 1)
                    x += (float)dpStageSpacing.Value;
            }
        }

        public override void Add(DrawableHitObject hitObject)
        {
            if (hitObject is DrawableBMSHitObject bmsHitObject)
            {
                columns[bmsHitObject.HitObject.Column].Add(hitObject);
            }
            else
            {
                base.Add(hitObject);
            }
        }

        public override bool Remove(DrawableHitObject hitObject)
        {
            if (hitObject is DrawableBMSHitObject bmsHitObject)
            {
                return columns[bmsHitObject.HitObject.Column].Remove(hitObject);
            }

            return base.Remove(hitObject);
        }
    }
}
