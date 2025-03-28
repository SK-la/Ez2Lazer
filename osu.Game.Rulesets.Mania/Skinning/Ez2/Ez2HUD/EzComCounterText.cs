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
        private readonly Ez2CounterSpriteText textPart;
        private readonly OsuSpriteText labelText;
        private readonly OsuSpriteText text;
        public IBindable<float> WireframeOpacity { get; } = new BindableFloat();
        public Bindable<bool> ShowLabel { get; } = new BindableBool();

        public Container NumberContainer { get; private set; }

        public LocalisableString Text
        {
            get => textPart.Text;
            set => textPart.Text = value;
        }

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
                        text = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.Default.With(size: 40)
                        },
                        textPart = new Ez2CounterSpriteText
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.Stat.With(size: 40)
                        },
                    }
                },
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

        protected void SetFont(FontUsage font) => text.Font = font.With(size: 40);

        protected void SetTextColour(Colour4 textColour) => text.Colour = textColour;
    }
}
