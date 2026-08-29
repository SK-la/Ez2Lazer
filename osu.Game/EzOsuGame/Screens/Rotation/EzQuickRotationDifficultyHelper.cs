// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Mods.LAsMods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public static class EzQuickRotationDifficultyHelper
    {
        public const double MIN_SPEED = 0.4;
        public const double MAX_SPEED = 3.0;

        public static bool UsesXxyStarRating(RulesetInfo ruleset) => EzXxyStarRatingSupport.SupportsRuleset(ruleset);

        public static double GetBaselineStarRating(BeatmapManager beatmapManager,
                                                   BeatmapInfo beatmapInfo,
                                                   RulesetInfo ruleset,
                                                   IReadOnlyList<Mod> mods,
                                                   CancellationToken cancellationToken = default) => measureDifficulty(beatmapManager, beatmapInfo, ruleset, mods, 1.0, cancellationToken);

        public static double MeasureDifficultyAtSpeed(BeatmapManager beatmapManager,
                                                      BeatmapInfo beatmapInfo,
                                                      RulesetInfo ruleset,
                                                      double speed,
                                                      CancellationToken cancellationToken = default) =>
            measureDifficulty(beatmapManager, beatmapInfo, ruleset, Array.Empty<Mod>(), speed, cancellationToken);

        private static double measureDifficulty(BeatmapManager beatmapManager,
                                                BeatmapInfo beatmapInfo,
                                                RulesetInfo ruleset,
                                                IReadOnlyList<Mod> mods,
                                                double speed,
                                                CancellationToken cancellationToken)
        {
            if (UsesXxyStarRating(ruleset))
            {
                var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                var playable = working.GetPlayableBeatmap(ruleset, mods, cancellationToken);

                if (EzAnalysisProviderBridge.TryGetValue(ruleset, new EzAnalysisRequest(playable, speed), EzAnalysisFields.XXY_SR, cancellationToken, out double xxySr))
                    return xxySr;
            }

            return measureOfficialStarRating(beatmapManager, beatmapInfo, ruleset, mods, speed, cancellationToken);
        }

        private static double measureOfficialStarRating(BeatmapManager beatmapManager,
                                                        BeatmapInfo beatmapInfo,
                                                        RulesetInfo ruleset,
                                                        IReadOnlyList<Mod> mods,
                                                        double speed,
                                                        CancellationToken cancellationToken)
        {
            var working = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            var rulesetInstance = ruleset.CreateInstance();
            var rateMods = createRateModsForMeasurement(rulesetInstance, speed, mods).ToArray();
            return rulesetInstance.CreateDifficultyCalculator(working).Calculate(rateMods, cancellationToken).StarRating;
        }

        private static IEnumerable<Mod> createRateModsForMeasurement(Ruleset ruleset, double speed, IReadOnlyList<Mod> baseMods)
        {
            foreach (var mod in baseMods.Where(m => m is not ModRateAdjust and not ModNiceBPM))
                yield return mod;

            if (Precision.AlmostEquals(speed, 1.0))
                yield break;

            ModRateAdjust? rateMod = ruleset.CreateMod<ModHalfTime>()?.DeepClone() as ModHalfTime ?? (ModRateAdjust?)(ruleset.CreateMod<ModDoubleTime>()?.DeepClone() as ModDoubleTime);

            if (rateMod == null)
                yield break;

            rateMod.SpeedChange.Value = Math.Clamp(speed, rateMod.SpeedChange.MinValue, rateMod.SpeedChange.MaxValue);
            yield return rateMod;
        }

        public static bool IsManiaRuleset(RulesetInfo ruleset) => ruleset.ShortName == "mania";
    }

    public readonly record struct EzQuickRotationBalanceResult(double Speed, double BalancedDifficulty, bool WithinTolerance);

    public static class EzQuickRotationDifficultyBalancer
    {
        public const int MAX_ITERATIONS = 8;

        public static EzQuickRotationBalanceResult Balance(BeatmapManager beatmapManager,
                                                           BeatmapInfo beatmapInfo,
                                                           RulesetInfo ruleset,
                                                           double baselineDifficulty,
                                                           double tolerance,
                                                           CancellationToken cancellationToken = default)
        {
            double accumulatedStep = 0;
            double speed = 1.0;
            double measured = EzQuickRotationDifficultyHelper.MeasureDifficultyAtSpeed(beatmapManager, beatmapInfo, ruleset, speed, cancellationToken);

            for (int i = 0; i < MAX_ITERATIONS; i++)
            {
                if (Math.Abs(measured - baselineDifficulty) <= tolerance)
                    return new EzQuickRotationBalanceResult(speed, measured, true);

                double step = (measured - baselineDifficulty) / 10.0;
                accumulatedStep += step;
                speed = Math.Clamp(1.0 - accumulatedStep, EzQuickRotationDifficultyHelper.MIN_SPEED, EzQuickRotationDifficultyHelper.MAX_SPEED);
                measured = EzQuickRotationDifficultyHelper.MeasureDifficultyAtSpeed(beatmapManager, beatmapInfo, ruleset, speed, cancellationToken);
            }

            return new EzQuickRotationBalanceResult(speed, measured, Math.Abs(measured - baselineDifficulty) <= tolerance);
        }

        public static ModNiceBPM CreateNiceBpmMod(RulesetInfo ruleset, double speed)
        {
            var mod = ruleset.CreateInstance().AllMods.OfType<ModNiceBPM>().FirstOrDefault()?.DeepClone() as ModNiceBPM ?? new ModNiceBPM();
            double clampedSpeed = Math.Clamp(speed, EzQuickRotationDifficultyHelper.MIN_SPEED, EzQuickRotationDifficultyHelper.MAX_SPEED);
            mod.InitialRate.Value = clampedSpeed;
            mod.SpeedChange.Value = clampedSpeed;
            return mod;
        }

        public static IReadOnlyList<Mod> MergeMods(IReadOnlyList<Mod> baseMods, ModNiceBPM niceBpm)
        {
            var merged = baseMods.Where(m => m is not ModRateAdjust and not ModNiceBPM).Select(m => m.DeepClone()).ToList();
            merged.Add(niceBpm);
            return ModUtils.CheckCompatibleSet(merged, out _) ? merged : new List<Mod> { niceBpm };
        }
    }
}
