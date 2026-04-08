// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    public partial class SbINotePiece : EzNoteBase
    {
        protected override bool UseColorization => true;

        protected readonly IBindable<double> CornerRadiusBindable = new Bindable<double>();
        // private readonly IBindable<Colour4> columnColour = new Bindable<Colour4>();

        private Box box = null!;

        private const float corner = 5;

        public SbINotePiece()
        {
            RelativeSizeAxes = Axes.X;
            Masking = true;

            CornerRadius = corner;
        }

        [BackgroundDependencyLoader(true)]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            CornerRadiusBindable.BindTo(ezSkinInfo.NoteCornerRadius);
            CornerRadiusBindable.BindValueChanged(_ => UpdateDrawable());
        }

        protected override void UpdateTexture()
        {
            if (MainContainer != null)
            {
                MainContainer.Child = box = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 8,
                };
            }

            UpdateDrawable();
        }

        protected override void UpdateDrawable()
        {
            // 确保父 drawable 有合适的高度，否则启用 Masking 时子元素会被裁剪不可见。
            // 这里将自身高度与 note 尺寸同步，并设置主体 box 的高度。
            float noteHeight = NoteSizeBindable.Value.Y;
            Height = noteHeight;
            box.Height = noteHeight;
            CornerRadius = (float)CornerRadiusBindable.Value;
        }

        // // 备用：实现异色外描边
        // private void onAccentChanged(ValueChangedEvent<Colour4> accent)
        // {
        //     var c = accent.NewValue;
        //     var color = new Color4(c.R, c.G, c.B, c.A);
        //     colouredBox.Colour = ColourInfo.GradientVertical(
        //         color.Lighten(0.1f),
        //         color
        //     );
        // }
    }
}
