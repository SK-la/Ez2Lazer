// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 为判定仿真提供独立 beatmap 实例：按 <c>(working beatmap, ruleset, mods, hitmode)</c> 缓存
    /// <c>IWorkingBeatmap.GetPlayableBeatmap</c> 的产物，并已绑定对应 hitmode。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 绑定 hitmode（<see cref="ManiaBeatmapBinding"/>）与 <see cref="ManiaWindowBaker"/> 都是就地改写，所以仿真不能使用调用方
    /// 传进来的实例——那可能是本局正在游玩的实例（曾导致局内 tick/tail 区间被改成官方区间而抛异常），
    /// 也可能是 Race 里被多个 ghost 共享的实例。副本走与 live 完全相同的转换管线，避免依赖私有字段的反射深拷贝。
    /// </para>
    /// <para>
    /// 键必须含 mods：随机类 Mod 的 Seed 会被 <c>EzModSeed</c> 写回 bindable，而 <c>Mod.Equals</c> /
    /// <c>Mod.GetHashCode</c> 把设置纳入比较，所以用户重掷种子会得到新键并重新转换，不会拿到旧谱面。
    /// 缓存由 <see cref="ConditionalWeakTable{TKey,TValue}"/> 弱持有，working beatmap 被回收即随之释放。
    /// </para>
    /// <para>
    /// 线程模型：调用方在后台线程（<c>EzReplaySession.runSessionDirect</c> / Race build）上调用，故键与缓存都按并发访问设计；
    /// 并发首次转换可能重复一次，但结果确定，且不会缓存失败。
    /// </para>
    /// </remarks>
    internal sealed class ManiaSimulationBeatmapProvider
    {
        private readonly IWorkingBeatmapCache beatmapCache;
        private readonly ConditionalWeakTable<IWorkingBeatmap, Bucket> buckets = new ConditionalWeakTable<IWorkingBeatmap, Bucket>();

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
            var key = new SimulationKey(scoreInfo.Ruleset.ShortName, mods, environment.ManiaHitMode);

            return buckets.GetValue(workingBeatmap, _ => new Bucket()).GetOrCreate(key, () =>
            {
                var playable = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, mods, cancellationToken);
                ManiaBeatmapBinding.BindForSimulation(playable, environment.ManiaHitMode, providerOwned: true);
                return playable;
            });
        }

        /// <summary>
        /// 键的 mods 部分沿用 <c>BeatmapDifficultyCache.DifficultyCacheLookup</c> 的构造（按 Acronym 排序 + 深拷贝），
        /// 借 <see cref="Mod"/> 自带的设置比较语义，不另发明序列化。
        /// </summary>
        private readonly struct SimulationKey : IEquatable<SimulationKey>
        {
            private readonly string rulesetShortName;
            private readonly EzEnumHitMode hitMode;
            private readonly Mod[] orderedMods;

            public SimulationKey(string rulesetShortName, IReadOnlyList<Mod> mods, EzEnumHitMode hitMode)
            {
                this.rulesetShortName = rulesetShortName;
                this.hitMode = hitMode;
                orderedMods = mods.OrderBy(m => m.Acronym).Select(m => m.DeepClone()).ToArray();
            }

            public bool Equals(SimulationKey other)
                => hitMode == other.hitMode
                   && string.Equals(rulesetShortName, other.rulesetShortName, StringComparison.Ordinal)
                   && orderedMods.SequenceEqual(other.orderedMods);

            public override bool Equals(object? obj) => obj is SimulationKey other && Equals(other);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();

                hashCode.Add(rulesetShortName);
                hashCode.Add((int)hitMode);

                foreach (var mod in orderedMods)
                    hashCode.Add(mod);

                return hashCode.ToHashCode();
            }
        }

        private sealed class Bucket
        {
            private readonly Dictionary<SimulationKey, IBeatmap> entries = new Dictionary<SimulationKey, IBeatmap>();

            public IBeatmap GetOrCreate(SimulationKey key, Func<IBeatmap> factory)
            {
                lock (entries)
                {
                    if (entries.TryGetValue(key, out var cached))
                        return cached;
                }

                // 转换放在锁外：失败不落缓存，并发重复转换的结果也一致。
                var created = factory();

                lock (entries)
                    entries[key] = created;

                return created;
            }
        }
    }
}
