// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Diagnostics
{
    /// <summary>
    /// DRAWABLE-MICRO-BENCH：无 Host 的列热路径合成负载。
    /// 覆盖 SelectPress / CollectOverlapping / Column automiss 到期队列 / pressTimes 列表增减，
    /// 不覆盖 SwapBuffer / 选歌 BDSP（那些需实机或其它 harness）。
    /// </summary>
    public sealed class ManiaLaneHotPathWorkload
    {
        public int Keys { get; init; } = 10;

        /// <summary>全键盘峰值按键吞吐（键次/秒）。10 列齐按 chord 的周期 = Keys*1000/PeakKps ms。</summary>
        public int PeakKps { get; init; } = 50;

        /// <summary>每列同时存活的叠 LN/note（进 miss 窗重叠区）。</summary>
        public int ConcurrentAlivePerColumn { get; init; } = 8;

        public int DurationMs { get; init; } = 2000;

        public int FrameStepMs { get; init; } = 1;

        /// <summary>叠窗间距（ms）；越小 Collect 候选越多。</summary>
        public double AliveSpacingMs { get; init; } = 12;

        public EzEnumJudgePrecedence Precedence { get; init; } = EzEnumJudgePrecedence.Combo;

        public EzEnumHitMode HitMode { get; init; } = EzEnumHitMode.Lazer;

        public bool AllowBmsFallbackToEarliest { get; init; }

        public bool PoorEnabled { get; init; }

        public ManiaLaneHotPathWorkloadResult Run()
        {
            if (Keys < 1)
                throw new ArgumentOutOfRangeException(nameof(Keys));
            if (PeakKps < 1)
                throw new ArgumentOutOfRangeException(nameof(PeakKps));

            var lanes = new ManiaLaneController[Keys];
            var pressHistories = new List<double>[Keys];

            double mid = DurationMs * 0.5;
            double retentionMs = 15_000;

            for (int col = 0; col < Keys; col++)
            {
                var lane = new ManiaLaneController();
                lane.ConfigureMissCollection(HitMode, overallDifficulty: 8);
                lane.SetPoorEnabled(PoorEnabled);
                pressHistories[col] = new List<double>(PeakKps);

                for (int i = 0; i < ConcurrentAlivePerColumn; i++)
                {
                    double start = mid - 40 - i * AliveSpacingMs;
                    var note = createNote(start, HitMode);
                    lane.Register(note, scheduleAutoMiss: true);
                }

                lanes[col] = lane;
            }

            int chordIntervalMs = Math.Max(1, (int)Math.Round(Keys * 1000.0 / PeakKps));
            int nextChordAt = 0;

            int frames = 0;
            int presses = 0;
            int autoMissQueuePolls = 0;
            int autoMissDueVisits = 0;
            int selectCalls = 0;

            int gen0Before = GC.CollectionCount(0);
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            for (int t = 0; t < DurationMs; t += FrameStepMs)
            {
                frames++;

                for (int col = 0; col < lanes.Length; col++)
                {
                    autoMissDueVisits += lanes[col].ProcessAutoMiss(t, evaluateResults: false);
                    autoMissQueuePolls++;
                }

                if (t >= nextChordAt)
                {
                    for (int col = 0; col < Keys; col++)
                    {
                        lanes[col].SelectPressEntry(t, Precedence);
                        selectCalls++;
                        presses++;

                        var history = pressHistories[col];
                        history.Add(t);
                        trimPressHistory(history, t, retentionMs);
                    }

                    nextChordAt += chordIntervalMs;
                }
            }

            sw.Stop();
            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            int gen0After = GC.CollectionCount(0);

            return new ManiaLaneHotPathWorkloadResult(
                sw.ElapsedMilliseconds,
                frames,
                presses,
                autoMissQueuePolls,
                autoMissDueVisits,
                selectCalls,
                Keys,
                PeakKps,
                ConcurrentAlivePerColumn,
                Precedence,
                AllowBmsFallbackToEarliest,
                PoorEnabled,
                allocAfter - allocBefore,
                gen0After - gen0Before);
        }

        /// <summary>
        /// 所有 deadline 均在仿真窗外：断言到期队列不访问任何候选。
        /// </summary>
        public static ManiaLaneHotPathWorkloadResult RunFutureDeadlineGuard(int objectCount = 200, int durationMs = 1000)
        {
            var lane = new ManiaLaneController();

            for (int i = 0; i < objectCount; i++)
            {
                var note = createNote(durationMs + 500 + i, EzEnumHitMode.Lazer);
                lane.Register(note, scheduleAutoMiss: true);
            }

            int queuePolls = 0;
            int dueVisits = 0;
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            for (int t = 0; t < durationMs; t++)
            {
                dueVisits += lane.ProcessAutoMiss(t, evaluateResults: false);
                queuePolls++;
            }

            sw.Stop();
            long allocAfter = GC.GetAllocatedBytesForCurrentThread();

            return new ManiaLaneHotPathWorkloadResult(
                sw.ElapsedMilliseconds,
                durationMs,
                pressCount: 0,
                queuePolls,
                dueVisits,
                selectPressCalls: 0,
                keys: 0,
                peakKps: 0,
                concurrentAlivePerColumn: objectCount,
                EzEnumJudgePrecedence.Earliest,
                allowBmsFallbackToEarliest: false,
                poorEnabled: false,
                allocAfter - allocBefore,
                gen0Collections: 0);
        }

        private static DrawableNote createNote(double startTime, EzEnumHitMode hitMode)
        {
            var note = new Note
            {
                StartTime = startTime,
                HitWindows = new ManiaHitWindows(hitMode)
            };
            note.HitWindows.SetDifficulty(8);

            var drawable = new DrawableNote();
            drawable.Apply(note);
            return drawable;
        }

        private static void trimPressHistory(List<double> pressTimes, double now, double retentionMs)
        {
            double cutoff = now - retentionMs;
            int removeCount = 0;

            while (removeCount < pressTimes.Count && pressTimes[removeCount] < cutoff)
                removeCount++;

            if (removeCount > 0)
                pressTimes.RemoveRange(0, removeCount);
        }
    }

    public readonly struct ManiaLaneHotPathWorkloadResult
    {
        public long ElapsedMilliseconds { get; }
        public int FrameCount { get; }
        public int PressCount { get; }
        public int AutoMissQueuePolls { get; }
        public int AutoMissDueVisits { get; }
        public int SelectPressCalls { get; }
        public int Keys { get; }
        public int PeakKps { get; }
        public int ConcurrentAlivePerColumn { get; }
        public EzEnumJudgePrecedence Precedence { get; }
        public bool AllowBmsFallbackToEarliest { get; }
        public bool PoorEnabled { get; }
        public long AllocatedBytes { get; }
        public int Gen0Collections { get; }

        public double MillisecondsPerFrame => FrameCount == 0 ? 0 : (double)ElapsedMilliseconds / FrameCount;

        public double BytesPerPress => PressCount == 0 ? 0 : (double)AllocatedBytes / PressCount;

        public double DueVisitsPerPoll => AutoMissQueuePolls == 0 ? 0 : (double)AutoMissDueVisits / AutoMissQueuePolls;

        public ManiaLaneHotPathWorkloadResult(
            long elapsedMilliseconds,
            int frameCount,
            int pressCount,
            int autoMissQueuePolls,
            int autoMissDueVisits,
            int selectPressCalls,
            int keys,
            int peakKps,
            int concurrentAlivePerColumn,
            EzEnumJudgePrecedence precedence,
            bool allowBmsFallbackToEarliest,
            bool poorEnabled,
            long allocatedBytes,
            int gen0Collections)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            FrameCount = frameCount;
            PressCount = pressCount;
            AutoMissQueuePolls = autoMissQueuePolls;
            AutoMissDueVisits = autoMissDueVisits;
            SelectPressCalls = selectPressCalls;
            Keys = keys;
            PeakKps = peakKps;
            ConcurrentAlivePerColumn = concurrentAlivePerColumn;
            Precedence = precedence;
            AllowBmsFallbackToEarliest = allowBmsFallbackToEarliest;
            PoorEnabled = poorEnabled;
            AllocatedBytes = allocatedBytes;
            Gen0Collections = gen0Collections;
        }

        public override string ToString()
            => $"keys={Keys} peakKps={PeakKps} alive/col={ConcurrentAlivePerColumn} prec={Precedence} "
               + $"bmsFb={AllowBmsFallbackToEarliest} poor={PoorEnabled} "
               + $"elapsed={ElapsedMilliseconds}ms frames={FrameCount} presses={PressCount} "
               + $"autoMissPolls={AutoMissQueuePolls} dueVisits={AutoMissDueVisits} (~{DueVisitsPerPoll:F2}/poll) select={SelectPressCalls} "
               + $"ms/frame={MillisecondsPerFrame:F4} alloc={AllocatedBytes}B (~{BytesPerPress:F0}/press) gen0={Gen0Collections}";
    }
}
