// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills.SkillTag.KeyPattern
{
    /// <summary>
    /// EzKeyPattern (EKP): measure the five persistable radar axes and write them under
    /// <see cref="EzAnalysisSystem.KeyPattern"/>. LN% is not persisted. Star and pp are Sunny's.
    /// </summary>
    public sealed class EzKeyPatternUnit
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzChartSkillStore skillStore;

        public const int VERSION = 20261116;

        /// <summary>Relative full score. 10 means that feature is the chart's clearest axis.</summary>
        public const double RELATIVE_FULL_SCORE = 10;

        public EzKeyPatternUnit(BeatmapManager beatmapManager, EzChartSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        public static bool SupportsRuleset(int rulesetOnlineId) => rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

        public static bool IsQuantifiableSunny(double sunnySR)
            => double.IsFinite(sunnySR) && sunnySR > 0;

        public static double ToQuantified(double relative, double sunnySR)
            => Math.Round(relative / RELATIVE_FULL_SCORE * sunnySR, 2, MidpointRounding.AwayFromZero);

        public static double ToRelative(double quantified, double sunnySR)
            => sunnySR > 0
                ? Math.Round(quantified / sunnySR * RELATIVE_FULL_SCORE, 2, MidpointRounding.AwayFromZero)
                : quantified;

        /// <summary>
        /// Rates one chart and writes its EKP axes. Returns false when the chart could not be loaded.
        /// Missing Sunny star leaves the debt for a later pass. Sunny's 0 N/A stamp is not a real star.
        /// </summary>
        public bool ComputeAndStore(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            var result = TryCompute(beatmapInfo, cancellationToken);

            if (result == null)
                return false;

            if (!result.RequiresStore)
                return true;

            if (result.Skills != null && result.BeatmapHash != null)
            {
                skillStore.WriteBeatmapSystemSkills(
                    result.BeatmapHash,
                    EzAnalysisSystem.KeyPattern,
                    result.Skills,
                    beatmapInfo.ID,
                    algorithmVersion: VERSION,
                    rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);
            }

            return true;
        }

        /// <summary>CPU-only half of KeyPattern axis backfill.</summary>
        public EzKeyPatternMaintenanceResult? TryCompute(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!SupportsRuleset(beatmapInfo.Ruleset.OnlineID) || string.IsNullOrEmpty(beatmapInfo.Hash))
                return EzKeyPatternMaintenanceResult.NoStoreRequired;

            double sunnySR = beatmapInfo.EzRatingData.SunnySR;

            if (!double.IsFinite(sunnySR) || sunnySR < 0)
                return EzKeyPatternMaintenanceResult.NoStoreRequired;

            IBeatmap playable;

            try
            {
                playable = EzPlayableBeatmapCache.GetShared(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                return null;
            }

            var chart = EzCSIEngine.FromPlayable(playable, cancellationToken);
            var skills = BuildSkillsForPersistence(chart, sunnySR, cancellationToken);

            if (skills == null)
                return EzKeyPatternMaintenanceResult.NoStoreRequired;

            return new EzKeyPatternMaintenanceResult(true, beatmapInfo.Hash, skills);
        }

        public static void Write(
            EzChartSkillStore skillStore,
            BeatmapInfo beatmapInfo,
            EzManiaChartInput chart,
            double sunnySR,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(skillStore);
            ArgumentNullException.ThrowIfNull(beatmapInfo);
            ArgumentNullException.ThrowIfNull(chart);

            if (string.IsNullOrEmpty(beatmapInfo.Hash))
                return;

            var skills = BuildSkillsForPersistence(chart, sunnySR, cancellationToken);

            if (skills == null)
                return;

            skillStore.WriteBeatmapSystemSkills(
                beatmapInfo.Hash,
                EzAnalysisSystem.KeyPattern,
                skills,
                beatmapInfo.ID,
                algorithmVersion: VERSION,
                rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);
        }

        /// <summary>Skill rows for persistence without touching Realm.</summary>
        public static IReadOnlyDictionary<string, double>? BuildSkillsForPersistence(EzManiaChartInput chart, double sunnySR, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(chart);

            var relative = ComputeRadar(chart, cancellationToken);
            return BuildSkillsForPersistence(chart, relative, sunnySR);
        }

        public static IReadOnlyDictionary<string, double>? BuildSkillsForPersistence(EzManiaChartInput chart, EzRadarChartData<string> relative, double sunnySR)
        {
            ArgumentNullException.ThrowIfNull(chart);

            bool emptyChart = chart.Notes.Count == 0;

            if (!emptyChart && !IsQuantifiableSunny(sunnySR))
                return null;

            return ToSkills(emptyChart ? relative : Quantify(relative, sunnySR));
        }

        /// <summary>Relative 0–10 scores for the five persistable axes. No LN%.</summary>
        public static EzRadarChartData<string> ComputeRadar(EzManiaChartInput chart, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(chart);

            var notes = EzManiaKeyPatternHelper.NotesInTimeOrder(chart);

            if (notes.Count == 0)
                return createRadar(0, 0, 0, 0, 0);

            int totalColumns = 0;

            for (int i = 0; i < notes.Count; i++)
                totalColumns = Math.Max(totalColumns, notes[i].Column + 1);

            var rows = EzManiaKeyPatternHelper.BuildRows(notes, time => EzManiaKeyPatternHelper.BeatLengthAt(chart, time), cancellationToken);

            return createRadar(
                EzManiaKeyPatternBracket.Compute(rows, totalColumns, cancellationToken),
                EzManiaKeyPatternChord.Compute(rows, cancellationToken),
                EzManiaKeyPatternJack.Compute(notes, time => EzManiaKeyPatternHelper.BeatLengthAt(chart, time), cancellationToken),
                EzManiaKeyPatternDelay.Compute(rows, totalColumns, cancellationToken),
                EzManiaKeyPatternDump.Compute(rows, totalColumns, cancellationToken));
        }

        public static EzRadarChartData<string> Quantify(in EzRadarChartData<string> relative, double sunnySR)
        {
            string lnId = EzKeyPatternAxis.LnPercent.ToMetaId();
            var axes = new EzRadarAxisValue<string>[relative.Count];

            for (int i = 0; i < relative.Count; i++)
            {
                var axis = relative[i];
                double value = axis.Axis == lnId ? axis.Value : ToQuantified(axis.Value, sunnySR);
                axes[i] = new EzRadarAxisValue<string>(axis.Axis, value, "0.00");
            }

            return EzRadarChartData<string>.Create(axes);
        }

        public static IReadOnlyDictionary<string, double> ToSkills(in EzRadarChartData<string> radar)
        {
            var values = new Dictionary<string, double>(EzKeyPatternAxisExtensions.RadarAxes.Length, StringComparer.Ordinal);

            for (int i = 0; i < radar.Count; i++)
            {
                string id = radar[i].Axis;

                if (id == EzKeyPatternAxis.LnPercent.ToMetaId())
                    continue;

                values[id] = radar[i].Value;
            }

            return values;
        }

        private static EzRadarChartData<string> createRadar(double bracket, double chord, double jack, double delay, double dump)
            => EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Bracket.ToMetaId(), bracket, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Chord.ToMetaId(), chord, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.LnPercent.ToMetaId(), 0, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Jack.ToMetaId(), jack, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Delay.ToMetaId(), delay, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Dump.ToMetaId(), dump, "0.00"));
    }
}
