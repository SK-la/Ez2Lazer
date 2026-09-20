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
#if DEBUG
        private static long holdUpdateCalls;
        private static long holdTickScanVisits;
        private static long holdTickUpdates;
        private static long maxObservedHoldTickScanVisits;
        private static long inputQueueRebuilds;
        private static long inputQueueReuses;
        private static long inputQueueScannedDrawables;
        private static long inputQueueHoldEnds;
        private static long maxObservedInputQueueLength;
        private static long maxObservedInputQueueHoldEnds;
        private static long holdBodyForceRedraws;
#endif

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

#if DEBUG
        /// <summary>存活 <see cref="Objects.Drawables.DrawableHoldNote"/> 的 Update 次数（= 帧数 × 存活 LN 数）。</summary>
        public static long HoldUpdateCalls => Interlocked.Read(ref holdUpdateCalls);

        /// <summary>LN tick 扫描访问的 nested 元素总数（含 head/body/tail，即每帧 O(存活 LN × 各自 tick 数)）。</summary>
        public static long HoldTickScanVisits => Interlocked.Read(ref holdTickScanVisits);

        /// <summary>LN tick 扫描实际触发的结算次数。</summary>
        public static long HoldTickUpdates => Interlocked.Read(ref holdTickUpdates);

        /// <summary>单条 LN 单帧扫描到的 nested 元素数峰值。</summary>
        public static long MaxObservedHoldTickScanVisits => Interlocked.Read(ref maxObservedHoldTickScanVisits);

        /// <summary><c>ManiaKeyBindingContainer.KeyBindingInputQueue</c> 实际重建次数。</summary>
        public static long InputQueueRebuilds => Interlocked.Read(ref inputQueueRebuilds);

        /// <summary>同帧命中按帧缓存、未重建的次数。</summary>
        public static long InputQueueReuses => Interlocked.Read(ref inputQueueReuses);

        /// <summary>重建时扫描到的 drawable 总数（每次重建累加队列长度）。</summary>
        public static long InputQueueScannedDrawables => Interlocked.Read(ref inputQueueScannedDrawables);

        /// <summary>重建时队列里的 head/tail 槽位数累计。</summary>
        public static long InputQueueHoldEnds => Interlocked.Read(ref inputQueueHoldEnds);

        public static long MaxObservedInputQueueLength => Interlocked.Read(ref maxObservedInputQueueLength);

        public static long MaxObservedInputQueueHoldEnds => Interlocked.Read(ref maxObservedInputQueueHoldEnds);

        /// <summary><see cref="Skinning.Default.DefaultBodyPiece"/> 触发两层 FBO <c>ForceRedraw</c> 的次数。</summary>
        public static long HoldBodyForceRedraws => Interlocked.Read(ref holdBodyForceRedraws);

        /// <summary>每个存活 LN 每帧 Update 调用一次。</summary>
        public static void RecordHoldUpdate()
        {
            if (Enabled)
                Interlocked.Increment(ref holdUpdateCalls);
        }

        /// <summary>每个存活 LN 每帧扫描 tick 时调用一次：<paramref name="visited"/> 为该 LN 本帧扫描的 nested 元素数。</summary>
        public static void RecordHoldTickScan(int visited)
        {
            if (!Enabled)
                return;

            Interlocked.Add(ref holdTickScanVisits, visited);
            updateMaxObservedHoldTickScanVisits(visited);
        }

        public static void RecordHoldTickUpdate()
        {
            if (Enabled)
                Interlocked.Increment(ref holdTickUpdates);
        }

        public static void RecordInputQueueRebuild(int queueLength, int holdEnds)
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref inputQueueRebuilds);
            Interlocked.Add(ref inputQueueScannedDrawables, queueLength);
            Interlocked.Add(ref inputQueueHoldEnds, holdEnds);
            updateMaxObserved(ref maxObservedInputQueueLength, queueLength);
            updateMaxObserved(ref maxObservedInputQueueHoldEnds, holdEnds);
        }

        public static void RecordInputQueueReuse(int queueLength)
        {
            if (!Enabled)
                return;

            Interlocked.Increment(ref inputQueueReuses);
            updateMaxObserved(ref maxObservedInputQueueLength, queueLength);
        }

        public static void RecordHoldBodyForceRedraw()
        {
            if (Enabled)
                Interlocked.Increment(ref holdBodyForceRedraws);
        }
#endif

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

#if DEBUG
        private static void updateMaxObservedHoldTickScanVisits(int count) => updateMaxObserved(ref maxObservedHoldTickScanVisits, count);
#endif

        private static void updateMaxObserved(ref long location, int count)
        {
            long current;

            do
            {
                current = Interlocked.Read(ref location);
                if (count <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref location, count, current) != current);
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
#if DEBUG
            Interlocked.Exchange(ref holdUpdateCalls, 0);
            Interlocked.Exchange(ref holdTickScanVisits, 0);
            Interlocked.Exchange(ref holdTickUpdates, 0);
            Interlocked.Exchange(ref maxObservedHoldTickScanVisits, 0);
            Interlocked.Exchange(ref inputQueueRebuilds, 0);
            Interlocked.Exchange(ref inputQueueReuses, 0);
            Interlocked.Exchange(ref inputQueueScannedDrawables, 0);
            Interlocked.Exchange(ref inputQueueHoldEnds, 0);
            Interlocked.Exchange(ref maxObservedInputQueueLength, 0);
            Interlocked.Exchange(ref maxObservedInputQueueHoldEnds, 0);
            Interlocked.Exchange(ref holdBodyForceRedraws, 0);
#endif
        }

        public static string FormatSummary()
            => $"IsHittable={IsHittableCalls} CheckForResult={CheckForResultCalls} O2Bpm={O2BpmLookups} "
               + $"ColPress={ColumnOnPressedCalls} DPress={DrawableOnPressedCalls} AutoMissSkip={AutoMissSkipped} "
               + $"MissOffset={MissStoredOffsetResolves} PressSnapAlloc={PressTimesSnapshotAllocations} PressTimesMax={MaxObservedPressTimesCount}"
#if DEBUG
               + $" HoldUpdate={HoldUpdateCalls} HoldTickScanVisits={HoldTickScanVisits} HoldTickUpdates={HoldTickUpdates} HoldTickScanMax={MaxObservedHoldTickScanVisits} "
               + $"QueueRebuild={InputQueueRebuilds} QueueReuse={InputQueueReuses} QueueScanned={InputQueueScannedDrawables} "
               + $"QueueHoldEnds={InputQueueHoldEnds} QueueLenMax={MaxObservedInputQueueLength} QueueHoldEndsMax={MaxObservedInputQueueHoldEnds} "
               + $"HoldBodyFboRedraw={HoldBodyForceRedraws}"
#endif
            ;
    }
}
