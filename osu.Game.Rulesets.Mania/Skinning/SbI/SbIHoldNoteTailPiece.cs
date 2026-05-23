// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    /// <summary>
    /// <see cref="Ez2Setting.ManiaLNGradientEnable"/> 开启时隐藏，由 <see cref="SbIHoldBodyPiece"/> 代替。
    /// 关闭时：完整 note 高度绘制，裁切容器仅显示上半区，露出顶部两个圆角。
    /// </summary>
    public partial class SbIHoldNoteTailPiece : FastNoteBase
    {
        private Container innerContainer = null!;
        private Box note = null!;

        private readonly IBindable<double> tailAlphaBindable = new Bindable<double>();

        private bool gradient;

        public SbIHoldNoteTailPiece()
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo, Ez2ConfigManager ezConfig)
        {
            MainContainer.RelativeSizeAxes = Axes.X;
            MainContainer.Anchor = Anchor.TopCentre;
            MainContainer.Origin = Anchor.TopCentre;
            MainContainer.Masking = true;
            MainContainer.Child = innerContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Masking = true,
                Child = note = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

            gradient = ezConfig.Get<bool>(Ez2Setting.ManiaLNGradientEnable);

            tailAlphaBindable.BindTo(ezSkinInfo.HoldTailAlpha);
            tailAlphaBindable.BindValueChanged(_ => UpdateColor(), true);

            ezSkinInfo.ManiaLNGradientEnable.BindValueChanged(e =>
            {
                if (gradient == e.NewValue)
                    return;

                gradient = e.NewValue;
                Alpha = gradient ? 0 : 1;
                UpdateDrawable();
                UpdateColor();
            });

            Alpha = gradient ? 0 : 1;
        }

        protected override void UpdateDrawable()
        {
            if (gradient)
                return;

            if (DrawWidth <= 1)
            {
                Schedule(UpdateDrawable);
                return;
            }

            float radius = (float)CornerRadiusBindable.Value;
            float height = UnitHeight;
            float halfHeight = height / 2;

            Height = halfHeight;
            MainContainer.Height = halfHeight;
            innerContainer.Height = height;
            innerContainer.CornerRadius = radius;
        }

        protected override void UpdateColor()
        {
            if (gradient)
                return;

            note.Colour = ColourInfo.GradientVertical(
                NoteColor.Opacity((float)tailAlphaBindable.Value),
                NoteColor);
        }
    }
}
