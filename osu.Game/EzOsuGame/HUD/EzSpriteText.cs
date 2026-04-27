// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Text;
using osu.Game.Graphics.Sprites;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 基于 EzResourceProvider 的纹理字体基类
    /// 提供通用的纹理加载和缓存机制，子类只需实现路径查找规则
    /// </summary>
    public abstract partial class EzSpriteText : OsuSpriteText
    {
        protected override char FixedWidthReferenceCharacter => '5';

        public Bindable<EzEnumGameThemeName> FontName { get; }

        private readonly Func<char, string> getLookup;
        private GlyphStore? glyphStore;

        [Resolved]
        private EzResourceProvider textures { get; set; } = null!;

        protected EzSpriteText(Func<char, string> getLookup, Bindable<EzEnumGameThemeName> fontName)
        {
            this.getLookup = getLookup;
            FontName = fontName;

            Shadow = false;
            UseFullGlyphHeight = false;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            FontName.BindValueChanged(e =>
            {
                Font = new FontUsage(FontName.Value.ToString(), 1);

                // 清理旧的 GlyphStore 缓存，防止每次切换字体时，缓存堆积
                glyphStore?.ClearCache();
                glyphStore = CreateGlyphStore(textures, getLookup);

                // 预加载常用字符
                PreloadCharacters(glyphStore, FontName.Value.ToString());
            }, true);
        }

        protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store) => base.CreateTextBuilder(glyphStore);

        /// <summary>
        /// 创建 GlyphStore 实例（允许子类自定义）
        /// </summary>
        protected virtual GlyphStore CreateGlyphStore(EzResourceProvider textures, Func<char, string> getLookup)
        {
            return new GlyphStore(textures, getLookup, GetPossiblePaths);
        }

        /// <summary>
        /// 预加载字符（允许子类自定义预加载范围）
        /// </summary>
        protected virtual void PreloadCharacters(GlyphStore store, string fontName)
        {
            // 默认预加载数字和常见符号
            foreach (char c in GetPreloadSpecialChars())
                store.Get(fontName, c);

            for (int i = 0; i < 10; i++)
                store.Get(fontName, (char)('0' + i));
        }

        /// <summary>
        /// 获取需要预加载的特殊字符
        /// </summary>
        protected virtual char[] GetPreloadSpecialChars() => Array.Empty<char>();

        /// <summary>
        /// 获取字符可能的纹理路径（子类必须实现）
        /// </summary>
        /// <param name="themeRoot">主题根路径</param>
        /// <param name="lookup">字符对应的查找名</param>
        /// <param name="character">原始字符</param>
        /// <returns>可能的路径数组，按优先级排序</returns>
        protected abstract string[] GetPossiblePaths(string themeRoot, string lookup, char character);

        /// <summary>
        /// GlyphStore - 纹理字符查找存储
        /// </summary>
        protected class GlyphStore : ITexturedGlyphLookupStore
        {
            private readonly EzResourceProvider textures;
            private readonly Func<char, string> getLookup;
            private readonly Func<string, string, char, string[]> getPathResolver;

            private readonly Dictionary<char, ITexturedCharacterGlyph?> cache = new Dictionary<char, ITexturedCharacterGlyph?>();

            public GlyphStore(EzResourceProvider textures, Func<char, string> getLookup, Func<string, string, char, string[]> getPathResolver)
            {
                this.textures = textures;
                this.getLookup = getLookup;
                this.getPathResolver = getPathResolver;
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
                    string themeRoot = @$"GameTheme/{textureNameReplace}/";

                    // 通过子类提供的路径解析器获取可能路径
                    string[] possiblePaths = getPathResolver(themeRoot, lookup, character);

                    foreach (string path in possiblePaths)
                    {
                        var texture = textures.Get(path);

                        if (texture != null)
                        {
                            glyph = new TexturedCharacterGlyph(
                                new CharacterGlyph(character, 0, 0, texture.Width, texture.Height, null),
                                texture,
                                0.125f);
                            break;
                        }
                    }
                }

                cache[character] = glyph;
                return glyph;
            }

            public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character)
                => Task.Run(() => Get(fontName, character));

            public void ClearCache() => cache.Clear();
        }
    }
}
