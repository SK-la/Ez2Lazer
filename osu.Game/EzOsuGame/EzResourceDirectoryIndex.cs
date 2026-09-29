// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 目录索引：回答「这个 EzResources 目录里有哪些文件、某个名字的真实取像键是什么」。
    /// 用户 <c>EzResources/**</c> 与内置 <c>Textures/EzResources/**</c> 双根合并（用户优先），大小写不敏感，
    /// 并把「以数字结尾」的文件名拆成 <c>(前缀, 补零宽度, 帧号)</c> 分组。
    /// </summary>
    /// <remarks>
    /// 只管文件事实，不含动画语义：哪个前缀算一组、命中顺序、如何回退都由上层决定。
    /// 内置换算已由 <see cref="EzEmbeddedResourceStore"/> 完成，故此处只见逻辑名 <c>6k</c>。
    /// 目录内容按需惰性枚举一次并缓存：用户根直接按目录 <c>GetFiles</c>（不递归、不扫全树），
    /// 内置根因资源清单是扁平的，首次需要时折叠成「目录 → 文件」映射后复用。
    /// <para>
    /// 取像键保持无后缀的逻辑名：纹理加载链自己会补 png/jpg 探针，而 <c>TextureStore</c> 的缓存键就是传入名本身，
    /// 索引若返回 <c>x.png</c>，与各处既有的 <c>x</c> 取像就会各解一遍码。只有加载链默认探不到的格式（如 <c>.gif</c>）
    /// 才保留真实文件名，否则那份资源根本取不到。
    /// </para>
    /// </remarks>
    internal sealed class EzResourceDirectoryIndex
    {
        // 后缀优先级：同名不同后缀时 png 先进表，即 png 优先被取到。
        private static readonly string[] extension_priority = { ".png", ".jpg", ".jpeg", ".gif" };

        // 根优先级：用户 EzResources 覆盖内置根。
        private const int user_root_rank = 0;
        private const int embedded_root_rank = 1;

        private readonly Storage? userRoot;
        private readonly IResourceStore<byte[]>? embeddedRoot;

        private readonly ConcurrentDictionary<string, EzResourceDirectory> directories = new ConcurrentDictionary<string, EzResourceDirectory>(StringComparer.OrdinalIgnoreCase);
        private readonly object embeddedLock = new object();
        private Dictionary<string, List<string>>? embeddedFiles;

        /// <param name="userRoot">用户 <c>EzResources</c> 存储根；可为 <c>null</c>。</param>
        /// <param name="embeddedRoot">已归一的内置根；可为 <c>null</c>。</param>
        public EzResourceDirectoryIndex(Storage? userRoot, IResourceStore<byte[]>? embeddedRoot)
        {
            this.userRoot = userRoot;
            this.embeddedRoot = embeddedRoot;
        }

        /// <summary>
        /// 取目录内容；目录不存在时返回空目录（不抛异常）。
        /// </summary>
        public EzResourceDirectory Get(string directory) => directories.GetOrAdd(EzResourcePath.Normalise(directory), build);

        /// <summary>
        /// 丢弃已缓存的目录内容，使新放进 <c>EzResources</c> 的文件重新可见。内置根清单不可变，无需重建。
        /// </summary>
        public void Invalidate() => directories.Clear();

        internal int CachedDirectoryCount => directories.Count;

        private EzResourceDirectory build(string directory)
        {
            var result = new EzResourceDirectory(directory);

            if (userRoot != null && userRoot.ExistsDirectory(directory))
            {
                foreach (string path in userRoot.GetFiles(directory))
                    result.AddFile(EzResourcePath.FileName(path), user_root_rank);

                foreach (string path in userRoot.GetDirectories(directory))
                    result.AddSubdirectory(EzResourcePath.FileName(path));
            }

            if (embeddedRoot != null && getEmbeddedFiles().TryGetValue(directory, out List<string>? embedded))
            {
                foreach (string file in embedded)
                    result.AddFile(file, embedded_root_rank);
            }

            result.Freeze();
            return result;
        }

        // 内置资源清单是扁平的（程序集里没有目录概念），折叠一次后按目录复用。
        private Dictionary<string, List<string>> getEmbeddedFiles()
        {
            Dictionary<string, List<string>>? cached = embeddedFiles;

            if (cached != null)
                return cached;

            lock (embeddedLock)
            {
                if (embeddedFiles != null)
                    return embeddedFiles;

                var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (string resource in embeddedRoot!.GetAvailableResources())
                {
                    if (!EzResourcePath.IsImage(resource, out _))
                        continue;

                    int slash = resource.LastIndexOf('/');
                    string directory = slash < 0 ? string.Empty : resource[..slash];
                    string fileName = slash < 0 ? resource : resource[(slash + 1)..];

                    if (!map.TryGetValue(directory, out List<string>? files))
                        map[directory] = files = new List<string>();

                    files.Add(fileName);
                }

                embeddedFiles = map;
                return map;
            }
        }

        internal static int extensionRank(string fileName)
        {
            int dot = fileName.LastIndexOf('.');

            if (dot <= 0)
                return extension_priority.Length;

            // 排序期热路径，故不借 EzResourcePath.Extension（那里会分配小写串）。
            ReadOnlySpan<char> extension = fileName.AsSpan(dot);

            for (int i = 0; i < extension_priority.Length; i++)
            {
                if (extension.Equals(extension_priority[i], StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return extension_priority.Length;
        }
    }

    /// <summary>
    /// 单个 EzResources 目录的内容：文件名到取像键的映射、帧分组、子目录名。
    /// </summary>
    internal sealed class EzResourceDirectory
    {
        private readonly Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EzFrameGroup> frames = new Dictionary<string, EzFrameGroup>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> subdirectories = new List<string>();
        private readonly List<(string Name, int Root)> pendingFiles = new List<(string Name, int Root)>();
        private readonly List<string> imageKeys = new List<string>();

        // 已登记的取像键集合：同名文件跨根（用户覆盖内置）或同名不同后缀只登记一次，即先到者胜。
        private readonly HashSet<string> registeredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string[] prefixes = Array.Empty<string>();
        private EzFrameGroup? bestFrames;
        private bool frozen;

        public EzResourceDirectory(string directory)
        {
            Directory = directory;
        }

        /// <summary>逻辑目录路径；根目录为空串。</summary>
        public string Directory { get; }

        public IReadOnlyList<string> Subdirectories => subdirectories;

        /// <summary>该目录内所有图片的逻辑取像键，按文件名忽略大小写排序。</summary>
        public IReadOnlyList<string> ImageKeys => imageKeys;

        /// <summary>该目录内所有可成帧的前缀（含裸数字文件的空前缀），供上层做前缀过滤。</summary>
        public IReadOnlyList<string> FramePrefixes => prefixes;

        /// <summary>
        /// 按名字取真实取像键；查什么名字就匹配什么名字，不折叠后缀。
        /// </summary>
        public bool TryGetExact(string name, [NotNullWhen(true)] out string? key) => exact.TryGetValue(name, out key);

        /// <summary>
        /// 按前缀取帧组。模板里写的补零位数不参与匹配——磁盘上是几位就按几位成组，故 <c>{0}</c> 与 <c>{000}</c> 都能吃下。
        /// </summary>
        public bool TryGetFrames(string prefix, [NotNullWhen(true)] out EzFrameGroup? group) => frames.TryGetValue(prefix, out group);

        /// <summary>取满足 <paramref name="accept"/> 的一组（并列取补零宽度更窄者、再按前缀序）；不比较帧数。</summary>
        /// <param name="accept">前缀过滤器；<c>null</c> 表示接受任意前缀。</param>
        /// <param name="group">选中的组；无满足条件者时为 <c>null</c>。</param>
        public bool TryGetBestFrames(Func<string, bool>? accept, [NotNullWhen(true)] out EzFrameGroup? group)
        {
            group = null;

            if (bestFrames != null && (accept == null || accept(bestFrames.Prefix)))
            {
                group = bestFrames;
                return true;
            }

            foreach (EzFrameGroup candidate in frames.Values)
            {
                if (accept != null && !accept(candidate.Prefix))
                    continue;

                group = isBetter(candidate, group) ? candidate : group;
            }

            return group != null;
        }

        public void AddFile(string fileName, int rootRank)
        {
            if (!frozen)
                pendingFiles.Add((fileName, rootRank));
        }

        public void AddSubdirectory(string name)
        {
            if (!frozen && name.Length > 0)
                subdirectories.Add(name);
        }

        public void Freeze()
        {
            if (frozen)
                return;

            frozen = true;

            pendingFiles.Sort(compareFiles);

            foreach ((string fileName, _) in pendingFiles)
            {
                if (!EzResourcePath.IsImage(fileName, out _))
                    continue;

                string key = keyOf(fileName);

                // 同名文件跨根（用户覆盖内置）或同名不同后缀：只登记先到的那个，用户根恒在前。
                if (!registeredKeys.Add(key))
                    continue;

                imageKeys.Add(key);
                registerExact(fileName, key);
                registerFrame(fileName, key);
            }

            pendingFiles.Clear();

            if (frames.Count > 0)
            {
                prefixes = new string[frames.Count];
                frames.Keys.CopyTo(prefixes, 0);
                Array.Sort(prefixes, StringComparer.OrdinalIgnoreCase);

                foreach (EzFrameGroup group in frames.Values)
                    bestFrames = isBetter(group, bestFrames) ? group : bestFrames;
            }
        }

        // 取像键：默认去掉后缀（加载链自会补 png/jpg 探针），仅加载链探不到的格式保留真实文件名。
        // TODO: .gif 本期按单帧处理（保留后缀才取得到）；多帧解码留作后续在同一处扩展。
        private string keyOf(string fileName)
        {
            string extension = EzResourcePath.Extension(fileName);

            string logicalName = extension is ".png" or ".jpg" ? EzResourcePath.Stem(fileName) : fileName;

            return EzResourcePath.Combine(Directory, logicalName);
        }

        // 无后缀与含后缀两种写法都登记，指向同一取像键。
        private void registerExact(string fileName, string key)
        {
            string stem = EzResourcePath.Stem(fileName);

            if (stem.Length > 0 && !exact.ContainsKey(stem))
                exact.Add(stem, key);

            if (!string.Equals(fileName, stem, StringComparison.Ordinal) && !exact.ContainsKey(fileName))
                exact.Add(fileName, key);
        }

        private void registerFrame(string fileName, string key)
        {
            string stem = EzResourcePath.Stem(fileName);

            // 末尾连续的 ASCII 数字即帧号，其前为前缀（可为空，如裸数字文件名 000.png）。
            int digitsStart = stem.Length;

            while (digitsStart > 0 && char.IsAsciiDigit(stem[digitsStart - 1]))
                digitsStart--;

            if (digitsStart >= stem.Length)
                return;

            string prefix = stem[..digitsStart];
            string digits = stem[digitsStart..];

            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                return;

            if (frames.TryGetValue(prefix, out EzFrameGroup? existing))
            {
                existing.Add(index, key, digits.Length);
                return;
            }

            var group = new EzFrameGroup(prefix, digits.Length);
            group.Add(index, key, digits.Length);
            frames[prefix] = group;
        }

        private static bool isBetter(EzFrameGroup candidate, EzFrameGroup? current)
        {
            if (current == null)
                return true;

            if (candidate.Width != current.Width)
                return candidate.Width < current.Width;

            return string.CompareOrdinal(candidate.Prefix, current.Prefix) < 0;
        }

        // 先按根优先级（用户覆盖内置），再按后缀优先级（同名时 png 胜出），最后按名忽略大小写。
        private static int compareFiles((string Name, int Root) a, (string Name, int Root) b)
        {
            int byRoot = a.Root.CompareTo(b.Root);

            if (byRoot != 0)
                return byRoot;

            int byExtension = EzResourceDirectoryIndex.extensionRank(a.Name).CompareTo(EzResourceDirectoryIndex.extensionRank(b.Name));

            return byExtension != 0 ? byExtension : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 同一前缀下、按帧号升序的一组帧；<see cref="Width"/> 为该组帧号观察到的最窄补零宽度。
    /// </summary>
    internal sealed class EzFrameGroup
    {
        private readonly SortedDictionary<int, string> byIndex = new SortedDictionary<int, string>();

        private string[]? keys;
        private int[]? numbers;

        public EzFrameGroup(string prefix, int width)
        {
            Prefix = prefix;
            Width = width;
        }

        public string Prefix { get; }

        public int Width { get; private set; }

        public int Count => byIndex.Count;

        public IReadOnlyList<string> Keys => keys ??= byIndex.Values.ToArray();

        public IReadOnlyList<int> Numbers => numbers ??= byIndex.Keys.ToArray();

        public void Add(int index, string key, int width)
        {
            if (width < Width)
                Width = width;

            if (!byIndex.ContainsKey(index))
                byIndex.Add(index, key);
        }
    }
}
