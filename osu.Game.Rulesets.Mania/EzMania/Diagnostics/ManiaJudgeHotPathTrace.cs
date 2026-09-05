// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.EzOsuGame.Diagnostics;

namespace osu.Game.Rulesets.Mania.EzMania.Diagnostics
{
    /// <summary>
    /// 高 KPS 判定热路径计数（TRACE-JUDGE）。与 <see cref="EzJudgmentDiagnostics.Enabled"/> 联动。
    /// </summary>
    public static class ManiaJudgeHotPathTrace
    {
        private static long isHittableCalls;
        private static long checkForResultCalls;
        private static long o2BpmLookups;
        private static long drawableOnPressedCalls;
        private static long columnOnPressedCalls;
        private static long autoMissSkipped;
        private static long missStoredOffsetResolves;
        private static long pressTimesSnapshotAllocations;
        private static long maxObservedPressTimesCount;

        public static bool Enabled => EzJudgmentDiagnostics.Enabled;

        public static long IsHittableCalls => Interlocked.Read(ref isHittableCalls);

        public static long CheckForResultCalls => Interlocked.Read(ref checkForResultCalls);

        public static long O2BpmLookups => Interlocked.Read(ref o2BpmLookups);

        public static long DrawableOnPressedCalls => Interlocked.Read(ref drawableOnPressedCalls);

        public static long ColumnOnPressedCalls => Interlocked.Read(ref columnOnPressedCalls);

        public static long AutoMissSkipped => Interlocked.Read(ref autoMissSkipped);

        public static long MissStoredOffsetResolves => Interlocked.Read(ref missStoredOffsetResolves);

        public static long PressTimesSnapshotAllocations => Interlocked.Read(ref pressTimesSnapshotAllocations);

        public static long MaxObservedPressTimesCount => Interlocked.Read(ref maxObservedPressTimesCount);

        public static void RecordIsHittable()
        {
            if (Enabled)
                Interlocked.Increment(ref isHittableCalls);
        }

        public static void RecordCheckForResult()
        {
            if (Enabled)
                Interlocked.Increment(ref checkForResultCalls);
        }

        public static void RecordO2BpmLookup()
        {
            if (Enabled)
                Interlocked.Increment(ref o2BpmLookups);
        }

        public static void RecordDrawableOnPressed()
        {
            if (Enabled)
                Interlocked.Increment(ref drawableOnPressedCalls);
        }

        public static void RecordColumnOnPressed()
        {
            if (Enabled)
                Interlocked.Increment(ref columnOnPressedCalls);
        }

        public static void RecordAutoMissSkipped()
        {
            if (Enabled)
                Interlocked.Increment(ref autoMissSkipped);
        }

        public static void RecordMissStoredOffsetResolve()
        {
            if (Enabled)
                Interlocked.Increment(ref missStoredOffsetResolves);
        }

        public static void RecordPressTimesSnapshotAllocation(int count)
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref pressTimesSnapshotAllocations);
            updateMaxObservedPressTimesCount(count);
        }

        public static void RecordPressTimesCount(int count) => updateMaxObservedPressTimesCount(count);

        private static void updateMaxObservedPressTimesCount(int count)
        {
            if (!Enabled)
                return;

            long current;

            do
            {
                current = Interlocked.Read(ref maxObservedPressTimesCount);
                if (count <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref maxObservedPressTimesCount, count, current) != current);
        }

        public static void Clear()
        {
            Interlocked.Exchange(ref isHittableCalls, 0);
            Interlocked.Exchange(ref checkForResultCalls, 0);
            Interlocked.Exchange(ref o2BpmLookups, 0);
            Interlocked.Exchange(ref drawableOnPressedCalls, 0);
            Interlocked.Exchange(ref columnOnPressedCalls, 0);
            Interlocked.Exchange(ref autoMissSkipped, 0);
            Interlocked.Exchange(ref missStoredOffsetResolves, 0);
            Interlocked.Exchange(ref pressTimesSnapshotAllocations, 0);
            Interlocked.Exchange(ref maxObservedPressTimesCount, 0);
        }

        public static string FormatSummary()
            => $"IsHittable={IsHittableCalls} CheckForResult={CheckForResultCalls} O2Bpm={O2BpmLookups} "
               + $"ColPress={ColumnOnPressedCalls} DPress={DrawableOnPressedCalls} AutoMissSkip={AutoMissSkipped} "
               + $"MissOffset={MissStoredOffsetResolves} PressSnapAlloc={PressTimesSnapshotAllocations} PressTimesMax={MaxObservedPressTimesCount}";
    }
}
