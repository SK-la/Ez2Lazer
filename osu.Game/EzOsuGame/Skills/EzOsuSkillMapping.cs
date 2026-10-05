// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Maps official <see cref="DifficultyAttributes"/> / <see cref="PerformanceAttributes"/>
    /// into Ez skill-id dictionaries without referencing ruleset assemblies.
    /// </summary>
    public static class EzOsuSkillMapping
    {
        // Mirrors protected DifficultyAttributes attrib ids (osu chart export).
        private const int attrib_aim = 1;
        private const int attrib_speed = 3;
        private const int attrib_flashlight = 17;
        private const int attrib_reading = 47;

        public static IReadOnlyDictionary<string, double> FromDifficultyAttributes(DifficultyAttributes attributes)
        {
            ArgumentNullException.ThrowIfNull(attributes);

            double aim = 0, speed = 0, flashlight = 0, reading = 0;

            foreach (var (id, raw) in attributes.ToDatabaseAttributes())
            {
                if (!tryToDouble(raw, out double value))
                    continue;

                switch (id)
                {
                    case attrib_aim:
                        aim = value;
                        break;

                    case attrib_speed:
                        speed = value;
                        break;

                    case attrib_flashlight:
                        flashlight = value;
                        break;

                    case attrib_reading:
                        reading = value;
                        break;
                }
            }

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzOsuSkillAxis.Aim.ToDiffSkillId()] = aim,
                [EzOsuSkillAxis.Speed.ToDiffSkillId()] = speed,
                [EzOsuSkillAxis.Flashlight.ToDiffSkillId()] = flashlight,
                [EzOsuSkillAxis.Reading.ToDiffSkillId()] = reading,
            };
        }

        public static IReadOnlyDictionary<string, double> FromPerformanceAttributes(PerformanceAttributes attributes)
        {
            ArgumentNullException.ThrowIfNull(attributes);

            double aim = 0, speed = 0, flashlight = 0, reading = 0, accuracy = 0;

            foreach (var display in attributes.GetAttributesForDisplay())
            {
                switch (display.PropertyName)
                {
                    case "Aim":
                        aim = display.Value;
                        break;

                    case "Speed":
                        speed = display.Value;
                        break;

                    case "Flashlight":
                        flashlight = display.Value;
                        break;

                    case "Reading":
                        reading = display.Value;
                        break;

                    case "Accuracy":
                        accuracy = display.Value;
                        break;
                }
            }

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzOsuSkillAxis.Aim.ToPerfSkillId()] = aim,
                [EzOsuSkillAxis.Speed.ToPerfSkillId()] = speed,
                [EzOsuSkillAxis.Flashlight.ToPerfSkillId()] = flashlight,
                [EzOsuSkillAxis.Reading.ToPerfSkillId()] = reading,
                [EzOsuSkillAxis.Accuracy.ToPerfSkillId()] = accuracy,
            };
        }

        public static bool IsCurrentDiffCache(IReadOnlyDictionary<string, double>? skills)
        {
            if (skills == null || skills.Count == 0)
                return false;

            foreach (var axis in EzOsuSkillAxisExtensions.ChartAxes)
            {
                if (!skills.ContainsKey(axis.ToDiffSkillId()))
                    return false;
            }

            return true;
        }

        private static bool tryToDouble(object? raw, out double value)
        {
            switch (raw)
            {
                case double d:
                    value = d;
                    return true;

                case float f:
                    value = f;
                    return true;

                case int i:
                    value = i;
                    return true;

                case long l:
                    value = l;
                    return true;

                case IConvertible c:
                    value = c.ToDouble(null);
                    return true;

                default:
                    value = 0;
                    return false;
            }
        }
    }
}
