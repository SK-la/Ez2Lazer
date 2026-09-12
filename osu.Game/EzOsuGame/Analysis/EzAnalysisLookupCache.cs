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

        public EzAnalysisLookupCache(BeatmapInfo beatmapInfo, IRulesetInfo? rulesetInfo, IEnumerable<Mod>? mods)
        {
            BeatmapInfo = beatmapInfo;
            Ruleset = (rulesetInfo as RulesetInfo) ?? BeatmapInfo.Ruleset;
            // Clone so wedge analysis does not share setting instances with SelectedMods while hashing.
            // Nullable Seed fill uses EzModSeed.Resolve during GetPlayable (update-thread safe).
            OrderedMods = createModSnapshot(mods);
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
                    if (Interlocked.Increment(ref modSnapshotFailCount) <= 10)
                    {
                        Logger.Log(
                            $"[EzAnalysis] Mod.DeepClone() failed for {mod.GetType().FullName}. Falling back to original instance.",
                            Ez2ConfigManager.LOGGER_NAME,
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
