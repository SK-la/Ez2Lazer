// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class AspireProbeTest
    {
        private static readonly string log_path = Path.Combine(Path.GetTempPath(), "aspire-probe-log.txt");
        private static readonly Stopwatch watch = new Stopwatch();

        private static void log(string message)
        {
            File.AppendAllText(log_path, $"[{watch.ElapsedMilliseconds} ms] {message}{Environment.NewLine}");
        }

        [Test]
        public void Probe()
        {
            File.WriteAllText(log_path, string.Empty);
            watch.Restart();

            string path = Directory.GetFiles(Path.Combine(Path.GetTempPath(), "aspire-measure"), "*.osu").Single();

            Beatmap decoded;

            using (var stream = File.OpenRead(path))
            using (var reader = new LineBufferedReader(stream))
                decoded = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

            log($"decoded: {decoded.HitObjects.Count} hitobjects");

            var converted = (OsuBeatmap)new OsuBeatmapConverter(decoded, new OsuRuleset()).Convert();
            log($"converted: {converted.HitObjects.Count} hitobjects");

            var mod = new OsuModAspire { Autoplay = { Value = true } };
            mod.ApplyToBeatmap(converted);
            log("applied mod");

            foreach (var obj in converted.HitObjects)
                obj.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

            log("applied defaults");

            var sliders = converted.HitObjects.OfType<Slider>().ToList();

            log($"sliders: {sliders.Count}");

            log($"non-finite duration: {sliders.Count(s => !double.IsFinite(s.Duration))}");
            log($"zero velocity: {sliders.Count(s => s.Velocity <= 0)}");
            log($"non-finite path distance: {sliders.Count(s => !double.IsFinite(s.Path.Distance))}");
            log($"non-finite calculated distance: {sliders.Count(s => !double.IsFinite(s.Path.CalculatedDistance))}");
            log($"zero calculated distance: {sliders.Count(s => s.Path.CalculatedDistance <= 0)}");

            foreach (var bucket in new[] { 1.0, 10, 100, 1000, 10000, 60000, 600000, 3600000, 86400000, double.MaxValue })
            {
                log($"duration <= {bucket:E0}: {sliders.Count(s => s.Duration <= bucket)}");
            }

            log($"duration > 1 day: {sliders.Count(s => s.Duration > 86400000)}");

            var withDuration = sliders.Where(s => double.IsFinite(s.Duration)).ToList();

            log($"max finite duration: {withDuration.Max(s => s.Duration):F0} ms");

            int maxVertices = 0;
            int totalVertices = 0;
            long worstProduct = 0;
            long totalIterations = 0;

            foreach (var s in sliders)
            {
                int count = s.Path.CalculatedPath.Count;
                maxVertices = Math.Max(maxVertices, count);
                totalVertices += count;
                worstProduct = Math.Max(worstProduct, (long)count * (s.RepeatCount + 1));
                totalIterations += (long)count * (s.RepeatCount + 1);
            }

            log($"vertices: max {maxVertices}, total {totalVertices}, worst vertex*span {worstProduct}, total vertex*span {totalIterations}");
            log($"tick+repeat: {sliders.Sum(s => s.NestedHitObjects.Count(n => n is SliderTick or SliderRepeat))}");
            log($"sum duration: {sliders.Sum(s => s.Duration):F0} ms");
            log($"last start time: {converted.HitObjects.Max(h => h.StartTime):F0}");

            if (worstProduct > 5_000_000 || withDuration.Count != sliders.Count)
            {
                log("skipping generation: input still degenerate");
                return;
            }

            log("generating replay...");
            var generateWatch = Stopwatch.StartNew();
            var data = mod.CreateReplayData(converted, new[] { (Mod)mod });
            generateWatch.Stop();
            log($"frames: {data.Replay.Frames.Count}");
            log($"generation: {generateWatch.ElapsedMilliseconds} ms");

            double lastStart = converted.HitObjects.Max(h => h.StartTime);
            log($"frames beyond last start: {data.Replay.Frames.Count(f => f.Time > lastStart):F0}");
            log($"frames beyond last start +10s: {data.Replay.Frames.Count(f => f.Time > lastStart + 10000):F0}");
            log($"max frame time: {data.Replay.Frames.Max(f => f.Time):F0}");
        }
    }
}
