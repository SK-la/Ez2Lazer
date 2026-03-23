// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Analysis
{
    public readonly struct EzAnalysisLookupCache : IEquatable<EzAnalysisLookupCache>
    {
        public readonly BeatmapInfo BeatmapInfo;
        public readonly RulesetInfo Ruleset;
        public readonly Mod[] OrderedMods;
        public readonly int ModsSignature;

        private static int modSnapshotFailCount;

        public EzAnalysisLookupCache(BeatmapInfo beatmapInfo, IEnumerable<Mod>? mods)
        {
            BeatmapInfo = beatmapInfo;
            Ruleset = BeatmapInfo.Ruleset;
            // 重要：mod 应用顺序对谱面转换很重要。
            OrderedMods = createModSnapshot(mods);
            // 重要：一些自定义 mods会在 ApplyToBeatmap 期间懒惰地分配随机种子
            //（例如 Seed.Value ??= RNG.Next()）。因为我们的缓存键包含 mod 设置，这种变异会在计算过程中改变
            // Mod.GetHashCode()/Equals() 并破坏字典使用。
            // 在克隆的快照上确定性地预填充缺失的种子以保持缓存键稳定。
            initialiseDeterministicSeedsIfRequired(OrderedMods, beatmapInfo);
            ModsSignature = computeModsSignature(OrderedMods);
        }

        private static int computeModsSignature(Mod[] orderedMods)
        {
            unchecked
            {
                var hash = new HashCode();

                // 包含顺序。顺序对转换和游戏很重要。
                for (int i = 0; i < orderedMods.Length; i++)
                {
                    var mod = orderedMods[i];
                    hash.Add(mod.GetType());

                    // 镜像 Mod.GetHashCode() 语义，但在计算签名后与 mod 实例变异解耦。
                    // 仅包含通过 [SettingSource] 公开的设置。
                    foreach (var setting in mod.SettingsBindables)
                        hash.Add(setting.GetUnderlyingSettingValue());
                }

                return hash.ToHashCode();
            }
        }

        private static void initialiseDeterministicSeedsIfRequired(Mod[] orderedMods, BeatmapInfo beatmapInfo)
        {
            if (orderedMods.Length == 0)
                return;

            unchecked
            {
                // 基础种子来源于谱面身份。
                int baseSeed = 17;
                baseSeed = baseSeed * 31 + beatmapInfo.ID.GetHashCode();
                baseSeed = baseSeed * 31 + (beatmapInfo.Hash.GetHashCode(StringComparison.Ordinal));

                for (int i = 0; i < orderedMods.Length; i++)
                {
                    if (orderedMods[i] is not IHasSeed hasSeed)
                        continue;

                    if (hasSeed.Seed.Value != null)
                        continue;

                    // 混合 mod 类型以避免所有带种子的 mods 共享相同的种子。
                    int seed = baseSeed;
                    seed = seed * 31 + orderedMods[i].GetType().FullName!.GetHashCode(StringComparison.Ordinal);
                    seed = seed * 31 + i;

                    // 确保非空。
                    if (seed == 0)
                        seed = 1;

                    hasSeed.Seed.Value = seed;
                }
            }
        }

        private static Mod[] createModSnapshot(IEnumerable<Mod>? mods)
        {
            if (mods == null)
                return Array.Empty<Mod>();

            var list = new List<Mod>();

            foreach (var mod in mods)
            {
                try
                {
                    list.Add(mod.DeepClone());
                }
                catch
                {
                    // 如果克隆失败，则回退到使用原始实例。
                    // 这对缓存来说并不理想，但比完全破坏分析要好。
                    if (Interlocked.Increment(ref modSnapshotFailCount) <= 10)
                    {
                        Logger.Log($"[EzBeatmapManiaAnalysisCache] Mod.DeepClone() failed for {mod.GetType().FullName}. Falling back to original instance.", Ez2ConfigManager.LOGGER_NAME,
                            LogLevel.Important);
                    }

                    list.Add(mod);
                }
            }

            return list.ToArray();
        }

        public bool Equals(EzAnalysisLookupCache other) => BeatmapInfo.ID.Equals(other.BeatmapInfo.ID)
                                                           && string.Equals(BeatmapInfo.Hash, other.BeatmapInfo.Hash, StringComparison.Ordinal)
                                                           && Ruleset.Equals(other.Ruleset)
                                                           && ModsSignature == other.ModsSignature;

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            hashCode.Add(BeatmapInfo.ID);
            hashCode.Add(BeatmapInfo.Hash);
            hashCode.Add(Ruleset.OnlineID);
            hashCode.Add(ModsSignature);

            return hashCode.ToHashCode();
        }
    }
}
