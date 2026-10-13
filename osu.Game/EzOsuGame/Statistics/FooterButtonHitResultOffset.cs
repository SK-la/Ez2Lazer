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
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Screens.Footer;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 结算界面底部的 HitResult Offset 控件：固定宽度的槽位里放一个 footer 按钮样式的滑块，
    /// 左右拖动滑块调整偏移量，不拖动地单击则重置为默认值。
    /// </summary>
    public partial class FooterButtonHitResultOffset : ScreenFooterButton
    {
        private const float slot_width = 300;
        private const float rail_height = 6;
        private const float tick_height = 16;
        private const float edge_inset = 8;

        private const double fallback_min = -200;
        private const double fallback_max = 200;

        /// <summary>
        /// 被调整的偏移量。
        /// </summary>
        public readonly Bindable<double> Current;

        private readonly ScreenFooterButton nub;
        private readonly Container railFill;
        private readonly Action<ValueChangedEvent<double>> updateValue;

        private double valueAtDragStart;
        private int lastHue;

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; } = null!;

        public FooterButtonHitResultOffset(Bindable<double> current)
        {
            Current = current;

            Size = new Vector2(slot_width, HEIGHT);
            // 槽位只承载轨道和拖动区域，不需要按钮的禁用态外观。
            Enabled.Value = true;

            nub = new ScreenFooterButton
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };
            nub.Action = reset;

            var rail = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = slot_width - 2 * edge_inset,
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

            railFill = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Height = rail_height,
                Masking = true,
                CornerRadius = rail_height / 2f,
            };

            Content.Add(rail);
            Content.Add(tick);
            Content.Add(railFill);
            Content.Add(nub);

            updateValue = _ => updateNub();
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            railFill.Add(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colours.Yellow,
            });

            nub.Icon = FontAwesome.Solid.ArrowsAltH;
            nub.AccentColour = colours.Yellow;
            nub.TooltipText = Strings.TOOLTIP;

            TooltipText = Strings.TOOLTIP;
            lastHue = overlayColourProvider.Hue;
        }

        protected override void Update()
        {
            base.Update();

            // footer 只对顶层按钮调用 UpdateDisplay，滑块需要自己跟随配色切换。
            if (overlayColourProvider.Hue != lastHue)
            {
                lastHue = overlayColourProvider.Hue;
                nub.UpdateDisplay();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(updateValue, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            Current.ValueChanged -= updateValue;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            valueAtDragStart = Current.Value;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (e.Button != MouseButton.Left)
                return;

            var (min, max) = valueRange;
            double unitsPerPixel = (max - min) / travel;

            // 以按下时的值为基准换算，越界由绑定自身收敛，往回拖不会留下死区。
            Current.Value = valueAtDragStart + (e.MousePosition.X - e.MouseDownPosition.X) * unitsPerPixel;
            base.OnDrag(e);
        }

        // 未触发拖动（框架在超过拖动阈值前不会发出拖动事件）的单击视为重置。
        protected override bool OnClick(ClickEvent e)
        {
            reset();
            return base.OnClick(e);
        }

        /// <summary>
        /// 滑块可移动的像素范围。
        /// </summary>
        private float travel => slot_width - nub.Width - 2 * edge_inset;

        /// <summary>
        /// 取值区间，优先使用绑定自身的上下限。
        /// </summary>
        private (double min, double max) valueRange
        {
            get
            {
                if (Current is IBindableNumber<double> number
                    && number.MaxValue > number.MinValue
                    && double.IsFinite(number.MaxValue - number.MinValue))
                {
                    return (number.MinValue, number.MaxValue);
                }

                return (fallback_min, fallback_max);
            }
        }

        private void reset() => Current.SetDefault();

        private void updateNub()
        {
            var (min, max) = valueRange;

            double progress = max > min ? Math.Clamp((Current.Value - min) / (max - min), 0, 1) : 0.5;

            nub.X = edge_inset + (float)(progress * travel);
            nub.Text = Current.Value.ToString(@"+#;-#;0");

            float nubCentre = nub.X + nub.Width / 2f;
            float slotCentre = slot_width / 2f;

            railFill.X = Math.Min(slotCentre, nubCentre);
            railFill.Width = Math.Abs(nubCentre - slotCentre);
        }

        private static class Strings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString TOOLTIP = new EzLocalizationManager.EzLocalisableString(
                "左右拖动调整 HitResult Offset，单击重置", "Drag left/right to adjust the hit result offset, click to reset");
        }
    }
}
