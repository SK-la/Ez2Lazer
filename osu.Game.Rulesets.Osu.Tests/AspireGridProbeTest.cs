// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class AspireGridProbeTest
    {
        private static readonly string log_path = Path.Combine(Path.GetTempPath(), "aspire-grid.txt");

        private static void log(string s) => File.AppendAllText(log_path, s + Environment.NewLine);

        [Test]
        public void Probe()
        {
            File.WriteAllText(log_path, string.Empty);

            string path = Directory.GetFiles(Path.Combine(Path.GetTempPath(), "aspire-measure"), "*.osu").Single();

            Beatmap decoded;

            using (var stream = File.OpenRead(path))
            using (var reader = new LineBufferedReader(stream))
                decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

            var converted = (OsuBeatmap)new OsuBeatmapConverter(decoded, new OsuRuleset()).Convert();
            new OsuModAspire().ApplyToBeatmap(converted);

            foreach (var obj in converted.HitObjects)
                obj.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

            var sliders = converted.HitObjects.OfType<Slider>().ToList();

            foreach (var s in sliders.Where(x => Math.Abs(x.StartTime - 65974) < 1 || Math.Abs(x.StartTime - 69175) < 1))
            {
                var v = s.Path.CalculatedPath;

                log($"=== t={s.StartTime:F0} vert={v.Count} ===");

                // 检查非有限值。
                int bad = v.Count(p => !float.IsFinite(p.X) || !float.IsFinite(p.Y));
                log($"non-finite vertices: {bad}");

                // 看每个顶点相对前一个跳多远。
                double maxJump = 0;
                int jumpIndex = -1;
                double totalLen = 0;

                for (int i = 1; i < v.Count; i++)
                {
                    double d = (v[i] - v[i - 1]).Length;
                    totalLen += d;

                    if (d > maxJump)
                    {
                        maxJump = d;
                        jumpIndex = i;
                    }
                }

                log($"total path length: {totalLen:F1}");
                log($"largest single-segment length: {maxJump:F1} at index {jumpIndex}");

                // 统计超过几单位的线段；编织网格不应有这种线段。
                int longSegments = 0;
                int veryShort = 0;

                for (int i = 1; i < v.Count; i++)
                {
                    double d = (v[i] - v[i - 1]).Length;

                    if (d > 4)
                        longSegments++;

                    if (d < 0.01)
                        veryShort++;
                }

                log($"segments longer than 4 units: {longSegments}");
                log($"segments shorter than 0.01 units: {veryShort}");

                // 按 x 把路径切成四份，看是否某一区域畸形。
                float minX = v.Min(p => p.X);
                float maxX = v.Max(p => p.X);

                log($"x range {minX:F1}..{maxX:F1}");

                for (int q = 0; q < 4; q++)
                {
                    float lo = minX + (maxX - minX) * q / 4f;
                    float hi = minX + (maxX - minX) * (q + 1) / 4f;

                    var inQ = v.Count(p => p.X >= lo && p.X < hi);
                    log($"  x quarter {q} ({lo:F0}..{hi:F0}): {inQ} vertices ({100.0 * inQ / v.Count:F1}%)");
                }

                // 报告前几个与最后几个顶点。
                log("first 5: " + string.Join(" ", v.Take(5).Select(p => $"({p.X:F1},{p.Y:F1})")));
                log("last 5: " + string.Join(" ", v.Skip(v.Count - 5).Select(p => $"({p.X:F1},{p.Y:F1})")));

                // 比较控制点——路径由此构建。
                var cps = s.Path.ControlPoints;

                log($"control points: {cps.Count}");
                log($"  cp x range {cps.Min(p => p.Position.X):F1}..{cps.Max(p => p.Position.X):F1}");
                log($"  cp y range {cps.Min(p => p.Position.Y):F1}..{cps.Max(p => p.Position.Y):F1}");

                for (int q = 0; q < 4; q++)
                {
                    float cxMin = cps.Min(p => p.Position.X);
                    float cxMax = cps.Max(p => p.Position.X);
                    float lo = cxMin + (cxMax - cxMin) * q / 4f;
                    float hi = cxMin + (cxMax - cxMin) * (q + 1) / 4f;

                    var inQ = cps.Count(p => p.Position.X >= lo && p.Position.X < hi);
                    log($"  cp x quarter {q} ({lo:F0}..{hi:F0}): {inQ} points ({100.0 * inQ / cps.Count:F1}%)");
                }

                log($"  declared={s.Path.ExpectedDistance.Value} calculated={s.Path.CalculatedDistance:F1} distance={s.Path.Distance:F1}");

                // 段类型重要：一条很大的 Bezier 与切成多段的相同点行为完全不同。
                var types = cps.Select(p => p.Type).Where(t => t != null).GroupBy(t => t!.Value).OrderByDescending(g => g.Count());

                log($"  segment-start markers: {string.Join(", ", types.Select(g => $"{g.Key}={g.Count()}"))}");
                log($"  points with no type (continue current segment): {cps.Count(p => p.Type == null)}");
                log(string.Empty);
            }
        }
    }
}
