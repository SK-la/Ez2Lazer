// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 一次动画帧加载请求：<see cref="Path"/> 指向 EzResources 下的资源，末段即资源名。
    /// 解析规则（三模板、连接符、前缀分级）只在 <see cref="EzResourceStore"/> 实现一次，此处只表达意图。
    /// </summary>
    /// <example>
    /// <code>
    /// // 默认三模板：子目录 → 同层 → 单图，命中即停
    /// new EzAnimationRequest { Path = "note/circle/whitenote" }
    ///
    /// // 显式模板：模板里带序号即多帧、不带即单图
    /// new EzAnimationRequest { Path = "GameTheme/AIR/judgement/Cool", FrameTemplate = "{name}/frame_{0}" }
    /// new EzAnimationRequest { Path = "Modify/Tachie/kiana", FrameTemplate = "{name}_overlay" }
    /// </code>
    /// </example>
    public sealed class EzAnimationRequest : IEquatable<EzAnimationRequest>
    {
        /// <summary>
        /// EzResources 相对路径，末段即资源名。如 <c>"GameTheme/AIR/judgement/Cool"</c> 拆为目录
        /// <c>GameTheme/AIR/judgement</c> 与名字 <c>Cool</c>。
        /// </summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// 可选的帧名模板，留空即启用三模板默认解析。支持的占位符：
        /// <c>{name}</c>（<c>{result}</c> 为其别名）与 <c>{0}</c> / <c>{00}</c> / <c>{000}</c>（序号位数只作说明，
        /// 实际位数由磁盘分组决定）。不含序号时该模板表达单图，如 <c>{name}_overlay</c>；
        /// 含序号但不含 <c>{name}</c> 时（如 <c>{0}</c>）自动把资源名前置。
        /// </summary>
        public string? FrameTemplate { get; init; }

        /// <summary>
        /// 可选的连接符白名单，按给出顺序优先；留空则用默认 <c>{name}-</c> → <c>{name}_</c>。
        /// 连接符只认 <c>-</c> 与 <c>_</c>：<c>{name}{数值}</c> 这种无分隔符命名不在解析内，
        /// 需要时在 <see cref="FrameTemplate"/> 里显式写 <c>{name}{0}</c>。
        /// </summary>
        public IReadOnlyList<string>? Separators { get; init; }

        /// <summary>起始帧号，小于该号的帧不取。</summary>
        public int StartIndex { get; init; }

        /// <summary>帧数上限。</summary>
        public int MaxFrames { get; init; } = 240;

        /// <summary>帧所用的纹理池，默认 <see cref="EzTextureUsage.AnimationSafe"/>。</summary>
        public EzTextureUsage Usage { get; init; } = EzTextureUsage.AnimationSafe;

        /// <summary>无多帧时是否回退到单图。</summary>
        public bool AllowSingleFallback { get; init; } = true;

        public bool Equals(EzAnimationRequest? other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (!string.Equals(Path, other.Path, StringComparison.Ordinal)
                || !string.Equals(FrameTemplate, other.FrameTemplate, StringComparison.Ordinal)
                || StartIndex != other.StartIndex
                || MaxFrames != other.MaxFrames
                || Usage != other.Usage
                || AllowSingleFallback != other.AllowSingleFallback)
                return false;

            if (Separators == null || other.Separators == null)
                return ReferenceEquals(Separators, other.Separators);

            if (Separators.Count != other.Separators.Count)
                return false;

            for (int i = 0; i < Separators.Count; i++)
            {
                if (!string.Equals(Separators[i], other.Separators[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as EzAnimationRequest);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            hash.Add(Path, StringComparer.Ordinal);

            if (FrameTemplate != null)
                hash.Add(FrameTemplate, StringComparer.Ordinal);

            hash.Add(StartIndex);
            hash.Add(MaxFrames);
            hash.Add(Usage);
            hash.Add(AllowSingleFallback);

            if (Separators != null)
            {
                foreach (string separator in Separators)
                    hash.Add(separator, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }

        public override string ToString() => $"{Path} (template={FrameTemplate ?? "<默认三模板>"}, {Usage})";
    }
}
