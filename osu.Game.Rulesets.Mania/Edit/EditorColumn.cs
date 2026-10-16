// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Edit
{
    public partial class EditorColumn : Column
    {
        public EditorColumn(int index, bool isSpecial)
            : base(index, isSpecial)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // With the skin's column background gone, column boundaries have to be marked here. Drawn behind the notes
            // (the column background layer is proxied below the playfield) so it reads as a divider, not an overlay.
            BackgroundContainer.Add(new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 1,
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Colour = Colour4.White.Opacity(0.25f),
            });
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
        {
            base.OnNewDrawableHitObject(drawableHitObject);
            drawableHitObject.ApplyCustomUpdateState += (dho, state) =>
            {
                switch (dho)
                {
                    // hold note heads are exempt from what follows due to the "freezing" mechanic
                    // which already ensures they'll never fade away on their own.
                    case DrawableHoldNoteHead:
                        break;

                    // mania features instantaneous hitobject fade-outs.
                    // this means that without manual intervention stopping the clock at the precise time of hitting the object
                    // means the object will fade out.
                    // this is anti-user in editor contexts, as the user is expecting to continue the see the note on the receptor line.
                    // therefore, apply a crude workaround to prevent it from going away.
                    default:
                    {
                        if (state == ArmedState.Hit)
                            dho.FadeTo(1).Delay(1).FadeOut().Expire();
                        break;
                    }
                }
            };
        }
    }
}
