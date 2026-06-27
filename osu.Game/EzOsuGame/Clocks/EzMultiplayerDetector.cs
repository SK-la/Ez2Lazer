// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Spectator;

namespace osu.Game.EzOsuGame.Clocks
{
    /// <summary>
    /// 多人上下文检测辅助类。
    ///
    /// 在 multiplayer / spectate / matchmaking 路径下，游戏框架会通过 DI 注入对应的 client；
    /// 单人 / 测试 / 编辑器路径下，这些 client 不存在（或为占位）。
    /// 我们用 "DI 中是否存在任一 client 实例" 作为多人上下文的近似判定；
    /// 测试场景下这些实例都不会被注入，从而保证单人 / 测试行为不受影响。
    /// </summary>
    public static class EzMultiplayerDetector
    {
        /// <summary>
        /// 给定一个 <see cref="IReadOnlyDependencyContainer"/>，判断当前是否处于多人 / 旁观 / 撮合上下文。
        /// 命中任一活跃 client 即视为多人上下文。
        /// </summary>
        public static bool IsMultiplayerContext(IReadOnlyDependencyContainer? dependencies)
        {
            if (dependencies == null)
                return false;

            // 任一接口能解析出非 null 实例即视为多人上下文。
            return dependencies.Get<IMultiplayerClient>() != null
                   || dependencies.Get<ISpectatorClient>() != null
                   || dependencies.Get<IMatchmakingClient>() != null;
        }
    }
}
