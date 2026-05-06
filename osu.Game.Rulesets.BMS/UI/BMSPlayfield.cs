// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
    /// Scratch lane identity follows decoder/global column mapping; DP spacing only affects layout between the two stages.
    /// </summary>
    [Cached]
    public partial class BMSPlayfield : ScrollingPlayfield
    {
        public const float COLUMN_WIDTH = 60f;
        public const float SCRATCH_WIDTH = 80f;

        /// <summary>
        /// Required for <see cref="Mania.UI.Column"/> load (match key count for mania skin / config).
        /// </summary>
        [Cached]
        public readonly StageDefinition StageDefinition;

        private readonly int totalColumns;
        private readonly Container<BMSColumn> columns;
        private readonly BindableDouble dpStageSpacing = new BindableDouble();

        public BMSPlayfield(int totalColumns)
        {
            this.totalColumns = totalColumns;
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
                bool isScratch = totalColumns switch
                {
                    6 or 8 => i == 0,
                    12 or 16 => i == 0 || i == totalColumns / 2,
                    _ => false,
                };
                float width = isScratch ? SCRATCH_WIDTH : COLUMN_WIDTH;

                columns.Add(new BMSColumn(i, isScratch)
                {
                    Width = width,
                    RelativeSizeAxes = Axes.Y,
                });
            }

            updateColumnLayout();
        }

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
