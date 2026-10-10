// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Animation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Effects;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Mania.EzMania.Localization;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzHUDComboCounter : ComboCounter, IScopedSkinSettings
    {
        private readonly EzScaleEffect scaleEffect = EzScaleEffect.ForCounter();
        private readonly EzBounceEffect bounceEffect = EzBounceEffect.ForDigits();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.ANIMATION_TEMPLATE_LABEL), nameof(EzHUDManiaStrings.ANIMATION_TEMPLATE_DESCRIPTION))]
        public Bindable<EzEnumDeformTemplate> AnimationTemplate { get; } = new Bindable<EzEnumDeformTemplate>();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.FONT_LABEL), nameof(EzHUDManiaStrings.FONT_DESCRIPTION), SettingControlType = typeof(EzSelectorEnumList))]
        public Bindable<EzEnumGameThemeName> ThemeName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_TYPE_LABEL), nameof(EzHUDManiaStrings.EFFECT_TYPE_DESCRIPTION))]
        public Bindable<EzEffectType> EffectType { get; } = new Bindable<EzEffectType>(EzEffectType.Scale);

        // [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_ORIGIN_LABEL), nameof(EzHUDManiaStrings.EFFECT_ORIGIN_DESCRIPTION), SettingControlType = typeof(AnchorDropdown))]
        // public Bindable<Anchor> EffectOrigin { get; } = new Bindable<Anchor>(Anchor.TopCentre)
        // {
        //     Default = Anchor.TopCentre,
        //     Value = Anchor.TopCentre
        // };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_START_FACTOR_LABEL), nameof(EzHUDManiaStrings.EFFECT_START_FACTOR_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Scale)]
        public BindableNumber<float> EffectStartFactor => scaleEffect.StartFactor;

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_END_FACTOR_LABEL), nameof(EzHUDManiaStrings.EFFECT_END_FACTOR_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Scale)]
        public BindableNumber<float> EffectEndFactor => scaleEffect.EndFactor;

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_START_DURATION_LABEL), nameof(EzHUDManiaStrings.EFFECT_START_DURATION_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Scale)]
        public BindableNumber<float> EffectStartTime => scaleEffect.StartTime;

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_END_DURATION_LABEL), nameof(EzHUDManiaStrings.EFFECT_END_DURATION_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Scale)]
        public BindableNumber<float> EffectEndDuration => scaleEffect.EndDuration;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_PEAK_LABEL), nameof(EzHUDStrings.BOUNCE_PEAK_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BouncePeak => bounceEffect.Peak;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_RETURN_LABEL), nameof(EzHUDStrings.BOUNCE_RETURN_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BounceReturnDuration => bounceEffect.ReturnDuration;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_EASING_LABEL), nameof(EzHUDStrings.BOUNCE_EASING_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public Bindable<Easing> BounceEasing => bounceEffect.Ease;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_OVERSHOOT_LABEL), nameof(EzHUDStrings.BOUNCE_OVERSHOOT_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BounceOvershoot => bounceEffect.Overshoot;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_START_ALPHA_LABEL), nameof(EzHUDStrings.BOUNCE_START_ALPHA_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BounceStartAlpha => bounceEffect.StartAlpha;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.BOUNCE_ALPHA_LABEL), nameof(EzHUDStrings.BOUNCE_ALPHA_DESCRIPTION))]
        [EzEffectSetting(EzEffectType.Bounce)]
        public BindableNumber<float> BounceAlpha => bounceEffect.Alpha;

        public bool IsSettingActive(PropertyInfo property) => EzEffectSettingAttribute.IsActive(property, EffectType.Value);

        public void WatchSettingsScope(Action onChange) => EffectType.BindValueChanged(_ => onChange());

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
        private void load(ScoreProcessor scoreProcessor, Ez2ConfigManager ezConfig)
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
            AnimationTemplate.BindValueChanged(_ => updateHandwrittenEffect(), true);
            ezConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName).BindValueChanged(theme =>
            {
                if (Enum.TryParse(theme.NewValue.ToString(), out EzEnumDeformTemplate template))
                    AnimationTemplate.Value = template;
            });
        }

        private void updateHandwrittenEffect()
        {
            bool extracted = EzAnimationLibrary.TryGetDeformTheme(AnimationTemplate.Value, out EzEnumGameThemeName theme)
                             && EzAnimationLibrary.TryGet(EzAnimationLibrary.COMBO, EzAnimationLibrary.COMBO_NEW, theme, out _);

            scaleEffect.SetEnabled(!extracted);
            bounceEffect.SetEnabled(!extracted);
        }

        private void applyAnimation(bool wasIncrease, bool wasMiss)
        {
            if (EzAnimationLibrary.TryPlayCombo(AnimationTemplate.Value, Text.TextContainer, title: false))
                return;

            resetHandwrittenTarget();

            switch (EffectType.Value)
            {
                case EzEffectType.Scale:
                    scaleEffect.Play(Text.TextContainer, wasIncrease, wasMiss);
                    break;

                case EzEffectType.Bounce:
                    bounceEffect.Play(Text.TextContainer, wasIncrease, wasMiss);
                    break;
            }
        }

        private void resetHandwrittenTarget()
        {
            Text.TextContainer.FinishTransforms();
            Text.TextContainer.Scale = Vector2.One;
            Text.TextContainer.Position = Vector2.Zero;
            Text.TextContainer.Alpha = 1;
        }

        protected override LocalisableString FormatCount(int count) => DisplayXSymbol ? $@"{count}" : count.ToString();

        protected override IHasText CreateText() => Text = new EzComboText(ThemeName)
        {
            Scale = new Vector2(1.8f),
        };
    }
}
