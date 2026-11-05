// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.Taiko.Difficulty
{
    internal static class EzPPPTaikoRegistration
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
            => EzPPPCalculators.Register(EzLocalProfileConstants.TAIKO_RULESET_ID, static (beatmap, mods, _) => EzPPPTaiko.Calculate(beatmap));
    }

    /// <summary>Taiko PPP skills: rhythm, colour, stamina, and the combined star rating.</summary>
    internal static class EzPPPTaiko
    {
        private const double difficulty_multiplier = 0.084375;
        private const double section_length = 400;
        private const double gamma = 0.9;

        private static readonly (double Rho, double Weight)[] rhythm_table =
        {
            (1.0 / 1, 0),
            (2.0 / 1, 0.3),
            (1.0 / 2, 0.5),
            (3.0 / 1, 0.3),
            (1.0 / 3, 0.35),
            (3.0 / 2, 0.6),
            (2.0 / 3, 0.4),
            (5.0 / 4, 0.5),
            (4.0 / 5, 0.7),
        };

        public static Dictionary<string, double> Calculate(IBeatmap beatmap)
        {
            var objects = new List<TaikoHitObject>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is Hit or DrumRoll or Swell)
                    objects.Add((TaikoHitObject)hitObject);
            }

            if (objects.Count < 3)
                return new Dictionary<string, double>(EzPPPSkillCatalog.ChartZeros(EzLocalProfileConstants.TAIKO_RULESET_ID), StringComparer.Ordinal);

            var times = new List<double>();
            var rhythmStrain = new List<double>();
            var colourStrain = new List<double>();
            var staminaStrain = new List<double>();

            double rhythmState = 0;
            int notesSinceChange = 0;
            var history = new List<double>();
            double[] colourStrainAt = colourStrains(objects);
            var sameColour = new Dictionary<HitType, List<double>>();
            double colourState = 0;
            double staminaState = 0;

            for (int i = 2; i < objects.Count; i++)
            {
                double delta = objects[i].StartTime - objects[i - 1].StartTime;
                double previousDelta = objects[i - 1].StartTime - objects[i - 2].StartTime;
                bool hit = objects[i] is Hit;

                if (!hit || delta >= 210)
                {
                    rhythmState = 0;
                    notesSinceChange = 0;
                }
                else
                {
                    notesSinceChange++;
                    double ratio = previousDelta > 0 ? delta / previousDelta : 1;
                    var (rho, weight) = matchRhythm(ratio);

                    if (weight > 0)
                    {
                        int since = notesSince(history, rho);
                        double repetition = Math.Min(1, 0.032 * Math.Max(1, since));
                        double length = Math.Min(0.15 * notesSinceChange, Math.Clamp(2.5 - 0.15 * notesSinceChange, 0, 1));
                        double speed = delta < 80 ? 1 : Math.Max(0, 1.4 - 0.005 * delta);
                        rhythmState = 0.96 * rhythmState + weight * repetition * length * speed;
                        notesSinceChange = 0;
                        history.Add(rho);
                    }
                }

                colourState = EzPPPMath.Decay(Math.Max(0, delta), 0.8) * colourState + 0.12 * colourStrainAt[i];

                double staminaAdd = 0;

                if (hit)
                {
                    var typed = (Hit)objects[i];
                    int k = colourChangedNear(objects, i) ? 2 : 4;

                    if (!sameColour.TryGetValue(typed.Type, out var hits))
                    {
                        hits = new List<double>();
                        sameColour[typed.Type] = hits;
                    }

                    int back = k - 1;

                    if (hits.Count >= back)
                    {
                        double previous = hits[^back];
                        staminaAdd = 0.5 + 30.0 / Math.Max(1, typed.StartTime - previous);
                    }

                    hits.Add(typed.StartTime);
                }

                staminaState = EzPPPMath.Decay(Math.Max(0, delta), 0.4) * staminaState + 1.1 * staminaAdd;

                times.Add(objects[i].StartTime);
                rhythmStrain.Add(10 * rhythmState);
                colourStrain.Add(colourState);
                staminaStrain.Add(staminaState);
            }

            double rhythmValue = EzPPPMath.Aggregate(times, rhythmStrain, section_length, gamma);
            double colourValue = EzPPPMath.Aggregate(times, colourStrain, section_length, gamma);
            double staminaValue = EzPPPMath.Aggregate(times, staminaStrain, section_length, gamma);
            double star = combinedStar(times, colourStrain, rhythmStrain, staminaStrain);

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.RHYTHM)] = EzPPPMath.Finite(0.2 * difficulty_multiplier * rhythmValue),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.COLOUR)] = EzPPPMath.Finite(0.375 * difficulty_multiplier * colourValue),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAMINA)] = EzPPPMath.Finite(0.375 * difficulty_multiplier * staminaValue),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)] = EzPPPMath.Finite(star),
            };
        }

        private static double[] colourStrains(List<TaikoHitObject> objects)
        {
            double[] strains = new double[objects.Count];
            var streaks = new List<(int Start, int Length, HitType Type)>();
            int run = 0;
            HitType? colour = null;
            int start = 0;

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] is not Hit hit)
                {
                    if (run > 0 && colour != null)
                        streaks.Add((start, run, colour.Value));

                    run = 0;
                    colour = null;
                    continue;
                }

                if (colour == null || hit.Type != colour)
                {
                    if (run > 0 && colour != null)
                        streaks.Add((start, run, colour.Value));

                    colour = hit.Type;
                    start = i;
                    run = 1;
                }
                else
                    run++;
            }

            if (run > 0 && colour != null)
                streaks.Add((start, run, colour.Value));

            var patterns = new List<(int Start, int Length, int Count)>();

            for (int i = 0; i < streaks.Count;)
            {
                int length = streaks[i].Length;
                int count = 1;
                int patternStart = streaks[i].Start;

                while (i + count < streaks.Count && streaks[i + count].Length == length)
                    count++;

                patterns.Add((patternStart, length, count));
                i += count;
            }

            double currentAlternating = 0;

            for (int p = 0; p < patterns.Count; p++)
            {
                int interval = 17;

                for (int back = p - 1; back >= 0; back--)
                {
                    if (patterns[back].Length == patterns[p].Length)
                    {
                        interval = Math.Clamp(p - back, 1, 16);
                        break;
                    }
                }

                double patternStrain = 2 * (1 - sigmoid(interval, 2, 2, 1, 0.5));
                double currentPattern = patternStrain;
                strains[patterns[p].Start] += patternStrain;

                double alternating = sigmoid(1, 2, 2, 1, 0.5) * (currentPattern > 0 ? currentPattern : 1);
                currentAlternating = alternating;
                strains[patterns[p].Start] += alternating;
            }

            for (int s = 0; s < streaks.Count; s++)
            {
                double mono = 0.5 * sigmoid(1, 2, 2, 1, 0.5) * (currentAlternating > 0 ? currentAlternating : 1);
                strains[streaks[s].Start] += mono;
            }

            return strains;
        }

        private static bool colourChangedNear(List<TaikoHitObject> objects, int index)
        {
            if (objects[index] is not Hit hit)
                return false;

            double time = hit.StartTime;

            for (int i = Math.Max(0, index - 8); i < Math.Min(objects.Count, index + 8); i++)
            {
                if (i == index || objects[i] is not Hit other)
                    continue;

                if (other.Type != hit.Type && Math.Abs(other.StartTime - time) < 300)
                    return true;
            }

            return false;
        }

        private static (double Rho, double Weight) matchRhythm(double ratio)
        {
            double best = double.MaxValue;
            var match = rhythm_table[0];

            foreach (var entry in rhythm_table)
            {
                double error = Math.Abs(ratio - entry.Rho);

                if (error < best)
                {
                    best = error;
                    match = entry;
                }
            }

            return match;
        }

        private static int notesSince(List<double> history, double rho)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (Math.Abs(history[i] - rho) < 1e-6)
                    return history.Count - i;
            }

            return 64;
        }

        private static double sigmoid(double x, double centre, double width, double height, double minimum)
            => minimum + height / 2 * Math.Tanh(Math.E * -(x - centre) / width);

        private static double combinedStar(List<double> times, List<double> colour, List<double> rhythm, List<double> stamina)
        {
            var colourPeaks = EzPPPMath.SectionPeaks(times, colour, section_length);
            var rhythmPeaks = EzPPPMath.SectionPeaks(times, rhythm, section_length);
            var staminaPeaks = EzPPPMath.SectionPeaks(times, stamina, section_length);
            var buckets = new HashSet<long>(colourPeaks.Keys);
            buckets.UnionWith(rhythmPeaks.Keys);
            buckets.UnionWith(staminaPeaks.Keys);

            var combined = new List<double>(buckets.Count);

            foreach (long bucket in buckets)
            {
                colourPeaks.TryGetValue(bucket, out double c);
                rhythmPeaks.TryGetValue(bucket, out double r);
                staminaPeaks.TryGetValue(bucket, out double s);
                c *= 0.375 * difficulty_multiplier;
                r *= 0.2 * difficulty_multiplier;
                s *= 0.375 * difficulty_multiplier;
                double peak = EzPPPMath.PNorm(EzPPPMath.PNorm(c, s, 1.5), r, 2);

                if (peak > 0)
                    combined.Add(peak);
            }

            combined.Sort((x, y) => y.CompareTo(x));
            double rating = 0;

            for (int i = 0; i < combined.Count; i++)
                rating += combined[i] * Math.Pow(gamma, i);

            double inside = 1.4 * rating / 8 + 1;
            return inside <= 0 ? rating : 10.43 * Math.Log(inside);
        }
    }
}
