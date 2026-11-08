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

            Console.WriteLine($"chart: {mania_beatmap_name}");
            Console.WriteLine($"objects: {starPlayable.HitObjects.Count}, keys: {keyCount}, od: {overallDifficulty:0.##}, iterations: {iterations}");
            Console.WriteLine();

            if (Array.IndexOf(args, "--profile") >= 0)
            {
                profile(rawBeatmap, ruleset, rulesetInfo);
                return;
            }

            Console.WriteLine($"{"stage",-22}{"op",-10}{"charts/s",10}{"ms/chart",12}{"alloc KB",12}");
            Console.WriteLine(new string('-', 68));

            Measurement decodeRow = measure("decode", iterations, () => decode(rawBeatmap));
            Measurement loadRow = measure("load+convert", iterations, () => loadPlayable(rawBeatmap, rulesetInfo, mods));
            Measurement extractRow = measure("extract", iterations, () => EzCSIEngine.FromPlayable(starPlayable));
            Measurement starRow = measure("ssr_star", iterations, () => computeStar(starPlayable, keyCount, overallDifficulty));
            Measurement difficultyRow = measure("official_difficulty", iterations, () => computeOfficial(officialPlayable, ruleset, mods));
            Measurement officialPpRow = measure("official_pp", iterations, () => computePp(officialPlayable, ruleset, mods, computeOfficial(officialPlayable, ruleset, mods)));

            // Current shape: star and official difficulty/pp run on two different conversions. Single pass: both run
            // on the conversion the star already used. Conversion itself is not repeated inside these rows.
            Measurement currentRow = measure("ssr_current", iterations, () =>
                computePp(officialPlayable, ruleset, mods, computeOfficial(officialPlayable, ruleset, mods), star));
            Measurement singleRow = measure("ssr_single_pass", iterations, () =>
                computePp(starPlayable, ruleset, mods, computeOfficial(starPlayable, ruleset, mods), star));

            print(decodeRow);
            print(loadRow);
            print(extractRow);
            print(starRow);
            print(difficultyRow);
            print(officialPpRow);
            print(currentRow);
            print(singleRow);

            printTotal("total_current", loadRow, starRow, currentRow, conversions: 2);
            printTotal("total_single_pass", loadRow, starRow, singleRow, conversions: 1);
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

        private static void print(Measurement row)
        {
            Console.WriteLine($"{row.Name,-22}{row.Iterations,-10}{1_000_000_000.0 / row.Nanoseconds,10:F1}{row.Nanoseconds / 1_000_000.0,12:F2}{row.AllocatedBytes / 1024.0 / row.Iterations,12:F1}");
        }

        private static void printTotal(string name, Measurement load, Measurement star, Measurement compute, int conversions)
        {
            double nanoseconds = load.Nanoseconds * conversions + star.Nanoseconds + compute.Nanoseconds;
            double allocated = load.AllocatedBytes * conversions + star.AllocatedBytes + compute.AllocatedBytes;
            Console.WriteLine($"{name,-22}{load.Iterations,-10}{1_000_000_000.0 / nanoseconds,10:F1}{nanoseconds / 1_000_000.0,12:F2}{allocated / 1024.0 / load.Iterations,12:F1}");
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
