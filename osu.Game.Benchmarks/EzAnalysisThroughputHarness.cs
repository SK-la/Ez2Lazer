// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SSR;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Tests.Resources;

namespace osu.Game.Benchmarks
{
    /// <summary>
    /// Console throughput harness for the Ez chart analysis pipeline.
    /// </summary>
    /// <remarks>
    /// Runs without a game host and without Realm: it measures the CPU halves of one chart (load, convert, chart
    /// extraction, SunnySR, official difficulty, performance) so a change can be compared branch-to-branch. Realm
    /// reads/writes and working-beatmap caching are deliberately excluded, so the absolute charts/s is not the
    /// number BDSP reports; only the relative change is.
    /// </remarks>
    public static class EzAnalysisThroughputHarness
    {
        private const string archive_resource = "Resources/Archives/241526 Soleily - Renatus.osz";
        private const string mania_beatmap_name = "Soleily - Renatus (ExPew) [Another].osu";

        public static void Run(string[] args)
        {
            // The getter builds a fresh manager (and its storage) on every access until one is assigned, so an
            // unconfigured process would measure config construction instead of analysis. Pin one here, the same
            // thing Ez2TestSetup does.
            var config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;

            int iterations = 8;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--iterations" && int.TryParse(args[i + 1], out int parsed) && parsed > 0)
                    iterations = parsed;
            }

            using var resources = new DllResourceStore(typeof(TestResources).Assembly);
            using var archive = resources.GetStream(archive_resource) ?? throw new InvalidOperationException("beatmap archive not found");
            using var archiveReader = new ZipArchiveReader(archive);

            byte[] rawBeatmap = readResource(archiveReader, mania_beatmap_name);

            var ruleset = new ManiaRuleset();
            var rulesetInfo = ruleset.RulesetInfo;
            var mods = Array.Empty<Mod>();

            // Two independent converted charts: the star path and the official path each get their own instance in
            // the current shape. Conversion mutates the source beatmap, so every load below starts from a fresh
            // decode rather than reusing one decoded object.
            IBeatmap starPlayable = loadPlayable(rawBeatmap, rulesetInfo, mods);
            IBeatmap officialPlayable = loadPlayable(rawBeatmap, rulesetInfo, mods);
            int keyCount = EzHoldMetrics.ResolveKeyCount(starPlayable);
            double overallDifficulty = starPlayable.Difficulty.OverallDifficulty;
            double star = computeStar(starPlayable, keyCount, overallDifficulty);
            var chartOnce = EzCSIEngine.FromPlayable(starPlayable);

            Console.WriteLine($"chart: {mania_beatmap_name}");
            Console.WriteLine($"objects: {starPlayable.HitObjects.Count}, keys: {keyCount}, od: {overallDifficulty:0.##}, iterations: {iterations}");
            Console.WriteLine();

            if (Array.IndexOf(args, "--profile") >= 0)
            {
                profile(rawBeatmap, ruleset, rulesetInfo);
                return;
            }

            if (Array.IndexOf(args, "--srprobe") >= 0)
            {
                srProbe(starPlayable, keyCount, overallDifficulty);
                return;
            }

            if (Array.IndexOf(args, "--srpretty") >= 0)
            {
                srPretty(starPlayable, keyCount, overallDifficulty);
                return;
            }

            if (Array.IndexOf(args, "--pipeline") >= 0)
            {
                pipeline(rawBeatmap, ruleset, rulesetInfo, mods);
                return;
            }

            Console.WriteLine($"{"stage",-22}{"op",-10}{"charts/s",10}{"ms/chart",12}{"alloc KB",12}");
            Console.WriteLine(new string('-', 68));

            Action decodeAction = () => decode(rawBeatmap);
            Action loadAction = () => loadPlayable(rawBeatmap, rulesetInfo, mods);
            Action extractAction = () => EzCSIEngine.FromPlayable(starPlayable);
            Action starAction = () => computeStar(starPlayable, keyCount, overallDifficulty);
            Action starOnlyAction = () => EzSSREngine.CalculateSR(chartOnce, keyCount, overallDifficulty);
            Action difficultyAction = () => computeOfficial(officialPlayable, ruleset, mods);
            Action officialPpAction = () => computePp(officialPlayable, ruleset, mods, computeOfficial(officialPlayable, ruleset, mods));

