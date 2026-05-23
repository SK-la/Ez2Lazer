// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    /// <summary>
    /// 优先 <see cref="EzNoteBase.TailName"/>；缺失时复用 <see cref="EzHoldNoteHead"/> 的加载与裁切逻辑，并整体旋转 180°。
    /// </summary>
    public partial class EzHoldNoteTail : EzNote
    {
        protected override bool UseColorization => true;

        protected override bool ShowSeparators => false;

        private readonly EzHoldNoteHittingLayer hittingLayer = new EzHoldNoteHittingLayer();

        private TextureAnimation? animation;

        private IBindable<double> tailAlpha = null!;

        private bool gradient;
        private bool useNoteTopHalfLayout;

        [Resolved]
        private DrawableHitObject? drawableObject { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject, IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;

            gradient = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);

            if (gradient)
                Alpha = 0;

            ezSkinInfo.ManiaLNGradientEnable.BindValueChanged(e =>
            {
                if (gradient == e.NewValue)
                    return;

                gradient = e.NewValue;
                Alpha = gradient ? 0 : 1f;
                OnLoadChanged();
            });

            tailAlpha = ezSkinInfo.HoldTailAlpha;
            tailAlpha.BindValueChanged(_ => OnColourChanged(), true);

            if (drawableObject != null)
                drawableObject.HitObjectApplied += hitObjectApplied;
        }

        protected override void UpdateTexture()
        {
            if (gradient)
                return;

            useNoteTopHalfLayout = false;

            animation = Factory.CreateAnimation(TailName);

            if (animation.FrameCount > 0)
            {
                MainContainer.Rotation = 0;
                MainContainer.Child = animation;
                return;
            }

            animation.Dispose();

            // 与 EzHoldNoteHead 相同：HeadName -> NoteName
            animation = Factory.CreateAnimation(HeadName);

            if (animation.FrameCount > 0)
            {
                MainContainer.Rotation = 180;
                MainContainer.Child = animation;
                return;
            }

            animation.Dispose();
            animation = Factory.CreateAnimation(NoteName);

            if (animation.FrameCount == 0)
            {
                animation = null;
                return;
            }

            useNoteTopHalfLayout = true;
            applyNoteTopHalfLayout();
        }

        private void applyNoteTopHalfLayout()
        {
            if (animation == null)
                return;

            if (useNoteTopHalfLayout)
            {
                MainContainer.Anchor = Anchor.TopCentre;
                MainContainer.Origin = Anchor.TopCentre;
                MainContainer.RelativeSizeAxes = Axes.X;
                MainContainer.Masking = true;
                MainContainer.Child = new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.X,
                    Masking = true,
                    Child = animation,
                };
            }
            else
            {
                // 与 EzHoldNoteHead 有 head 资源时一致，仅附加旋转
                MainContainer.Child = animation;
            }
        }

        protected override void UpdateDrawable()
        {
            if (gradient)
                return;

            Height = NoteHeight;

            if (useNoteTopHalfLayout && MainContainer.Child is Container c)
            {
                MainContainer.Height = NoteHeight / 2;
                c.Height = NoteHeight;
                // c.Y = -NoteHeight / 2;
            }
        }

        protected override void UpdateColor()
        {
            if (gradient)
                return;

            MainContainer.Colour = ColourInfo.GradientVertical(
                NoteColor.Opacity((float)tailAlpha.Value),
                NoteColor);
        }

        private void hitObjectApplied(DrawableHitObject drawableHitObject)
        {
            var holdNoteTail = (DrawableHoldNoteTail)drawableHitObject;

            ((IBindable<bool>)hittingLayer.IsHitting).UnbindBindings();
            ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNoteTail.HoldNote.IsHolding);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (drawableObject.IsNotNull())
                drawableObject.HitObjectApplied -= hitObjectApplied;

            animation = null;
            base.Dispose(isDisposing);
        }
    }
}
