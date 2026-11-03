// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using BenchmarkDotNet.Attributes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;

namespace osu.Game.Benchmarks
{
    /// <summary>
    /// DRAWABLE-MICRO-BENCH：10 列叠 LN，PeakKps × alive/col 扫描。
    /// </summary>
    public class BenchmarkManiaLaneHotPath : BenchmarkTest
    {
        private ManiaLaneHotPathWorkload workload = null!;

        [Params(20, 50, 100)]
        public int PeakKps { get; set; }

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
                PeakKps = PeakKps,
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
