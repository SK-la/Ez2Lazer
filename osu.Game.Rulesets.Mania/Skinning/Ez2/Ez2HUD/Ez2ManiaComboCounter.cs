// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play.HUD;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class Ez2ManiaComboCounter : ComboCounter
    {
        protected EzComCounterText Text = null!;
        public IBindable<float> WireframeOpacity { get; } = new BindableFloat();
        protected override double RollingDuration => 250;
        protected virtual bool DisplayXSymbol => true;

        [Resolved]
        private IScrollingInfo scrollingInfo { get; set; } = null!;

        private IBindable<ScrollingDirection> direction = null!;

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.ShowLabel), nameof(SkinnableComponentStrings.ShowLabelDescription))]
        public Bindable<bool> ShowLabel { get; } = new BindableBool(true);

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
            Current.BindTo(scoreProcessor.Combo);
            Current.BindValueChanged(combo =>
            {
                bool wasIncrease = combo.NewValue > combo.OldValue;
                bool wasMiss = combo.OldValue > 1 && combo.NewValue == 0;

                float newScale = Math.Clamp(Text.NumberContainer.Scale.X * (wasIncrease ? 3f : 1f), 0.6f, 3f);

                float duration = wasMiss ? 2000 : 500;

                Text.NumberContainer
                    .ScaleTo(new Vector2(newScale))
                    .ScaleTo(Vector2.One, duration, Easing.OutQuint);

                if (wasMiss)
                    Text.FlashColour(Color4.Red, duration, Easing.OutQuint);
            });
        }

        private int getDigitsRequiredForDisplayCount()
        {
            // one for the single presumed starting digit, one for the "x" at the end (unless disabled).
            int digitsRequired = DisplayXSymbol ? 2 : 1;
            long c = DisplayedCount;
            while ((c /= 10) > 0)
                digitsRequired++;
            return digitsRequired;
        }

        protected override LocalisableString FormatCount(int count) => DisplayXSymbol ? $@"{count}" : count.ToString();

        protected override IHasText CreateText() => Text = new EzComCounterText(Anchor.TopCentre, MatchesStrings.MatchScoreStatsCombo.ToUpper())
        {
            WireframeOpacity = { BindTarget = WireframeOpacity },
            ShowLabel = { BindTarget = ShowLabel },
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            UsesFixedAnchor = true;

            direction = scrollingInfo.Direction.GetBoundCopy();
            direction.BindValueChanged(_ => updateAnchor());

            // 需要两个调度，以便在下一帧执行 updateAnchor，
            // 这是 combo 计数器通过 Ez2ManiaSkinTransformer 的默认布局接收其 Y 位置的时间。
            Schedule(() => Schedule(updateAnchor));
        }

        private void updateAnchor()
        {
            // 如果锚点不是垂直中心，则根据滚动方向设置顶部或底部锚点
            if (Anchor.HasFlag(Anchor.y1))
                return;

            Anchor &= ~(Anchor.y0 | Anchor.y2);
            Anchor |= direction.Value == ScrollingDirection.Up ? Anchor.y2 : Anchor.y0;

            // 根据滚动方向更改 Y 坐标的符号。
            // 即，如果用户将方向从下更改为上，锚点从顶部更改为底部，Y 从正数翻转为负数。
            Y = Math.Abs(Y) * (direction.Value == ScrollingDirection.Up ? -1 : 1);
        }
    }
}
