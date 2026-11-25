// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Ez 主题标准轨道滑条：左右拖动改值，未拖动单击重置。
    /// </summary>
    public partial class EzRailDragSlider : ScreenFooterButton
    {
        private const float rail_height = 6;
        private const float tick_height = 16;

        private const double fallback_min = -200;
        private const double fallback_max = 200;

        public IconUsage NubIcon
        {
            set => nub.Icon = value;
        }

        /// <summary>
        /// 滑块宽度
        /// </summary>
        public float NubWidth { get; }

        /// <summary>
        /// 细轨槽两侧边距
        /// </summary>
        private float layoutSideMargin => NubWidth * 0.5f;

        /// <summary>
        /// 被调整的数值
        /// </summary>
        public Bindable<double> Current { get; }

        private readonly Container rail;
        private readonly ScreenFooterButton nub;
        private readonly Func<double, string> formatValue;

        private float? relativeNormalizedAtDragStart;
        private readonly BindableNumber<double>? currentNumber;

        private float centreTravel;

        public LocalisableString Tooltip
        {
            set
            {
                nub.TooltipText = value;
                TooltipText = value;
            }
        }

        /// <summary>
        /// Creates a slider that adjusts the supplied bindable value.
        /// </summary>
        /// <param name="current">The value to adjust.</param>
        /// <param name="formatValue">Formats the value displayed by the nub.</param>
        /// <param name="nubWidth">The width of the nub in screen space.</param>
        public EzRailDragSlider(Bindable<double> current, Func<double, string>? formatValue = null, float nubWidth = 80)
        {
            if (!float.IsFinite(nubWidth) || nubWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(nubWidth));

            Current = current;
            NubWidth = nubWidth;
            currentNumber = current as BindableNumber<double>;
            this.formatValue = formatValue ?? (v => v.ToString(@"+#;-#;0"));

            Enabled.Value = true;
            Height = HEIGHT;
            Width = 260;

            nub = new ScreenFooterButton
            {
                RelativeSizeAxes = Axes.Y,
                Height = 1,
                Width = NubWidth,
                AccentBarWidth = NubWidth,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                FlashLayerColour = Colour4.White.Opacity(0.15f),
                Action = reset,
            };

            rail = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Height = rail_height,
                Masking = true,
                CornerRadius = rail_height / 2f,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.White.Opacity(0.15f),
                },
            };

            var tick = new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(2, tick_height),
                Colour = Colour4.White.Opacity(0.35f),
            };

            Content.Add(new EzBoxElement());
            Content.Add(rail);
            Content.Add(tick);
            Content.Add(nub);
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            nub.AccentColour = colours.Yellow;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            buildLayout();
            Current.BindValueChanged(applyNubPosition, true);
        }

        private void applyNubPosition(ValueChangedEvent<double> c)
        {
            nub.X = normalizedValue * centreTravel;
            nub.Text = formatValue(c.NewValue);
        }

        public override void UpdateDisplay()
        {
            base.UpdateDisplay();
            // footer 只对顶层按钮调 UpdateDisplay。
            nub.UpdateDisplay();
        }

        private float usableWidth => DrawWidth - 2 * layoutSideMargin;

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            relativeNormalizedAtDragStart = normalizedValue;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (e.Button != MouseButton.Left || relativeNormalizedAtDragStart == null)
                return;

            float travel = usableWidth;
            if (travel <= 0)
                return;

            float localX = ToLocalSpace(e.ScreenSpaceMousePosition).X;
            float downX = ToLocalSpace(e.ScreenSpaceMouseDownPosition).X;
            float newNormalized = relativeNormalizedAtDragStart.Value + (localX - downX) / travel;

            setProportional(Math.Clamp(newNormalized, 0, 1));
            base.OnDrag(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            reset();
            return base.OnClick(e);
        }

        protected override void Flash()
        {
        }

        private void reset() => Current.SetDefault();

        private float normalizedValue
        {
            get
            {
                if (currentNumber?.HasDefinedRange == true)
                    return currentNumber.NormalizedValue;

                var (min, max) = valueRange;
                return max > min ? (float)Math.Clamp((Current.Value - min) / (max - min), 0, 1) : 0.5f;
            }
        }

        private void setProportional(float amt)
        {
            if (currentNumber?.HasDefinedRange == true)
            {
                currentNumber.SetProportional(amt);
                return;
            }

            var (min, max) = valueRange;
            Current.Value = min + (max - min) * amt;
        }

        private (double min, double max) valueRange
        {
            get
            {
                if (currentNumber?.HasDefinedRange == true)
                    return (currentNumber.MinValue, currentNumber.MaxValue);

                if (Current is IBindableNumber<double> number
                    && number.MaxValue > number.MinValue
                    && double.IsFinite(number.MaxValue - number.MinValue))
                {
                    return (number.MinValue, number.MaxValue);
                }

                return (fallback_min, fallback_max);
            }
        }

        private void buildLayout()
        {
            float width = DrawWidth;
            float padding = layoutSideMargin;

            centreTravel = Math.Max(0, usableWidth);

            float shear = MathF.Abs(OsuGame.SHEAR.X) * DrawHeight;
            float railEnd = width - padding - shear;

            rail.X = padding;
            rail.Width = Math.Max(0, railEnd - padding);
        }
    }
}
