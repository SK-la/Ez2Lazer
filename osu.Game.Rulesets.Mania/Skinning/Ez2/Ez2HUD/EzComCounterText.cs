// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
                        wireframesPart = new Ez2CounterSpriteText(wireframesLookup)
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.Centre,
                            Font = new FontUsage("Stat", 20),
                        },
                        textPart = new Ez2CounterSpriteText(textLookup)
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.Centre,
                            Font = new FontUsage("Stat", 40),
                        },
                    }
                }
            };
        }

        private string textLookup(char c)
        {
            switch (c)
            {
                case '.':
                    return ".";

                case '%':
                    return "%";

                default:
                    return c.ToString();
            }
        }

        private string wireframesLookup(char c)
        {
            if (c == '.') return ".";

            return "wireframes"; // You can adjust this based on your needs
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
            private readonly Func<char, string> getLookup;

            public Ez2CounterSpriteText(Func<char, string> getLookup)
            {
                this.getLookup = getLookup;

                Shadow = false;
                UseFullGlyphHeight = false;
            }
        }
    }
}
//
//             protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store) => base.CreateTextBuilder(glyphStore);
//
//             private class GlyphStore : ITexturedGlyphLookupStore
//             {
//                 private readonly string fontName;
//                 private readonly TextureStore textures;
//                 private readonly Func<char, string> getLookup;
//
//                 private readonly Dictionary<char, ITexturedCharacterGlyph?> cache = new Dictionary<char, ITexturedCharacterGlyph?>();
//
//                 public GlyphStore(string fontName, TextureStore textures, Func<char, string> getLookup)
//                 {
//                     this.fontName = fontName;
//                     this.textures = textures;
//                     this.getLookup = getLookup;
//                 }
//
//                 public ITexturedCharacterGlyph? Get(string? fontName, char character)
//                 {
//                     if (cache.TryGetValue(character, out var cached))
//                         return cached;
//
//                     string lookup = getLookup(character);
//                     var texture = textures.Get($"Gameplay/Fonts/{fontName}-{lookup}");
//
//                     TexturedCharacterGlyph? glyph = null;
//
//                     if (texture != null)
//                         glyph = new TexturedCharacterGlyph(new CharacterGlyph(character, 0, 0, texture.Width, texture.Height, null), texture, 0.125f);
//
//                     cache[character] = glyph;
//                     return glyph;
//                 }
//
//                 public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => Task.Run(() => Get(fontName, character));
//             }
//         }
//     }
// }
