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

        [SettingSource("Animation Type", "The type of animation to apply")]
        public Bindable<AnimationType> Animation { get; } = new Bindable<AnimationType>(AnimationType.Scale);

        [SettingSource("Increase Scale", "The scale factor when the combo increases")]
        public BindableNumber<float> IncreaseScale { get; } = new BindableNumber<float>(1.2f)
        {
            MinValue = 0.5f,
            MaxValue = 5f,
            Precision = 0.05f,
        };

        [SettingSource("Decrease Scale", "The scale factor when the combo decreases")]
        public BindableNumber<float> DecreaseScale { get; } = new BindableNumber<float>(0.8f)
        {
            MinValue = 0.5f,
            MaxValue = 1.5f,
            Precision = 0.1f,
        };

        [SettingSource("Increase Duration", "The scale duration time when the combo increases")]
        public BindableNumber<float> IncreaseDuration { get; } = new BindableNumber<float>(10)
        {
            MinValue = 1,
            MaxValue = 300,
            Precision = 1f,
        };

        [SettingSource("Decrease Duration", "The scale duration time when the combo decrease")]
        public BindableNumber<float> DecreaseDuration { get; } = new BindableNumber<float>(200)
        {
            MinValue = 10,
            MaxValue = 500,
            Precision = 10f,
        };

        [SettingSource("Animation Origin", "The origin point for the animation")]
        public Bindable<OriginOptions> AnimationOrigin { get; } = new Bindable<OriginOptions>(OriginOptions.TopCentre);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.ShowLabel), nameof(SkinnableComponentStrings.ShowLabelDescription))]
        public Bindable<bool> ShowLabel { get; } = new BindableBool(true);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.TextElementText), nameof(SkinnableComponentStrings.TextElementTextDescription))]
        public Bindable<string> LabelContent { get; } = new Bindable<string>("combo");

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

                switch (Animation.Value)
                {
                    case AnimationType.Scale:
                        applyScaleAnimation(wasIncrease, wasMiss);
                        break;

                    case AnimationType.Bounce:
                        applyBounceAnimation(wasIncrease, wasMiss);
                        break;
                }
            });
        }

        private void applyScaleAnimation(bool wasIncrease, bool wasMiss)
        {
            float newScaleValue = Math.Clamp(Text.NumberContainer.Scale.X * (wasIncrease ? IncreaseScale.Value : DecreaseScale.Value), 0.5f, 3f);
            Vector2 newScale = new Vector2(newScaleValue);

            Anchor originAnchor = Enum.Parse<Anchor>(AnimationOrigin.Value.ToString());

            Text.NumberContainer.Anchor = originAnchor;
            Text.NumberContainer.Origin = originAnchor;

            Text.NumberContainer
                .ScaleTo(newScale, IncreaseDuration.Value, Easing.OutQuint)
                .Then()
                .ScaleTo(Vector2.One, DecreaseDuration.Value, Easing.OutQuint);

            if (wasMiss)
                Text.FlashColour(Color4.Red, DecreaseDuration.Value, Easing.OutQuint);
        }

        private void applyBounceAnimation(bool wasIncrease, bool wasMiss)
        {
            float factor = Math.Clamp(wasIncrease ? -10 * IncreaseScale.Value : 10 * DecreaseScale.Value, -100f, 100f);

            Anchor originAnchor = Enum.Parse<Anchor>(AnimationOrigin.Value.ToString());

            Text.NumberContainer.Anchor = originAnchor;
            Text.NumberContainer.Origin = originAnchor;

            Text.NumberContainer
                .MoveToY(factor, IncreaseDuration.Value / 2, Easing.OutBounce)
                .Then()
                .MoveToY(0, DecreaseDuration.Value, Easing.OutBounce);

            if (wasMiss)
                Text.FlashColour(Color4.Red, DecreaseDuration.Value, Easing.OutQuint);
        }

        protected override LocalisableString FormatCount(int count) => DisplayXSymbol ? $@"{count}" : count.ToString();

        protected override IHasText CreateText() => Text = new EzComCounterText(Anchor.TopCentre, LabelContent.Value)
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

    public enum AnimationType
    {
        Scale,
        Bounce
    }

    public enum OriginOptions
    {
        TopCentre,
        Centre,
        BottomCentre
    }
}
