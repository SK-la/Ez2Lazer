// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzComCounterText : CompositeDrawable, IHasText
    {
        private readonly Ez2CounterSpriteText wireframesPart;
        private readonly Ez2CounterSpriteText textPart;
        private readonly OsuSpriteText labelText;

        public FontUsage ComboFont { get; set; } = new FontUsage("Stat", 40);

        public IBindable<float> WireframeOpacity { get; } = new BindableFloat();
        public Bindable<bool> ShowLabel { get; } = new BindableBool();

        public Container NumberContainer { get; private set; }

        public LocalisableString Text
        {
            get => textPart.Text;
            set => textPart.Text = value;
        }

        public string WireframeTemplate
        {
            get => wireframeTemplate;
            set => wireframesPart.Text = wireframeTemplate = value;
        }

        private string wireframeTemplate = string.Empty;

        public EzComCounterText(Anchor anchor, LocalisableString? label = null)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            AutoSizeAxes = Axes.Both;

            InternalChildren = new[]
            {
                labelText = new OsuSpriteText
                {
                    Alpha = 0,
                    BypassAutoSizeAxes = Axes.X,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Text = label.GetValueOrDefault(),
                    Font = OsuFont.Stat.With(size: 14, weight: FontWeight.Bold),
                    Margin = new MarginPadding { Vertical = 1 },
                },
                Empty(),
                NumberContainer = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = anchor,
                    Origin = anchor,
                    Children = new[]
                    {
                        wireframesPart = new Ez2CounterSpriteText
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.Centre,
                            Font = new FontUsage("Stat", 20),
                        },
                        textPart = new Ez2CounterSpriteText
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.Centre,
                            Font = new FontUsage("Stat", 40),
                        },
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            labelText.Colour = colours.Blue0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            WireframeOpacity.BindValueChanged(v => wireframesPart.Alpha = v.NewValue, true);
            ShowLabel.BindValueChanged(s =>
            {
                labelText.Alpha = s.NewValue ? 0.8f : 0;
                NumberContainer.Y = s.NewValue ? 14 : 0;
            }, true);
        }

        private partial class Ez2CounterSpriteText : OsuSpriteText
        {
            public Ez2CounterSpriteText()
            {
                Shadow = false;
                UseFullGlyphHeight = false;
            }
        }
    }
}
