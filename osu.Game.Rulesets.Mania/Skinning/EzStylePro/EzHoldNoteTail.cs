// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteTail : EzNoteBase
    {
        private readonly EzHoldNoteHittingLayer hittingLayer = new EzHoldNoteHittingLayer();
        private TextureAnimation? animation;
        private Container container = null!;

        private IBindable<bool> enabledColor = null!;
        private IBindable<double> tailAlpha = null!;

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
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabledColor = Column.EzSkinInfo.ColorSettingsEnabled;
            tailAlpha = Column.EzSkinInfo.HoldTailAlpha;
            // Column-level notifications will trigger UpdateSize/UpdateColor; set initial alpha now
            Alpha = (float)tailAlpha.Value;
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

            AddInternal(container);
        }

        private void hitObjectApplied(DrawableHitObject drawableHitObject)
        {
            var holdNoteTail = (DrawableHoldNoteTail)drawableHitObject;

            // hittingLayer.AccentColour.UnbindBindings();
            // hittingLayer.AccentColour.BindTo(holdNoteTail.HoldNote.AccentColour);

            ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNoteTail.HoldNote.IsHolding);
        }
    }
}
