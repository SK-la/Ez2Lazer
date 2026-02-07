// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShift : Mod, IApplicableAfterBeatmapConversion, IApplicableToBeatmapConverter, IHasSeed, IHasApplyOrder
    {
        public override string Name => "Pattern Shift";

        public override string Acronym => "PS";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => EzManiaModStrings.PatternShift_Description;

        public override IconUsage? Icon => FontAwesome.Solid.Magic;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_KeyCount_Label), nameof(EzManiaModStrings.PatternShift_KeyCount_Description))]
        public BindableNumber<int> KeyCount { get; } = new BindableInt(8)
        {
            MinValue = 2,
            MaxValue = ManiaRuleset.MAX_STAGE_KEYS,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_Density_Label), nameof(EzManiaModStrings.PatternShift_Density_Description))]
        public BindableNumber<int> Density { get; } = new BindableInt(7)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_MaxChord_Label), nameof(EzManiaModStrings.PatternShift_MaxChord_Description))]
        public BindableNumber<int> MaxChord { get; } = new BindableInt(5)
        {
            MinValue = 1,
            MaxValue = ManiaRuleset.MAX_STAGE_KEYS,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_AlignDivisor_Label), nameof(EzManiaModStrings.PatternShift_AlignDivisor_Description))]
        public BindableNumber<int> AlignDivisor { get; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 16,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_DelayLevel_Label), nameof(EzManiaModStrings.PatternShift_DelayLevel_Description))]
        public BindableNumber<int> DelayLevel { get; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Seed_Label), nameof(EzModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ApplyOrder_Label), nameof(EzManiaModStrings.ApplyOrder_Description))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Key Count", $"{KeyCount.Value}");
                yield return ("Density", $"{Density.Value}");
                yield return ("Max Chord", $"{MaxChord.Value}");
                yield return ("Align Divisor", AlignDivisor.Value == 0 ? "Off" : $"1/{AlignDivisor.Value}");
                yield return ("Delay Level", $"{DelayLevel.Value}");
                yield return ("Seed", Seed.Value?.ToString() ?? "Random");
            }
        }

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            if (converter is ManiaBeatmapConverter maniaConverter)
                maniaConverter.TargetColumns = Math.Clamp(KeyCount.Value, 2, ManiaRuleset.MAX_STAGE_KEYS);
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Seed.Value ??= RNG.Next();
            var rng = new Random(Seed.Value.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int targetColumns = Math.Clamp(KeyCount.Value, 2, ManiaRuleset.MAX_STAGE_KEYS);
            int maxChord = Math.Clamp(MaxChord.Value, 1, targetColumns);
            int difficulty = Math.Clamp(Density.Value, 1, 10);

            if (maniaBeatmap.HitObjects.Count == 0)
                return;

            maniaBeatmap.Stages.Clear();
            maniaBeatmap.Stages.Add(new StageDefinition(targetColumns));
            maniaBeatmap.Difficulty.CircleSize = targetColumns;

            double snap(double time)
            {
                if (AlignDivisor.Value <= 0)
                    return time;

                return beatmap.ControlPointInfo.GetClosestSnappedTime(time, AlignDivisor.Value);
            }

            var notes = maniaBeatmap.HitObjects.Select(h =>
            {
                if (h is HoldNote hold)
                {
                    double start = snap(hold.StartTime);
                    double end = snap(hold.EndTime);

                    if (end < start)
                        end = start;

                    return new PatternShiftNote(start, end, hold.GetNodeSamples(0), hold.Column, end > start);
                }

                double time = snap(h.StartTime);
                return new PatternShiftNote(time, time, h.Samples, h.Column, false);
            }).OrderBy(n => n.StartTime).ThenBy(n => n.SourceColumn).ToList();

            var chords = buildChords(notes);
            applyDelay(chords, beatmap.ControlPointInfo, DelayLevel.Value, rng);
            reduceAllChords(chords, maxChord, difficulty);
            assignColumns(chords, targetColumns, rng);

            var newObjects = new List<ManiaHitObject>(notes.Count);

            foreach (var chord in chords)
            {
                foreach (var note in chord.Notes)
                {
                    if (note.IsHold && note.EndTime > note.StartTime)
                    {
                        newObjects.Add(new HoldNote
                        {
                            Column = note.AssignedColumn,
                            StartTime = note.StartTime,
                            Duration = note.EndTime - note.StartTime,
                            NodeSamples = new List<IList<HitSampleInfo>> { note.Samples, Array.Empty<HitSampleInfo>() }
                        });
                    }
                    else
                    {
                        newObjects.Add(new Note
                        {
                            Column = note.AssignedColumn,
                            StartTime = note.StartTime,
                            Samples = note.Samples
                        });
                    }
                }
            }

            maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ThenBy(h => h.Column).ToList();
        }

        private static List<PatternShiftChord> buildChords(List<PatternShiftNote> notes)
        {
            var chords = new List<PatternShiftChord>();
            PatternShiftChord? current = null;

            foreach (var note in notes)
            {
                if (current == null || note.StartTime != current.Time)
                {
                    current = new PatternShiftChord(note.StartTime);
                    chords.Add(current);
                }

                current.Notes.Add(note);
            }

            return chords;
        }

        private static void reduceAllChords(List<PatternShiftChord> chordList, int maxChord, int difficulty)
        {
            int[] chordTimeLimits = { 200, 100, 50, 25, 12 };
            int[] chordNeighborLimits = { 1, 1, 1, 1, 1 };

            for (int i = 0; i < chordTimeLimits.Length; i++)
                chordTimeLimits[i] = chordTimeLimits[i] * 100 / difficulty / difficulty;

            foreach (var chord in chordList)
                reduceChordSize(chord, maxChord);

            for (int i = 1; i < chordList.Count - 1; i++)
            {
                for (int j = 0; j < chordTimeLimits.Length; j++)
                {
                    double spacing = chordList[i + 1].Time - chordList[i - 1].Time;
                    int neighborSize = chordList[i - 1].Notes.Count + chordList[i + 1].Notes.Count;

                    if (spacing < chordTimeLimits[j] && neighborSize > chordNeighborLimits[j])
                        reduceChordSize(chordList[i], Math.Min(maxChord, 5 - j));
                }
            }
        }

        private static void reduceChordSize(PatternShiftChord chord, int newSize)
        {
            if (chord.Notes.Count <= newSize)
                return;

            chord.Notes = chord.Notes.OrderBy(n => n.SourceColumn).ToList();

            while (chord.Notes.Count > newSize)
                chord.Notes.RemoveAt(0);
        }

        private static void applyDelay(List<PatternShiftChord> chords, ControlPointInfo controlPoints, int delayLevel, Random rng)
        {
            if (delayLevel <= 0)
                return;

            foreach (var chord in chords)
            {
                int noteCount = chord.Notes.Count;
                int maxShift = getMaxShiftCount(delayLevel, noteCount);

                if (maxShift <= 0)
                    continue;

                double beatLength = controlPoints.TimingPointAt(chord.Time).BeatLength;
                double offsetAmount = beatLength * getDelayBeatFraction(delayLevel);

                var indexes = Enumerable.Range(0, noteCount).OrderBy(_ => rng.Next()).Take(maxShift).ToList();

                foreach (int index in indexes)
                {
                    var note = chord.Notes[index];
                    double direction = rng.NextDouble() < 0.5 ? -1 : 1;
                    double offset = direction * offsetAmount;

                    note.StartTime = Math.Max(0, note.StartTime + offset);
                    note.EndTime = Math.Max(note.StartTime, note.EndTime + offset);
                }
            }
        }

        private static int getMaxShiftCount(int level, int noteCount)
        {
            if (noteCount <= 0)
                return 0;

            if (level <= 3)
                return Math.Max(0, Math.Min(level, noteCount - level));

            if (level <= 6)
                return Math.Max(0, Math.Min(level, noteCount - 1));

            return Math.Min(level, noteCount);
        }

        private static double getDelayBeatFraction(int level)
        {
            double t;

            if (level <= 3)
                t = (level - 1) / 2.0;
            else if (level <= 6)
                t = (level - 4) / 2.0;
            else
                t = (level - 7) / 3.0;

            return (1.0 / 16.0) * (1 + t);
        }

        private static void assignColumns(List<PatternShiftChord> chords, int keyCount, Random rng)
        {
            double[] lastColumnTime = new double[keyCount];

            for (int i = 0; i < keyCount; i++)
                lastColumnTime[i] = -1000;

            int lastNote = 0;

            foreach (var chord in chords)
            {
                chord.Notes = chord.Notes.OrderBy(n => n.SourceColumn).ToList();

                foreach (var note in chord.Notes)
                {
                    int column = chooseColumn(keyCount, lastColumnTime, lastNote, rng);

                    note.AssignedColumn = column;
                    lastNote = column;
                    lastColumnTime[column] = chord.Time;
                }
            }
        }

        private static int chooseColumn(int keys, double[] lastUsedTime, int lastNote, Random rng)
        {
            double minTime = lastUsedTime[0] + 1;
            var minIndexList = new List<int>();

            for (int i = 0; i < keys; i++)
            {
                if (lastUsedTime[i] < minTime)
                {
                    minIndexList.Clear();
                    minTime = lastUsedTime[i];
                    minIndexList.Add(i);
                }
                else if (lastUsedTime[i] <= minTime + 24 && lastUsedTime[i] >= minTime - 24)
                {
                    minIndexList.Add(i);
                }
            }

            int noteLeft = minIndexList.Count(i => i < (keys / 2));
            int noteRight = minIndexList.Count(i => i >= ((keys + 1) / 2));

            if (noteRight > 0 && noteLeft > 0)
            {
                bool lastOnLeft = lastNote < (keys / 2);
                minIndexList = minIndexList.Where(i => lastOnLeft ? i >= ((keys + 1) / 2) : i < (keys / 2)).ToList();
            }

            return minIndexList[rng.Next(minIndexList.Count)];
        }

        private class PatternShiftNote
        {
            public double StartTime { get; set; }
            public double EndTime { get; set; }
            public IList<HitSampleInfo> Samples { get; }
            public int SourceColumn { get; }
            public bool IsHold { get; }
            public int AssignedColumn { get; set; }

            public PatternShiftNote(double startTime, double endTime, IList<HitSampleInfo> samples, int sourceColumn, bool isHold)
            {
                StartTime = startTime;
                EndTime = endTime;
                Samples = samples;
                SourceColumn = sourceColumn;
                IsHold = isHold;
            }
        }

        private class PatternShiftChord
        {
            public double Time { get; }
            public List<PatternShiftNote> Notes { get; set; } = new List<PatternShiftNote>();

            public PatternShiftChord(double time)
            {
                Time = time;
            }
        }
    }
}
