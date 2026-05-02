// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.Localization;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzHUDComboCounter : ComboCounter
    {
        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.FONT_LABEL), nameof(EzHUDManiaStrings.FONT_DESCRIPTION), SettingControlType = typeof(EzSelectorEnumList))]
        public Bindable<EzEnumGameThemeName> ThemeName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_TYPE_LABEL), nameof(EzHUDManiaStrings.EFFECT_TYPE_DESCRIPTION))]
        public Bindable<EzComEffectType> EffectType { get; } = new Bindable<EzComEffectType>(EzComEffectType.Scale);

        // [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_ORIGIN_LABEL), nameof(EzHUDManiaStrings.EFFECT_ORIGIN_DESCRIPTION), SettingControlType = typeof(AnchorDropdown))]
        // public Bindable<Anchor> EffectOrigin { get; } = new Bindable<Anchor>(Anchor.TopCentre)
        // {
        //     Default = Anchor.TopCentre,
        //     Value = Anchor.TopCentre
        // };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_START_FACTOR_LABEL), nameof(EzHUDManiaStrings.EFFECT_START_FACTOR_DESCRIPTION))]
        public BindableNumber<float> EffectStartFactor { get; } = new BindableNumber<float>(1.5f)
        {
            MinValue = 0.1f,
            MaxValue = 5f,
            Precision = 0.05f,
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_END_FACTOR_LABEL), nameof(EzHUDManiaStrings.EFFECT_END_FACTOR_DESCRIPTION))]
        public BindableNumber<float> EffectEndFactor { get; } = new BindableNumber<float>(1f)
        {
            MinValue = 0.1f,
            MaxValue = 5f,
            Precision = 0.05f,
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_START_DURATION_LABEL), nameof(EzHUDManiaStrings.EFFECT_START_DURATION_DESCRIPTION))]
        public BindableNumber<float> EffectStartTime { get; } = new BindableNumber<float>(10)
        {
            MinValue = 1,
            MaxValue = 300,
            Precision = 1f,
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_END_DURATION_LABEL), nameof(EzHUDManiaStrings.EFFECT_END_DURATION_DESCRIPTION))]
        public BindableNumber<float> EffectEndDuration { get; } = new BindableNumber<float>(300)
        {
            MinValue = 10,
            MaxValue = 500,
            Precision = 10f,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.ALPHA_LABEL), nameof(EzHUDStrings.ALPHA_DESCRIPTION))]
        public BindableNumber<float> AccentAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        public EzComboText Text = null!;
        protected override double RollingDuration => 250;
        protected virtual bool DisplayXSymbol => true;

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
            Current.BindTo(scoreProcessor.Combo);
            Current.BindValueChanged(combo =>
            {
                bool wasIncrease = combo.NewValue > combo.OldValue;
                bool wasMiss = combo.OldValue > 1 && combo.NewValue == 0;

                applyAnimation(wasIncrease, wasMiss);
            });

            // EffectOrigin.BindValueChanged(e =>
            // {
            //     Text.TextPart.Origin = e.NewValue;
            // }, true);

            AccentAlpha.BindValueChanged(alpha => Text.Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => Text.Colour = AccentColour.Value, true);
        }

        private void applyAnimation(bool wasIncrease, bool wasMiss)
        {
            switch (EffectType.Value)
            {
                case EzComEffectType.Scale:
                    EzEffectHelper.ApplyScaleAnimation(
                        Text.TextContainer,
                        wasIncrease,
                        wasMiss,
                        EffectStartFactor.Value,
                        EffectEndFactor.Value,
                        EffectStartTime.Value,
                        EffectEndDuration.Value);
                    break;

                case EzComEffectType.Bounce:
                    EzEffectHelper.ApplyBounceAnimation(
                        Text.TextContainer,
                        wasIncrease,
                        wasMiss,
                        EffectStartFactor.Value,
                        EffectEndFactor.Value,
                        EffectStartTime.Value,
                        EffectEndDuration.Value);
                    break;
            }
        }

        protected override LocalisableString FormatCount(int count) => DisplayXSymbol ? $@"{count}" : count.ToString();

        protected override IHasText CreateText() => Text = new EzComboText(ThemeName)
        {
            Scale = new Vector2(1.8f),
        };
    }
}
