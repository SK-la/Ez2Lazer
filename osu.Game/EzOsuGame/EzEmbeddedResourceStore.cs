// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 内置 EzResources 根（资源程序集下的 <c>Textures/EzResources</c>）的适配器：MSBuild 把「以数字开头的目录」
    /// 嵌进程序集时会写成 <c>_6k</c> / <c>_7k</c>，此处把这一打包产物在取像与枚举两个方向归一，
    /// 使索引与调用方只需面对逻辑名 <c>6k</c>。
    /// </summary>
    /// <remarks>
    /// 归一放在内置根而非目录索引，是因为它只属于「内置程序集」这一个物理根的怪癖，不是 EzResources 的命名规则。
    /// 代价可控：目录段不含数字开头时不做任何分配、直接透传；枚举方向用 <c>yield</c> 逐个翻译，不物化全量列表。
    /// 只有目录段参与翻译——MSBuild 不会改写文件名，故 <c>Dans/6k/7.png</c> 里的 <c>7.png</c> 保持原样。
    /// </remarks>
    internal sealed class EzEmbeddedResourceStore : IResourceStore<byte[]>
    {
        private readonly IResourceStore<byte[]> inner;

        public EzEmbeddedResourceStore(IResourceStore<byte[]> inner)
        {
            this.inner = inner;
        }

        // 先按传入名取，未命中再试改写形式，故 6k 与 _6k 两种写法都能取到。
        // 接口把返回值标注为非空，但「未命中返回 null」是本存储链的约定，故未命中处用 null! 显式声明。
        public byte[] Get(string name)
        {
            byte[]? result = inner.Get(name);

            if (result != null)
                return result;

            return tryCreateFlipped(name, out string flipped) ? inner.Get(flipped)! : null!;
        }

        public async Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            byte[]? result = await inner.GetAsync(name, cancellationToken).ConfigureAwait(false);

            if (result != null)
                return result;

            return tryCreateFlipped(name, out string flipped)
                ? (await inner.GetAsync(flipped, cancellationToken).ConfigureAwait(false))!
                : null!;
        }

        public Stream? GetStream(string name)
        {
            Stream? result = inner.GetStream(name);

            if (result != null)
                return result;

            return tryCreateFlipped(name, out string flipped) ? inner.GetStream(flipped) : null;
        }

        public IEnumerable<string> GetAvailableResources()
        {
            foreach (string name in inner.GetAvailableResources())
                yield return toLogicalName(name);
        }

        public void Dispose() => inner.Dispose();

        // 枚举方向：_6k → 6k。返回值可能即入参本身（无改动时不做分配）。
        private static string toLogicalName(string name)
        {
            int lastSlash = name.LastIndexOf('/');
            int scan = 0;

            while (scan < lastSlash)
            {
                int slash = name.IndexOf('/', scan);
                Debug.Assert(slash >= 0);

                if (isUnderscoredNumber(name, scan, slash))
                    return stripUnderscore(name, scan);

                scan = slash + 1;
            }

            return name;
        }

        // 取像方向：6k → _6k 与 _6k → 6k 都试；只翻目录段里的第一处，翻完即返回。
        private static bool tryCreateFlipped(string name, out string flipped)
        {
            flipped = name;
            int lastSlash = name.LastIndexOf('/');
            int scan = 0;

            while (scan < lastSlash)
            {
                int slash = name.IndexOf('/', scan);
                Debug.Assert(slash >= 0);
                int length = slash - scan;

                if (length > 0 && char.IsAsciiDigit(name[scan]))
                {
                    // 6k → _6k（MSBuild 的嵌入形式）
                    flipped = string.Concat(name.AsSpan(0, scan), "_", name.AsSpan(scan));
                    return true;
                }

                if (isUnderscoredNumber(name, scan, slash))
                {
                    // _6k → 6k（调用方已按嵌入形式传入）
                    flipped = stripUnderscore(name, scan);
                    return true;
                }

                scan = slash + 1;
            }

            return false;
        }

        private static bool isUnderscoredNumber(string name, int start, int end)
            => end - start >= 2 && name[start] == '_' && char.IsAsciiDigit(name[start + 1]);

        private static string stripUnderscore(string name, int start)
            => string.Concat(name.AsSpan(0, start), name.AsSpan(start + 1));
    }
}
