// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenTabletDriver.Plugin;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzCounterText : CompositeDrawable, IHasText
    {
        private readonly Ez2CounterSpriteText textPart;
        public Bindable<string> FontName { get; } = new Bindable<string>("stat");
        public FillFlowContainer NumberContainer { get; private set; }

        public LocalisableString Text
        {
            get => textPart.Text;
            set => textPart.Text = value;
        }

        public EzCounterText(Anchor anchor, Bindable<string>? externalFontName = null)
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            if (externalFontName is not null)
                FontName.BindTo(externalFontName);

            textPart = new Ez2CounterSpriteText(textLookup, FontName);
            Debug.WriteLine("👀 EzCounterText FontName Updated:", FontName.Value);

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
                        textPart
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

        protected override void LoadComplete()
        {
            base.LoadComplete();

            FontName.BindValueChanged(e =>
            {
                textPart.FontName.Value = e.NewValue;
                // textPart.LoadAsync(); // **强制重新加载字体**
                textPart.Invalidate(); // **确保 UI 立即刷新**
            }, true);
        }

        private partial class Ez2CounterSpriteText : OsuSpriteText
        {
            public Bindable<string> FontName { get; }

            private readonly Func<char, string> getLookup;
            private GlyphStore glyphStore = null!;

            protected override char FixedWidthReferenceCharacter => '5';

            public Ez2CounterSpriteText(Func<char, string> getLookup, Bindable<string> fontName)
            {
                this.getLookup = getLookup;
                FontName = fontName;

                Shadow = false;
                UseFullGlyphHeight = false;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Spacing = new Vector2(-2f, 0f);
                FontName.BindValueChanged(e =>
                {
                    Font = new FontUsage(FontName.Value, 1);
                    glyphStore = new GlyphStore(textures, getLookup);

                    foreach (char c in new[] { '.', '%' })
                        glyphStore.Get(FontName.Value, c);
                    for (int i = 0; i < 10; i++)
                        glyphStore.Get(FontName.Value, (char)('0' + i));
                }, true);
            }

            protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store) => base.CreateTextBuilder(glyphStore);

            private class GlyphStore : ITexturedGlyphLookupStore
            {
                // private readonly string fontFolder;
                private readonly TextureStore textures;
                private readonly Func<char, string> getLookup;

                private readonly Dictionary<char, ITexturedCharacterGlyph?> cache = new Dictionary<char, ITexturedCharacterGlyph?>();

                public GlyphStore(TextureStore textures, Func<char, string> getLookup)
                {
                    // this.fontFolder = fontFolder;
                    this.textures = textures;
                    this.getLookup = getLookup;
                }

                public ITexturedCharacterGlyph? Get(string? textureName, char character)
                {
                    if (cache.TryGetValue(character, out var cached))
                        return cached;

                    string lookup = getLookup(character);
                    var texture = textures.Get($"Gameplay/Fonts/{textureName}/{textureName}-counter-{lookup}") ?? textures.Get($"Gameplay/Fonts/{textureName}-counter-{lookup}");
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

    public partial class FontNameSelector : SettingsDropdown<string>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();

            Items = new List<string>
            {
                "argon",
                "Gold",
                "green",
                "Italics2",
                "purple",
                "sb1",
                "stat",
                "Sliver",
                "TOMATO",
            };
            Log.Debug("Items", Items.ToString());
        }
    }
}
