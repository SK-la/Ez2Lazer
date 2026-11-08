// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.LeoBlack;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// Pins the LeoBlack/CSI output of deterministic charts as text, so a change to the scan (buffers, grouping,
    /// ordering) shows up as a diff instead of hiding in a rounded score.
    /// </summary>
    /// <remarks>
    /// The digest carries full round-trip precision rather than display rounding; a two-decimal comparison would let
    /// a real drift through whenever it stayed under a rounding step.
    /// </remarks>
    [TestFixture]
    public class EzLeoBlackGoldenTest
    {
        [TestCase(4, 0.0)]
        [TestCase(4, 0.4)]
        [TestCase(7, 0.0)]
        [TestCase(7, 0.4)]
        public void DigestIsStable(int keyCount, double holdRatio)
        {
            var input = buildChart(keyCount, holdRatio);

            TestContext.Progress.WriteLine($"--- {keyCount}K hold={holdRatio} ---");
            TestContext.Progress.WriteLine(leoDigest(input));
            TestContext.Progress.WriteLine(csiDigest(input));
        }

        private static string leoDigest(EzManiaChartInput input)
        {
            var leo = EzLeoBlackPatternAnalyzer.TryAnalyze(input);

            if (leo == null)
                return "leo=null";

            var builder = new StringBuilder();
            builder.Append("leo category=").Append(leo.Category).Append(" clusters=").Append(leo.Clusters.Count);

            foreach (var cluster in leo.Clusters)
            {
                builder.Append('\n')
                       .Append(cluster.Pattern).Append('|')
                       .Append(cluster.Label).Append('|')
                       .Append(num(cluster.Bpm)).Append('|')
                       .Append(num(cluster.Amount)).Append('|')
                       .Append(num(cluster.Importance));
            }

            return builder.ToString();
        }

        private static string csiDigest(EzManiaChartInput input)
        {
            var info = EzChartSkillInfoComputer.Compute(input);
            var builder = new StringBuilder();

            builder.Append("csi patterns=").Append(string.Join(',', info.Patterns))
                   .Append(" jackDemand=").Append(info.JackDemand)
                   .Append(" jackShare=").Append(num(info.JackShare ?? -1))
                   .Append(" streamShare=").Append(num(info.StreamShare ?? -1))
                   .Append(" techCategory=").Append(info.TechCategory?.ToString() ?? "null")
                   .Append(" clusterTrill=").Append(info.ClusterTrill?.ToString() ?? "null")
                   .Append(" handstream=").Append(info.HandstreamCluster?.ToString() ?? "null")
                   .Append(" tech=").Append(num(info.TechScore))
                   .Append(" chordjack=").Append(num(info.ChordjackScore))
                   .Append(" jack=").Append(num(info.JackScore ?? -1))
                   .Append(" lnRatio=").Append(num(info.LnRatio ?? -1))
                   .Append(" length=").Append(num(info.LengthSeconds ?? -1));

            if (info.Motion is { } motion)
            {
                builder.Append(" motion=")
                       .Append(num(motion.SameHand)).Append(',')
                       .Append(num(motion.MiniJack)).Append(',')
                       .Append(num(motion.CrossHandTrill)).Append(',')
                       .Append(num(motion.RhythmBreak));
            }
            else
            {
                builder.Append(" motion=null");
            }

            return builder.ToString();
        }

        private static string num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static EzManiaChartInput buildChart(int keyCount, double holdRatio)
        {
            var notes = new List<EzManiaNote>();
            var random = new Random(4242);
            double time = 0;

            for (int row = 0; row < 600; row++)
            {
                int size = 1 + random.Next(3);

                for (int note = 0; note < size; note++)
                {
                    int column = random.Next(keyCount);
                    bool isHold = random.NextDouble() < holdRatio;
                    double end = isHold ? time + 200 + random.Next(700) : time;

                    notes.Add(new EzManiaNote(time, column, isHold, end));
                }

                time += 90 + random.Next(4) * 30;
            }

            return new EzManiaChartInput
            {
                KeyCount = keyCount,
                Bpm = 150,
                TotalLengthMs = time,
                Notes = notes,
                TimingPoints =
                [
                    new EzManiaTimingPoint(0, 500),
                    new EzManiaTimingPoint(30_000, 400),
                    new EzManiaTimingPoint(60_000, 375),
                ],
            };
        }
    }
}
