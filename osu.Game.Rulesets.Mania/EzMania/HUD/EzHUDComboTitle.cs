// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Animation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Effects;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.Localization;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzHUDComboTitle : HitErrorMeter
    {
        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.ANIMATION_TEMPLATE_LABEL), nameof(EzHUDManiaStrings.ANIMATION_TEMPLATE_DESCRIPTION))]
        public Bindable<EzEnumDeformTemplate> AnimationTemplate { get; } = new Bindable<EzEnumDeformTemplate>();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.FONT_LABEL), nameof(EzHUDManiaStrings.FONT_DESCRIPTION), SettingControlType = typeof(EzSelectorEnumList))]
        public Bindable<EzEnumGameThemeName> Font { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.EFFECT_ORIGIN_LABEL), nameof(EzHUDManiaStrings.EFFECT_ORIGIN_DESCRIPTION), SettingControlType = typeof(AnchorDropdown))]
        public Bindable<Anchor> EffectOrigin { get; } = new Bindable<Anchor>(Anchor.TopCentre)
        {
            Default = Anchor.TopCentre,
            Value = Anchor.TopCentre
        };

        [EzBuiltinEffectSource("Effect")]
        public EzBuiltinEffectHost Effects { get; } = EzBuiltinEffectHost.ForTitle();

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.ALPHA_LABEL), nameof(EzHUDStrings.ALPHA_DESCRIPTION))]
        public BindableNumber<float> BoxAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        public EzComboText Text = null!;

        public Bindable<int> Current { get; } = new Bindable<int>();

        public EzHUDComboTitle()
        {
            // Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = new Vector2(120, 30);
        }

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor, Ez2ConfigManager ezConfig)
        {
            InternalChildren = new Drawable[]
            {
                Text = new EzComboText
                {
                    Scale = new Vector2(0.8f),
                    Text = "t",
                    Alpha = 1
                },
            };

            Current.BindTo(scoreProcessor.Combo);
            Current.BindValueChanged(combo =>
            {
                bool wasIncrease = combo.NewValue > combo.OldValue;
                bool wasMiss = combo.OldValue > 1 && combo.NewValue == 0;

                applyAnimation(wasIncrease, wasMiss);
            });

            ezConfig.GetBindable<EzEnumGameThemeName>(Ez2Setting.GameThemeName).BindValueChanged(theme =>
            {
                if (Enum.TryParse(theme.NewValue.ToString(), out EzEnumDeformTemplate template))
                    AnimationTemplate.Value = template;
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxAlpha.BindValueChanged(alpha => Text.Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => Text.Colour = AccentColour.Value, true);

            EffectOrigin.BindValueChanged(e =>
            {
                if (e.NewValue != Anchor.TopCentre && e.NewValue != Anchor.Centre && e.NewValue != Anchor.BottomCentre)
                {
                    EffectOrigin.Value = Anchor.TopCentre; // 设置为默认值
                }

                Text.TextContainer.Anchor = EffectOrigin.Value;
            }, true);
            Font.BindValueChanged(e =>
            {
                Text.ThemeName.Value = e.NewValue;
                Text.Invalidate();
            }, true);
        }

        private void applyAnimation(bool wasIncrease, bool wasMiss)
        {
            if (EzAnimationLibrary.TryPlayCombo(AnimationTemplate.Value, Text.TextContainer, true, Text.SourcePixelScale))
                return;

            Text.TextContainer.FinishTransforms();
            Text.TextContainer.Scale = Vector2.One;
            Text.TextContainer.Position = Vector2.Zero;
            Text.TextContainer.Alpha = 1;
            Effects.Play(Text.TextContainer, wasIncrease, wasMiss);
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit)
                return;

            Text.Text = judgement.IsHit ? "t" : string.Empty;
        }

        public override void Clear()
        {
            Text.Text = string.Empty;
        }
    }
}
