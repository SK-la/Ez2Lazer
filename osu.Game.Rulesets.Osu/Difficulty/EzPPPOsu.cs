// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    internal static class EzPPPOsuRegistration
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
        {
            EzPPPCalculators.Register(EzLocalProfileConstants.OSU_RULESET_ID, static (beatmap, mods, _) => EzPPPOsu.Calculate(beatmap, mods));
            EzPPPPerformanceCalculators.Register(EzLocalProfileConstants.OSU_RULESET_ID, static (skills, beatmap, mods, rate) => performance(skills, beatmap, mods, rate));
        }

        /// <summary>PP+ performance for a perfect play (documented section 1.8).</summary>
        private static double performance(IReadOnlyDictionary<string, double> skills, IBeatmap beatmap, IReadOnlyList<Mod> mods, double clockRate)
        {
            var difficulty = beatmap.Difficulty.Clone();
            bool hidden = false;
            bool flashlight = false;

            foreach (var mod in mods)
            {
                if (mod is IApplicableToDifficulty applicable)
                    applicable.ApplyToDifficulty(difficulty);

                // Any hidden-derived mod counts: Blinds and Traceable read as HD for the reading bonus.
                if (EzPPPOsu.IsHiddenMod(mod))
                    hidden = true;

                if (mod is OsuModFlashlight)
                    flashlight = true;
            }

            var hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(difficulty.OverallDifficulty);

            double rate = clockRate > 0 ? clockRate : 1;
            double greatWindow = hitWindows.WindowFor(HitResult.Great) / rate;

            int totalHits = 0;
            int circles = 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is not OsuHitObject)
                    continue;

                totalHits++;

                if (hitObject is HitCircle)
                    circles++;
            }

            return EzPPPPerformance.Osu(
                EzPPPPerformanceCalculators.Rating(skills, EzPPPSkillAxis.Aim.ToId()),
                EzPPPPerformanceCalculators.Rating(skills, EzPPPSkillAxis.Speed.ToId()),
                EzPPPPerformanceCalculators.Rating(skills, EzPPPSkillAxis.Stamina.ToId()),
                EzPPPPerformanceCalculators.Rating(skills, EzPPPSkillCatalog.ACCURACY_DIFFICULTY),
                totalHits,
                circles,
                greatWindow,
                hidden,
                flashlight);
        }
    }

    /// <summary>osu! PPP skills: aim family, speed, stamina, rhythm complexity, star rating.</summary>
    internal static class EzPPPOsu
    {
        private const double normalised_radius = 52;
        private const double section_length = 400;
        private const double gamma = 0.9;
        private const double rating_scale = 0.0675;
        private const float field_width = 512;
        private const float field_height = 384;

        /// <summary>Millisecond tolerance of the one-sided ratio comparisons.</summary>
        private const double ratio_tolerance_ms = 5;

        private const double aim_decay = 0.15;
        private const double aim_multiplier = 1059;
        private const double speed_decay = 0.1;
        private const double speed_multiplier = 2600;
        private const double stamina_decay = 0.45;
        private const double stamina_multiplier = 780;

        public static Dictionary<string, double> Calculate(IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            var notes = new List<OsuHitObject>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is OsuHitObject osuObject)
                    notes.Add(osuObject);
            }

            if (notes.Count < 3)
                return new Dictionary<string, double>(EzPPPSkillCatalog.ChartZeros(EzLocalProfileConstants.OSU_RULESET_ID), StringComparer.Ordinal);

            bool hidden = false;
            bool flashlight = false;

            foreach (var mod in mods)
            {
                if (IsHiddenMod(mod))
                    hidden = true;

                if (mod is OsuModFlashlight)
                    flashlight = true;
            }

            int count = notes.Count;
            double[] startTime = new double[count];
            double[] endTime = new double[count];
            double[] strain = new double[count];
            double[] gap = new double[count];
            double[] deltaTime = new double[count];
            double[] delta2 = new double[count];
            double[] rawDistance = new double[count];
            double[] distance = new double[count];
            double[] angle = new double[count];
            double[] flow = new double[count];
            double[] baseFlow = new double[count];
            double[] leniency = new double[count];
            double[] radius = new double[count];
            double[] preempt = new double[count];
            var cursorEnd = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                var current = notes[i];
                radius[i] = Math.Max(1, current.Radius);
                preempt[i] = Math.Max(1, current.TimePreempt);
                cursorEnd[i] = cursorEndOf(current);
                startTime[i] = current.StartTime;
                endTime[i] = current.GetEndTime();

                if (i == 0)
                {
                    // The first object has no predecessor, so its delta is measured from the beatmap origin.
                    strain[i] = Math.Max(50, startTime[i]);
                    delta2[i] = 100;
                    continue;
                }

                deltaTime[i] = Math.Max(0, startTime[i] - startTime[i - 1]);
                strain[i] = Math.Max(50, deltaTime[i]);

                bool previousSpan = notes[i - 1] is Slider || notes[i - 1] is Spinner;
                gap[i] = previousSpan ? Math.Max(50, startTime[i] - endTime[i - 1]) : strain[i];
                delta2[i] = i >= 2 ? Math.Max(100, startTime[i] - startTime[i - 2]) : 100;

                if (current is Spinner)
                {
                    rawDistance[i] = 0;
                    distance[i] = 0;
                }
                else
                {
                    rawDistance[i] = Vector2.Distance(cursorEnd[i - 1], current.StackedPosition);
                    distance[i] = rawDistance[i] * scalingFactor(radius[i]);
                }

                // The angle is measured from the previous object's cursor to the previous cursor, and from that
                // cursor to the current object.
                angle[i] = i >= 2 ? angleOf(cursorEnd[i - 2], notes[i - 1].StackedPosition, cursorEnd[i - 1], current.StackedPosition) : double.NaN;
            }

            for (int i = 0; i < count; i++)
            {
                // pi(i) is i - 2, matching the current branch's swapped constructor arguments: the flow state machine
                // compares against the object two back, not the one back.
                int pi = i >= 2 ? i - 2 : -1;
                double bpm = 15000.0 / strain[i];
                double speedFactor = EzPPPMath.T2T(bpm, 90, 30);

                if (pi < 0 || strain[i] - ratio_tolerance_ms < 2.0 / 3.0 * strain[pi])
                {
                    baseFlow[i] = speedFactor * distanceFactor(distance[i], bpm, 1);
                }
                else if (EzPPPMath.CoarseEqual(strain[i], strain[pi]))
                {
                    double a = angleScale(angle[i]);
                    double alpha = a + (1 - a) * leniency[pi];
                    baseFlow[i] = speedFactor * distanceFactor(distance[i], bpm, alpha);
                }
                else
                {
                    baseFlow[i] = 0;
                }

                double irregular = 0;

                if (pi >= 0)
                {
                    irregular = EzPPPMath.T2F(distance[i], wideDistance(bpm), wideDistance(bpm));

                    if (EzPPPMath.CoarseEqual(strain[i], strain[pi]))
                        irregular *= baseFlow[pi];
                    else
                        irregular = 0;

                    if (EzPPPMath.CoarseEqual(strain[i], strain[i - 1]))
                        irregular *= baseFlow[i - 1];
                    else
                        irregular = 0;
                }

                flow[i] = Math.Max(baseFlow[i], irregular);
                leniency[i] = pi < 0 ? 0 : (1 - baseFlow[i]) * irregular;
            }

            var aimStrain = new List<double>(count);
            var rawStrain = new List<double>(count);
            var jumpStrain = new List<double>(count);
            var flowStrain = new List<double>(count);
            var speedStrain = new List<double>(count);
            var staminaStrain = new List<double>(count);
            var times = new List<double>(count);

            double aimState = 0;
            double rawState = 0;
            double jumpState = 0;
            double flowState = 0;
            double speedState = 0;
            double staminaState = 0;

            var rhythm = new RhythmComplexity();
            int circles = 0;

            for (int i = 0; i < count; i++)
            {
                if (notes[i] is HitCircle)
                    circles++;
            }

            for (int i = 0; i < count; i++)
            {
                double read = reading(i, notes, startTime, baseFlow, distance, preempt[i], hidden, flashlight, rawDistance[i], radius[i]);
                double location = locationWeight(i > 0 ? notes[i - 1].StackedPosition : notes[i].StackedPosition, notes[i].StackedPosition);
                double requirement = distanceRequirement(i, distance, strain);
                double circleBonus = 1 + 120.0 / (radius[i] * radius[i]);

                double jump = jumpComponent(i, distance, strain, angle, requirement, location, flow[i]);
                double aimFlow = flowComponent(i, distance, strain, angle, flow, location);
                double jumpRaw = jumpComponent(i, rawDistance, strain, angle, requirement, location, flow[i]);

                double decay = EzPPPMath.Decay(deltaTime[i], aim_decay);
                aimState = decay * aimState + aim_multiplier * (jump + aimFlow) * circleBonus * read;
                // RawAim drops the small-circle bonus and measures the jump at the true distance; its flow term is
                // identical to Aim's, so it reuses the normalised distance.
                rawState = decay * rawState + aim_multiplier * (jumpRaw + aimFlow) * read;
                jumpState = decay * jumpState + aim_multiplier * jump * circleBonus * read;
                flowState = decay * flowState + aim_multiplier * aimFlow * circleBonus * read;

                double half = delta2[i] / 2;
                double tap = 30 / ((half - 20) * (half - 20)) + 2 / half;
                double stream = 12.5 / ((half - 20) * (half - 20)) + 0.25 / half + 0.005;
                double speedAdd = (1 - flow[i]) * tap + flow[i] * stream;
                speedState = EzPPPMath.Decay(deltaTime[i], speed_decay) * speedState + speed_multiplier * speedAdd;

                double tapStamina = 2 / (half - 20);
                double streamStamina = 1 / (half - 20);
                double staminaAdd = (1 - flow[i]) * tapStamina + flow[i] * streamStamina;
                staminaState = EzPPPMath.Decay(deltaTime[i], stamina_decay) * staminaState + stamina_multiplier * staminaAdd;

                times.Add(startTime[i]);
                aimStrain.Add(aimState);
                rawStrain.Add(rawState);
                jumpStrain.Add(jumpState);
                flowStrain.Add(flowState);
                speedStrain.Add(speedState);
                staminaStrain.Add(staminaState);

                if (notes[i] is HitCircle)
                    rhythm.Add(i, i > 0 ? notes[i - 1] : null, gap, strain, flow[i], distance[i]);
                else
                    rhythm.Reset();
            }

            double aimValue = EzPPPMath.Aggregate(times, aimStrain, section_length, gamma, aim_decay);
            double rawValue = EzPPPMath.Aggregate(times, rawStrain, section_length, gamma, aim_decay);
            double jumpValue = EzPPPMath.Aggregate(times, jumpStrain, section_length, gamma, aim_decay);
            double flowValue = EzPPPMath.Aggregate(times, flowStrain, section_length, gamma, aim_decay);
            double speedValue = EzPPPMath.Aggregate(times, speedStrain, section_length, gamma, speed_decay);
            double staminaValue = EzPPPMath.Aggregate(times, staminaStrain, section_length, gamma, stamina_decay);

            double aimRating = rating_scale * Math.Sqrt(Math.Max(0, aimValue));
            double rawRating = rating_scale * Math.Sqrt(Math.Max(0, rawValue));
            double jumpRating = rating_scale * Math.Sqrt(Math.Max(0, jumpValue));
            double flowRating = rating_scale * Math.Sqrt(Math.Max(0, flowValue));
            double precision = rating_scale * Math.Sqrt(Math.Max(0, aimValue - rawValue));
            double speedRating = rating_scale * Math.Sqrt(Math.Max(0, speedValue));
            double staminaRating = rating_scale * Math.Sqrt(Math.Max(0, staminaValue));
            double accuracy = rhythm.Value(circles);
            double star = 1.6 * Math.Pow(Math.Pow(aimRating, 3) + Math.Pow(Math.Max(speedRating, staminaRating), 3), 1.0 / 3.0);

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPPPSkillAxis.Aim.ToChartSkillId()] = EzPPPMath.Finite(aimRating),
                [EzPPPSkillAxis.RawAim.ToChartSkillId()] = EzPPPMath.Finite(rawRating),
                [EzPPPSkillAxis.JumpAim.ToChartSkillId()] = EzPPPMath.Finite(jumpRating),
                [EzPPPSkillAxis.FlowAim.ToChartSkillId()] = EzPPPMath.Finite(flowRating),
                [EzPPPSkillAxis.Precision.ToChartSkillId()] = EzPPPMath.Finite(precision),
                [EzPPPSkillAxis.Speed.ToChartSkillId()] = EzPPPMath.Finite(speedRating),
                [EzPPPSkillAxis.Stamina.ToChartSkillId()] = EzPPPMath.Finite(staminaRating),
                [EzPPPSkillAxis.Accuracy.ToChartSkillId()] = EzPPPMath.Finite(accuracy),
                [EzPPPSkillAxis.StarRating.ToChartSkillId()] = EzPPPMath.Finite(star),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.ACCURACY_DIFFICULTY)] = EzPPPMath.Finite(accuracy),
            };
        }

        /// <summary>Whether the mod hides the playfield for the reading multiplier (HD and its visibility variants).</summary>
        internal static bool IsHiddenMod(Mod mod) => mod is ModHidden or ModBlinds or ModTraceable;

        /// <summary>Distance scale that makes every circle radius read as <see cref="normalised_radius"/>.</summary>
        private static double scalingFactor(double radius)
        {
            double factor = normalised_radius / radius;

            if (radius < 30)
                factor *= 1 + Math.Min(30 - radius, 5) / 50;

            return factor;
        }

        private static double wideDistance(double bpm)
            => (Math.Tanh((bpm - 140) / 20.0) * 1.75 + 2.75) * normalised_radius;

        private static double distanceFactor(double scaledDistance, double bpm, double alpha)
        {
            double delta = (Math.Tanh((bpm - 140) / 20.0) + 2) * normalised_radius;
            return EzPPPMath.T2F(scaledDistance, delta * alpha, delta);
        }

        /// <summary>Angle contribution to the flow distance threshold; NaN means "no angle", which sits at the middle.</summary>
        private static double angleScale(double angle)
        {
            if (double.IsNaN(angle))
                return 0.5;

            return (-Math.Sin(Math.Cos(angle) * Math.PI / 2) + 3) / 4;
        }

        /// <summary>Jump basis without the weight terms.</summary>
        private static double jumpBasis(double scaledDistance, double strainMs)
            => scaledDistance / normalised_radius / strainMs;

        /// <summary>How much of the previous jump's distance this jump is expected to cover.</summary>
        private static double distanceRequirement(int index, double[] distance, double[] strain)
        {
            if (index == 0 || !(strain[index] + ratio_tolerance_ms > strain[index - 1]))
                return 0;

            double reach = strain[index - 1] / strain[index] * 2 * normalised_radius;
            return EzPPPMath.T2T(distance[index - 1], 0, reach);
        }

        private static double jumpComponent(int index, double[] distance, double[] strain, double[] angle, double requirement,
                                            double location, double flowValue)
        {
            if (index == 0)
                return 0;

            double basis = jumpBasis(distance[index], strain[index]);
            double angleWeight = 1;

            if (!double.IsNaN(angle[index]))
                angleWeight += (-Math.Sin(Math.Cos(angle[index]) * Math.PI / 2) + 1) / 10 * requirement;

            double pattern = 1;

            for (int back = 1; back <= 2 && index - back >= 0; back++)
            {
                int j = index - back;
                int exponent = 3 - back;
                double distanceWeight = distance[j] <= 0 ? 1.05 : ratioWeight(jumpBasis(distance[index], strain[index]) / jumpBasis(distance[j], strain[j]) - 1);
                double angleWeightStep = angleStepWeight(index, j, strain, angle);
                pattern *= Math.Pow(distanceWeight * angleWeightStep, exponent);
            }

            pattern = 1 + (pattern - 1) * requirement;

            return basis * angleWeight * pattern * location * (1 - flowValue);
        }

        private static double ratioWeight(double ratio)
        {
            if (ratio <= 0)
                return 1 + ratio * ratio / 2;

            if (ratio < 1)
                return 1 + (-Math.Cos(ratio * Math.PI) + 1) / 40;

            return 1.05;
        }

        private static double angleStepWeight(int index, int other, double[] strain, double[] angle)
        {
            if (!EzPPPMath.RatioEqual(1, strain[index], strain[other]))
                return 1;

            if (double.IsNaN(angle[index]) || double.IsNaN(angle[other]))
                return 1;

            double delta = Math.Abs(angle[index]) - Math.Abs(angle[other]);

            if (Math.Abs(delta) >= Math.PI / 1.5)
                return 1.05;

            return 1 + (-Math.Sin(Math.Cos(delta * 1.5) * Math.PI / 2) + 1) / 40;
        }

        private static double flowComponent(int index, double[] distance, double[] strain, double[] angle, double[] flow, double location)
        {
            if (index == 0)
                return 0;

            double basis = ((Math.Tanh(distance[index] / normalised_radius - 2) + 1) * 2.5 + distance[index] / (5 * normalised_radius)) / strain[index];
            double angleWeight = 1 + (Math.Cos(double.IsNaN(angle[index]) ? 0 : angle[index]) + 1) / 10;
            double pattern = flowPattern(index, distance, angle, flow);

            return basis * angleWeight * pattern * (1 + (location - 1) / 2) * flow[index];
        }

        private static double flowPattern(int index, double[] distance, double[] angle, double[] flow)
        {
            if (index == 0)
                return 1;

            double ratio = distance[index - 1] > 0 ? distance[index] / distance[index - 1] - 1 : 1;

            // At or above a full distance increase the bonus saturates at 1 (the initial value), not 0.
            double distanceBonus = ratio <= 0
                ? ratio * ratio
                : ratio < 1
                    ? (-Math.Cos(ratio * Math.PI) + 1) / 2
                    : 1;

            double angleBonus = 0;

            if (!double.IsNaN(angle[index]) && !double.IsNaN(angle[index - 1]) && Math.Abs(angle[index]) < Math.Abs(angle[index - 1]))
            {
                angleBonus = (-Math.Cos(Math.Sin((angle[index] - angle[index - 1]) / 2) * Math.PI) + 1) / 2;

                if (angleBonus > 0)
                {
                    double reversed = (-Math.Cos(Math.Sin((Math.Abs(angle[index]) - Math.Abs(angle[index - 1])) / 2) * Math.PI) + 1) / 2;
                    angleBonus = Math.Min(angleBonus, reversed);
                }
            }

            double blend = EzPPPMath.T2T(ratio, 0, 1);

            double jump = (Math.Tanh(distance[index] / normalised_radius - 2) + 1) * 2.5 + distance[index] / (5 * normalised_radius);
            double single = distance[index] <= 0
                ? 1
                : (1 - blend) + blend * (distance[index] / normalised_radius) / jump;

            double distanceWeight = (1 + distanceBonus) * single;
            double angleWeight = 1 + angleBonus * (1 - blend);

            return 1 + (distanceWeight * angleWeight - 1) * flow[index - 1];
        }

        /// <summary>How far off the playfield centre the movement sits, rotated a third of a turn.</summary>
        private static double locationWeight(Vector2 previous, Vector2 current)
        {
            var centre = (previous + current) * 0.5f - new Vector2(field_width / 2, field_height / 2);
            const double rotation = Math.PI / 3;
            double a = (centre.X * Math.Cos(rotation) + centre.Y * Math.Sin(rotation)) / 750;
            double b = (centre.X * Math.Sin(rotation) - centre.Y * Math.Cos(rotation)) / 1000;

            return 1 + a * a + b * b;
        }

        private static double reading(int index, List<OsuHitObject> notes, double[] startTime, double[] baseFlow, double[] distance,
                                     double preempt, bool hidden, bool flashlight, double rawDistance, double radius)
        {
            double readingStrain = 0;

            for (int k = index - 1; k >= 0; k--)
            {
                if (startTime[index] - startTime[k] > preempt)
                    break;

                readingStrain += (1 - 0.75 * baseFlow[k]) * (1 + 0.5 * baseFlow[k] * distance[k] / normalised_radius);
            }

            double bonus = Math.Pow(Math.Max(0, readingStrain), 1.5) / 100;
            double read = hidden ? 1.05 + 1.5 * bonus : 1 + bonus;
            read *= 1 + (-Math.Tanh((preempt - 325) / 30.0) + 1) / 15.0;

            if (flashlight)
                read *= 1 + 0.3 * EzPPPMath.T2T(rawDistance, field_height / 4, radius);

            return read;
        }

        private static Vector2 cursorEndOf(OsuHitObject hitObject)
        {
            if (hitObject is not Slider slider)
                return hitObject.StackedEndPosition;

            float follow = (float)Math.Max(1, slider.Radius) * 3f;
            Vector2 cursor = slider.StackedPosition;
            const int samples = 24;

            for (int i = 1; i <= samples; i++)
            {
                float progress = i / (float)samples;
                Vector2 point = slider.StackedPosition + slider.Path.PositionAt(slider.ProgressAt(progress));
                Vector2 delta = point - cursor;
                float length = delta.Length;

                if (length > follow && length > 0.001f)
                    cursor += delta * ((length - follow) / length);
            }

            return cursor;
        }

        /// <summary>
        /// Angle between the previous cursor-to-object direction and the object-to-cursor direction, in [0, π].
        /// </summary>
        private static double angleOf(Vector2 previousCursor, Vector2 previousObject, Vector2 currentCursor, Vector2 current)
        {
            Vector2 left = previousObject - previousCursor;
            Vector2 right = current - currentCursor;
            float leftLen = left.Length;
            float rightLen = right.Length;

            if (leftLen < 0.001f || rightLen < 0.001f)
                return double.NaN;

            double dot = Math.Clamp(Vector2.Dot(left, right) / (leftLen * rightLen), -1, 1);
            return Math.Acos(dot);
        }

        /// <summary>
        /// RhythmComplexity: accumulates the per-circle bonus that becomes AccuracyDifficulty.
        /// </summary>
        /// <remarks>
        /// The offbeat state and the short insertion queue are what make a rhythm change score differently the
        /// second time: a repeated pattern decays the bonus it earned the first time.
        /// </remarks>
        private sealed class RhythmComplexity
        {
            private const int queue_capacity = 10;

            private readonly List<int> queue = new List<int>(queue_capacity + 1);
            private double sum;
            private bool offbeat;

            /// <summary>
            /// Adds the bonus for a circle at <paramref name="index"/>. Only circles call this; every other object
            /// type just clears the offbeat state through <see cref="Reset"/>.
            /// </summary>
            public void Add(int index, OsuHitObject? previous, double[] gap, double[] strain, double flow, double distance)
            {
                double current = gap[index];
                double bonus = 0.05 * flow;
                double previousGap = previous != null ? gap[index - 1] : 0;

                if (previous is HitCircle)
                {
                    if (offbeat && ratioGreater(current, previousGap, 1.5))
                    {
                        double delta = 5;

                        foreach (int entry in queue)
                        {
                            if (entry > 0)
                                delta *= 1 - 0.5 * Math.Pow(0.9, index - entry);
                            else if (entry == -1)
                                delta = 5;
                        }

                        enqueue(index);
                        bonus += delta;
                    }
                    else if (ratioEqual(current, previousGap, 2.0 / 3.0))
                    {
                        bonus += 4 + 8 * flow;

                        if (flow > 0.8)
                            enqueue(-1);
                    }
                    else if (ratioEqual(current, previousGap, 1.0 / 3.0))
                    {
                        bonus += 0.4 + 0.8 * flow;
                    }
                    else if (ratioEqual(current, previousGap, 0.5) || ratioLess(current, previousGap, 0.25))
                    {
                        bonus += 0.1 + 0.2 * flow;
                    }

                    if (ratioLess(current, previousGap, 2.0 / 3.0) && flow > 0.8)
                        offbeat = true;
                    else if (ratioEqual(current, previousGap, 1) && flow > 0.8)
                        offbeat = !offbeat;
                    else
                        offbeat = false;
                }
                else if (previous is Slider)
                {
                    // A circle placed on the tail of a slider body: only the tap-on-release fractions count.
                    double body = strain[index] - current;

                    if (body > 0 && (ratioEqual(current, body, 0.5) || ratioEqual(current, body, 0.25)))
                    {
                        double density = 15000 / Math.Max(1, current);
                        double expectation = EzPPPMath.T2T(density, 120, 30)
                                             * EzPPPMath.T2F(distance, (Math.Tanh((density - 140) / 20) + 2) * normalised_radius, normalised_radius);
                        bonus += 0.3 * expectation;
                        offbeat = expectation > 0.8;
                    }
                    else
                    {
                        offbeat = false;
                    }
                }
                else
                {
                    offbeat = false;
                }

                sum += bonus;
            }

            /// <summary>Non-circle objects contribute no bonus; they only clear the offbeat state.</summary>
            public void Reset() => offbeat = false;

            public double Value(int circles)
                => circles > 0 ? 1 + sum / circles * Math.Tanh(circles / 50.0) : 1;

            private void enqueue(int entry)
            {
                if (queue.Count == queue_capacity)
                    queue.RemoveAt(0);

                queue.Add(entry);
            }

            private static bool ratioEqual(double current, double previous, double expected)
                => ratioGreater(current, previous, expected) && ratioLess(current, previous, expected);

            /// <summary><c>current + 5 &gt; expected * previous</c>.</summary>
            private static bool ratioGreater(double current, double previous, double expected)
                => current + ratio_tolerance_ms > expected * previous;

            /// <summary><c>current - 5 &lt; expected * previous</c>.</summary>
            private static bool ratioLess(double current, double previous, double expected)
                => current - ratio_tolerance_ms < expected * previous;
        }
    }
}
