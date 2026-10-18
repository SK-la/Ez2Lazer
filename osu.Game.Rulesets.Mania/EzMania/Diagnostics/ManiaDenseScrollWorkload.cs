// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.EzMania.Diagnostics
{
    /// <summary>
    /// 密集下落场景的密度口径与 note 生成。所有 Mania 同类性能测试共用同一套密度算法。
    /// <para>
    /// 标定密度：每列 <c>PerColumnKps</c> 取 10 / 15 / 20，画面驻留 <c>DwellMs</c> 取 300 / 500 / 700 ms。
    /// 每列瞬时存活数 = 每列 KPS × 驻留秒数（20 KPS × 700 ms = 14），总 KPS = 每列 KPS × 列数。
    /// </para>
    /// <para>
    /// 只做数据生成与密度换算，不依赖 host，也不创建 drawable。
    /// </para>
    /// </summary>
    public sealed class ManiaDenseScrollWorkload
    {
        /// <summary>标定用的每列 KPS 挡位。</summary>
        public static readonly int[] CalibrationPerColumnKps = { 10, 15, 20 };

        /// <summary>标定用的画面驻留挡位（ms）。</summary>
        public static readonly double[] CalibrationDwellMs = { 300, 500, 700 };

        public int ColumnCount { get; init; } = 10;

        /// <summary>每列每秒生成的 note 数。与「全键盘合计 KPS」不同，这里的口径固定为单列。</summary>
        public double PerColumnKps { get; init; } = 20;

        /// <summary>note 从出生到判定线的滚动驻留时长（ms），直接对应 <c>IScrollingInfo.TimeRange</c>。</summary>
        public double DwellMs { get; init; } = 700;

        /// <summary>hold 占全部 note 的比例。0 = 纯 tap，0.5 = 一半 hold。</summary>
        public double HoldRatio { get; init; }

        /// <summary>hold 的按住时长（ms）。不参与存活数公式，仅用于 hold 场景。</summary>
        public double HoldDurationMs { get; init; } = 150;

        /// <summary>各列是否错开一个分数间隔，避免所有列同刻齐发。</summary>
        public bool StaggerColumns { get; init; } = true;

        /// <summary>首个 note 的时间。</summary>
        public double StartTimeMs { get; init; } = 1000;

        /// <summary>枚举场景覆盖的总时长。</summary>
        public double DurationMs { get; init; } = 10_000;

        /// <summary>列内 note 间隔（ms）。</summary>
        public double IntervalMs => 1000.0 / PerColumnKps;

        /// <summary>每列瞬时存活数（理论值）。</summary>
        public double AlivePerColumn => PerColumnKps * DwellMs / 1000.0;

        /// <summary>全列瞬时存活数（理论值，按 tap 计）。</summary>
        public int AliveTotal => (int)Math.Round(AlivePerColumn * ColumnCount);

        /// <summary>全键盘合计 KPS（派生值，仅用于和既有结果表对照）。</summary>
        public int TotalKps => (int)Math.Round(ColumnCount * PerColumnKps);

        /// <summary>是否命中 10/15/20 × 300/500/700 的标定网格。</summary>
        public bool IsCalibrationDensity => Array.IndexOf(CalibrationPerColumnKps, (int)Math.Round(PerColumnKps)) >= 0
                                            && PerColumnKps == Math.Round(PerColumnKps)
                                            && Array.IndexOf(CalibrationDwellMs, DwellMs) >= 0;

        public bool UsesHolds => HoldRatio > 0;

        public void Validate()
        {
            if (ColumnCount < 1)
                throw new ArgumentOutOfRangeException(nameof(ColumnCount));
            if (PerColumnKps <= 0)
                throw new ArgumentOutOfRangeException(nameof(PerColumnKps));
            if (DwellMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(DwellMs));
            if (HoldRatio < 0 || HoldRatio > 1)
                throw new ArgumentOutOfRangeException(nameof(HoldRatio));
            if (HoldDurationMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(HoldDurationMs));
        }

        /// <summary>该列首个 note 相对 <see cref="StartTimeMs"/> 的错开量。</summary>
        public double ColumnOffsetMs(int column)
        {
            if (column < 0 || column >= ColumnCount)
                throw new ArgumentOutOfRangeException(nameof(column));

            return StaggerColumns ? column * IntervalMs / ColumnCount : 0;
        }

        /// <summary>
        /// 返回该列在 <paramref name="afterTimeMs"/> 之后（含）的下一个 note 开始时间。
        /// 流式生成使用：调用方每帧按需补到「当前时间 + 驻留」即可维持稳定的存活数。
        /// </summary>
        public double GetNextStartTime(int column, double afterTimeMs)
        {
            Validate();

            double origin = StartTimeMs + ColumnOffsetMs(column);
            long index = (long)Math.Ceiling((afterTimeMs - origin) / IntervalMs);

            if (index < 0)
                index = 0;

            return origin + index * IntervalMs;
        }

        /// <summary>按列内序号确定性选取 hold，保证同一配置重复运行得到同一份谱面。</summary>
        public bool IsHoldAt(int column, double startTimeMs)
        {
            if (!UsesHolds)
                return false;

            int period = Math.Max(1, (int)Math.Round(1 / HoldRatio));
            double origin = StartTimeMs + ColumnOffsetMs(column);
            long index = (long)Math.Round((startTimeMs - origin) / IntervalMs);

            return index % period == 0;
        }

        /// <summary>枚举整个场景内的 note 数据。仅用于测试断言与离线计算，不用于逐帧流式生成。</summary>
        public IEnumerable<ManiaDenseScrollNote> EnumerateNotes()
        {
            Validate();

            for (int column = 0; column < ColumnCount; column++)
            {
                for (double start = GetNextStartTime(column, StartTimeMs); start < StartTimeMs + DurationMs; start += IntervalMs)
                {
                    bool isHold = IsHoldAt(column, start);

                    yield return new ManiaDenseScrollNote(start, column, isHold, isHold ? HoldDurationMs : 0);
                }
            }
        }

        /// <summary>创建一条普通 note 数据（尚未创建 drawable）。</summary>
        public Note CreateNote(int column, double startTimeMs)
        {
            var note = new Note
            {
                Column = column,
                StartTime = startTimeMs,
            };

            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return note;
        }

        /// <summary>创建一条 hold note 数据（尚未创建 drawable）。</summary>
        public HoldNote CreateHoldNote(int column, double startTimeMs)
        {
            var note = new HoldNote
            {
                Column = column,
                StartTime = startTimeMs,
                Duration = HoldDurationMs,
            };

            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return note;
        }

        public string Describe()
            => $"columns={ColumnCount} perColKps={PerColumnKps:0.##} dwell={DwellMs:0}ms "
               + $"alive/col={AlivePerColumn:0.##} aliveTotal={AliveTotal} totalKps={TotalKps} "
               + $"hold={HoldRatio:0.##} stagger={(StaggerColumns ? "on" : "off")} "
               + $"calibration={(IsCalibrationDensity ? "yes" : "no")}";
    }

    /// <summary>一条密集下落场景里的 note 数据。</summary>
    public readonly struct ManiaDenseScrollNote
    {
        public double StartTimeMs { get; }
        public int Column { get; }
        public bool IsHold { get; }
        public double HoldDurationMs { get; }

        public ManiaDenseScrollNote(double startTimeMs, int column, bool isHold, double holdDurationMs)
        {
            StartTimeMs = startTimeMs;
            Column = column;
            IsHold = isHold;
            HoldDurationMs = holdDurationMs;
        }

        public override string ToString() => $"t={StartTimeMs:0.##} col={Column} hold={(IsHold ? "yes" : "no")}";
    }
}
