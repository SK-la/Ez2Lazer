// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Localization;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEzMania.Localization;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

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
        public BindableNumber<int> AlignDivisor { get; } = new BindableInt
        {
            MinValue = 0,
            MaxValue = 16,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_DelayLevel_Label), nameof(EzManiaModStrings.PatternShift_DelayLevel_Description))]
        public BindableNumber<int> DelayLevel { get; } = new BindableInt
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_Regenerate_Label), nameof(EzManiaModStrings.PatternShift_Regenerate_Description))]
        public BindableBool Regenerate { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.PatternShift_RegenerateDifficulty_Label), nameof(EzManiaModStrings.PatternShift_RegenerateDifficulty_Description))]
        public BindableNumber<int> RegenerateDifficulty { get; } = new BindableInt(5)
        {
            MinValue = 2,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.Seed_Label), nameof(EzModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.ApplyOrder_Label), nameof(EzModStrings.ApplyOrder_Description))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt
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
                yield return ("Regenerate", Regenerate.Value ? "On" : "Off");
                yield return ("Regenerate Difficulty", $"{RegenerateDifficulty.Value}");
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
            ApplyToBeatmapInternal((ManiaBeatmap)beatmap, null);
        }

        public void ApplyToWorkingBeatmap(WorkingBeatmap workingBeatmap)
        {
            ArgumentNullException.ThrowIfNull(workingBeatmap);

            double trackLength = 0;

            try
            {
                if (workingBeatmap.TrackLoaded)
                    trackLength = workingBeatmap.Track.Length;
            }
            catch
            {
                trackLength = 0;
            }

            ApplyToBeatmapInternal((ManiaBeatmap)workingBeatmap.Beatmap, workingBeatmap.Waveform, trackLength);
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
                int maxShift = ManiaKeyPatternHelp.GetDelayMaxShiftCount(delayLevel, noteCount);

                if (maxShift <= 0)
                    continue;

                double beatLength = controlPoints.TimingPointAt(chord.Time).BeatLength;
                double offsetAmount = beatLength * ManiaKeyPatternHelp.GetDelayBeatFraction(delayLevel);

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

        private static void assignColumns(List<PatternShiftChord> chords, int keyCount, Random rng)
        {
            double[] lastColumnTime = new double[keyCount];
            var placedNotes = new List<PatternShiftNote>();

            for (int i = 0; i < keyCount; i++)
                lastColumnTime[i] = -1000;

            int lastNote = 0;

            foreach (var chord in chords)
            {
                chord.Notes = chord.Notes.OrderBy(n => n.SourceColumn).ToList();
                var usedColumns = new HashSet<int>();
                var assigned = new List<PatternShiftNote>();

                foreach (var note in chord.Notes)
                {
                    int column = chooseColumn(keyCount, lastColumnTime, lastNote, rng, note.StartTime);
                    if (column < 0)
                        continue;

                    if (usedColumns.Contains(column))
                        continue;

                    if (hasAssignedNoteAtTime(placedNotes, column, note.StartTime))
                        continue;

                    note.AssignedColumn = column;
                    lastNote = column;
                    lastColumnTime[column] = note.IsHold ? note.EndTime : note.StartTime;
                    usedColumns.Add(column);
                    assigned.Add(note);
                    placedNotes.Add(note);
                }

                chord.Notes = assigned;
            }
        }

        private static int chooseColumn(int keys, double[] lastUsedTime, int lastNote, Random rng, double currentTime)
        {
            var candidates = new List<int>();

            for (int i = 0; i < keys; i++)
            {
                if (lastUsedTime[i] <= currentTime)
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
                return -1;

            double minTime = double.MaxValue;
            var minIndexList = new List<int>();

            for (int i = 0; i < candidates.Count; i++)
            {
                int index = candidates[i];

                if (lastUsedTime[index] < minTime)
                {
                    minIndexList.Clear();
                    minTime = lastUsedTime[index];
                    minIndexList.Add(index);
                }
                else if (lastUsedTime[index] <= minTime + 24 && lastUsedTime[index] >= minTime - 24) minIndexList.Add(index);
            }

            int noteLeft = minIndexList.Count(i => i < keys / 2);
            int noteRight = minIndexList.Count(i => i >= (keys + 1) / 2);

            if (noteRight > 0 && noteLeft > 0)
            {
                bool lastOnLeft = lastNote < keys / 2;
                minIndexList = minIndexList.Where(i => lastOnLeft ? i >= (keys + 1) / 2 : i < keys / 2).ToList();
            }

            return minIndexList.Count > 0 ? minIndexList[rng.Next(minIndexList.Count)] : -1;
        }

        private static bool hasAssignedNoteAtTime(List<PatternShiftNote> notes, int column, double time, double tolerance = 0.5)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                if (note.AssignedColumn != column)
                    continue;

                if (!note.IsHold && Math.Abs(note.StartTime - time) <= tolerance)
                    return true;
            }

            return false;
        }

        private List<PatternShiftNote> modifyNotesByDifficulty(List<PatternShiftNote> originalNotes, ManiaBeatmap beatmap, int targetColumns, int stars, Random rng)
        {
            // Default behavior: stars==5 => minimal change. stars<5 remove notes; stars>5 add notes.
            var notes = new List<PatternShiftNote>(originalNotes);

            const int center = 5;
            int delta = stars - center;

            int oscSeed = Seed.Value ?? RNG.Next();
            var osc = new EzOscillator(oscSeed);

            if (notes.Count == 0)
            {
                if (delta <= 0)
                    return notes;

                // If there are no notes but user asked to increase difficulty, generate a few seed notes
                var tp0 = beatmap.ControlPointInfo.TimingPoints.FirstOrDefault();
                double beatLen0 = tp0?.BeatLength ?? 500;
                int initialAdd = Math.Min(8, Math.Max(1, (int)Math.Round(3 * (delta / (double)center))));
                var seedList = new List<PatternShiftNote>();

                for (int i = 0; i < initialAdd; i++)
                {
                    double t = i * beatLen0 * (1.0 + 0.25 * osc.Next());
                    int col = rng.Next(0, Math.Max(1, targetColumns));
                    seedList.Add(new PatternShiftNote(Math.Max(0, t), Math.Max(0, t), Array.Empty<HitSampleInfo>(), col, false));
                }

                return seedList.OrderBy(n => n.StartTime).ThenBy(n => n.SourceColumn).ToList();
            }

            if (delta < 0)
            {
                double removalRatio = Math.Min(0.95, -delta / (double)(center - 1));
                int targetRemove = (int)Math.Round(notes.Count * removalRatio);

                var remaining = new List<PatternShiftNote>();
                int removed = 0;

                for (int i = 0; i < notes.Count; i++)
                {
                    double oscVal = osc.Next(); // 0..1
                    double p = removalRatio * (0.6 + 0.8 * (1.0 - oscVal));

                    if (removed < targetRemove && rng.NextDouble() < p)
                    {
                        removed++;
                        continue;
                    }

                    remaining.Add(notes[i]);
                }

                // if still need to remove, remove from tail
                while (removed < targetRemove && remaining.Count > 0)
                {
                    remaining.RemoveAt(remaining.Count - 1);
                    removed++;
                }

                return remaining.OrderBy(n => n.StartTime).ThenBy(n => n.SourceColumn).ToList();
            }

            if (delta > 0)
            {
                double insertRatio = Math.Min(2.0, delta / (double)center); // allow up to doubling local density
                int targetAdd = (int)Math.Round(notes.Count * insertRatio * 0.25); // conservative additions

                var result = new List<PatternShiftNote>(notes);

                // First: conservative local additions around existing notes (as before)
                for (int i = 0; i < targetAdd; i++)
                {
                    int idx = rng.Next(0, notes.Count);
                    var anchor = notes[idx];
                    var tp = beatmap.ControlPointInfo.TimingPointAt(anchor.StartTime);
                    double beatLength = tp.BeatLength;

                    int[] allowedSubdiv = new[] { 2, 4, 8, 16 };
                    int subdiv = allowedSubdiv[rng.Next(allowedSubdiv.Length)];

                    double offset = (rng.NextDouble() - 0.5) * (beatLength / subdiv) * (1.0 + 0.5 * (1.0 - osc.Next()));
                    double newTime = Math.Max(0, anchor.StartTime + offset);

                    int col = rng.Next(0, Math.Max(1, targetColumns));

                    // avoid duplicate at same time+col within tolerance
                    bool exists = result.Any(n => n.SourceColumn == col && Math.Abs(n.StartTime - newTime) <= 48);

                    if (exists)
                    {
                        // try different column
                        for (int t = 0; t < 6 && exists; t++)
                        {
                            col = (col + 1) % targetColumns;
                            exists = result.Any(n => n.SourceColumn == col && Math.Abs(n.StartTime - newTime) <= 48);
                        }

                        if (exists) continue;
                    }

                    result.Add(new PatternShiftNote(newTime, newTime, Array.Empty<HitSampleInfo>(), col, false));
                }

                // Second: fill larger gaps between existing notes to create notes in previously empty regions
                int remainingGapAdds = targetAdd; // allow similar amount for gaps

                for (int i = 0; i < notes.Count - 1 && remainingGapAdds > 0; i++)
                {
                    double left = notes[i].StartTime;
                    double right = notes[i + 1].StartTime;
                    double gap = right - left;

                    var tpLeft = beatmap.ControlPointInfo.TimingPointAt(left);
                    double localBeat = tpLeft.BeatLength;

                    // consider gaps larger than ~1.5 beats
                    if (gap > Math.Max(300, localBeat * 1.5))
                    {
                        // number of potential inserts proportional to gap size and difficulty
                        int inserts = Math.Min(2, Math.Max(1, (int)Math.Floor(gap / (localBeat * 2.0))));

                        for (int j = 0; j < inserts && remainingGapAdds > 0; j++)
                        {
                            double t = left + (osc.Next() * 0.75 + 0.125) * gap; // biased away from edges

                            // snap to a subdivision of local beat
                            int[] allowedSubdiv = new[] { 2, 4, 8 };
                            int subdiv = allowedSubdiv[rng.Next(allowedSubdiv.Length)];
                            double step = localBeat / subdiv;
                            double snapped = Math.Round(t / step) * step;

                            int col = rng.Next(0, Math.Max(1, targetColumns));
                            bool exists = result.Any(n => n.SourceColumn == col && Math.Abs(n.StartTime - snapped) <= 48);

                            if (exists)
                            {
                                // try other columns quickly
                                for (int c = 0; c < Math.Min(6, targetColumns) && exists; c++)
                                {
                                    col = (col + 1) % targetColumns;
                                    exists = result.Any(n => n.SourceColumn == col && Math.Abs(n.StartTime - snapped) <= 48);
                                }

                                if (exists) continue;
                            }

                            result.Add(new PatternShiftNote(Math.Max(0, snapped), Math.Max(0, snapped), Array.Empty<HitSampleInfo>(), col, false));
                            remainingGapAdds--;
                        }
                    }
                }

                return result.OrderBy(n => n.StartTime).ThenBy(n => n.SourceColumn).ToList();
            }

            return notes;
        }

        // In-place Cooley-Tukey FFT (radix-2)
        private static void FFT(Complex[] buffer, bool inverse)
        {
            int n = buffer.Length;
            int bits = (int)Math.Log(n, 2);

            // bit-reverse permutation
            for (int i = 0; i < n; i++)
            {
                int j = 0;
                int x = i;

                for (int k = 0; k < bits; k++)
                {
                    j = (j << 1) | (x & 1);
                    x >>= 1;
                }

                if (j > i)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = 2 * Math.PI / len * (inverse ? 1 : -1);
                var wlen = new Complex(Math.Cos(angle), Math.Sin(angle));

                for (int i = 0; i < n; i += len)
                {
                    var w = Complex.One;

                    for (int j = 0; j < len / 2; j++)
                    {
                        var u = buffer[i + j];
                        var v = buffer[i + j + len / 2] * w;
                        buffer[i + j] = u + v;
                        buffer[i + j + len / 2] = u - v;
                        w *= wlen;
                    }
                }
            }

            if (inverse)
            {
                for (int i = 0; i < n; i++)
                    buffer[i] /= n;
            }
        }

        private void ApplyToBeatmapInternal(ManiaBeatmap maniaBeatmap, Waveform? waveform, double? trackLength = null)
        {
            Seed.Value ??= RNG.Next();
            var rng = new Random(Seed.Value.Value);

            int targetColumns = Math.Clamp(KeyCount.Value, 2, ManiaRuleset.MAX_STAGE_KEYS);
            int maxChord = Math.Clamp(MaxChord.Value, 1, targetColumns);
            int difficulty = Math.Clamp(Density.Value, 1, 10);

            if (maniaBeatmap.HitObjects.Count == 0 && !Regenerate.Value)
                return;

            maniaBeatmap.Stages.Clear();
            maniaBeatmap.Stages.Add(new StageDefinition(targetColumns));
            maniaBeatmap.Difficulty.CircleSize = targetColumns;

            double snap(double time)
            {
                if (AlignDivisor.Value <= 0)
                    return time;

                return maniaBeatmap.ControlPointInfo.GetClosestSnappedTime(time, AlignDivisor.Value);
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

            if (Regenerate.Value)
                notes = modifyNotesByDifficulty(notes, maniaBeatmap, targetColumns, RegenerateDifficulty.Value, rng);

            var chords = buildChords(notes);
            applyDelay(chords, maniaBeatmap.ControlPointInfo, DelayLevel.Value, rng);
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
            ManiaNoteCleanupTool.EnforceHoldReleaseGap(maniaBeatmap);
        }

        private class PatternShiftNote
        {
            public IList<HitSampleInfo> Samples { get; }
            public int SourceColumn { get; }
            public bool IsHold { get; }
            public double StartTime { get; set; }
            public double EndTime { get; set; }
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
