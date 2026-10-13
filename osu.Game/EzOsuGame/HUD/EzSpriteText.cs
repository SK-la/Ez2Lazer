// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.WarmUp;
using osu.Game.Graphics.Sprites;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 基于 EzTextureStore 的纹理字体基类
    /// 提供通用的纹理加载和缓存机制，子类只需实现路径查找规则
    /// </summary>
    public abstract partial class EzSpriteText : OsuSpriteText, IEzGameplayWarmUp
    {
        // 与 Argon 位图字体相同：度量先缩到纹理的 1/8，Font.Size 再决定绘制大小。
        internal const float GLYPH_METRIC_SCALE = 0.125f;

        protected override char FixedWidthReferenceCharacter => '5';

        public Bindable<string> ThemeName { get; }
        private Bindable<string> themeName = null!;

        private readonly Func<char, string> getLookup;
        private GlyphStore? glyphStore;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private EzResourceStore resources { get; set; } = null!;

        [Resolved]
        private EzGameplayWarmUpService warmUpService { get; set; } = null!;

        protected EzSpriteText(Func<char, string> getLookup, Bindable<string> themeName)
        {
            this.getLookup = getLookup;
            ThemeName = themeName;

            Shadow = false;
            UseFullGlyphHeight = false;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            themeName = ezConfig.GetBindable<string>(Ez2Setting.GameThemeName);

            themeName.BindValueChanged(e =>
            {
                ThemeName.Value = EzGameThemeNames.Normalize(e.NewValue);
                Preload();
            });

            ThemeName.BindValueChanged(e =>
            {
                string fontName = EzGameThemeNames.Normalize(e.NewValue);
                Font = new FontUsage(fontName, 1);

                // 清理旧的 GlyphStore 缓存，防止每次切换字体时，缓存堆积
                glyphStore?.ClearCache();
                glyphStore = CreateGlyphStore(resources, getLookup);
            }, true);

            // 登记放在字形库建好之后。PlayerLoader 要等这份预热结束才进局。
            warmUpService.Register(this);
        }

        /// <summary>
        /// 进图前把 0–9 和子类声明的特殊字符解码完，并等到字形页上传结束。
        /// 否则首个判定把分数/连击从 0 跳开时，缺的数字会在更新线程上现解。
        /// </summary>
        public Task WarmUpAsync(CancellationToken cancellationToken)
        {
            GlyphStore? store = glyphStore;
            string fontName = EzGameThemeNames.Normalize(ThemeName.Value);

            if (store == null)
                return Task.CompletedTask;

            return EzGameplayWarmUpService.RunBlocking(() =>
            {
                var uploads = new List<Texture>();
                PreloadCharacters(store, fontName, uploads);
                EzGameplayWarmUpService.WaitForUpload(uploads, cancellationToken);
            }, cancellationToken);
        }

        protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store) => base.CreateTextBuilder(glyphStore);

        /// <summary>
        /// 创建 GlyphStore 实例（允许子类自定义）
        /// </summary>
        protected virtual GlyphStore CreateGlyphStore(EzResourceStore resources, Func<char, string> getLookup)
        {
            return new GlyphStore(resources, getLookup, GetPossiblePaths);
        }

        /// <summary>
        /// 预加载字符（允许子类自定义预加载范围）
        /// </summary>
        protected virtual void PreloadCharacters(GlyphStore store, string fontName, List<Texture>? uploads = null)
        {
            foreach (char c in GetPreloadSpecialChars())
                rememberUpload(store.Get(fontName, c), uploads);

            for (int i = 0; i < 10; i++)
                rememberUpload(store.Get(fontName, (char)('0' + i)), uploads);
        }

        public void Preload()
        {
            PreloadCharacters(glyphStore!, EzGameThemeNames.Normalize(ThemeName.Value));
        }

        private static void rememberUpload(ITexturedCharacterGlyph? glyph, List<Texture>? uploads)
        {
            if (uploads != null && glyph?.Texture != null)
                uploads.Add(glyph.Texture);
        }

        /// <summary>
        /// 获取需要预加载的特殊字符
        /// </summary>
        protected virtual char[] GetPreloadSpecialChars() => Array.Empty<char>();

        /// <summary>
        /// 获取字符可能的纹理路径（子类必须实现）
        /// </summary>
        /// <param name="themeName">主题名，不含目录。子类自己拼写死的字形路径。</param>
        /// <param name="lookup">字符对应的查找名</param>
        /// <param name="character">原始字符</param>
        /// <returns>可能的路径数组，按优先级排序</returns>
        protected abstract string[] GetPossiblePaths(string themeName, string lookup, char character);

        /// <summary>
        /// GlyphStore - 纹理字符查找存储
        /// </summary>
        protected class GlyphStore : ITexturedGlyphLookupStore
        {
            private readonly EzResourceStore resources;
            private readonly Func<char, string> getLookup;
            private readonly Func<string, string, char, string[]> getPathResolver;

            private readonly Dictionary<char, ITexturedCharacterGlyph?> cache = new Dictionary<char, ITexturedCharacterGlyph?>();
            private readonly Lock cacheLock = new Lock();

            public GlyphStore(EzResourceStore resources, Func<char, string> getLookup, Func<string, string, char, string[]> getPathResolver)
            {
                this.resources = resources;
                this.getLookup = getLookup;
                this.getPathResolver = getPathResolver;
            }

            public ITexturedCharacterGlyph? Get(string? textureName, char character)
            {
                lock (cacheLock)
                {
                    if (cache.TryGetValue(character, out var cached))
                        return cached;
                }

                string lookup = getLookup(character);
                TexturedCharacterGlyph? glyph = null;

                if (textureName != null)
                {
                    string themeName = textureName.Replace(" ", "_");
                    string[] possiblePaths = getPathResolver(themeName, lookup, character);
                    Texture? texture = resources.GetGlyph(possiblePaths);

                    if (texture != null)
                    {
                        glyph = new TexturedCharacterGlyph(
                            new CharacterGlyph(character, 0, 0, texture.Width, texture.Height, null),
                            texture,
                            GLYPH_METRIC_SCALE);
                    }
                }

                lock (cacheLock)
                {
                    if (cache.TryGetValue(character, out var cached))
                        return cached;

                    cache[character] = glyph;
                    return glyph;
                }
            }

            public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character)
                => Task.Run(() => Get(fontName, character));

            public void ClearCache()
            {
                lock (cacheLock)
                    cache.Clear();
            }
        }
    }
}
