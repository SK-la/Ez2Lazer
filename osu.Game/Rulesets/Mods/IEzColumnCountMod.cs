// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// 在后转换阶段改变谱面列数的 mod。
    /// </summary>
    /// <remarks>
    /// 真正转换出的谱面可读时，一律以 <c>ManiaBeatmap.Stages</c> 为准；本接口只服务于拿不到产物的场合——
    /// 选歌界面的键数展示、分析侧的列数补全。这些场合只有元数据与 mod 列表，所以由实现方按
    /// <see cref="IEzApplyOrder"/> 顺序把自己那一步的列数讲清楚。
    /// <para>
    /// 实现方必须与 <c>ApplyToBeatmap</c> 的结果一致：列数展示对不上时，用户会拿错误的键数去截图和比较。
    /// </para>
    /// </remarks>
    public interface IEzColumnCountMod
    {
        /// <summary>
        /// 给出当前列数后本 mod 产生的列数。无法预先判定时原样返回 <paramref name="currentColumns"/>。
        /// </summary>
        int ResolveColumnCount(int currentColumns);
    }
}
