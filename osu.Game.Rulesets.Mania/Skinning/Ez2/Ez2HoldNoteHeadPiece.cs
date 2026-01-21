// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    internal partial class Ez2HoldNoteHeadPiece : Ez2NotePiece
    {
        protected override Drawable CreateIcon() => new Box
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(20, 5),
            Y = 0,
            Rotation = 0,
        };

        protected override void Update()
        {
            base.Update();
            Height = DrawWidth;
        }
    }
}
