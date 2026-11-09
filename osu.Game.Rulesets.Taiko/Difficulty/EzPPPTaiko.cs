// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Scoring;

namespace osu.Game.Rulesets.Taiko.Difficulty
{
    internal static class EzPPPTaikoRegistration
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
        {
            EzPPPCalculators.Register(EzLocalProfileConstants.TAIKO_RULESET_ID, static (beatmap, mods, rate) => EzPPPTaiko.Calculate(beatmap, mods, rate));
            EzPPPPerformanceCalculators.Register(EzLocalProfileConstants.TAIKO_RULESET_ID, static (skills, beatmap, mods, rate) => performance(skills, beatmap, mods, rate));
        }

        /// <summary>PP+ performance for a perfect play (documented section 2.7).</summary>
        private static double performance(IReadOnlyDictionary<string, double> skills, IBeatmap beatmap, IReadOnlyList<Mod> mods, double clockRate)
        {
            var difficulty = beatmap.Difficulty.Clone();

            foreach (var mod in mods)
            {
                if (mod is IApplicableToDifficulty applicable)
                    applicable.ApplyToDifficulty(difficulty);
            }

            var hitWindows = new TaikoHitWindows();
            hitWindows.SetDifficulty(difficulty.OverallDifficulty);

            double rate = clockRate > 0 ? clockRate : 1;

            int totalHits = 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is Hit)
                    totalHits++;
            }

            bool hidden = false;
            bool easy = false;
            bool hardRock = false;
            bool flashlight = false;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ModHidden:
                        hidden = true;
                        break;

                    case ModEasy:
                        easy = true;
                        break;

                    case ModHardRock:
                        hardRock = true;
                        break;

                    case ModFlashlight:
                        flashlight = true;
                        break;
                }
            }

            bool isConvert = beatmap.BeatmapInfo.Ruleset.OnlineID != EzLocalProfileConstants.TAIKO_RULESET_ID;

            return EzPPPPerformance.Taiko(
                EzPPPPerformanceCalculators.Rating(skills, EzPPPSkillCatalog.STAR_RATING),
                totalHits,
                hitWindows.WindowFor(HitResult.Great) / rate,
                hitWindows.WindowFor(HitResult.Ok) / rate,
                hidden,
                easy,
                hardRock,
                flashlight,
                isConvert);
        }
    }

    /// <summary>Taiko PPP skills: rhythm, colour, stamina, and the combined star rating.</summary>
    internal static class EzPPPTaiko
    {
        private const double difficulty_multiplier = 0.084375;
        private const double section_length = 400;
        private const double gamma = 0.9;

        private const double rhythm_decay = 0.96;
        private const double rhythm_multiplier = 10;
        private const double rhythm_max_gap = 210;
        private const int rhythm_history_capacity = 8;

        private const double colour_decay = 0.8;
        private const double colour_multiplier = 0.12;

        private const double stamina_decay = 0.4;
        private const double stamina_multiplier = 1.1;

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

        public static Dictionary<string, double> Calculate(IBeatmap beatmap, IReadOnlyList<Mod> mods, double clockRate)
        {
            _ = mods;

            var objects = new List<TaikoHitObject>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is Hit or DrumRoll or Swell)
                    objects.Add((TaikoHitObject)hitObject);
            }

            if (objects.Count < 3)
                return new Dictionary<string, double>(EzPPPSkillCatalog.ChartZeros(EzLocalProfileConstants.TAIKO_RULESET_ID), StringComparer.Ordinal);

            double rate = positiveRate(clockRate);

            int count = objects.Count;
            var times = new List<double>(count);
            var rhythmStrain = new List<double>(count);
            var colourStrain = new List<double>(count);
            var staminaStrain = new List<double>(count);

            double[] rhythmValues = rhythms(objects, rate);
            double[] colourValues = colours(objects);
            double[] staminaValues = stamina(objects, rate);

            double rhythmState = 0;
            double colourState = 0;
            double staminaState = 0;
            double previousTime = objects[1].StartTime;

            for (int i = 2; i < count; i++)
            {
                double delta = scaledDelta(objects[i].StartTime, previousTime, rate);

                rhythmState = rhythm_decay * rhythmState + rhythm_multiplier * rhythmValues[i];
                colourState = EzPPPMath.Decay(delta, colour_decay) * colourState + colour_multiplier * colourValues[i];
                staminaState = EzPPPMath.Decay(delta, stamina_decay) * staminaState + stamina_multiplier * staminaValues[i];

                times.Add(scaledTime(objects[i].StartTime, rate));
                rhythmStrain.Add(rhythmState);
                colourStrain.Add(colourState);
                staminaStrain.Add(staminaState);

                previousTime = objects[i].StartTime;
            }

            double rhythmValue = EzPPPMath.Aggregate(times, rhythmStrain, section_length, gamma, 0);
            double colourValue = EzPPPMath.Aggregate(times, colourStrain, section_length, gamma, colour_decay);
            double staminaValue = EzPPPMath.Aggregate(times, staminaStrain, section_length, gamma, stamina_decay);
            double star = combinedStar(times, colourStrain, rhythmStrain, staminaStrain);

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.RHYTHM)] = EzPPPMath.Finite(0.2 * difficulty_multiplier * rhythmValue),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.COLOUR)] = EzPPPMath.Finite(0.375 * difficulty_multiplier * colourValue),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAMINA)] = EzPPPMath.Finite(0.375 * difficulty_multiplier * staminaValue),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)] = EzPPPMath.Finite(star),
            };
        }

        /// <summary>
        /// Rhythm skill contribution per object. Only a Hit that repeats a table ratio inside the speed window
        /// contributes; everything else contributes zero and may also reset the rolling state.
        /// </summary>
        private static double positiveRate(double clockRate) => clockRate > 0 ? clockRate : 1;

        private static double scaledTime(double milliseconds, double rate) => milliseconds / rate;

        private static double scaledDelta(double currentMs, double previousMs, double rate)
            => Math.Max(0, (currentMs - previousMs) / rate);

        private static double[] rhythms(List<TaikoHitObject> objects, double rate)
        {
            double[] result = new double[objects.Count];
            var history = new List<RhythmEntry>(rhythm_history_capacity + 1);
            double rolling = 0;
            int notesSinceAccumulated = 0;

            for (int i = 2; i < objects.Count; i++)
            {
                if (objects[i] is not Hit)
                {
                    rolling = 0;
                    notesSinceAccumulated = 0;
                    continue;
                }

                notesSinceAccumulated++;

                double delta = scaledDelta(objects[i].StartTime, objects[i - 1].StartTime, rate);
                double previousDelta = scaledDelta(objects[i - 1].StartTime, objects[i - 2].StartTime, rate);
                double ratio = previousDelta > 0 ? delta / previousDelta : 1;
                var (rho, weight) = matchRhythm(ratio);

                rolling *= rhythm_decay;

                if (delta >= rhythm_max_gap)
                {
                    // Too slow to be a rhythm: the roll and the counter both restart.
                    rolling = 0;
                    notesSinceAccumulated = 0;
                    continue;
                }

                if (weight <= 0)
                {
                    // No rhythm change: the roll keeps decaying and the counter keeps growing, but nothing is added.
                    continue;
                }

                if (history.Count == rhythm_history_capacity)
                    history.RemoveAt(0);

                history.Add(new RhythmEntry(i, rho));

                double repetition = repetitionPenalty(history, i);
                double length = Math.Min(0.15 * notesSinceAccumulated, Math.Clamp(2.5 - 0.15 * notesSinceAccumulated, 0, 1));
                double speed = delta < 80 ? 1 : Math.Max(0, 1.4 - 0.005 * delta);

                rolling += weight * repetition * length * speed;
                notesSinceAccumulated = 0;
                result[i] = rolling;
            }

            return result;
        }

        /// <summary>
        /// Repeated-rhythm penalty: compares the newest 2/3/4-entry window against earlier equal-length windows and
        /// decays the reward by how recently the same shape appeared.
        /// </summary>
        private static double repetitionPenalty(List<RhythmEntry> history, int objectIndex)
        {
            double penalty = 1;

            for (int length = 2; length <= 4; length++)
            {
                if (history.Count < length * 2)
                    continue;

                int tailStart = history.Count - length;
                int matchedAt = -1;

                for (int start = tailStart - length; start >= 0; start--)
                {
                    bool same = true;

                    for (int k = 0; k < length; k++)
                    {
                        if (!history[start + k].SameRhythmAs(history[tailStart + k]))
                        {
                            same = false;
                            break;
                        }
                    }

                    if (same)
                    {
                        matchedAt = start;
                        break;
                    }
                }

                if (matchedAt < 0)
                    continue;

                penalty *= Math.Min(1, 0.032 * (objectIndex - history[matchedAt].Index));
                break;
            }

            return penalty;
        }

        /// <summary>
        /// Colour skill contribution. Encodings are grouped hierarchically (mono streak → alternating mono pattern →
        /// repeating hit pattern) and only each unit's first note carries its share.
        /// </summary>
        private static double[] colours(List<TaikoHitObject> objects)
        {
            double[] result = new double[objects.Count];
            var streaks = monoStreaks(objects);

            if (streaks.Count == 0)
                return result;

            var patterns = alternatingPatterns(streaks);
            var repeating = repeatingPatterns(patterns);

            foreach (var pattern in repeating)
            {
                result[pattern.Start] += 2 * (1 - sigmoid(pattern.Interval, 2, 2, 1, 0.5));
            }

            foreach (var pattern in repeating)
            {
                for (int a = pattern.FirstPattern; a <= pattern.LastPattern; a++)
                {
                    var alternating = patterns[a];
                    int positionInPattern = a - pattern.FirstPattern + 1;
                    double parent = 2 * (1 - sigmoid(pattern.Interval, 2, 2, 1, 0.5));

                    result[alternating.Start] += sigmoid(positionInPattern, 2, 2, 1, 0.5) * parent;

                    for (int s = alternating.FirstStreak; s <= alternating.LastStreak; s++)
                    {
                        var streak = streaks[s];
                        int position = s - alternating.FirstStreak + 1;
                        double alternatingValue = sigmoid(positionInPattern, 2, 2, 1, 0.5) * parent;
                        result[streak.Start] += 0.5 * sigmoid(position, 2, 2, 1, 0.5) * alternatingValue;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Mono streaks: runs of same-colour hits. A non-hit object forms its own one-object streak, which is what
        /// makes a drum roll break the chain.
        /// </summary>
        private static List<Unit> monoStreaks(List<TaikoHitObject> objects)
        {
            var streaks = new List<Unit>();
            var current = new Unit(-1, -1, null, 0);

            for (int i = 2; i < objects.Count; i++)
            {
                if (objects[i] is not Hit hit)
                {
                    if (current.Length > 0)
                        streaks.Add(current);

                    streaks.Add(new Unit(i, i, null, 1));
                    current = new Unit(-1, -1, null, 0);
                    continue;
                }

                bool continues = current.Length > 0 && current.End + 1 == i && current.Type == hit.Type;

                if (continues)
                {
                    current = current.Extend();
                }
                else
                {
                    if (current.Length > 0)
                        streaks.Add(current);

                    current = new Unit(i, i, hit.Type, 1);
                }
            }

            if (current.Length > 0)
                streaks.Add(current);

            return streaks;
        }

        private static List<Alternating> alternatingPatterns(List<Unit> streaks)
        {
            var patterns = new List<Alternating>();
            int i = 0;

            while (i < streaks.Count)
            {
                int last = i;

                while (last + 1 < streaks.Count && streaks[last + 1].Length == streaks[i].Length)
                    last++;

                int[] lengths = new int[last - i + 1];

                for (int k = i; k <= last; k++)
                    lengths[k - i] = streaks[k].Length;

                patterns.Add(new Alternating(i, last, streaks[i].Start, lengths, streaks[i].Type));
                i = last + 1;
            }

            return patterns;
        }

        private static List<Repeating> repeatingPatterns(List<Alternating> patterns)
        {
            var result = new List<Repeating>();
            int i = 0;

            while (i < patterns.Count)
            {
                int last = i;

                if (i + 2 < patterns.Count && strictlyRepeats(patterns[i], patterns[i + 2]))
                {
                    last = i + 2;

                    while (last + 2 < patterns.Count && strictlyRepeats(patterns[last], patterns[last + 2]))
                        last += 2;
                }

                int interval = 17;

                for (int back = result.Count - 1, steps = 1; back >= 0 && steps <= 15; back--, steps++)
                {
                    if (repeats(patterns[last], patterns[result[back].LastPattern]))
                    {
                        interval = steps;
                        break;
                    }
                }

                result.Add(new Repeating(i, last, patterns[i].Start, interval));
                i = last + 1;
            }

            return result;
        }

        private static bool strictlyRepeats(Alternating a, Alternating b)
            => a.Count == b.Count && a.FirstType == b.FirstType && a.FirstLengthAt(0) == b.FirstLengthAt(0);

        private static bool repeats(Alternating a, Alternating b)
        {
            if (a.Count != b.Count)
                return false;

            int compare = Math.Min(a.Count, 2);

            for (int k = 0; k < compare; k++)
            {
                if (a.FirstLengthAt(k) != b.FirstLengthAt(k))
                    return false;
            }

            return true;
        }

        /// <summary>Stamina contribution: same-colour press cadence with a two- or four-finger assumption.</summary>
        private static double[] stamina(List<TaikoHitObject> objects, double rate)
        {
            double[] result = new double[objects.Count];
            var history = new Dictionary<HitType, List<double>>();

            for (int i = 2; i < objects.Count; i++)
            {
                if (objects[i] is not Hit hit)
                    continue;

                int fingers = colourChangedNear(objects, i, rate) ? 2 : 4;

                if (!history.TryGetValue(hit.Type, out var times))
                {
                    times = new List<double>();
                    history[hit.Type] = times;
                }

                int back = fingers - 1;

                if (times.Count >= back && back > 0)
                    result[i] = 0.5 + 30.0 / Math.Max(1, scaledDelta(hit.StartTime, times[^back], rate));

                times.Add(hit.StartTime);
            }

            return result;
        }

        /// <summary>Whether a colour change sits within 300 ms before or after this object.</summary>
        private static bool colourChangedNear(List<TaikoHitObject> objects, int index, double rate)
        {
            var hit = (Hit)objects[index];

            for (int i = Math.Max(0, index - 8); i < Math.Min(objects.Count, index + 8); i++)
            {
                if (i == index || objects[i] is not Hit other)
                    continue;

                if (other.Type != hit.Type && scaledDelta(other.StartTime, hit.StartTime, rate) < 300)
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

        private static double sigmoid(double x, double centre, double width, double height, double minimum)
            => minimum + height / 2 * Math.Tanh(Math.E * -(x - centre) / width);

        /// <summary>
        /// Combined rating: per section, the three scaled peaks are folded with two p-norms, then the sections are
        /// decay-weighted and compressed through the logarithmic star curve.
        /// </summary>
        private static double combinedStar(List<double> times, List<double> colour, List<double> rhythm, List<double> stamina)
        {
            var colourPeaks = EzPPPMath.SectionPeaks(times, colour, section_length, colour_decay);
            var rhythmPeaks = EzPPPMath.SectionPeaks(times, rhythm, section_length, 0);
            var staminaPeaks = EzPPPMath.SectionPeaks(times, stamina, section_length, stamina_decay);

            int sections = Math.Max(colourPeaks.Count, Math.Max(rhythmPeaks.Count, staminaPeaks.Count));
            var combined = new List<double>(sections);

            for (int i = 0; i < sections; i++)
            {
                double c = (i < colourPeaks.Count ? colourPeaks[i] : 0) * 0.375 * difficulty_multiplier;
                double r = (i < rhythmPeaks.Count ? rhythmPeaks[i] : 0) * 0.2 * difficulty_multiplier;
                double s = (i < staminaPeaks.Count ? staminaPeaks[i] : 0) * 0.375 * difficulty_multiplier;

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

        private readonly record struct RhythmEntry(int Index, double Rho)
        {
            public bool SameRhythmAs(RhythmEntry other) => Math.Abs(Rho - other.Rho) < 1e-9;
        }

        private readonly record struct Unit(int Start, int End, HitType? Type, int Length)
        {
            public Unit Extend() => this with { End = End + 1, Length = Length + 1 };
        }

        private readonly record struct Alternating(int FirstStreak, int LastStreak, int Start, IReadOnlyList<int> Lengths, HitType? FirstType)
        {
            public int Count => Lengths.Count;

            public int Length => Lengths[0];

            public int FirstLengthAt(int offset) => Lengths[offset];
        }

        private readonly record struct Repeating(int FirstPattern, int LastPattern, int Start, int Interval);
    }
}
