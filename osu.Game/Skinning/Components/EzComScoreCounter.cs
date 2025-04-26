// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Skinning.Components
{
    public partial class EzComScoreCounter : GameplayScoreCounter, ISerialisableDrawable
    {
        protected override double RollingDuration => 250;

        [SettingSource("Font", "Font", SettingControlType = typeof(OffsetNumberNameSelector))]
        public Bindable<string> FontNameDropdown { get; } = new Bindable<string>("NIGHTwhite");

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.ShowLabel), nameof(SkinnableComponentStrings.ShowLabelDescription))]
        public Bindable<bool> ShowLabel { get; } = new BindableBool();

        [SettingSource("Alpha", "The alpha value of this box")]
        public BindableNumber<float> BoxAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour), nameof(SkinnableComponentStrings.ColourDescription))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        public bool UsesFixedAnchor { get; set; }
        public EzCounterText Text = null!;
        protected override LocalisableString FormatCount(long count) => count.ToString();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxAlpha.BindValueChanged(alpha => Text.Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => Text.Colour = AccentColour.Value, true);

            FontNameDropdown.BindValueChanged(e =>
            {
                Text.FontName.Value = e.NewValue;
                Text.Invalidate(); // **强制刷新 EzCounterText**
            }, true);

            Padding = new MarginPadding
            {
                Left = 2,
                Right = 2,
            };
        }

        protected override IHasText CreateText()
        {
            Text = new EzCounterText(FontNameDropdown);
            return Text;
        }
    }
}
