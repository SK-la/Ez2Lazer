// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.EzOsuGame.Skills.SSR;
using osu.Game.IO;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// Pins whether BDSP write, shared conversion, and empty-mod L3 feed the same SSR core inputs on real .osu charts.
    /// </summary>
    [TestFixture]
    public class EzSsrL1L3ParityOsuTest
    {
        private const int bdsp_binding_scope = 3000;

        private Ez2ConfigManager config = null!;

        [SetUp]
        public void SetUp()
        {
            config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;
            config.SetValue(Ez2Setting.SsrRescaleThreshold, EzSSRTuning.FALLBACK.RescaleThreshold);
            config.SetValue(Ez2Setting.SsrLnMultiplier, EzSSRTuning.FALLBACK.LnMultiplier);

            EzPlayableBeatmapCache.Reset();
        }

        [TearDown]
        public void TearDown() => EzPlayableBeatmapCache.Reset();

        public static IEnumerable<string> NomodManiaFixtures()
        {
            yield return "sample-beatmap-mania.osu";
            yield return "mania-0-01-sv.osu";
            yield return "mania-last-object-not-latest.osu";
            yield return "out-of-range-difficulties-mania.osu";

            string? repoRoot = tryFindRepoRoot();

            if (repoRoot == null)
                yield break;

            foreach (string name in new[]
            {
                "diffcalc-test.osu",
                "9K.osu",
                "16K.osu",
                "100374.osu",
                "GramNibelungen23.osu",
            })
            {
                string path = Path.Combine(repoRoot, "osu.Game.Rulesets.Mania.Tests", "Resources", "Testing", "Beatmaps", name);

                if (File.Exists(path))
                    yield return path;
            }
        }

        [TestCaseSource(nameof(NomodManiaFixtures))]
        public void Nomod_ssr_three_paths_agree_on_real_chart(string fixture)
        {
            var working = loadWorkingBeatmap(fixture);
            var ruleset = working.BeatmapInfo.Ruleset;
            int rulesetOnlineId = ruleset.OnlineID;

            Assert.That(rulesetOnlineId, Is.EqualTo(EzLocalProfileConstants.MANIA_RULESET_ID));

            working.BeatmapInfo.EzRatingData.SunnySR = -1;

            IBeatmap bound = EzPlayableBeatmapCache.GetBound(working, ruleset, null, bdsp_binding_scope, static _ => { });
            IBeatmap shared = EzPlayableBeatmapCache.GetShared(working, ruleset, null);
            IBeatmap l3Dedicated = working.GetPlayableBeatmap(ruleset, Array.Empty<Mod>());

            var boundInputs = describeInputs(bound);
            var sharedInputs = describeInputs(shared);
            var l3Inputs = describeInputs(l3Dedicated);

            double boundStar = starFromPlayable(bound, rulesetOnlineId, rate: 1);
            double sharedStar = starFromPlayable(shared, rulesetOnlineId, rate: 1);
            double l3CoreStar = starFromL3CorePath(l3Dedicated, rulesetOnlineId, rate: 1);

            var lookup = new EzAnalysisLookupCache(working.BeatmapInfo, ruleset, mods: null);
            var result = EzAnalysisComputation.Compute(working, lookup);
            double? l3Headline = result.RatingOf(EzAnalysisSystem.SSR).StarRating;

            string diag = formatDiag(fixture, boundInputs, sharedInputs, l3Inputs, boundStar, sharedStar, l3CoreStar, l3Headline);

            Assert.That(sharedInputs, Is.EqualTo(boundInputs).Using(new ChartInputsComparer()), "GetShared vs GetBound conversion inputs differ\n" + diag);

            Assert.That(l3Inputs, Is.EqualTo(boundInputs).Using(new ChartInputsComparer()), "L3 dedicated conversion inputs differ from BDSP\n" + diag);

            Assert.That(sharedStar, Is.EqualTo(boundStar).Within(1e-9), "GetShared SSR != GetBound SSR\n" + diag);
            Assert.That(l3CoreStar, Is.EqualTo(boundStar).Within(1e-9), "L3 core SSR != BDSP SSR\n" + diag);
            Assert.That(l3Headline, Is.EqualTo(boundStar).Within(1e-9), "L3 headline SSR != BDSP SSR (overlay or assembly)\n" + diag);
        }

        [TestCaseSource(nameof(NomodManiaFixtures))]
        public void Nomod_ssr_overlay_would_hide_live_drift_from_bdsp(string fixture)
        {
            var working = loadWorkingBeatmap(fixture);
            var ruleset = working.BeatmapInfo.Ruleset;

            IBeatmap bound = EzPlayableBeatmapCache.GetBound(working, ruleset, null, bdsp_binding_scope, static _ => { });
            double bdspStar = starFromPlayable(bound, ruleset.OnlineID, rate: 1);

            working.BeatmapInfo.EzRatingData.SunnySR = bdspStar + 0.42;

            var lookup = new EzAnalysisLookupCache(working.BeatmapInfo, ruleset, mods: null);
            double? headline = EzAnalysisComputation.Compute(working, lookup).RatingOf(EzAnalysisSystem.SSR).StarRating;

            IBeatmap l3Dedicated = working.GetPlayableBeatmap(ruleset, lookup.OrderedMods);
            double liveCore = starFromL3CorePath(l3Dedicated, ruleset.OnlineID, rate: 1);

            if (Math.Abs(liveCore - bdspStar) > 1e-9)
            {
                Assert.That(headline, Is.EqualTo(working.BeatmapInfo.EzRatingData.SunnySR).Within(1e-9),
                    "persistedSunnyStar masks a live vs L1 core drift on " + fixture);
            }
        }

        private static double starFromPlayable(IBeatmap playable, int rulesetOnlineId, double rate)
            => EzAnalysisComputation.ComputeSsr(playable, rulesetOnlineId, rate).SunnySR ?? -1;

        private static double starFromL3CorePath(IBeatmap playable, int rulesetOnlineId, double rate)
        {
            var measurements = EzChartMeasurements.FromPlayable(playable);
            return EzAnalysisComputation.ComputeSsr(playable, rulesetOnlineId, rate, CancellationToken.None, measurements).SunnySR ?? -1;
        }

        private static ChartInputs describeInputs(IBeatmap playable)
        {
            var chart = EzCSIEngine.FromPlayable(playable);
            return new ChartInputs(
                playable.HitObjects.Count,
                chart.Notes.Count,
                EzHoldMetrics.ResolveKeyCount(playable),
                playable.Difficulty.OverallDifficulty,
                EzSSREngine.Tuning);
        }

        private static string formatDiag(
            string fixture,
            ChartInputs bound,
            ChartInputs shared,
            ChartInputs l3,
            double boundStar,
            double sharedStar,
            double l3CoreStar,
            double? l3Headline)
            => $"""
               fixture={fixture}
               bound: hitObjects={bound.HitObjectCount} csiNotes={bound.CsiNoteCount} keys={bound.KeyCount} OD={bound.OverallDifficulty:R} tuning=({bound.Tuning.RescaleThreshold},{bound.Tuning.LnMultiplier}) star={boundStar:R}
               shared: hitObjects={shared.HitObjectCount} csiNotes={shared.CsiNoteCount} keys={shared.KeyCount} OD={shared.OverallDifficulty:R} star={sharedStar:R}
               l3: hitObjects={l3.HitObjectCount} csiNotes={l3.CsiNoteCount} keys={l3.KeyCount} OD={l3.OverallDifficulty:R} star={l3CoreStar:R}
               l3Headline={l3Headline:R} realmSunny={-1}
               """;

        private static TestWorkingBeatmap loadWorkingBeatmap(string fixture)
        {
            if (fixture.EndsWith(".osu", StringComparison.OrdinalIgnoreCase) && Path.IsPathRooted(fixture))
                return decodeFile(fixture);

            return decodeEmbedded(fixture);
        }

        private static TestWorkingBeatmap decodeEmbedded(string resourceName)
        {
            using var resStream = TestResources.OpenResource(resourceName);
            using var stream = new LineBufferedReader(resStream);
            return wrapDecoded(stream);
        }

        private static TestWorkingBeatmap decodeFile(string path)
        {
            using var stream = new LineBufferedReader(File.OpenRead(path));
            return wrapDecoded(stream);
        }

        private static TestWorkingBeatmap wrapDecoded(LineBufferedReader stream)
        {
            var decoder = Decoder.GetDecoder<Beatmap>(stream);

            if (decoder is LegacyBeatmapDecoder legacy)
                legacy.ApplyOffsets = false;

            var beatmap = decoder.Decode(stream);
            beatmap.BeatmapInfo.Ruleset = new ManiaRuleset().RulesetInfo;
            return new TestWorkingBeatmap(beatmap);
        }

        private static string? tryFindRepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "osu.sln")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return null;
        }

        private readonly record struct ChartInputs(
            int HitObjectCount,
            int CsiNoteCount,
            int KeyCount,
            double OverallDifficulty,
            EzSSRTuning Tuning);

        private sealed class ChartInputsComparer : IEqualityComparer<ChartInputs>
        {
            public bool Equals(ChartInputs x, ChartInputs y)
                => x.HitObjectCount == y.HitObjectCount
                   && x.CsiNoteCount == y.CsiNoteCount
                   && x.KeyCount == y.KeyCount
                   && Math.Abs(x.OverallDifficulty - y.OverallDifficulty) < 1e-9;

            public int GetHashCode(ChartInputs obj) => HashCode.Combine(obj.HitObjectCount, obj.CsiNoteCount, obj.KeyCount, obj.OverallDifficulty);
        }
    }
}
