// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzComComboCounter : ComboCounter
    {
        public EzComCounterText Text = null!;

        protected override double RollingDuration => 250;

        protected virtual bool DisplayXSymbol => true;

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.ShowLabel), nameof(SkinnableComponentStrings.ShowLabelDescription))]
        public Bindable<bool> ShowLabel { get; } = new BindableBool(true);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.TextElementText), nameof(SkinnableComponentStrings.TextElementTextDescription))]
        public Bindable<string> LabelContent { get; } = new Bindable<string>("Combo");

        [SettingSource("Alpha", "The alpha value of this box")]
        public BindableNumber<float> BoxAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour), nameof(SkinnableComponentStrings.ColourDescription))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
            Current.BindTo(scoreProcessor.Combo);
            Current.BindValueChanged(combo =>
            {
                bool wasIncrease = combo.NewValue > combo.OldValue;
                bool wasMiss = combo.OldValue > 1 && combo.NewValue == 0;

                float newScale = Math.Clamp(Text.NumberContainer.Scale.X * (wasIncrease ? 1.1f : 0.8f), 0.6f, 1.4f);

                float duration = wasMiss ? 1000 : 100;

                Text.NumberContainer
                    .ScaleTo(new Vector2(newScale))
                    .ScaleTo(Vector2.One, duration, Easing.OutQuint);

                if (wasMiss)
                    Text.FlashColour(Color4.Red, duration, Easing.OutQuint);
            });
        }

        protected override LocalisableString FormatCount(int count) => DisplayXSymbol ? $@"{count}" : count.ToString();

        protected override IHasText CreateText() => Text = new EzComCounterText(Anchor.TopLeft, LabelContent.Value.ToUpperInvariant())
        {
            ShowLabel = { BindTarget = ShowLabel },
        };

        protected override OsuSpriteText CreateSpriteText() => new OsuSpriteText
        {
            Font = OsuFont.Stat.With(size: 40f, family: "Ez"),
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxAlpha.BindValueChanged(alpha => Text.Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => Text.Colour = AccentColour.Value, true);
        }
    }
}
