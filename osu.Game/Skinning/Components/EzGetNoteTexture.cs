// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning.Components
{
    public partial class EzGetNoteTexture : Sprite
    {
        public Bindable<string> NoteName { get; }
        public Bindable<string> ThemeName { get; }

        private readonly Func<string, string> getLookup;
        private NoteTextureStore textureStore = null!;

        public EzGetNoteTexture(Func<string, string> getLookup, Bindable<string> themeName, Bindable<string> noteName)
        {
            this.getLookup = getLookup;
            ThemeName = themeName;
            NoteName = noteName;

            RelativeSizeAxes = Axes.None;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            textureStore = new NoteTextureStore(textures, getLookup);

            NoteName.BindValueChanged(_ => updateTexture(), true);
            ThemeName.BindValueChanged(_ => updateTexture(), true);
        }

        private void updateTexture()
        {
            Texture = textureStore.Get(ThemeName.Value, NoteName.Value);
        }

        private class NoteTextureStore
        {
            private readonly TextureStore textures;
            private readonly Func<string, string> getLookup;
            private readonly Dictionary<string, Texture?> cache = new Dictionary<string, Texture?>();

            public NoteTextureStore(TextureStore textures, Func<string, string> getLookup)
            {
                this.textures = textures;
                this.getLookup = getLookup;
            }

            public Texture? Get(string themeName, string noteName)
            {
                string cacheKey = $"{themeName}:{noteName}";

                if (cache.TryGetValue(cacheKey, out var cached))
                    return cached;

                string lookup = getLookup(noteName);
                Texture? texture = null;

                if (!string.IsNullOrEmpty(themeName))
                {
                    string themeNameNormalized = themeName.Replace(" ", "_");

                    string[] possiblePaths = new[]
                    {
                        $"EzResources/GameTheme/{themeNameNormalized}/notes/{lookup}",
                        $"EzResources/GameTheme/{themeNameNormalized}/notes/default/{lookup}",
                    };

                    foreach (string path in possiblePaths)
                    {
                        texture = textures.Get(path);
                        if (texture != null)
                            break;
                    }
                }

                cache[cacheKey] = texture;
                return texture;
            }
        }
    }
}
