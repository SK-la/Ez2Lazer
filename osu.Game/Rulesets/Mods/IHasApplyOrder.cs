// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// 提供一个简单的排序接口，用于调整不同Mod的应用顺序。
    /// <para></para>主要帮助<see cref="IApplicableAfterBeatmapConversion"/> 在正确环节处理谱面。
    /// <para>处理顺序从0开始，数字越大，优先级越靠后。</para>
    /// </summary>
    public interface IHasApplyOrder
    {
        int ApplyOrder { get; }
    }
}
