// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.Analysis
{
    public static class HalfHoldBeatmapSets
    {
        public static IBeatmap[] CreateTen(int columns = 4, int notesPerColumn = 32)
        {
            // use fixed seeds to produce deterministic but different beatmaps
            return Enumerable.Range(0, 10).Select(i => HalfHoldBeatmapFactory.Create(columns, notesPerColumn, 1000 + i)).ToArray();
        }
    }

    public static class HalfHoldBeatmapFactory
    {
        /// <summary>
        /// Create a Mania beatmap with the specified number of columns and notes per column.
        /// Approximately half of the hit objects will be long notes (HoldNote) and half simple notes.
        /// </summary>
        public static IBeatmap Create(int columns = 4, int notesPerColumn = 32, int seed = 1234)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(columns))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty
                    {
                        DrainRate = 6,
                        OverallDifficulty = 6,
                        ApproachRate = 6,
                        CircleSize = columns
                    }
                },
                ControlPointInfo = new ControlPointInfo()
            };

            var rnd = new Random(seed);

            for (int col = 0; col < columns; col++)
            {
                for (int i = 0; i < notesPerColumn; i++)
                {
                    // spread notes in time with a base spacing and random jitter
                    double baseTime = col * 2000 + i * 300;
                    double jitter = rnd.NextDouble() * 200 - 100; // ±100ms
                    double startTime = baseTime + jitter;

                    // randomly decide whether this object is a hold note, target ~50%
                    bool isHold = (i % 2 == 0);

                    if (isHold)
                    {
                        var hold = new HoldNote
                        {
                            StartTime = startTime,
                            Column = col,
                            Duration = 200 + rnd.Next(0, 800) // 200-1000ms
                        };
                        beatmap.HitObjects.Add(hold);
                    }
                    else
                    {
                        var note = new Note
                        {
                            StartTime = startTime,
                            Column = col
                        };
                        beatmap.HitObjects.Add(note);
                    }
                }
            }

            return beatmap;
        }
    }
}
