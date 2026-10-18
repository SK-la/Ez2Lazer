// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.Diagnostics
{
    /// <summary>
    /// 密集下落标定用的消融开关。仅 Debug 构建可切换；Release 下全部为编译期常量 false。
    /// <para>
    /// 与 <see cref="ManiaHoldAblation"/> 的分工：这里只放「皮肤订阅」和「每帧布局写入」两项，
    /// 其余 hold / tick / 描边开关继续沿用 <see cref="ManiaHoldAblation"/>。
    /// </para>
    /// </summary>
    public static class ManiaDenseScrollAblation
    {
#if DEBUG
        /// <summary>跳过 <c>ColumnWatcher</c> 的订阅登记与释放，用于量化静态字典 + 锁的占比。</summary>
        public static bool DisableColumnWatcherSubscriptions;

        /// <summary>跳过皮肤组件的每帧布局写入，用于量化 layout invalidation 的占比。</summary>
        public static bool DisablePerFrameLayoutWrites;

        /// <summary>
        /// 在没有 <c>DrawableManiaRuleset</c> 的场景里强制启用列级路由：到期由 <c>LaneController</c> 统一落判，
        /// 每条 drawable 不再每帧走 <c>UpdateResult(false)</c>。用于量化该轮询在标定结果里的占比。
        /// </summary>
        public static bool ForceColumnRoutedAutoMiss;

        /// <summary>LN 的掩码容器不启用 <c>Masking</c>，用于量化每帧掩码更新的占比。</summary>
        public static bool DisableHoldMasking;

        /// <summary>LN 不创建 body / tail 的掩码代理，用于量化每条 LN 两个 proxy 的占比。</summary>
        public static bool DisableHoldProxies;

        /// <summary>
        /// LN 的三段像素改由一个合成 drawable 绘制，head / body / tail 判定框不再带视觉。
        /// 切换需要重建 drawable 才能生效。
        /// </summary>
        public static bool SplitHoldVisual;

        public static void Reset()
        {
            DisableColumnWatcherSubscriptions = false;
            DisablePerFrameLayoutWrites = false;
            ForceColumnRoutedAutoMiss = false;
            DisableHoldMasking = false;
            DisableHoldProxies = false;
            SplitHoldVisual = false;
        }
#else
        public const bool DisableColumnWatcherSubscriptions = false;
        public const bool DisablePerFrameLayoutWrites = false;
        public const bool ForceColumnRoutedAutoMiss = false;
        public const bool DisableHoldMasking = false;
        public const bool DisableHoldProxies = false;
        public const bool SplitHoldVisual = false;

        public static void Reset()
        {
        }
#endif
    }
}
