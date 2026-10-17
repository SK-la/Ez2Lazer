// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Mania.UI.Components
{
    /// <summary>
    /// The judgement line, spanning the width of the surface it is placed on.
    /// </summary>
    /// <remarks>
    /// The line sits on the edge of the hit object area, not in its middle: when notes scroll downwards the hit position is
    /// the bottom edge, and when they scroll upwards it is the top edge. The surface therefore has to be the hit object
    /// area itself — a <see cref="HitPositionPaddedContainer"/> on the playfield, or a surface already limited to the hit
    /// objects' span (the editor's extension columns) — rather than the full column.
    /// </remarks>
    public partial class ManiaJudgementLine : CompositeDrawable
    {
        public const float LINE_HEIGHT = 1;

        /// <summary>
        /// Opacity of the line when shown.
        /// </summary>
        public const float LINE_ALPHA = 0.9f;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        public ManiaJudgementLine()
        {
            RelativeSizeAxes = Axes.X;
            Height = LINE_HEIGHT;
            Alpha = LINE_ALPHA;

            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Colour4.White,
            };
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(d => placeAtHitPosition(d.NewValue), true);

            // TODO: 以后实现自定义贴图，JudgementLine。
        }

        private void placeAtHitPosition(ScrollingDirection scrollingDirection)
        {
            Anchor = Origin = scrollingDirection == ScrollingDirection.Up ? Anchor.TopLeft : Anchor.BottomLeft;
        }
    }
}
