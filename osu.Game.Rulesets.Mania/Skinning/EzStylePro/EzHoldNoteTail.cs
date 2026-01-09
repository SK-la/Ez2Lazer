// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteTail : EzNoteBase
    {
        private readonly EzHoldNoteHittingLayer hittingLayer = null!;
        private TextureAnimation? animation;
        private Container container = null!;

        private Bindable<bool> enabledColor = null!;
        private Bindable<double> tailAlpha = null!;

        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0f;

            if (drawableObject != null)
            {
                // accentColour.BindTo(drawableObject.AccentColour);
                // accentColour.BindValueChanged(onAccentChanged, true);

                drawableObject.HitObjectApplied += hitObjectApplied;
            }

            enabledColor = EzSkinConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            tailAlpha = EzSkinConfig.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha);
            tailAlpha.BindValueChanged(alpha =>
            {
                Alpha = (float)alpha.NewValue;
            }, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (drawableObject != null)
                drawableObject.HitObjectApplied -= hitObjectApplied;
        }

        protected virtual string ComponentSuffix => "longnote/tail";
        protected virtual string ComponentName => $"{ColorPrefix}{ComponentSuffix}";

        protected override void OnDrawableChanged()
        {
            ClearInternal();
            animation = Factory.CreateAnimation(ComponentName);

            if (animation.FrameCount == 0)
            {
                animation.Dispose();
                animation = Factory.CreateAnimation($"{ColorPrefix}note");
            }

            container = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Masking = true,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Child = animation,
                }
            };

            if (enabledColor.Value)
                container.Colour = NoteColor;
            Schedule(() =>
            {
                Invalidate();
            });

            AddInternal(container);
        }

        private void hitObjectApplied(DrawableHitObject drawableHitObject)
        {
            var holdNoteTail = (DrawableHoldNoteTail)drawableHitObject;

            // hittingLayer.AccentColour.UnbindBindings();
            // hittingLayer.AccentColour.BindTo(holdNoteTail.HoldNote.AccentColour);

            hittingLayer.IsHitting.UnbindBindings();
            ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNoteTail.HoldNote.IsHolding);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Scale = new Vector2(1, direction.NewValue == ScrollingDirection.Up ? -1 : 1);
        }
    }
}
