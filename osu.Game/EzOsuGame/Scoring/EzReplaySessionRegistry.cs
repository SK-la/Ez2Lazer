// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 各规则集 Replay Session 实现的静态注册表。
    /// Ruleset 初始化时调用 <see cref="Register"/> 注册实现；
    /// osu.Game 通过 <see cref="Instance"/> 注入到消费者（Graph / Panel / Race）。
    /// </summary>
    public static class EzReplaySessionRegistry
    {
        /// <summary>
        /// 当前注册的 Session 实现。可能在 Mania 模块加载前为 null。
        /// </summary>
        public static IEzReplaySession? Instance { get; private set; }

        /// <summary>
        /// 注册规则集的 Session 实现（由 ManiaRuleset 初始化时调用）。
        /// 同一进程只注册一次。
        /// </summary>
        public static void Register(IEzReplaySession session)
        {
            if (Instance != null)
                return;

            Instance = session;
        }
    }
}
