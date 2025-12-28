// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Skinning.Components
{
    public partial class EzGetComboTexture : OsuSpriteText
    {
        public Bindable<EzEnumGameThemeName> FontName { get; }

        private readonly Func<char, string> getLookup;
        private GlyphStore glyphStore = null!;

        protected override char FixedWidthReferenceCharacter => '6';

        public EzGetComboTexture(Func<char, string> getLookup, Bindable<EzEnumGameThemeName> fontName)
        {
            this.getLookup = getLookup;
            FontName = fontName;

            Shadow = false;
            UseFullGlyphHeight = false;
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, IRenderer renderer)
        {
            Storage gameStorage = host.Storage;

            var userResourceStore = new StorageBackedResourceStore(gameStorage);
            var textureLoader = new TextureLoaderStore(userResourceStore);
            var localSkinStore = new TextureStore(renderer, textureLoader);

            FontName.BindValueChanged(e =>
            {
                Font = new FontUsage(FontName.Value.ToString(), 1);
                glyphStore = new GlyphStore(localSkinStore, getLookup);

                foreach (char c in new[] { '.', '%', 'c', 'e', 'l', 'j' })
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
                    string themeRoot = Path.Combine("EzResources", "GameTheme", textureNameReplace);

                    switch (character)
                    {
                        case '.':
                        case '%':
                        case 'e':
                        case 'l':
                            possiblePaths = new[]
                            {
                                Path.Combine(themeRoot, lookup)
                            };
                            break;

                        case 'c':
                            possiblePaths = new[]
                            {
                                Path.Combine(themeRoot, "combo", lookup)
                            };
                            break;

                        case 'j':
                            possiblePaths = new[]
                            {
                                Path.Combine(themeRoot, "judgement")
                            };
                            break;

                        default:
                            possiblePaths = new[]
                            {
                                Path.Combine(themeRoot, "combo", "number", lookup),
                                Path.Combine(themeRoot, "judgement", lookup)
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
                            break;
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
