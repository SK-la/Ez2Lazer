// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    internal partial class Ez2HitExplosion : Ez2HitTarget, IHitExplosion
    {
        public override bool RemoveWhenNotAlive => true;

        [Resolved]
        private Column column { get; set; } = null!;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Container largeFaint = null!;

        private Bindable<Color4> accentColour = null!;

        public Ez2HitExplosion()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            // Blending = BlendingParameters.Additive;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            Size = new Vector2(2);
            Alpha = 1;

            InternalChildren = new Drawable[]
            {
                largeFaint = new Container
                {
                    Blending = BlendingParameters.Additive,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Masking = true,
                    Scale = new Vector2(0.5f),
                    Child = new Circle
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        // Scale = new Vector2(0.2f),
                        Alpha = 0.8f,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Glow,
                            Colour = new Color4(1f, 1f, 1f, 0.5f),
                            Radius = 2f, // 调整光晕半径
                            Roundness = 0f,
                        }
                    },
                },
            };

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            accentColour = column.AccentColour.GetBoundCopy();
            accentColour.BindValueChanged(colour =>
            {
                largeFaint.Colour = Interpolation.ValueAt(0.8f, colour.NewValue, Color4.White, 0, 1);

                largeFaint.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = colour.NewValue,
                    Roundness = NoteHeight,
                    Radius = 50,
                };
            }, true);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Anchor = Origin = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        public void Animate(JudgementResult result)
        {
            this.FadeOutFromOne(PoolableHitExplosion.DURATION, Easing.Out);
        }
    }
}
