// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Text;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzComCounterText : CompositeDrawable, IHasText
    {
        private readonly Ez2CounterSpriteText textPart;
        private readonly OsuSpriteText labelText;
        public Bindable<bool> ShowLabel { get; } = new BindableBool();

        public FillFlowContainer NumberContainer { get; private set; }

        public LocalisableString Text
        {
            get => textPart.Text;
            set => textPart.Text = value;
        }

        public EzComCounterText(Anchor anchor, LocalisableString? label = null)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                NumberContainer = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                    Anchor = anchor,
                    Origin = anchor,

                    Children = new Drawable[]
                    {
                        labelText = new OsuSpriteText
                        {
                            Alpha = 0,
                            // BypassAutoSizeAxes = Axes.X,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.BottomCentre,
                            Text = label.GetValueOrDefault(),
                            Font = OsuFont.Stat.With(size: 12, weight: FontWeight.Bold),
                            // Margin = new MarginPadding { Bottom = 1 },
                        },
                        textPart = new Ez2CounterSpriteText(textLookup)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.TopCentre,
                        },
                    }
                },
            };
        }

        private string textLookup(char c)
        {
            switch (c)
            {
                case '.':
                    return @"dot";

                case '%':
                    return @"percentage";

                default:
                    return c.ToString();
            }
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
                labelText.Alpha = s.NewValue ? 1 : 0;
                NumberContainer.Y = s.NewValue ? 12 : 0;
            }, true);
        }

        private partial class Ez2CounterSpriteText : OsuSpriteText
        {
            private readonly Func<char, string> getLookup;

            private GlyphStore glyphStore = null!;

            protected override char FixedWidthReferenceCharacter => '5';

            public Ez2CounterSpriteText(Func<char, string> getLookup)
            {
                this.getLookup = getLookup;

                Shadow = false;
                UseFullGlyphHeight = false;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                const string font_name = @"stat-counter";

                Spacing = new Vector2(-2f, 0f);
                Font = new FontUsage(font_name, 1);
                glyphStore = new GlyphStore(font_name, textures, getLookup);

                // cache common lookups ahead of time.
                foreach (char c in new[] { '.', '%' })
                    glyphStore.Get(font_name, c);
                for (int i = 0; i < 10; i++)
                    glyphStore.Get(font_name, (char)('0' + i));
            }

            protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store) => base.CreateTextBuilder(glyphStore);

            private class GlyphStore : ITexturedGlyphLookupStore
            {
                private readonly string fontName;
                private readonly TextureStore textures;
                private readonly Func<char, string> getLookup;

                private readonly Dictionary<char, ITexturedCharacterGlyph?> cache = new Dictionary<char, ITexturedCharacterGlyph?>();

                public GlyphStore(string fontName, TextureStore textures, Func<char, string> getLookup)
                {
                    this.fontName = fontName;
                    this.textures = textures;
                    this.getLookup = getLookup;
                }

                public ITexturedCharacterGlyph? Get(string? fontName, char character)
                {
                    // We only service one font.
                    if (fontName != this.fontName)
                        return null;

                    if (cache.TryGetValue(character, out var cached))
                        return cached;

                    string lookup = getLookup(character);
                    var texture = textures.Get($"Gameplay/Fonts/{fontName}-{lookup}");

                    TexturedCharacterGlyph? glyph = null;

                    if (texture != null)
                        glyph = new TexturedCharacterGlyph(new CharacterGlyph(character, 0, 0, texture.Width, texture.Height, null), texture, 0.125f);

                    cache[character] = glyph;
                    return glyph;
                }

                public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => Task.Run(() => Get(fontName, character));
            }
        }
    }
}
