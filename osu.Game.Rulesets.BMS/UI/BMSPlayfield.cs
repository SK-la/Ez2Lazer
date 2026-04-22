// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.BMS.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.BMS.UI
{
    [Cached]
    public partial class BMSPlayfield : ScrollingPlayfield
    {
        public const float COLUMN_WIDTH = 60f;
        public const float SCRATCH_WIDTH = 80f;

        private readonly int totalColumns;
        private readonly Container<BMSColumn> columns;

        public BMSPlayfield(int totalColumns)
        {
            this.totalColumns = totalColumns;

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
                bool isScratch = i == 0 || i == totalColumns / 2;
                float width = isScratch ? SCRATCH_WIDTH : COLUMN_WIDTH;

                columns.Add(new BMSColumn(i)
                {
                    Width = width,
                    RelativeSizeAxes = Axes.Y,
                });
            }

            // Position columns
            float x = 0;
            foreach (var column in columns)
            {
                column.X = x;
                x += column.Width;
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
