// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Screens
{
    public partial class OsuColourPickerWithAlpha : ColourPicker
    {
        private readonly BindableDouble alphaBindable;
        private FillFlowContainer? mainContent;

        public OsuColourPickerWithAlpha()
        {
            CornerRadius = 10;
            Masking = true;

            alphaBindable = new BindableDouble(1)
            {
                MinValue = 0,
                MaxValue = 1,
                Precision = 0.05
            };
        }

        protected override HSVColourPicker CreateHSVColourPicker() => new OsuHSVColourPicker();
        protected override HexColourPicker CreateHexColourPicker() => new OsuHexColourPicker();

        [BackgroundDependencyLoader]
        private void load()
        {
            mainContent = InternalChildren.OfType<FillFlowContainer>().FirstOrDefault();

            // 添加透明度滑块到已有的布局容器中
            mainContent?.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Margin = new MarginPadding { Top = 8, Bottom = 15 },
                Child = new SettingsSlider<double>
                {
                    LabelText = "Alpha",
                    RelativeSizeAxes = Axes.X,
                    Current = alphaBindable
                }
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            alphaBindable.Value = Current.Value.A;

            // 当透明度值变化时更新颜色
            alphaBindable.BindValueChanged(alpha =>
            {
                // 创建新的颜色，只更改Alpha通道
                Current.Value = new Color4(
                    Current.Value.R,
                    Current.Value.G,
                    Current.Value.B,
                    (float)alpha.NewValue
                );
            });

            // 当颜色变化时更新透明度值
            Current.BindValueChanged(colour =>
            {
                // 防止循环绑定
                if (Math.Abs(alphaBindable.Value - colour.NewValue.A) > 0.001)
                    alphaBindable.Value = colour.NewValue.A;
            });
        }
    }
}
