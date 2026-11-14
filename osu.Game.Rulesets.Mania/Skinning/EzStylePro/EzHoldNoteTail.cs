// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    /// <summary>
    /// 优先 HoldTail 部件；缺失时复用 <see cref="EzHoldNoteHead"/> 的加载与裁切逻辑，并整体旋转 180°。
    /// </summary>
    public partial class EzHoldNoteTail : EzNote
    {
        protected override ManiaNoteDisplayColourPart DisplayColourPart => ManiaNoteDisplayColourPart.HoldBody;

        protected override bool ShowSeparators => false;

        private readonly EzHoldNoteHittingLayer hittingLayer = new EzHoldNoteHittingLayer();

        private Drawable? tailDrawable;

        // Clipping container for the full-note fallback. Cached so layout never reads the single-child property while it is empty.
        private Container? topHalfMask;

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
            useNoteTopHalfLayout = false;
            topHalfMask = null;

            if (gradient)
                return;

            tailDrawable = LoadEzProNoteComponent(EzManiaNotePart.HoldTail);

            if (tailDrawable != null)
            {
                MainContainer.Rotation = 0;
                MainContainer.Child = tailDrawable;
                return;
            }

            // 与 EzHoldNoteHead 相同：HoldHead → Note
            tailDrawable = LoadEzProNoteComponent(EzManiaNotePart.HoldHead);

            if (tailDrawable != null)
            {
                MainContainer.Rotation = 180;
                MainContainer.Child = tailDrawable;
                return;
            }

            tailDrawable = LoadEzProNoteComponent(EzManiaNotePart.Note);

            if (tailDrawable == null)
                return;

            useNoteTopHalfLayout = true;
            applyNoteTopHalfLayout();
        }

        private void applyNoteTopHalfLayout()
        {
            if (tailDrawable == null)
                return;

            if (useNoteTopHalfLayout)
            {
                MainContainer.Anchor = Anchor.TopCentre;
                MainContainer.Origin = Anchor.TopCentre;
                MainContainer.RelativeSizeAxes = Axes.X;
                MainContainer.Masking = true;
                MainContainer.Child = topHalfMask = new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.X,
                    Masking = true,
                    Child = tailDrawable,
                };
            }
            else
            {
                // 与 EzHoldNoteHead 有 head 资源时一致，仅附加旋转
                MainContainer.Child = tailDrawable;
            }
        }

        protected override void UpdateDrawable()
        {
            if (gradient)
                return;

            Height = NoteHeight;

            if (useNoteTopHalfLayout && topHalfMask != null)
            {
                MainContainer.Height = NoteHeight / 2;
                topHalfMask.Height = NoteHeight;
                // topHalfMask.Y = -NoteHeight / 2;
            }
        }

        protected override void UpdateColor()
        {
            if (gradient)
                return;

            MainContainer.Colour = ColourInfo.GradientVertical(
                Colour4.White.Opacity((float)tailAlpha.Value),
                Colour4.White);
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

            tailDrawable = null;
            base.Dispose(isDisposing);
        }
    }
}
