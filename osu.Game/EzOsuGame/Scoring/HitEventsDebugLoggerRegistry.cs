// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// HitEvents 调试日志记录器接口。
    /// 由各 Ruleset 实现（目前是 Mania）并注册到全局。
    /// </summary>
    public interface IHitEventsDebugLogger
    {
        void LogLiveHitEvents(ScoreInfo score);

        void LogSessionHitEvents(ScoreInfo score);
    }

    /// <summary>
    /// 全局 HitEvents 调试日志记录器注册表。
    /// </summary>
    public static class HitEventsDebugLoggerRegistry
    {
        private static IHitEventsDebugLogger? s_logger;

        public static void Register(IHitEventsDebugLogger logger)
        {
            s_logger = logger;
        }

        public static void LogLiveHitEvents(ScoreInfo score)
        {
            s_logger?.LogLiveHitEvents(score);
        }

        public static void LogSessionHitEvents(ScoreInfo score)
        {
            s_logger?.LogSessionHitEvents(score);
        }
    }
}
