// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning.Components;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    public partial class EzComComboCounter : ComboCounter
    {
        [SettingSource("Font", "Font", SettingControlType = typeof(EzSelectorEnumList))]
        public Bindable<string> NameDropdown { get; } = new Bindable<string>("Celeste_Lumiere");

        [SettingSource("Effect Type", "Effect Type")]
        public Bindable<EzComEffectType> Effect { get; } = new Bindable<EzComEffectType>(EzComEffectType.Scale);

        // [SettingSource("Effect Origin", "Effect Origin", SettingControlType = typeof(AnchorDropdown))]
        // public Bindable<Anchor> EffectOrigin { get; } = new Bindable<Anchor>(Anchor.TopCentre)
        // {
        //     Default = Anchor.TopCentre,
        //     Value = Anchor.TopCentre
        // };

        [SettingSource("Effect Start Factor", "Effect Start Factor")]
        public BindableNumber<float> EffectStartFactor { get; } = new BindableNumber<float>(1.5f)
        {
            MinValue = 0.1f,
            MaxValue = 5f,
            Precision = 0.05f,
        };

        [SettingSource("Effect End Factor", "Effect End Factor")]
        public BindableNumber<float> EffectEndFactor { get; } = new BindableNumber<float>(1f)
        {
            MinValue = 0.1f,
            MaxValue = 5f,
            Precision = 0.05f,
        };

        [SettingSource("Effect Start Duration", "Effect Start Duration")]
        public BindableNumber<float> EffectStartTime { get; } = new BindableNumber<float>(10)
        {
            MinValue = 1,
            MaxValue = 300,
            Precision = 1f,
        };

        [SettingSource("Effect End Duration", "Effect End Duration")]
        public BindableNumber<float> EffectEndDuration { get; } = new BindableNumber<float>(300)
        {
            MinValue = 10,
            MaxValue = 500,
            Precision = 10f,
        };

        [SettingSource("Alpha", "The alpha value of this box")]
        public BindableNumber<float> BoxAlpha { get; } = new BindableNumber<float>(1)
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
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxAlpha.BindValueChanged(alpha => Text.Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => Text.Colour = AccentColour.Value, true);

            NameDropdown.BindValueChanged(e =>
            {
                Text.FontName.Value = e.NewValue;
                Text.Invalidate(); // **强制刷新 EzCounterText**
            }, true);
        }

        private void applyAnimation(bool wasIncrease, bool wasMiss)
        {
            switch (Effect.Value)
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

        protected override IHasText CreateText()
        {
            Text = new EzComboText(NameDropdown)
            {
                Scale = new Vector2(1.8f),
            };
            return Text;
        }
    }
}
