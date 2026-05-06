// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// A single column in the BMS playfield. Reuses mania skin components via a hidden <see cref="Mania.UI.Column"/>
    /// (<see cref="skinDependencyColumn"/>): <see cref="Mania.UI.Column.Index"/> matches this lane,
    /// <see cref="Mania.UI.Column.IsSpecial"/> mirrors BMS scratch, and <see cref="Mania.UI.Column.Action"/> follows <see cref="BMSStageLayout.ManiaSkinActionForColumn"/>.
    /// </summary>
    public partial class BMSColumn : ScrollingPlayfield
    {
        public readonly int ColumnIndex;

        /// <summary>
        /// Provides <c>Resolved</c> <see cref="Column"/> for <see cref="DefaultColumnBackground"/>, <see cref="DefaultHitTarget"/>, <see cref="DefaultKeyArea"/> and skin colour lookup.
        /// </summary>
        [Cached]
        private readonly Column skinDependencyColumn;

        private readonly SkinnableDrawable skinColumnBackground;
        private readonly SkinnableDrawable skinHitTarget;
        private readonly SkinnableDrawable skinKeyArea;

        public BMSColumn(int columnIndex, bool isScratch)
        {
            ColumnIndex = columnIndex;

            RelativeSizeAxes = Axes.Y;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;

            skinDependencyColumn = new Column(columnIndex, isScratch)
            {
                Alpha = 0,
                RelativeSizeAxes = Axes.Both,
                Action = { Value = BMSStageLayout.ManiaSkinActionForColumn(columnIndex) }
            };

            InternalChildren = new Drawable[]
            {
                skinDependencyColumn,
                skinColumnBackground = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.ColumnBackground), _ => new DefaultColumnBackground())
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.3f,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 1,
                    Colour = Color4.White,
                    Alpha = 0.2f,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 20,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -100,
                    Child = skinHitTarget = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.HitTarget), _ => new DefaultHitTarget())
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                },
                skinKeyArea = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.KeyArea), _ => new DefaultKeyArea())
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.15f,
                },
                HitObjectContainer,
            };
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            skinDependencyColumn.ApplyGameWideClock(host);
            skinColumnBackground.ApplyGameWideClock(host);
            skinHitTarget.ApplyGameWideClock(host);
            skinKeyArea.ApplyGameWideClock(host);
        }

        public static Color4 GetColumnColour(int columnIndex)
        {
            return columnIndex switch
            {
                0 => new Color4(255, 0, 0, 255),
                1 => new Color4(255, 255, 255, 255),
                2 => new Color4(0, 150, 255, 255),
                3 => new Color4(255, 255, 255, 255),
                4 => new Color4(0, 150, 255, 255),
                5 => new Color4(255, 255, 255, 255),
                6 => new Color4(0, 150, 255, 255),
                7 => new Color4(255, 255, 255, 255),
                _ => new Color4(200, 200, 200, 255),
            };
        }
    }
}
