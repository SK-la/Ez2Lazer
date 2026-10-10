// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    public partial class EzHUDComboCounter : ComboCounter
    {
        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.ANIMATION_TEMPLATE_LABEL), nameof(EzHUDManiaStrings.ANIMATION_TEMPLATE_DESCRIPTION))]
        public Bindable<EzEnumDeformTemplate> AnimationTemplate { get; } = new Bindable<EzEnumDeformTemplate>();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.FONT_LABEL), nameof(EzHUDManiaStrings.FONT_DESCRIPTION), SettingControlType = typeof(EzSelectorEnumList))]
        public Bindable<EzEnumGameThemeName> ThemeName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [EzBuiltinEffectSource]
        public EzBuiltinEffectHost Effects { get; } = EzBuiltinEffectHost.ForDigits();

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
            ezConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName).BindValueChanged(theme =>
            {
                if (Enum.TryParse(theme.NewValue.ToString(), out EzEnumDeformTemplate template))
                    AnimationTemplate.Value = template;
            });
        }

        private void applyAnimation(bool wasIncrease, bool wasMiss)
        {
            if (EzAnimationLibrary.TryPlayCombo(AnimationTemplate.Value, Text.TextContainer, false, Text.SourcePixelScale))
                return;

            resetHandwrittenTarget();
            Effects.Play(Text.TextContainer, wasIncrease, wasMiss);
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
