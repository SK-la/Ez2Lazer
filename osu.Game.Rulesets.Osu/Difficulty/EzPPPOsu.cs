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
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    internal static class EzPPPOsuRegistration
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
            => EzPPPCalculators.Register(EzLocalProfileConstants.OSU_RULESET_ID, static (beatmap, mods, _) => EzPPPOsu.Calculate(beatmap, mods));
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
                if (mod is OsuModHidden)
                    hidden = true;

                if (mod is OsuModFlashlight)
                    flashlight = true;
            }

            int count = notes.Count;
            double[] strain = new double[count];
            double[] gap = new double[count];
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

                if (i == 0)
                    continue;

                strain[i] = Math.Max(50, current.StartTime - notes[i - 1].StartTime);
                bool previousSpan = notes[i - 1] is Slider || notes[i - 1] is Spinner;
                gap[i] = previousSpan ? Math.Max(50, current.StartTime - notes[i - 1].GetEndTime()) : strain[i];

                if (i >= 2)
                    delta2[i] = Math.Max(100, current.StartTime - notes[i - 2].StartTime);

                if (current is Spinner)
                {
                    rawDistance[i] = 0;
                    distance[i] = 0;
                }
                else
                {
                    rawDistance[i] = Vector2.Distance(cursorEnd[i - 1], current.StackedPosition);
                    distance[i] = rawDistance[i] * (normalised_radius / radius[i]);
                }

                if (i >= 2)
                    angle[i] = angleOf(notes[i - 2].StackedPosition, notes[i - 1].StackedPosition, current.StackedPosition);
                else
                    angle[i] = double.NaN;
            }

            for (int i = 1; i < count; i++)
            {
                double bpm = 15000.0 / strain[i];
                double speedFactor = EzPPPMath.T2T(bpm, 90, 30);
                double alpha = double.IsNaN(angle[i]) ? 0.5 : 0.5 * (angle[i] / Math.PI) + 0.5 * leniency[i - 1];
                double distFactor = distanceFactor(distance[i], bpm, alpha);
                double ratio = strain[i - 1] > 0 ? strain[i] / strain[i - 1] : 0;

                if (i == 1 || ratio < 2.0 / 3.0)
                    baseFlow[i] = speedFactor * distanceFactor(distance[i], bpm, 0.5);
                else if (EzPPPMath.CoarseEqual(strain[i], strain[i - 1]))
                    baseFlow[i] = speedFactor * distFactor;
                else
                    baseFlow[i] = 0;

                double irregular = 0;

                if (i >= 2 && EzPPPMath.CoarseEqual(strain[i], strain[i - 1]) && EzPPPMath.CoarseEqual(strain[i], strain[i - 2]))
                {
                    double wide = (Math.Tanh((bpm - 140) / 20.0) * 1.75 + 2.75) * normalised_radius;
                    irregular = EzPPPMath.T2F(distance[i], wide, wide) * baseFlow[i - 1] * baseFlow[i - 2];
                }

                flow[i] = Math.Max(baseFlow[i], irregular);
                leniency[i] = (1 - baseFlow[i]) * irregular;
            }

            var aimStrain = new List<double>();
            var rawStrain = new List<double>();
            var jumpStrain = new List<double>();
            var flowStrain = new List<double>();
            var speedStrain = new List<double>();
            var staminaStrain = new List<double>();
            var times = new List<double>();

            double aimState = 0;
            double rawState = 0;
            double jumpState = 0;
            double flowState = 0;
            double speedState = 0;
            double staminaState = 0;
            double rhythmSum = 0;
            int circles = 0;
            int doubleRun = 0;
            bool offbeat = false;
            double previousCircleGap = 0;

            for (int i = 0; i < count; i++)
            {
                if (notes[i] is HitCircle)
                    circles++;
            }

            for (int i = 2; i < count; i++)
            {
                double read = reading(i, notes, baseFlow, distance, preempt[i], hidden, flashlight, rawDistance[i], radius[i]);
                double location = locationWeight(cursorEnd[i - 1], notes[i].StackedPosition);
                double q = distanceRequirement(i, distance, strain);
                double wide = double.IsNaN(angle[i]) ? 0 : (1 - Math.Cos(angle[i])) / 2;
                double jumpAngle = 1 + 0.2 * wide * (0.25 + 0.75 * q);
                double previousAngle = i >= 3 && !double.IsNaN(angle[i - 1]) ? angle[i - 1] : angle[i];
                double angleDelta = double.IsNaN(angle[i]) || double.IsNaN(previousAngle) ? 0 : Math.Abs(angle[i] - previousAngle) / Math.PI;
                double speedRatio = strain[i - 1] > 0 ? strain[i] / strain[i - 1] : 1;
                double jumpPattern = (1 + 0.15 * Math.Clamp(Math.Abs(Math.Log(Math.Max(1e-3, speedRatio))), 0, 0.5) + 0.1 * angleDelta) * (0.5 + 0.5 * q);
                double flowAngle = 1 + (Math.Cos(double.IsNaN(angle[i]) ? 0 : angle[i]) + 1) / 10.0;
                double flowPattern = 1 + 0.2 * EzPPPMath.T2T(distance[i] / normalised_radius, 1, 2) * flow[i - 1] + 0.1 * ((Math.Cos(double.IsNaN(angle[i]) ? 0 : angle[i]) + 1) / 2);
                double circleBonus = 1 + 120.0 / (radius[i] * radius[i]);

                double jump = jumpComponent(distance[i], strain[i], jumpAngle, jumpPattern, location, flow[i]);
                double aimFlow = flowComponent(distance[i], strain[i], flowAngle, flowPattern, location, flow[i]);
                double jumpRaw = jumpComponent(rawDistance[i], strain[i], jumpAngle, jumpPattern, location, flow[i]);
                double aimFlowRaw = flowComponent(rawDistance[i], strain[i], flowAngle, flowPattern, location, flow[i]);

                double decay = EzPPPMath.Decay(strain[i], 0.15);
                aimState = decay * aimState + 1059 * (jump + aimFlow) * circleBonus * read;
                rawState = decay * rawState + 1059 * (jumpRaw + aimFlowRaw) * read;
                jumpState = decay * jumpState + 1059 * jump * circleBonus * read;
                flowState = decay * flowState + 1059 * aimFlow * circleBonus * read;

                double half = delta2[i] / 2;
                double tapDenom = Math.Max(1, half - 20);
                double tap = 30 / (tapDenom * tapDenom) + 2 / half;
                double stream = 12.5 / (tapDenom * tapDenom) + 0.25 / half + 0.005;
                double speedAdd = (1 - flow[i]) * tap + flow[i] * stream;
                speedState = EzPPPMath.Decay(strain[i], 0.1) * speedState + 2600 * speedAdd;

                double tapStamina = 2 / tapDenom;
                double streamStamina = 1 / tapDenom;
                double staminaAdd = (1 - flow[i]) * tapStamina + flow[i] * streamStamina;
                staminaState = EzPPPMath.Decay(strain[i], 0.45) * staminaState + 780 * staminaAdd;

                times.Add(notes[i].StartTime);
                aimStrain.Add(aimState);
                rawStrain.Add(rawState);
                jumpStrain.Add(jumpState);
                flowStrain.Add(flowState);
                speedStrain.Add(speedState);
                staminaStrain.Add(staminaState);

                if (notes[i] is HitCircle)
                    rhythmSum += rhythmBonus(i, notes, gap, flow[i], ref previousCircleGap, ref doubleRun, ref offbeat);
            }

            double aimValue = EzPPPMath.Aggregate(times, aimStrain, section_length, gamma);
            double rawValue = EzPPPMath.Aggregate(times, rawStrain, section_length, gamma);
            double jumpValue = EzPPPMath.Aggregate(times, jumpStrain, section_length, gamma);
            double flowValue = EzPPPMath.Aggregate(times, flowStrain, section_length, gamma);
            double speedValue = EzPPPMath.Aggregate(times, speedStrain, section_length, gamma);
            double staminaValue = EzPPPMath.Aggregate(times, staminaStrain, section_length, gamma);

            double aimRating = rating_scale * Math.Sqrt(Math.Max(0, aimValue));
            double jumpRating = rating_scale * Math.Sqrt(Math.Max(0, jumpValue));
            double flowRating = rating_scale * Math.Sqrt(Math.Max(0, flowValue));
            double precision = rating_scale * Math.Sqrt(Math.Max(0, aimValue - rawValue));
            double speedRating = rating_scale * Math.Sqrt(Math.Max(0, speedValue));
            double staminaRating = rating_scale * Math.Sqrt(Math.Max(0, staminaValue));
            double accuracy = circles > 0 ? 1 + rhythmSum / circles * Math.Tanh(circles / 50.0) : 1;
            double star = 1.6 * Math.Pow(Math.Pow(aimRating, 3) + Math.Pow(Math.Max(speedRating, staminaRating), 3), 1.0 / 3.0);

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPPPSkillAxis.Aim.ToChartSkillId()] = EzPPPMath.Finite(aimRating),
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

        private static double distanceFactor(double scaledDistance, double bpm, double alpha)
        {
            double delta = (Math.Tanh((bpm - 140) / 20.0) + 2) * normalised_radius;
            return EzPPPMath.T2F(scaledDistance, delta * alpha, delta);
        }

        private static double jumpComponent(double scaledDistance, double strainMs, double angleWeight, double patternWeight, double location, double flowValue)
        {
            double basis = (scaledDistance / normalised_radius) / strainMs;
            return basis * angleWeight * patternWeight * location * (1 - flowValue);
        }

        private static double flowComponent(double scaledDistance, double strainMs, double angleWeight, double patternWeight, double location, double flowValue)
        {
            double basis = ((Math.Tanh(scaledDistance / normalised_radius - 2) + 1) * 2.5 + scaledDistance / (5 * normalised_radius)) / strainMs;
            return basis * angleWeight * patternWeight * (1 + (location - 1) / 2) * flowValue;
        }

        private static double locationWeight(Vector2 previousEnd, Vector2 current)
        {
            var mid = (previousEnd + current) * 0.5f;
            double x = (mid.X - field_width / 2) / (field_width / 2);
            double y = (mid.Y - field_height / 2) / (field_height / 2);
            const double rot = Math.PI / 4;
            double a = x * Math.Cos(rot) + y * Math.Sin(rot);
            double b = -x * Math.Sin(rot) + y * Math.Cos(rot);
            return 1 + a * a + b * b;
        }

        private static double distanceRequirement(int index, double[] distance, double[] strain)
        {
            double far = EzPPPMath.T2T(distance[index - 1], normalised_radius, normalised_radius);
            double time = EzPPPMath.CoarseEqual(strain[index], strain[index - 1]) ? 1 : 0.35;
            return Math.Clamp(far * time, 0, 1);
        }

        private static double reading(int index, List<OsuHitObject> notes, double[] baseFlow, double[] distance, double preempt, bool hidden, bool flashlight, double rawDistance, double radius)
        {
            double density = 0;
            double time = notes[index].StartTime;

            for (int k = index - 1; k >= 0; k--)
            {
                if (time - notes[k].StartTime > preempt)
                    break;

                density += (1 - 0.75 * baseFlow[k]) * (1 + 0.5 * baseFlow[k] * distance[k] / normalised_radius);
            }

            double bonus = Math.Pow(Math.Max(0, density), 1.5) / 100.0;
            double read = hidden ? 1.05 + 1.5 * bonus : 1 + bonus;
            read *= 1 + (-Math.Tanh((preempt - 325) / 30.0) + 1) / 15.0;

            if (flashlight)
            {
                double span = Math.Sqrt(field_width * field_width + field_height * field_height);
                read *= 1 + 0.3 * EzPPPMath.T2T(rawDistance, radius * 2, span * 0.35);
            }

            return read;
        }

        private static double rhythmBonus(int index, List<OsuHitObject> notes, double[] gap, double flowValue, ref double previousGap, ref int doubleRun, ref bool offbeat)
        {
            double currentGap = gap[index];
            double bonus = 0.05 * flowValue;

            if (previousGap > 0)
            {
                if (EzPPPMath.RatioEqual(2, currentGap, previousGap) || EzPPPMath.RatioEqual(0.5, currentGap, previousGap))
                    bonus += 0.12;
                else if (EzPPPMath.RatioEqual(3, currentGap, previousGap) || EzPPPMath.RatioEqual(1.0 / 3.0, currentGap, previousGap))
                    bonus += 0.20;
                else if (EzPPPMath.RatioEqual(6, currentGap, previousGap) || EzPPPMath.RatioEqual(1.0 / 6.0, currentGap, previousGap))
                    bonus += 0.16;
                else if (EzPPPMath.RatioEqual(1.5, currentGap, previousGap) || EzPPPMath.RatioEqual(2.0 / 3.0, currentGap, previousGap))
                {
                    bonus += 0.18;
                    offbeat = !offbeat;
                }
                else if (!EzPPPMath.RatioEqual(1, currentGap, previousGap))
                    bonus += offbeat ? 0.1 : 0.08;

                if (currentGap < 120 && previousGap < 120)
                {
                    doubleRun++;
                    double decay = 1;

                    for (int n = 1; n <= doubleRun; n++)
                        decay *= 1 - 0.5 * Math.Pow(0.9, n);

                    bonus *= Math.Max(0, decay);
                }
                else
                    doubleRun = 0;
            }

            previousGap = currentGap;
            return bonus;
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

        private static double angleOf(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 left = b - a;
            Vector2 right = c - b;
            float leftLen = left.Length;
            float rightLen = right.Length;

            if (leftLen < 0.001f || rightLen < 0.001f)
                return double.NaN;

            double dot = Math.Clamp(Vector2.Dot(left, right) / (leftLen * rightLen), -1, 1);
            return Math.Acos(dot);
        }
    }
}
