// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge
{
    /// <summary>
    /// 为 std 判定仿真取一份独立 beatmap：按 <c>(working beatmap, ruleset, mods, JudgeMode)</c> 从
    /// <see cref="EzPlayableBeatmapCache"/> 取本 JudgeMode 专属的副本，并已绑定对应判定语义。
    /// </summary>
    /// <remarks>
    /// <para>
    /// StableClassic 的窗口替换与滑条 classic 语义都是就地改写，所以仿真不能使用调用方传进来的实例——
    /// 那可能是本局正在游玩的实例，也可能是 Race 里被多个 JudgeMode 共享的 session-ready 实例。
    /// 共享实例被绑成 stable 语义后，同一实例上的 Lazer 判定会读到 stable 窗口（串扰）。
    /// </para>
    /// <para>
    /// 副本走与 live 完全相同的转换管线，避免依赖私有字段的反射深拷贝。
    /// </para>
    /// </remarks>
    internal sealed class OsuSimulationBeatmapProvider
    {
        private readonly IWorkingBeatmapCache beatmapCache;

        public OsuSimulationBeatmapProvider(IWorkingBeatmapCache beatmapCache)
        {
            this.beatmapCache = beatmapCache ?? throw new ArgumentNullException(nameof(beatmapCache));
        }

        /// <summary>
        /// 返回可自由绑定的仿真副本。无法产出时返回 null：调用方必须自行回退，
        /// 不能假定一定拿得到副本。
        /// </summary>
        public IBeatmap? TryCreate(Score score, IGameplayEnvironment environment, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score);
            ArgumentNullException.ThrowIfNull(environment);

            var scoreInfo = score.ScoreInfo;

            if (scoreInfo?.BeatmapInfo == null)
                return null;

            var workingBeatmap = beatmapCache.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

            // 谱面不在本地（已被删除 / 尚未下载）时没有可转换的源：交由调用方回退。
            if (workingBeatmap is DummyWorkingBeatmap)
                return null;

            var mods = EzModCompatibility.StripUnknown(scoreInfo.Mods);
            EzEnumJudgeMode judgeMode = environment.JudgeMode;

            return EzPlayableBeatmapCache.GetBound(workingBeatmap, scoreInfo.Ruleset, mods, bindingScopeFor(judgeMode),
                playable => bindForSimulation(playable, judgeMode), cancellationToken);
        }

        private static void bindForSimulation(IBeatmap beatmap, EzEnumJudgeMode judgeMode)
        {
            if (OsuStableClassic.IsActive(judgeMode))
                OsuStableClassic.BindSemantics(beatmap);
        }

        /// <remarks>
        /// 作用域按 JudgeMode 分段，且避开 <see cref="EzScoreRacePlayableResolver.SESSION_READY_SCOPE"/>：
        /// 后者是「未绑定判定语义」的转换产物，两者混用会把 classic 语义泄漏到其它判定档。
        /// </remarks>
        private static int bindingScopeFor(EzEnumJudgeMode judgeMode) => 2000 + (int)judgeMode;
    }
}
