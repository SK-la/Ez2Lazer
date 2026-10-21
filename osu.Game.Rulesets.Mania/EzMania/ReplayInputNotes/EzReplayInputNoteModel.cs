// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayInputNotes
{
    /// <summary>
    /// Builds the per-column replay input notes drawn over the playfield, in one of two modes:
    /// every press-to-release span, or one note per judged object only.
    /// </summary>
    /// <remarks>
    /// Press edges come from the replay frames; judgement results come from a replay session so that the overlay never
    /// re-implements judgement. The result source is optional: without it the overlay still draws, just uncoloured.
    /// </remarks>
    public sealed class EzReplayInputNoteModel
    {
        /// <summary>Judgement times are reconstructed from stored offsets, so an exact comparison needs a small tolerance.</summary>
        private const double time_tolerance = 0.5;

        private readonly Dictionary<int, List<double>> pressTimes = new Dictionary<int, List<double>>();
        private readonly Dictionary<int, List<double>> releaseTimes = new Dictionary<int, List<double>>();

        private readonly Dictionary<int, List<JudgedEntry>> judgedNotes = new Dictionary<int, List<JudgedEntry>>();
        private readonly Dictionary<int, List<JudgedEntry>> judgedHeads = new Dictionary<int, List<JudgedEntry>>();

        private readonly Dictionary<(int Column, double Time), HoldNote> holdByHead = new Dictionary<(int, double), HoldNote>();
        private readonly Dictionary<(int Column, double Time), HoldNote> holdByTail = new Dictionary<(int, double), HoldNote>();
        private readonly Dictionary<(int Column, double Time), HoldNote> holdByBody = new Dictionary<(int, double), HoldNote>();

        private readonly Dictionary<HoldNote, JudgedEntry> tailByHold = new Dictionary<HoldNote, JudgedEntry>();
        private readonly Dictionary<HoldNote, double> brokenByHold = new Dictionary<HoldNote, double>();

        private readonly Dictionary<int, List<EzReplayInputNote>> buildBuffer = new Dictionary<int, List<EzReplayInputNote>>();
        private readonly Dictionary<int, EzReplayInputNote[]> notesByColumn = new Dictionary<int, EzReplayInputNote[]>();

        private double fallbackEndTime;
        private bool loaded;
        private bool resultsApplied;

        /// <summary>Whether the overlay is drawn at all; the master switch of the replay sidebar section.</summary>
        public BindableBool ShowOverlay { get; } = new BindableBool(true);

        /// <summary>Whether the overlay colours notes by the judgement of the press / hold sections.</summary>
        public BindableBool ColourByHitResult { get; } = new BindableBool();

        /// <summary>Whether only judged objects produce a note, instead of every press-and-release span.</summary>
        public BindableBool OnlyJudgedNotes { get; } = new BindableBool();

        /// <summary>Incremented whenever the per-column notes are rebuilt, so drawables can detect a rebuild.</summary>
        public int Version { get; private set; }

        /// <summary>Whether a replay was loaded and the overlay has anything to show.</summary>
        public bool Enabled => loaded;

        /// <summary>Whether judgement results have been applied; <see cref="OnlyJudgedNotes"/> has no notes before this.</summary>
        public bool ResultsApplied => resultsApplied;

        /// <summary>
        /// Whether the overlay should draw with the current mode. The judged-notes mode needs results, the span mode does not.
        /// </summary>
        public bool CanDraw => loaded && ShowOverlay.Value && (!OnlyJudgedNotes.Value || resultsApplied);

        public EzReplayInputNoteModel()
        {
            ShowOverlay.BindValueChanged(_ => Rebuild(), true);
            OnlyJudgedNotes.BindValueChanged(_ => Rebuild(), true);
        }

        public IReadOnlyList<EzReplayInputNote> NotesFor(int column)
            => notesByColumn.TryGetValue(column, out var notes) ? notes : Array.Empty<EzReplayInputNote>();

        /// <summary>
        /// Parses the replay edges and indexes the beatmap's hold notes. Safe to call once per replay.
        /// </summary>
        /// <remarks>
        /// Frames recorded as <see cref="LegacyReplayFrame"/> are converted on the fly so an unconverted replay still
        /// produces edges instead of silently drawing nothing.
        /// </remarks>
        public void Load(Replay? replay, IBeatmap beatmap)
        {
            indexHoldNotes(beatmap);

            if (replay != null && replay.Frames.Count > 0)
            {
                var inputData = ManiaReplayFrameEdgeParser.ParseAll(normaliseFrames(replay, beatmap));

                if (inputData.SortedFrameTimes.Count > 0)
                    fallbackEndTime = inputData.SortedFrameTimes[^1];

                foreach (var input in inputData.SortedEvents)
                {
                    if (input.Column < 0)
                        continue;

                    var table = input.IsPress ? pressTimes : releaseTimes;

                    if (!table.TryGetValue(input.Column, out var times))
                        table[input.Column] = times = new List<double>();

                    times.Add(input.Time);
                }
            }

            loaded = true;
            Rebuild();
        }

        private static Replay normaliseFrames(Replay replay, IBeatmap beatmap)
        {
            if (ManiaReplayFrameEdgeParser.IsManiaReplay(replay))
                return replay;

            var frames = new List<ReplayFrame>(replay.Frames.Count);

            foreach (var frame in replay.Frames)
            {
                switch (frame)
                {
                    case ManiaReplayFrame mania:
                        frames.Add(mania);
                        break;

                    case LegacyReplayFrame legacy:
                        var converted = new ManiaReplayFrame { Time = legacy.Time };
                        converted.FromLegacy(legacy, beatmap);
                        frames.Add(converted);
                        break;
                }
            }

            return new Replay { Frames = frames };
        }

        /// <summary>
        /// Applies judgement results and rebuilds. Replaces any previously applied results.
        /// </summary>
        public void ApplyResults(IReadOnlyList<HitEvent> events)
        {
            tailByHold.Clear();
            brokenByHold.Clear();
            judgedNotes.Clear();
            judgedHeads.Clear();

            foreach (var hitEvent in events)
            {
                var hitObject = hitEvent.HitObject;

                if (hitObject == null)
                    continue;

                double time = hitObject.GetEndTime() + hitEvent.TimeOffset;

                switch (hitObject)
                {
                    case HeadNote head:
                        // IgnoreMiss means the head was never pressed; IgnoreHit still marks a real press (stable classic).
                        if (hitEvent.Result == HitResult.None || hitEvent.Result == HitResult.IgnoreMiss)
                            break;

                        addEntry(judgedHeads, head.Column, new JudgedEntry(time, hitEvent.Result, head));
                        break;

                    case TailNote tail:
                        if (holdByTail.TryGetValue((tail.Column, tail.StartTime), out var tailHold))
                            tailByHold[tailHold] = new JudgedEntry(time, hitEvent.Result, tail);

                        break;

                    case HoldNoteBody body:
                        if (hitEvent.Result == HitResult.ComboBreak && holdByBody.TryGetValue((body.Column, body.StartTime), out var bodyHold))
                            recordBreak(bodyHold, time);

                        break;

                    case Note note:
                        if (!hitEvent.Result.IsHit())
                            break;

                        addEntry(judgedNotes, note.Column, new JudgedEntry(time, hitEvent.Result, note));
                        break;
                }
            }

            foreach (var entries in judgedNotes.Values)
                entries.Sort(static (a, b) => a.Time.CompareTo(b.Time));

            foreach (var entries in judgedHeads.Values)
                entries.Sort(static (a, b) => a.Time.CompareTo(b.Time));

            resultsApplied = true;
            Rebuild();
        }

        /// <summary>
        /// Rebuilds the per-column notes from the loaded edges, results and the current mode.
        /// </summary>
        public void Rebuild()
        {
            notesByColumn.Clear();
            buildBuffer.Clear();

            if (loaded && ShowOverlay.Value)
            {
                if (OnlyJudgedNotes.Value)
                    buildJudgedNotes();
                else
                    buildSpanNotes();
            }

            foreach (var (column, notes) in buildBuffer)
            {
                notes.Sort(static (a, b) => a.StartTime.CompareTo(b.StartTime));
                notesByColumn[column] = notes.ToArray();
            }

            buildBuffer.Clear();

            Version++;
        }

        private void buildSpanNotes()
        {
            foreach (var (column, presses) in pressTimes)
            {
                foreach (double press in presses)
                    add(column, createSpanNote(column, press));
            }
        }

        private EzReplayInputNote createSpanNote(int column, double press)
        {
            double release = findRelease(column, press);

            if (tryFind(judgedHeads, column, press, out var head))
                return createHoldNote(column, press, release, head);

            if (tryFind(judgedNotes, column, press, out var plain))
            {
                return new EzReplayInputNote
                {
                    Column = column,
                    StartTime = press,
                    EndTime = release,
                    Kind = EzReplayInputNoteKind.Note,
                    HeadResult = plain.Result,
                    NonHeadRevealTime = press,
                };
            }

            return new EzReplayInputNote
            {
                Column = column,
                StartTime = press,
                EndTime = release,
            };
        }

        private void buildJudgedNotes()
        {
            if (!resultsApplied)
                return;

            // Plain notes: one note per judgement, drawn as an editor tap note and ignoring the release time.
            foreach (var (column, entries) in judgedNotes)
            {
                foreach (var entry in entries)
                {
                    add(column, new EzReplayInputNote
                    {
                        Column = column,
                        StartTime = entry.Time,
                        EndTime = entry.Time,
                        IsTapStyle = true,
                        Kind = EzReplayInputNoteKind.Note,
                        HeadResult = entry.Result,
                        NonHeadRevealTime = entry.Time,
                    });
                }
            }

            // Hold heads still span the real press-to-release so the LN keeps its shape.
            foreach (var (column, entries) in judgedHeads)
            {
                foreach (var entry in entries)
                    add(column, createHoldNote(column, entry.Time, findRelease(column, entry.Time), entry));
            }
        }

        private EzReplayInputNote createHoldNote(int column, double press, double release, JudgedEntry head)
        {
            HitResult? nonHead = null;
            bool broken = false;
            double reveal = press;

            if (head.Target is HeadNote headNote && holdByHead.TryGetValue((headNote.Column, headNote.StartTime), out var hold))
            {
                if (tailByHold.TryGetValue(hold, out var tail))
                {
                    nonHead = tail.Result;
                    reveal = tail.Time;
                }

                if (brokenByHold.TryGetValue(hold, out double breakTime))
                {
                    broken = true;
                    reveal = Math.Min(reveal, breakTime);
                }
            }

            return new EzReplayInputNote
            {
                Column = column,
                StartTime = press,
                EndTime = release,
                Kind = EzReplayInputNoteKind.Hold,
                HeadResult = head.Result,
                NonHeadResult = nonHead,
                NonHeadRevealTime = reveal,
                BodyBroken = broken,
            };
        }

        private double findRelease(int column, double press)
        {
            if (releaseTimes.TryGetValue(column, out var releases))
            {
                int index = firstAtOrAfter(releases, press);

                // A release at the press time belongs to the previous press (edges at one frame are ordered press-last).
                while (index < releases.Count && releases[index] <= press)
                    index++;

                if (index < releases.Count)
                    return releases[index];
            }

            return Math.Max(press, fallbackEndTime);
        }

        private void indexHoldNotes(IBeatmap beatmap)
        {
            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is not HoldNote hold)
                    continue;

                // Keyed by time and column rather than identity: replay sessions judge an isolated beatmap copy.
                holdByHead[(hold.Head.Column, hold.Head.StartTime)] = hold;
                holdByTail[(hold.Tail.Column, hold.Tail.StartTime)] = hold;

                if (hold.Body != null)
                    holdByBody[(hold.Body.Column, hold.Body.StartTime)] = hold;
            }
        }

        private static void addEntry(Dictionary<int, List<JudgedEntry>> table, int column, JudgedEntry entry)
        {
            if (!table.TryGetValue(column, out var entries))
                table[column] = entries = new List<JudgedEntry>();

            entries.Add(entry);
        }

        private void recordBreak(HoldNote hold, double time)
        {
            brokenByHold[hold] = brokenByHold.TryGetValue(hold, out double existing) ? Math.Min(existing, time) : time;
        }

        private void add(int column, EzReplayInputNote note)
        {
            if (!buildBuffer.TryGetValue(column, out var notes))
                buildBuffer[column] = notes = new List<EzReplayInputNote>();

            notes.Add(note);
        }

        private static bool tryFind(Dictionary<int, List<JudgedEntry>> table, int column, double time, out JudgedEntry entry)
        {
            entry = default;

            if (!table.TryGetValue(column, out var entries) || entries.Count == 0)
                return false;

            int index = firstAtOrAfter(entries, time - time_tolerance);

            if (index >= entries.Count || Math.Abs(entries[index].Time - time) > time_tolerance)
                return false;

            entry = entries[index];
            return true;
        }

        private static int firstAtOrAfter(List<double> times, double time)
        {
            int low = 0;
            int high = times.Count;

            while (low < high)
            {
                int mid = low + (high - low) / 2;

                if (times[mid] < time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        private static int firstAtOrAfter(List<JudgedEntry> entries, double time)
        {
            int low = 0;
            int high = entries.Count;

            while (low < high)
            {
                int mid = low + (high - low) / 2;

                if (entries[mid].Time < time)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        private readonly record struct JudgedEntry(double Time, HitResult Result, HitObject Target);
    }
}
