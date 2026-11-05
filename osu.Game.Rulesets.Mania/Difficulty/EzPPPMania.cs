// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    internal static class EzPPPManiaRegistration
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
        {
            EzPPPCalculators.Register(EzLocalProfileConstants.MANIA_RULESET_ID, static (beatmap, mods, _) => EzPPPMania.Calculate(beatmap));
            EzPPPPerformanceCalculators.Register(EzLocalProfileConstants.MANIA_RULESET_ID, static (skills, beatmap, mods, _) => performance(skills, beatmap, mods));
        }

        /// <summary>PP+ performance for a perfect play (documented section 4.1).</summary>
        private static double performance(IReadOnlyDictionary<string, double> skills, IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            // Every object of a perfect play earns the top judgement, so the hit count is the object count.
            int totalHits = beatmap.HitObjects.Count;

            bool noFail = false;
            bool easy = false;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ModNoFail:
                        noFail = true;
                        break;

                    case ModEasy:
                        easy = true;
                        break;
                }
            }

            return EzPPPPerformance.Mania(
                EzPPPPerformanceCalculators.Rating(skills, EzPPPSkillCatalog.STAR_RATING),
                totalHits,
                noFail,
                easy);
        }
    }

    /// <summary>osu!mania PPP strain skill and its star rating.</summary>
    internal static class EzPPPMania
    {
        private const double individual_decay = 0.125;
        private const double overall_decay = 0.3;
        private const double section_length = 400;
        private const double gamma = 0.9;
        private const double star_scale = 0.018;

        /// <summary>Millisecond tolerance for "these two objects are a chord" and for the interval comparisons.</summary>
        private const double tolerance_ms = 1;

        public static Dictionary<string, double> Calculate(IBeatmap beatmap)
        {
            var notes = new List<(int Column, double Start, double End)>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is HoldNote hold)
                    notes.Add((hold.Column, hold.StartTime, hold.EndTime));
                else if (hitObject is Note note)
                    notes.Add((note.Column, note.StartTime, note.StartTime));
            }

            if (notes.Count == 0)
                return new Dictionary<string, double>(EzPPPSkillCatalog.ChartZeros(EzLocalProfileConstants.MANIA_RULESET_ID), StringComparer.Ordinal);

            notes.Sort((a, b) => a.Start.CompareTo(b.Start));

            int columns = beatmap is ManiaBeatmap mania ? mania.ResolveColumnCount() : 0;

            foreach (var note in notes)
                columns = Math.Max(columns, note.Column + 1);

            columns = Math.Max(1, columns);
            double[] columnStrain = new double[columns];
            double[] columnStart = new double[columns];
            double[] columnEnd = new double[columns];
            bool[] occupied = new bool[columns];

            // Overall starts at 1: the first object's contribution is measured against a non-zero baseline.
            double overall = 1;
            double individual = 0;
            double previousEnd = 0;
            double previousStart = notes[0].Start;

            var times = new List<double>(notes.Count);
            var strains = new List<double>(notes.Count);
            var individualLevels = new List<double>(notes.Count);
            var overallLevels = new List<double>(notes.Count);

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                int column = Math.Clamp(note.Column, 0, columns - 1);
                double delta = i == 0 ? 0 : note.Start - previousStart;
                double noteEnd = Math.Max(note.End, note.Start);

                bool overlap = false;
                bool holdAcross = false;
                double nearestRelease = double.MaxValue;

                for (int other = 0; other < columns; other++)
                {
                    if (!occupied[other])
                        continue;

                    if (definitelyBigger(columnEnd[other], note.Start)
                        && definitelyBigger(noteEnd, columnEnd[other])
                        && definitelyBigger(note.Start, columnStart[other]))
                    {
                        overlap = true;
                        nearestRelease = Math.Min(nearestRelease, Math.Abs(noteEnd - columnEnd[other]));
                    }

                    if (definitelyBigger(columnEnd[other], noteEnd) && definitelyBigger(note.Start, columnStart[other]))
                        holdAcross = true;
                }

                double holdFactor = holdAcross ? 1.25 : 1;
                double holdBonus = overlap ? 1.0 / (1 + Math.Exp(0.27 * (30 - nearestRelease))) : 0;

                double columnDelta = occupied[column] ? Math.Max(0, note.Start - columnStart[column]) : 0;
                columnStrain[column] = EzPPPMath.Decay(columnDelta, individual_decay) * columnStrain[column] + 2 * holdFactor;

                // A chord shares the peak of its columns; a lone note takes its own column's strain.
                individual = i > 0 && note.Start - previousStart <= tolerance_ms
                    ? Math.Max(individual, columnStrain[column])
                    : columnStrain[column];

                overall = EzPPPMath.Decay(delta, overall_decay) * overall + (1 + holdBonus) * holdFactor;

                // The wrapper does not decay on its own, so the object's strain is the increment of the level.
                double level = individual + overall;
                strains.Add(i == 0 ? level : Math.Max(0, level - (individualLevels[i - 1] + overallLevels[i - 1])));

                times.Add(note.Start);
                individualLevels.Add(individual);
                overallLevels.Add(overall);

                columnStart[column] = note.Start;
                columnEnd[column] = noteEnd;
                occupied[column] = true;
                previousStart = note.Start;
                previousEnd = noteEnd;
            }

            double value = peakValue(times, strains, individualLevels, overallLevels);
            double star = star_scale * value;

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STRAIN)] = EzPPPMath.Finite(star),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)] = EzPPPMath.Finite(star),
            };
        }

        /// <summary>
        /// Section peaks with the two-part initial value: a section that starts inside a gap inherits both strain
        /// levels, each with its own decay constant.
        /// </summary>
        private static double peakValue(List<double> times, List<double> strains, List<double> individualLevels, List<double> overallLevels)
        {
            var peaks = new List<double>();
            double sectionEnd = Math.Ceiling(times[0] / section_length) * section_length;
            double peak = 0;

            for (int i = 0; i < times.Count; i++)
            {
                while (times[i] > sectionEnd)
                {
                    peaks.Add(peak);

                    if (i > 0)
                    {
                        double gap = sectionEnd - times[i - 1];
                        peak = EzPPPMath.Decay(gap, individual_decay) * individualLevels[i - 1]
                               + EzPPPMath.Decay(gap, overall_decay) * overallLevels[i - 1];
                    }
                    else
                    {
                        peak = 0;
                    }

                    sectionEnd += section_length;
                }

                if (double.IsFinite(strains[i]) && strains[i] > peak)
                    peak = strains[i];
            }

            peaks.Add(peak);
            peaks.Sort((x, y) => y.CompareTo(x));

            double sum = 0;

            for (int i = 0; i < peaks.Count; i++)
            {
                if (peaks[i] > 0)
                    sum += peaks[i] * Math.Pow(gamma, i);
            }

            return sum;
        }

        private static bool definitelyBigger(double a, double b) => a - b > tolerance_ms;
    }
}
