// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 为判定仿真取一份独立 beatmap：按 <c>(working beatmap, ruleset, mods, hitmode)</c> 从
    /// <see cref="EzPlayableBeatmapCache"/> 取本 hitmode 专属的副本，并已绑定对应 hitmode。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 绑定 hitmode（<see cref="ManiaBeatmapBinding"/>）与 <see cref="ManiaWindowBaker"/> 都是就地改写，所以仿真不能使用调用方
    /// 传进来的实例——那可能是本局正在游玩的实例（曾导致局内 tick/tail 区间被改成官方区间而抛异常），
    /// 也可能是 Race 里被多个 ghost 共享的实例。副本走与 live 完全相同的转换管线，避免依赖私有字段的反射深拷贝。
    /// </para>
    /// <para>
    /// 缓存本身按 working beatmap 实例弱持有，键含谱面内容哈希、保序 mod 指纹（含解析后的 seed）与 hitmode，
    /// 所以用户重掷种子、或谱面被重新导入，都会自然拿到新键而不是旧谱面。
    /// </para>
    /// <para>
    /// 线程模型：调用方在后台线程（<c>EzReplaySession.runSessionDirect</c> / Race build）上调用；并发首次转换可能重复一次，
    /// 但结果确定，且不会缓存失败。
    /// </para>
    /// </remarks>
    internal sealed class ManiaSimulationBeatmapProvider
    {
        private readonly IWorkingBeatmapCache beatmapCache;

        public ManiaSimulationBeatmapProvider(IWorkingBeatmapCache beatmapCache)
        {
            this.beatmapCache = beatmapCache ?? throw new ArgumentNullException(nameof(beatmapCache));
        }

        /// <summary>
        /// 返回可自由绑定的仿真副本（已绑定 hitmode）。无法产出时返回 null：调用方必须自行回退，
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
            EzEnumHitMode hitMode = environment.ManiaHitMode;

            return EzPlayableBeatmapCache.GetBound(workingBeatmap, scoreInfo.Ruleset, mods, bindingScopeFor(hitMode),
                playable => ManiaBeatmapBinding.BindForSimulation(playable, hitMode, providerOwned: true), cancellationToken);
        }

        /// <remarks>
        /// <see cref="EzEnumHitMode.Lazer"/> 是 0，而 0 是共享作用域，所以每个 hitmode 偏移 1 后才作为绑定作用域。
        /// </remarks>
        private static int bindingScopeFor(EzEnumHitMode hitMode) => (int)hitMode + 1;
    }
}