            // The three full-library passes a first backfill runs, each measured as the whole pass does it: one
            // conversion of its own, then its own compute. `ssr_pass` also produces the official pp, which is why the
            // official pp pass no longer has to touch the same chart.
            Action ssrPassAction = () =>
            {
                IBeatmap playable = loadPlayable(rawBeatmap, rulesetInfo, mods);
                double sr = EzSSREngine.CalculateSR(EzCSIEngine.FromPlayable(playable), EzHoldMetrics.ResolveKeyCount(playable), playable.Difficulty.OverallDifficulty);
                DifficultyAttributes attributes = computeOfficial(playable, ruleset, mods);
                GC.KeepAlive(computePp(playable, ruleset, mods, attributes, sr));
            };

            Action officialPpPassAction = () =>
            {
                IBeatmap playable = loadPlayable(rawBeatmap, rulesetInfo, mods);
                GC.KeepAlive(computePp(playable, ruleset, mods, computeOfficial(playable, ruleset, mods)));
            };

            Action chainPassAction = () =>
            {
                IBeatmap playable = loadPlayable(rawBeatmap, rulesetInfo, mods);
                runChainStages(playable);
            };

            Action officialStarPassAction = () =>
            {
                IBeatmap playable = loadPlayable(rawBeatmap, rulesetInfo, mods);
                GC.KeepAlive(computeOfficial(playable, ruleset, mods));
            };

            // Every action is warmed before anything is measured: tiered compilation promotes a method after enough
            // calls, and a row measured before its callees are promoted reads as a regression that is not there.
            Action[] allActions =
            {
                decodeAction, loadAction, extractAction, starAction, starOnlyAction, difficultyAction, officialPpAction,
                ssrPassAction, officialPpPassAction, officialStarPassAction, chainPassAction,
            };

            for (int warmup = 0; warmup < 40; warmup++)
            {
                foreach (var action in allActions)
                    action();
            }

            Measurement decodeRow = measure("decode", iterations, decodeAction);
            Measurement loadRow = measure("load+convert", iterations, loadAction);
            Measurement extractRow = measure("extract", iterations, extractAction);
            Measurement starRow = measure("ssr_star", iterations, starAction);
            Measurement starOnlyRow = measure("ssr_star_only", iterations, starOnlyAction);
            Measurement difficultyRow = measure("official_difficulty", iterations, difficultyAction);
            Measurement officialPpRow = measure("official_pp", iterations, officialPpAction);
            Measurement ssrPassRow = measure("ssr_pass", iterations, ssrPassAction);
            Measurement officialPpPassRow = measure("official_pp_pass", iterations, officialPpPassAction);
            Measurement officialStarPassRow = measure("official_star_pass", iterations, officialStarPassAction);
            Measurement chainPassRow = measure("chain_pass", iterations, chainPassAction);

            print(decodeRow);
            print(loadRow);
            print(extractRow);
            print(starRow);
            print(starOnlyRow);
            print(difficultyRow);
            print(officialPpRow);
            print(ssrPassRow);
            print(officialPpPassRow);
            print(officialStarPassRow);
            print(chainPassRow);

            // Per chart, across every pass a first backfill runs. `ssr_pass` now also produces the official star and
            // pp, which is why the official passes no longer have to touch the same chart.
            Measurement before = add("backfill_before", officialStarPassRow, officialPpPassRow, ssrPassRow, chainPassRow);
            Measurement after = add("backfill_after", ssrPassRow, chainPassRow);

            print(before);
            print(after);

            Console.WriteLine();

            // Always reported: a change inside one computation can look like a win on a single thread and still make
            // the pass slower by allocating more, so the parallel number is part of every result.
            pipeline(rawBeatmap, ruleset, rulesetInfo, mods);
        }

