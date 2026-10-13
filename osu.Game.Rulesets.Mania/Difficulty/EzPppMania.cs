// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Ppp;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty;

internal static class EzPppManiaRegistration
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register()
        => EzPppChartCalculators.Register(3, static (beatmap, mods, _) => EzPppMania.Calculate(beatmap));
}
    /// <summary>osu!mania PPP strain skill and its star rating.</summary>
    internal static class EzPppMania
    {
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
                return new Dictionary<string, double>(EzPppSkillCatalog.ChartZeros(3), StringComparer.Ordinal);

            notes.Sort((a, b) => a.Start.CompareTo(b.Start));

            int columns = beatmap is ManiaBeatmap mania ? mania.ResolveColumnCount() : 0;

            foreach (var note in notes)
                columns = Math.Max(columns, note.Column + 1);

            columns = Math.Max(1, columns);
            var columnStrain = new double[columns];
            var columnStart = new double[columns];
            var columnEnd = new double[columns];
            var occupied = new bool[columns];
            double overall = 0;
            double chordIndividual = 0;
            var times = new List<double>();
            var strains = new List<double>();
            double previousTime = notes[0].Start;

            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                int column = Math.Clamp(note.Column, 0, columns - 1);
                double delta = i == 0 ? 0 : note.Start - previousTime;
                bool overlap = false;
                double nearestRelease = double.MaxValue;
                bool holdAcross = false;

                for (int other = 0; other < columns; other++)
                {
                    if (!occupied[other] || other == column)
                        continue;

                    if (columnEnd[other] > note.Start && note.End > columnEnd[other] && note.Start > columnStart[other])
                    {
                        overlap = true;
                        nearestRelease = Math.Min(nearestRelease, Math.Abs(note.End - columnEnd[other]));
                    }

                    if (columnStart[other] < note.Start && columnEnd[other] > note.End && note.End > note.Start)
                        holdAcross = true;
                }

                double holdFactor = holdAcross ? 1.25 : 1;
                double holdBonus = overlap ? 1.0 / (1 + Math.Exp(-0.27 * (30 - nearestRelease))) : 0;
                columnStrain[column] = EzPppMath.Decay(delta, 0.125) * columnStrain[column] + 2 * holdFactor;

                chordIndividual = delta <= 1 && i > 0 ? Math.Max(chordIndividual, columnStrain[column]) : columnStrain[column];
                overall = EzPppMath.Decay(delta, 0.3) * overall + (1 + holdBonus) * holdFactor;
                times.Add(note.Start);
                strains.Add(chordIndividual + overall);
                columnStart[column] = note.Start;
                columnEnd[column] = Math.Max(note.End, note.Start);
                occupied[column] = true;
                previousTime = note.Start;
            }

            double value = EzPppMath.Aggregate(times, strains, 400, 0.9);
            double star = 0.018 * value;

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPppSkillCatalog.ChartSkillId(EzPppSkillCatalog.STRAIN)] = EzPppMath.Finite(star),
                [EzPppSkillCatalog.ChartSkillId(EzPppSkillCatalog.STAR_RATING)] = EzPppMath.Finite(star),
            };
        }
    }
