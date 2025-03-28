// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class Ez2ScoreCounter : GameplayScoreCounter, ISerialisableDrawable
    {
        protected override double RollingDuration => 250;

        [SettingSource("Wireframe opacity", "Controls the opacity of the wireframes behind the digits.")]
        public BindableFloat WireframeOpacity { get; } = new BindableFloat(0)
        {
            Precision = 0.01f,
            MinValue = 0,
            MaxValue = 1,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.ShowLabel), nameof(SkinnableComponentStrings.ShowLabelDescription))]
        public Bindable<bool> ShowLabel { get; } = new BindableBool(true);

        public bool UsesFixedAnchor { get; set; }

        protected override LocalisableString FormatCount(long count) => count.ToString();

        protected override IHasText CreateText() => new EzComScoreText(Anchor.TopRight, BeatmapsetsStrings.ShowScoreboardHeadersScore.ToUpper())
        {
            ShowLabel = { BindTarget = ShowLabel },
        };

        private partial class EzComScoreText : EzComCounterText
        {
            public EzComScoreText(Anchor anchor, LocalisableString? label = null)
                : base(anchor, label)
            {
            }
        }
    }
}
