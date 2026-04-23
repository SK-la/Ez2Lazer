// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbIHitTarget : CompositeDrawable
    {
        public SbIHitTarget()
        {
            InternalChildren = new[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 1f,
                    Alpha = 0.5f,
                    Blending = BlendingParameters.Additive,
                    Colour = Color4.Black
                },
            };
        }
    }
}
