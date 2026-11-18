// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Overlays;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Ez 主题组件底板元素：微渐变背景、微圆角、斜切。
    /// </summary>
    public partial class EzBoxElement : Container
    {
        /// <summary>
        /// 切换状态颜色
        /// </summary>
        public readonly Bindable<Visibility> OverlayState = new Bindable<Visibility>();

        private readonly Box backgroundBox;
        private readonly Box glowBox;
        private Colour4 buttonAccentColour;
        private int lastHue;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public Colour4 AccentColour
        {
            set
            {
                buttonAccentColour = value;
                updateDisplay();
            }
        }

        public EzBoxElement()
        {
            RelativeSizeAxes = Axes.Both;

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 4,
                    Colour = Colour4.Black.Opacity(0.25f),
                    Offset = new Vector2(0, 2),
                },
                Shear = OsuGame.SHEAR,
                Masking = true,
                CornerRadius = ScreenFooterButton.CORNER_RADIUS,
                Children = new Drawable[]
                {
                    backgroundBox = new Box { RelativeSizeAxes = Axes.Both },
                    glowBox = new Box { RelativeSizeAxes = Axes.Both },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            lastHue = colourProvider.Hue;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            OverlayState.BindValueChanged(_ => updateDisplay(), true);
        }

        protected override void Update()
        {
            base.Update();

            if (colourProvider.Hue != lastHue)
            {
                lastHue = colourProvider.Hue;
                updateDisplay();
            }
        }

        private void updateDisplay()
        {
            bool active = OverlayState.Value == Visibility.Visible;

            backgroundBox.Colour = active ? buttonAccentColour : colourProvider.Background3;

            var glowTop = active ? buttonAccentColour.Opacity(0.2f) : buttonAccentColour.Opacity(0f);
            var glowBottom = active ? buttonAccentColour.Opacity(0f) : buttonAccentColour.Opacity(0.2f);
            glowBox.Colour = ColourInfo.GradientVertical(glowTop, glowBottom);
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => false;
    }
}