        private static Measurement add(string name, params Measurement[] parts)
        {
            double nanoseconds = 0;
            long allocated = 0;
            long iterations = 0;

            foreach (var part in parts)
            {
                nanoseconds += part.Nanoseconds;
                allocated += part.AllocatedBytes;
                iterations = part.Iterations;
            }

            return new Measurement(name, iterations, nanoseconds, allocated);
        }

        /// <summary>
        /// The chart-chain stages that read a converted chart: Mina's wasm rating, and the structure pass CSI shares
        /// with the Sunny rating.
        /// </summary>
        private static void runChainStages(IBeatmap playable)
        {
            int keys = EzHoldMetrics.ResolveKeyCount(playable);
            var chart = EzCSIEngine.FromPlayable(playable);

            if (EzMinaEngine.IsSupportedKeyCount(keys))
                GC.KeepAlive(EzMinaEnginePool.Rent().CalculateMsd(EzMinaNoteConverter.Convert(chart.Notes), keys, 1f));

            GC.KeepAlive(EzCSIEngine.ComputeAxes(chart, out _));
        }

        private static void profile(byte[] raw, Ruleset ruleset, IRulesetInfo rulesetInfo)
        {
            for (int run = 0; run < 3; run++)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                var stopwatch = Stopwatch.StartNew();

                Beatmap decoded = decode(raw);
                long decodeMs = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                var converter = ruleset.CreateBeatmapConverter(decoded);
                IBeatmap converted = converter.Convert(CancellationToken.None);
                long convertMs = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                var allocatedByType = new Dictionary<string, (long Bytes, int Count)>();
                foreach (var obj in converted.HitObjects)
                {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    obj.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty, CancellationToken.None);
                    long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                    string type = obj.GetType().Name;
                    allocatedByType.TryGetValue(type, out var entry);
                    allocatedByType[type] = (entry.Bytes + bytes, entry.Count + 1);
                }

                long defaultsMs = stopwatch.ElapsedMilliseconds;

                if (run == 0)
                {
                    var sample = converted.HitObjects[0];
                    long beforeCtor = GC.GetAllocatedBytesForCurrentThread();
                    var windows = new osu.Game.Rulesets.Mania.Scoring.ManiaHitWindows(osu.Game.EzOsuGame.Configuration.EzEnumJudgeMode.Lazer);
                    long afterCtor = GC.GetAllocatedBytesForCurrentThread();
                    windows.SetDifficulty(converted.Difficulty.OverallDifficulty);
                    long afterDifficulty = GC.GetAllocatedBytesForCurrentThread();
                    long beforeEffect = GC.GetAllocatedBytesForCurrentThread();
                    converted.ControlPointInfo.EffectPointAt(sample.StartTime);
                    long afterEffect = GC.GetAllocatedBytesForCurrentThread();
                    long beforeDefaultWindows = GC.GetAllocatedBytesForCurrentThread();
                    _ = new osu.Game.Rulesets.Scoring.DefaultHitWindows();
                    long afterDefaultWindows = GC.GetAllocatedBytesForCurrentThread();
                    long beforeTiming = GC.GetAllocatedBytesForCurrentThread();
                    converted.ControlPointInfo.TimingPointAt(sample.StartTime);
                    long afterTiming = GC.GetAllocatedBytesForCurrentThread();
                    var hold = converted.HitObjects.FirstOrDefault(h => h is osu.Game.Rulesets.Mania.Objects.HoldNote) as osu.Game.Rulesets.Mania.Objects.HoldNote;
                    long beforeDefaultSamples = GC.GetAllocatedBytesForCurrentThread();
                    var nodeSamples = hold == null ? null : osu.Game.Rulesets.Mania.Objects.HoldNote.CreateDefaultNodeSamples(hold);
                    long afterDefaultSamples = GC.GetAllocatedBytesForCurrentThread();
                    Console.WriteLine($"  maniaWindows={afterCtor - beforeCtor}B defaultWindows={afterDefaultWindows - beforeDefaultWindows}B setDifficulty={afterDifficulty - afterCtor}B effectPoint={afterEffect - beforeEffect}B timingPoint={afterTiming - beforeTiming}B defaultNodeSamples={afterDefaultSamples - beforeDefaultSamples}B hop={hold?.Samples.Count}/{nodeSamples?.Count}");
                }

