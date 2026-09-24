// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 把 hitmode 绑进 beatmap 的唯一入口：就地写 <c>HitWindows</c>（<see cref="ManiaWindowBaker"/>）
    /// 与 <c>HitObject.Judgement</c>（<see cref="ManiaEnvironmentJudgements"/>）。
    /// </summary>
    /// <remarks>
    /// 绑定是就地改写，所以一个实例只应承载一个 hitmode。仿真侧的保证来自
    /// <see cref="ManiaSimulationBeatmapProvider"/>：每个 <c>(working beatmap, ruleset, mods, hitmode)</c> 都有自己的实例。
    /// 冲突因此按来源分档：
    /// <list type="bullet">
    /// <item>provider 产出的实例（<see cref="BindForSimulation"/> 标记为 owned）出现异 hitmode 绑定 ⇒ provider 的键或缓存逻辑有 bug，DEBUG 直接断言。</item>
    /// <item>其它实例（测试的静态注入、调用方自备且反复复用的实例）沿用 last-wins 并打点，保持既有行为。</item>
    /// </list>
    /// </remarks>
    internal static class ManiaBeatmapBinding
    {
        private const int unbound = -1;

        private static int sharedBindingWarnCount;

        private sealed class BindingRecord
        {
            public int BoundHitMode = unbound;
            public bool Owned;
        }

        private static readonly ConditionalWeakTable<IBeatmap, BindingRecord> records = new ConditionalWeakTable<IBeatmap, BindingRecord>();

        /// <summary>本地游玩：绑定 hitmode，并初始化 O2 HUD / press-time BPM 所需的运行态。</summary>
        public static void BindForLive(IBeatmap beatmap, IGameplayEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            ArgumentNullException.ThrowIfNull(environment);

            record(beatmap, environment.ManiaHitMode, owned: false);
            ManiaWindowBaker.AlignForLive(beatmap, environment);
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, environment.ManiaHitMode);
        }

        /// <summary>
        /// 仿真：对齐 <c>HitWindows</c> 并绑定 Judgement。Session 用它绑定拿到的实例；
        /// <paramref name="providerOwned"/> 由 <see cref="ManiaSimulationBeatmapProvider"/> 为自己的副本声明归属，
        /// 声明之后若再被绑到别的 hitmode，按 provider 的实现缺陷处理（DEBUG 断言）。
        /// </summary>
        public static void BindForSimulation(IBeatmap beatmap, EzEnumHitMode hitMode, bool providerOwned = false)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            record(beatmap, hitMode, providerOwned);
            ManiaWindowBaker.Align(beatmap, hitMode);
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, hitMode);
        }

        /// <summary>
        /// 仅绑定 Judgement，不动 <c>HitWindows</c>。
        /// <see cref="Scoring.ManiaScoreProcessor"/> 全谱模拟沿用此入口：新增的 Tail/Tick 判定必须早于 base，
        /// 否则 EZ2AC / Malody 会把官方尾 Perfect 计入 MaximumBaseScore。
        /// </summary>
        public static void BindJudgements(IBeatmap beatmap, EzEnumHitMode hitMode)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            record(beatmap, hitMode, owned: false);
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, hitMode);
        }

        private static void record(IBeatmap beatmap, EzEnumHitMode hitMode, bool owned)
        {
            warnIfSharedInstanceBound(beatmap, hitMode, owned);

            var entry = records.GetValue(beatmap, _ => new BindingRecord());
            bool conflict;
            bool isOwned;

            lock (entry)
            {
                conflict = entry.BoundHitMode != unbound && entry.BoundHitMode != (int)hitMode;
                entry.BoundHitMode = (int)hitMode;
                entry.Owned |= owned;
                isOwned = entry.Owned;
            }

            if (!conflict)
                return;

            ManiaJudgeHotPathTrace.RecordBeatmapRebindConflict();

            string message = $"[ManiaJudgeBinding] beatmap rebound to a different hitmode (now {hitMode}, owned={isOwned}, "
                             + $"objects={beatmap.HitObjects.Count}); one instance must not carry two hitmodes.";

            Logger.Log(message, Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

            if (isOwned)
                Debug.Fail(message);
        }

        /// <summary>
        /// 共享缓存条目（<see cref="EzPlayableBeatmapCache.GetShared"/>）按契约只读：对它做就地绑定，等于把一个 hitmode 的
        /// Judgement / HitWindows 泄漏给同进程里拿到同一实例的其它消费者，而且是 last-wins。
        /// </summary>
        /// <remarks>
        /// 只报不改：这是调用方用错了入口（该走 <see cref="EzPlayableBeatmapCache.GetBound"/>），改绑定语义会把问题藏起来。
        /// </remarks>
        private static void warnIfSharedInstanceBound(IBeatmap beatmap, EzEnumHitMode hitMode, bool owned)
        {
            if (owned || !EzPlayableBeatmapCache.IsSharedInstance(beatmap))
                return;

            if (Interlocked.Increment(ref sharedBindingWarnCount) > 10)
                return;

            string message = $"[ManiaJudgeBinding] bound a shared EzPlayableBeatmapCache instance in place (hitmode {hitMode}, "
                             + $"objects={beatmap.HitObjects.Count}); shared instances are read-only, ask for GetBound instead.";

            Logger.Log(message, Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            Debug.Fail(message);
        }
    }
}
