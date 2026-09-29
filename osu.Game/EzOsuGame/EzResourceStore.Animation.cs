// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.EzOsuGame
{
    public partial class EzResourceStore
    {
        /// <summary>默认帧时长；调用方不给时用 60fps 一帧。</summary>
        private const double default_frame_length = 1000d / 60d;

        private readonly ConcurrentDictionary<EzAnimationRequest, EzFrameSet> frameSets = new ConcurrentDictionary<EzAnimationRequest, EzFrameSet>();

        #region 层2 公开动画加载

        /// <summary>
        /// 按请求加载动画：命中多帧返回 <see cref="TextureAnimation"/>，单帧返回 <see cref="Sprite"/>，无资源返回 <c>null</c>。
        /// </summary>
        /// <param name="request">帧加载请求；见 <see cref="EzAnimationRequest"/>。</param>
        /// <param name="looping">多帧时是否循环。</param>
        /// <param name="startAtCurrentTime">多帧是否从当前时钟位置起播。</param>
        /// <param name="frameLength">单帧时长，留空即 60fps。</param>
        /// <param name="isPlaying">多帧是否自行推进；调用方自行驱动播放时传 <c>false</c>。</param>
        public Drawable? GetAnimation(EzAnimationRequest request, bool looping = true, bool startAtCurrentTime = true, double? frameLength = null, bool isPlaying = true)
            => createAnimationDrawable(GetTextureFrames(request), looping, startAtCurrentTime, frameLength, isPlaying);

        /// <summary>
        /// <see cref="GetAnimation(EzAnimationRequest, bool, bool, double?, bool)"/> 的路径快捷重载，用默认三模板解析。
        /// </summary>
        /// <param name="path">EzResources 相对路径，末段即资源名。</param>
        public Drawable? GetAnimation(string path, bool looping = true, bool startAtCurrentTime = true, double? frameLength = null, bool isPlaying = true)
            => GetAnimation(new EzAnimationRequest { Path = path }, looping, startAtCurrentTime, frameLength, isPlaying);

        /// <summary>
        /// 取请求对应的帧纹理；无资源时返回空数组。同一请求只解析一次并复用帧纹理，
        /// 但 <see cref="EzTextureUsage.Large"/> 例外（带引用计数，长期持有会让纹理无法回收）。
        /// </summary>
        public Texture[] GetTextureFrames(EzAnimationRequest request)
        {
            EzFrameSet set = resolveFrameSet(request);

            if (set.Keys.Count == 0)
                return Array.Empty<Texture>();

            Texture[]? cached = set.CachedTextures;

            if (cached != null)
                return cached;

            Texture[] materialised = materialiseFrames(set.Keys, request.Usage);

            return request.Usage == EzTextureUsage.Large ? materialised : set.CacheTextures(materialised);
        }

        // 只解析帧键、不解码：供工厂按自己的节奏做预解码（工厂的并行预解码是既有、已验证做法）。
        internal IReadOnlyList<string> ResolveFrameKeys(EzAnimationRequest request) => resolveFrameSet(request).Keys;

        #endregion

        #region 层3 场景消费方法

        /// <summary>
        /// 获取当前 note set 下一个组件的首帧（动画首帧即静态外观）。
        /// </summary>
        /// <param name="component">组件名称（如 "whitenote"）</param>
        public Texture? GetNote(string component)
        {
            var request = new EzAnimationRequest
            {
                Path = $"note/{noteSetName.Value}/{component}",
                Usage = EzTextureUsage.AnimationSafe,
            };

            return getFirstFrame(request);
        }

        /// <summary>
        /// 获取 Stage 组件的首帧；单帧大图走 <see cref="EzTextureUsage.Large"/>。
        /// 多帧 Stage 请用 <see cref="LoadStageFrames"/>。
        /// </summary>
        /// <param name="component">组件名称</param>
        public Texture? GetStage(string component)
        {
            var request = new EzAnimationRequest
            {
                Path = $"Stage/{stageName.Value}/Stage/{component}",
                Usage = EzTextureUsage.Large,
            };

            return getFirstFrame(request);
        }

        /// <summary>
        /// 加载 Stage 组件帧：多帧走 <see cref="EzTextureUsage.AnimationSafe"/>，无帧时回退单张
        /// <see cref="EzTextureUsage.Large"/> 静态图。
        /// </summary>
        /// <param name="basePath">基础路径（不含扩展名）</param>
        public List<Texture> LoadStageFrames(string basePath)
        {
            var framesRequest = new EzAnimationRequest
            {
                Path = basePath,
                Usage = EzTextureUsage.AnimationSafe,
                AllowSingleFallback = false,
            };

            IReadOnlyList<string> keys = resolveFrameSet(framesRequest).Keys;

            // 无多帧时按静态大图取单张（单图不能进 AnimationSafe 池被当帧 Dispose）。
            if (keys.Count == 0)
            {
                Texture? single = getFirstFrame(new EzAnimationRequest { Path = basePath, Usage = EzTextureUsage.Large });

                return single != null ? new List<Texture>(1) { single } : new List<Texture>();
            }

            var frames = new List<Texture>(keys.Count);

            foreach (string key in keys)
            {
                Texture? texture = Get(key, EzTextureUsage.AnimationSafe);

                if (texture != null)
                    frames.Add(texture);
            }

            return frames;
        }

        #endregion

        #region 层1 内部查询

        /// <summary>
        /// 列出目录内可成帧的前缀（层1 文件事实），用于「这个目录里有哪些动画」的枚举场景。
        /// 平铺命名（<c>{clip}-0.png</c>）的片段名即来自此处。
        /// </summary>
        internal IReadOnlyList<string> ListFramePrefixes(string directory) => directoryIndex.Get(directory).FramePrefixes;

        /// <summary>
        /// 列出目录内的子目录名，用于「子目录命名」（<c>{clip}/000.png</c>）的枚举场景。
        /// </summary>
        internal IReadOnlyList<string> ListSubdirectories(string directory) => directoryIndex.Get(directory).Subdirectories;

        /// <summary>
        /// 列出目录内所有图片的取像键（已按名排序），用于「无法预先知道名字、取第一张即可」的预览场景。
        /// </summary>
        internal IReadOnlyList<string> ListImageKeys(string directory) => directoryIndex.Get(directory).ImageKeys;

        #endregion

        #region 解析

        private Texture? getFirstFrame(EzAnimationRequest request)
        {
            IReadOnlyList<string> keys = resolveFrameSet(request).Keys;

            return keys.Count > 0 ? Get(keys[0], request.Usage) : null;
        }

        // 帧键与帧纹理分两层缓存：只要首帧的场景（GetNote / GetStage）只走键缓存，不触发整段解码。
        private EzFrameSet resolveFrameSet(EzAnimationRequest request)
        {
            if (frameSets.TryGetValue(request, out EzFrameSet? cached))
                return cached;

            return frameSets.GetOrAdd(request, buildFrameSet(request));
        }

        private EzFrameSet buildFrameSet(EzAnimationRequest request)
        {
            EzResourcePath.Split(request.Path, out string directory, out string name);

            if (name.Length == 0 || request.MaxFrames <= 0)
                return EzFrameSet.Empty;

            string? template = string.IsNullOrWhiteSpace(request.FrameTemplate) ? null : request.FrameTemplate!.Trim();

            IReadOnlyList<string> keys = template == null
                ? resolveDefaultKeys(request, directory, name)
                : resolveTemplateKeys(request, directory, name, template);

            if (keys.Count == 0 && request.AllowSingleFallback)
            {
                string? single = resolveSingleKey(directory, name, template);

                if (single != null)
                    keys = new[] { single };
            }

            return keys.Count == 0 ? EzFrameSet.Empty : new EzFrameSet(keys);
        }

        // 三模板默认：子目录 → 同层 → 单图，命中即停；优先级固定，不按帧数比较。
        private IReadOnlyList<string> resolveDefaultKeys(EzAnimationRequest request, string directory, string name)
        {
            // 模板3 子目录：[dir]/[name]/[前缀][连接符][数值]，连接符只认 - 与 _（纯数字文件名亦可，前缀为空）。
            // 只用于多帧动画：{name}{数值} 这种没有连接符的命名不算帧组。
            if (directoryIndex.Get(EzResourcePath.Combine(directory, name)).TryGetBestFrames(isFramePrefix, out EzFrameGroup? group))
                return collectKeys(group, request.StartIndex, request.MaxFrames);

            EzResourceDirectory parent = directoryIndex.Get(directory);

            // 模板2 同层：[dir]/[name][连接符][数值]，连接符同样只认 - 与 _。
            if (request.Separators is { Count: > 0 } separators)
            {
                foreach (string separator in separators)
                {
                    if (parent.TryGetFrames(name + separator, out group))
                        return collectKeys(group, request.StartIndex, request.MaxFrames);
                }

                return Array.Empty<string>();
            }

            if (parent.TryGetFrames(name + "-", out group) || parent.TryGetFrames(name + "_", out group))
                return collectKeys(group, request.StartIndex, request.MaxFrames);

            // 模板1 单图由 AllowSingleFallback 处理
            return Array.Empty<string>();
        }

        // 帧文件名的前缀：空（纯数字，如 note 的 000.png），或以连接符 - / _ 结尾。
        private static bool isFramePrefix(string prefix)
            => prefix.Length == 0 || prefix[^1] is '-' or '_';

        private IReadOnlyList<string> resolveTemplateKeys(EzAnimationRequest request, string directory, string name, string template)
        {
            if (!trySplitFrameTemplate(template, out string prefixText))
                return Array.Empty<string>();

            string expandable = prefixText;

            if (!containsNamePlaceholder(template))
                expandable = name + expandable;

            string expanded = substituteName(expandable, name);

            EzResourcePath.Split(expanded, out string prefixDirectory, out string prefix);

            EzResourceDirectory target = directoryIndex.Get(EzResourcePath.Combine(directory, prefixDirectory));

            return target.TryGetFrames(prefix, out EzFrameGroup? group)
                ? collectKeys(group, request.StartIndex, request.MaxFrames)
                : Array.Empty<string>();
        }

        // 无多帧时回退单图：模板不含序号时先试模板文本（如 {name}_overlay），再回退资源名本身。
        private string? resolveSingleKey(string directory, string name, string? template)
        {
            if (template != null && !trySplitFrameTemplate(template, out _))
            {
                string expanded = substituteName(containsNamePlaceholder(template) ? template : name + template, name);

                if (!string.Equals(expanded, name, StringComparison.Ordinal) && tryResolveExact(directory, expanded, out string? covered))
                    return covered;
            }

            return tryResolveExact(directory, name, out string? key) ? key : null;
        }

        private bool tryResolveExact(string directory, string relativeName, [NotNullWhen(true)] out string? key)
        {
            EzResourcePath.Split(relativeName, out string subDirectory, out string fileName);

            if (fileName.Length == 0)
            {
                key = null;
                return false;
            }

            return directoryIndex.Get(EzResourcePath.Combine(directory, subDirectory)).TryGetExact(fileName, out key);
        }

        // 从分组里取连续帧：跳过起始帧号之前的部分，遇到断号即停（停帧令只取前半段）。
        private static IReadOnlyList<string> collectKeys(EzFrameGroup group, int startIndex, int maxFrames)
        {
            IReadOnlyList<int> numbers = group.Numbers;
            IReadOnlyList<string> keys = group.Keys;

            int start = 0;

            while (start < numbers.Count && numbers[start] < startIndex)
                start++;

            if (start >= numbers.Count)
                return Array.Empty<string>();

            int count = 1;

            while (count < maxFrames
                   && start + count < numbers.Count
                   && numbers[start + count] == numbers[start + count - 1] + 1)
                count++;

            if (start == 0 && count == keys.Count)
                return keys;

            var slice = new string[count];

            for (int i = 0; i < count; i++)
                slice[i] = keys[start + i];

            return slice;
        }

        private Texture[] materialiseFrames(IReadOnlyList<string> keys, EzTextureUsage usage)
        {
            if (keys.Count == 1)
            {
                Texture? single = Get(keys[0], usage);

                return single != null ? new[] { single } : Array.Empty<Texture>();
            }

            var loaded = new Texture?[keys.Count];

            // 顺序解码。并行预解码只在经过验证的工厂里做，此处不引入 Parallel。
            for (int i = 0; i < keys.Count; i++)
                loaded[i] = Get(keys[i], usage);

            // 分组内帧号已连续，个别键解码失败时按序截断，避免动画中间出现空洞。
            int count = 0;

            while (count < loaded.Length && loaded[count] != null)
                count++;

            var result = new Texture[count];

            for (int i = 0; i < count; i++)
                result[i] = loaded[i]!;

            return result;
        }

        private static Drawable? createAnimationDrawable(Texture[] textures, bool looping, bool startAtCurrentTime, double? frameLength, bool isPlaying)
        {
            switch (textures.Length)
            {
                case 0:
                    return null;

                case 1:
                    return new Sprite { Texture = textures[0] };

                default:
                    var animation = new TextureAnimation(startAtCurrentTime)
                    {
                        DefaultFrameLength = frameLength ?? default_frame_length,
                        Loop = looping,
                        IsPlaying = isPlaying,
                    };

                    animation.AddFrames(textures);
                    return animation;
            }
        }

        #endregion

        #region 模板语法

        /// <summary>
        /// 拆出序号占位符（<c>{0}</c> / <c>{00}</c> …）之前的文本；无序号时返回 <c>false</c>（该模板表达单图）。
        /// </summary>
        /// <remarks>
        /// 只认最后一个占位符且允许其后跟图片后缀（如 <c>frame_{0}.png</c>）：位数只作说明，
        /// 实际宽度由磁盘分组决定；序号后还有其它文本的模板不当作帧模板，避免拼出永远不存在的键。
        /// </remarks>
        private static bool trySplitFrameTemplate(string template, out string prefixText)
        {
            prefixText = string.Empty;

            if (string.IsNullOrEmpty(template))
                return false;

            int open = -1;
            int digits = 0;

            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] != '{')
                    continue;

                int j = i + 1;

                while (j < template.Length && template[j] == '0')
                    j++;

                if (j == i + 1 || j >= template.Length || template[j] != '}')
                    continue;

                open = i;
                digits = j - (i + 1);
                i = j;
            }

            if (open < 0)
                return false;

            string tail = template[(open + digits + 2)..];

            if (tail.Length > 0 && !EzResourcePath.IsImage(tail, out _))
                return false;

            prefixText = template[..open];
            return true;
        }

        private static bool containsNamePlaceholder(string template)
            => template.Contains("{name}", StringComparison.Ordinal) || template.Contains("{result}", StringComparison.Ordinal);

        private static string substituteName(string text, string name)
        {
            if (text.Contains("{name}", StringComparison.Ordinal))
                text = text.Replace("{name}", name, StringComparison.Ordinal);

            if (text.Contains("{result}", StringComparison.Ordinal))
                text = text.Replace("{result}", name, StringComparison.Ordinal);

            return text;
        }

        #endregion
    }

    /// <summary>
    /// 一次请求解析出的帧键，以及按需物化的帧纹理。
    /// </summary>
    internal sealed class EzFrameSet
    {
        public static readonly EzFrameSet Empty = new EzFrameSet(Array.Empty<string>());

        private Texture[]? textures;

        public EzFrameSet(IReadOnlyList<string> keys)
        {
            Keys = keys;
        }

        public IReadOnlyList<string> Keys { get; }

        public Texture[]? CachedTextures => textures;

        public Texture[] CacheTextures(Texture[] value)
        {
            Texture[]? existing = Interlocked.CompareExchange(ref textures, value, null);

            return existing ?? value;
        }
    }
}