                foreach (var (type, entry) in allocatedByType)
                    Console.WriteLine($"  {type} count={entry.Count} avg={entry.Bytes / entry.Count / 1024.0:F1}KB total={entry.Bytes / 1024}KB");

                stopwatch.Restart();
                var processor = rulesetInfo.CreateInstance()?.CreateBeatmapProcessor(converted);
                processor?.PreProcess();
                processor?.PostProcess();
                long processorMs = stopwatch.ElapsedMilliseconds;

                long allocatedKb = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 1024;
                Console.WriteLine($"run{run} objects={converted.HitObjects.Count} decode={decodeMs}ms convert={convertMs}ms defaults={defaultsMs}ms processor={processorMs}ms alloc={allocatedKb}KB");
            }
        }

        /// <summary>
        /// Prints the full-precision Sunny rating for a fixed set of charts, so a change to the engine can be shown
        /// to leave the numbers alone (or to move them, when that is intended).
        /// </summary>
        private static void srPretty(IBeatmap realChart, int realKeyCount, double realOverallDifficulty)
        {
            Console.WriteLine($"real keys={realKeyCount} sr={EzSSREngine.CalculateSR(EzCSIEngine.FromPlayable(realChart), realKeyCount, realOverallDifficulty):R}");

            foreach (int keyCount in new[] { 4, 6, 7, 10 })
            {
                foreach (int notesPerSecond in new[] { 6, 12 })
                {
                    foreach (bool holds in new[] { false, true })
                    {
                        var chart = syntheticChart(keyCount, notesPerSecond, seconds: 90, holds);
                        Console.WriteLine($"synth keys={keyCount} nps={notesPerSecond} holds={holds} sr={EzSSREngine.CalculateSR(chart, keyCount, 8):R}");
                    }
                }
            }
        }

        private static EzManiaChartInput syntheticChart(int keyCount, int notesPerSecond, int seconds, bool holds)
        {
            int noteCount = notesPerSecond * seconds;
            var notes = new List<EzManiaNote>(noteCount);
            double interval = 1000.0 / notesPerSecond;

            for (int i = 0; i < noteCount; i++)
            {
                double time = i * interval;
                bool isHold = holds && i % 3 == 0;
                notes.Add(new EzManiaNote(time, i % keyCount, isHold, isHold ? time + 220 : time));
            }

            return new EzManiaChartInput
            {
                KeyCount = keyCount,
                Bpm = 180,
                TotalLengthMs = seconds * 1000.0,
                Notes = notes,
                TimingPoints = new[] { new EzManiaTimingPoint(0, 1000.0 / 3) },
                FullyRepresented = true,
            };
        }

        private static void srProbe(IBeatmap playable, int keyCount, double overallDifficulty)
        {
            var chart = EzCSIEngine.FromPlayable(playable);

            for (int i = 0; i < 12; i++)
            {
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                int gc0 = GC.CollectionCount(0);
                int gc1 = GC.CollectionCount(1);
                int gc2 = GC.CollectionCount(2);
                var stopwatch = Stopwatch.StartNew();
                double sr = EzSSREngine.CalculateSR(chart, keyCount, overallDifficulty);

                Console.WriteLine($"call={i} sr={sr:F4} ms={stopwatch.Elapsed.TotalMilliseconds:F2} allocKB={(GC.GetAllocatedBytesForCurrentThread() - allocated) / 1024.0:F0} "
                                  + $"gc={GC.CollectionCount(0) - gc0}/{GC.CollectionCount(1) - gc1}/{GC.CollectionCount(2) - gc2}");
            }
        }

        /// <summary>
        /// Runs what a backfill pass does to one chart — load, convert, extract, rate, and score — on a given number
        /// of workers, over the real chart.
        /// </summary>
        /// <remarks>
        /// This is the shape the pipeline has, not the pass itself: the Realm read and write are absent, and every
        /// iteration loads its own working beatmap so no conversion is reused. It answers how much throughput the
        /// per-chart work has, and how it scales with workers.
        /// </remarks>
        private static void pipeline(byte[] raw, Ruleset ruleset, IRulesetInfo rulesetInfo, Mod[] mods)
        {
            const int chart_count = 48;

            Console.WriteLine($"{"workers",-10}{"charts/s",10}{"ms/chart",12}{"alloc KB",12}   (whole per-chart pass)");

            foreach (int workers in new[] { 1, 2, 4, 8 })
            {
                double totalMs = 0;
                long totalAllocated = 0;

                // Two passes: the first pays JIT and engine warm-up for every stage.
                for (int pass = 0; pass < 2; pass++)
                {
                    long allocated = GC.GetTotalAllocatedBytes();
                    var stopwatch = Stopwatch.StartNew();
                    int next = -1;

                    var threads = new Thread[workers];

                    for (int w = 0; w < workers; w++)
                    {
                        threads[w] = new Thread(() =>
                        {
                            while (true)
                            {
                                if (Interlocked.Increment(ref next) >= chart_count)
                                    return;

                                runPipeline(raw, ruleset, rulesetInfo, mods);
                            }
                        }, 4 * 1024 * 1024) { IsBackground = true };

                        threads[w].Start();
                    }

                    foreach (var thread in threads)
                        thread.Join();

                    if (pass == 1)
                    {
                        totalMs = stopwatch.Elapsed.TotalMilliseconds;
                        totalAllocated = GC.GetTotalAllocatedBytes() - allocated;
                    }
                }

                Console.WriteLine($"{workers,-10}{chart_count / (totalMs / 1000.0),10:F1}{totalMs / chart_count,12:F1}{totalAllocated / 1024.0 / chart_count,12:F0}");
            }
        }

        /// <summary>
        /// One chart, start to finish: load, convert, and every system that reads the converted chart.
        /// </summary>
        private static void runPipeline(byte[] raw, Ruleset ruleset, IRulesetInfo rulesetInfo, Mod[] mods)
        {
            // Own working beatmap per chart: the shared cache would otherwise reuse the conversion for later charts.
            IBeatmap playable = loadPlayable(raw, rulesetInfo, mods);
            int keys = EzHoldMetrics.ResolveKeyCount(playable);
            double overallDifficulty = playable.Difficulty.OverallDifficulty;

            double sr = EzSSREngine.CalculateSR(EzCSIEngine.FromPlayable(playable), keys, overallDifficulty);
            DifficultyAttributes attributes = computeOfficial(playable, ruleset, mods);
            double pp = computePp(playable, ruleset, mods, attributes, sr);

            runChainStages(playable);

            GC.KeepAlive(pp);
        }

        private static void print(Measurement row)
        {
            // The columns are per chart, so both the rate and the time are derived from the per-iteration share.
            double perChartNanoseconds = row.Nanoseconds / row.Iterations;
            Console.WriteLine($"{row.Name,-22}{row.Iterations,-10}{1_000_000_000.0 / perChartNanoseconds,10:F1}{perChartNanoseconds / 1_000_000.0,12:F2}{row.AllocatedBytes / 1024.0 / row.Iterations,12:F1}");
        }

        private static void printTotal(string name, Measurement load, Measurement star, Measurement compute, int conversions)
        {
            double nanoseconds = load.Nanoseconds * conversions + star.Nanoseconds + compute.Nanoseconds;
            double allocated = load.AllocatedBytes * conversions + star.AllocatedBytes + compute.AllocatedBytes;
            double perChartNanoseconds = nanoseconds / load.Iterations;
            Console.WriteLine($"{name,-22}{load.Iterations,-10}{1_000_000_000.0 / perChartNanoseconds,10:F1}{perChartNanoseconds / 1_000_000.0,12:F2}{allocated / 1024.0 / load.Iterations,12:F1}");
        }

        private static double computeStar(IBeatmap playable, int keyCount, double overallDifficulty)
            => EzSSREngine.CalculateSR(EzCSIEngine.FromPlayable(playable), keyCount, overallDifficulty);

        private static DifficultyAttributes computeOfficial(IBeatmap playable, Ruleset ruleset, Mod[] mods)
            => ruleset.CreateDifficultyCalculator(new StaticWorkingBeatmap(playable)).Calculate(mods);

        private static double computePp(IBeatmap playable, Ruleset ruleset, Mod[] mods, DifficultyAttributes attributes, double? starRating = null)
        {
            var calculator = ruleset.CreatePerformanceCalculator();

            if (calculator == null)
                return -1;

            ScoreProcessor scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = mods;
            scoreProcessor.ApplyBeatmap(playable);

            var score = new ScoreInfo(playable.BeatmapInfo, ruleset.RulesetInfo)
            {
                Passed = true,
                Accuracy = 1,
                Mods = mods,
                MaxCombo = scoreProcessor.MaximumCombo,
                Combo = scoreProcessor.MaximumCombo,
                TotalScore = scoreProcessor.MaximumTotalScore,
                Statistics = scoreProcessor.MaximumStatistics,
                MaximumStatistics = scoreProcessor.MaximumStatistics,
            };

            if (starRating is double star)
                attributes.StarRating = star;

            return calculator.Calculate(score, attributes).Total;
        }

        private static Beatmap decode(byte[] raw)
        {
            using var stream = new MemoryStream(raw, writable: false);
            using var reader = new LineBufferedReader(stream);
            var decoder = Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader);
            return decoder.Decode(reader);
        }

        // A fresh conversion, the same thing a cache miss produces. The pp path binds judgements in place, so it
        // must never receive a shared cache instance.
        private static IBeatmap loadPlayable(byte[] raw, IRulesetInfo rulesetInfo, Mod[] mods)
            => new FlatWorkingBeatmap(decode(raw)).GetPlayableBeatmap(rulesetInfo, mods);

        private static byte[] readResource(ZipArchiveReader archiveReader, string name)
        {
            using var stream = new MemoryStream();
            archiveReader.GetStream(name).CopyTo(stream);
            return stream.ToArray();
        }

        private static Measurement measure(string name, int iterations, Action action, int warmup = 3)
        {
            for (int i = 0; i < warmup; i++)
                action();

            long startAllocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            long ops = 0;

            for (int i = 0; i < iterations; i++)
            {
                action();
                ops++;
            }

            long elapsed = Stopwatch.GetTimestamp() - start;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - startAllocated;

            return new Measurement(name, ops, elapsed * 1_000_000_000.0 / Stopwatch.Frequency, allocated);
        }

        private readonly record struct Measurement(string Name, long Iterations, double Nanoseconds, long AllocatedBytes);

        private sealed class StaticWorkingBeatmap : IWorkingBeatmap
        {
            private readonly IBeatmap playable;

            public StaticWorkingBeatmap(IBeatmap playable)
            {
                this.playable = playable;
            }

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods) => playable;

            public IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, System.Threading.CancellationToken cancellationToken) => playable;

            IBeatmapInfo IWorkingBeatmap.BeatmapInfo => playable.BeatmapInfo;
            bool IWorkingBeatmap.BeatmapLoaded => true;
            bool IWorkingBeatmap.TrackLoaded => false;
            IBeatmap IWorkingBeatmap.Beatmap => playable;
            Texture IWorkingBeatmap.GetBackground() => null!;
            Texture IWorkingBeatmap.GetPanelBackground() => null!;
            Waveform IWorkingBeatmap.Waveform => null!;
            Storyboard IWorkingBeatmap.Storyboard => null!;
            ISkin IWorkingBeatmap.Skin => null!;
            Track IWorkingBeatmap.Track => null!;
            Track IWorkingBeatmap.LoadTrack() => null!;
            Stream IWorkingBeatmap.GetStream(string storagePath) => Stream.Null;
            void IWorkingBeatmap.BeginAsyncLoad()
            {
            }

            void IWorkingBeatmap.CancelAsyncLoad()
            {
            }

            void IWorkingBeatmap.PrepareTrackForPreview(bool looping, double? offsetFromPreviewPoint)
            {
            }
        }
    }
}
