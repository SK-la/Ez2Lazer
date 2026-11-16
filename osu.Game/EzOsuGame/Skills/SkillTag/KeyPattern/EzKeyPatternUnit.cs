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
    /// Chart KeyPattern structure: measure the six radar axes and persist them under <see cref="EzAnalysisSystem.Pattern"/>.
    /// </summary>
    public sealed class EzKeyPatternUnit
    {
        private readonly BeatmapManager beatmapManager;
        private readonly EzChartSkillStore skillStore;

        public const int VERSION = 20261116;

        public EzKeyPatternUnit(BeatmapManager beatmapManager, EzChartSkillStore skillStore)
        {
            this.beatmapManager = beatmapManager;
            this.skillStore = skillStore;
        }

        public static bool SupportsRuleset(int rulesetOnlineId) => rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

        /// <summary>
        /// Rates one chart and writes its KeyPattern axes. Returns false when the chart could not be loaded.
        /// </summary>
        public bool ComputeAndStore(BeatmapInfo beatmapInfo, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (!SupportsRuleset(beatmapInfo.Ruleset.OnlineID) || string.IsNullOrEmpty(beatmapInfo.Hash))
                return true;

            IBeatmap playable;

            try
            {
                playable = EzPlayableBeatmapCache.GetShared(beatmapManager.GetWorkingBeatmap(beatmapInfo), beatmapInfo.Ruleset);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                return false;
            }

            var chart = EzCSIEngine.FromPlayable(playable, cancellationToken);
            Write(skillStore, beatmapInfo, chart, EzHoldMetrics.ComputeHoldRatio(playable), cancellationToken);
            return true;
        }

        public static void Write(
            EzChartSkillStore skillStore,
            BeatmapInfo beatmapInfo,
            EzManiaChartInput chart,
            double holdRatio,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(skillStore);
            ArgumentNullException.ThrowIfNull(beatmapInfo);
            ArgumentNullException.ThrowIfNull(chart);

            if (string.IsNullOrEmpty(beatmapInfo.Hash))
                return;

            var radar = ComputeRadar(chart, holdRatio, cancellationToken);

            skillStore.WriteBeatmapSystemSkills(
                beatmapInfo.Hash,
                EzAnalysisSystem.Pattern,
                ToSkills(radar),
                beatmapInfo.ID,
                algorithmVersion: VERSION,
                rulesetOnlineId: EzLocalProfileConstants.MANIA_RULESET_ID);
        }

        public static EzRadarChartData<string> ComputeRadar(EzManiaChartInput chart, double holdRatio, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(chart);

            var notes = EzManiaKeyPatternHelper.NotesInTimeOrder(chart);

            if (notes.Count == 0)
                return createRadar(0, 0, 0, 0, 0, 0);

            int totalColumns = 0;

            for (int i = 0; i < notes.Count; i++)
                totalColumns = Math.Max(totalColumns, notes[i].Column + 1);

            var rows = EzManiaKeyPatternHelper.BuildRows(notes, time => EzManiaKeyPatternHelper.BeatLengthAt(chart, time), cancellationToken);
            double longNoteRatio = Math.Round(holdRatio * 100, 2, MidpointRounding.AwayFromZero);

            return createRadar(
                EzManiaKeyPatternBracket.Compute(rows, totalColumns, cancellationToken),
                EzManiaKeyPatternChord.Compute(rows, cancellationToken),
                longNoteRatio,
                EzManiaKeyPatternJack.Compute(notes, time => EzManiaKeyPatternHelper.BeatLengthAt(chart, time), cancellationToken),
                EzManiaKeyPatternDelay.Compute(rows, totalColumns, cancellationToken),
                EzManiaKeyPatternDump.Compute(rows, totalColumns, cancellationToken));
        }

        public static IReadOnlyDictionary<string, double> ToSkills(in EzRadarChartData<string> radar)
        {
            var values = new Dictionary<string, double>(EzKeyPatternAxisExtensions.RadarAxes.Length, StringComparer.Ordinal);

            for (int i = 0; i < radar.Count; i++)
                values[radar[i].Axis] = radar[i].Value;

            return values;
        }

        private static EzRadarChartData<string> createRadar(double bracket, double chord, double longNoteRatio, double jack, double delay, double dump)
            => EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Bracket.ToMetaId(), bracket, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Chord.ToMetaId(), chord, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.LnPercent.ToMetaId(), longNoteRatio, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Jack.ToMetaId(), jack, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Delay.ToMetaId(), delay, "0.00"),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.Dump.ToMetaId(), dump, "0.00"));
    }
}
