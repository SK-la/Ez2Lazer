// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using BenchmarkDotNet.Attributes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;

namespace osu.Game.Benchmarks
{
    /// <summary>
    /// DRAWABLE-MICRO-BENCH：10 列叠 LN，每列 KPS × 每列存活密度扫描。
    /// <para>
    /// 密度口径与 <see cref="ManiaDenseScrollWorkload"/> 一致：每列 10 / 15 / 20 KPS（标定挡）。
    /// <see cref="ConcurrentAlivePerColumn"/> 是「判定窗重叠区里的未判定条数」，不是滚动驻留数，
    /// 因此仍独立扫描 8 / 24 / 40，不受 300–700 ms 驻留约束。
    /// </para>
    /// </summary>
    public class BenchmarkManiaLaneHotPath : BenchmarkTest
    {
        private ManiaLaneHotPathWorkload workload = null!;

        [Params(10, 15, 20)]
        public int PerColumnKps { get; set; }

        [Params(8, 24, 40)]
        public int ConcurrentAlivePerColumn { get; set; }

        [Params(EzEnumJudgePrecedence.Earliest, EzEnumJudgePrecedence.Combo, EzEnumJudgePrecedence.Duration)]
        public EzEnumJudgePrecedence Precedence { get; set; }

        public override void SetUp()
        {
            base.SetUp();

            workload = new ManiaLaneHotPathWorkload
            {
                Keys = 10,
                PerColumnKps = PerColumnKps,
                ConcurrentAlivePerColumn = ConcurrentAlivePerColumn,
                AliveSpacingMs = 12,
                DurationMs = 2000,
                FrameStepMs = 1,
                Precedence = Precedence,
            };
        }

        [Benchmark]
        public ManiaLaneHotPathWorkloadResult RunTenKeyDenseLn() => workload.Run();
    }
}
