// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Skinning.Components
{
    public partial class EzGetScoreTexture : OsuSpriteText
    {
        public Bindable<EzEnumGameThemeName> FontName { get; }

        private readonly Func<char, string> getLookup;
        private GlyphStore glyphStore = null!;

        protected override char FixedWidthReferenceCharacter => '5';

        public EzGetScoreTexture(Func<char, string> getLookup, Bindable<EzEnumGameThemeName> fontName)
        {
            this.getLookup = getLookup;
            FontName = fontName;

            Shadow = false;
            UseFullGlyphHeight = false;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            // Spacing = new Vector2(-2f, 0f);
            FontName.BindValueChanged(e =>
            {
                Font = new FontUsage(FontName.Value.ToString(), 1);
                glyphStore = new GlyphStore(textures, getLookup);

                foreach (char c in new[] { '.', '%' })
                    glyphStore.Get(FontName.Value.ToString(), c);
                for (int i = 0; i < 10; i++)
                    glyphStore.Get(FontName.Value.ToString(), (char)('0' + i));
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
                TexturedCharacterGlyph? glyph = null;

                if (textureName != null)
                {
                    string textureNameReplace = textureName.Replace(" ", "_");

                    string[] possiblePaths;

                    switch (character)
                    {
                        case '.':
                        case '%':

                        default:
                            possiblePaths = new[]
                            {
                                $"EzResources/GameTheme/{textureNameReplace}/number/score/{lookup}",
                                $"EzResources/GameTheme/{textureNameReplace}/number/combo/{lookup}", //combo图
                                $"EzResources/GameTheme/{textureNameReplace}/number/{lookup}", //combo图
                            };
                            break;
                    }

                    foreach (string path in possiblePaths)
                    {
                        var texture = textures.Get(path);

                        if (texture != null)
                        {
                            glyph = new TexturedCharacterGlyph(new CharacterGlyph(character, 0, 0, texture.Width, texture.Height, null),
                                texture, 0.125f);
                        }
                    }
                }

                cache[character] = glyph;
                return glyph;
            }

            public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => Task.Run(() => Get(fontName, character));
        }
    }
}
