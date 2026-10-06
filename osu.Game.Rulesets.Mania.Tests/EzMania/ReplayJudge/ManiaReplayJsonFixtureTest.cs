// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaReplayJsonFixtureTest
    {
        [Test]
        public void TestEmbeddedTapFixtureRoundTripsSubMillisecondTimes()
        {
            var document = ManiaReplayJsonFixture.ReadResource("Resources/Testing/ReplayJson/Lazer-two-note-tap.json");
            var (_, _, hitObjects, frames, _) = ManiaReplayJsonFixture.ToParts(document);

            Assert.That(hitObjects[0].StartTime, Is.EqualTo(1000.25).Within(1e-9));
            Assert.That(hitObjects[1].StartTime, Is.EqualTo(2000.75).Within(1e-9));
            Assert.That(((ManiaReplayFrame)frames[0]).Time, Is.EqualTo(900.1).Within(1e-9));
            Assert.That(((ManiaReplayFrame)frames[0]).Actions, Is.EquivalentTo(new[] { ManiaAction.Key1 }));
            Assert.That(((ManiaReplayFrame)frames[1]).Actions, Is.Empty);
        }

        [Test]
        public void TestWriteThenReadPreservesHoldAndKeys()
        {
            var source = ManiaReplayJsonFixture.ReadResource("Resources/Testing/ReplayJson/Lazer-hold-break-repress-meh.json");
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "replay_json_roundtrip.json");
            ManiaReplayJsonFixture.Write(path, source);

            using var stream = File.OpenRead(path);
            var roundTrip = ManiaReplayJsonFixture.Read(stream);
            var (_, _, hitObjects, frames, _) = ManiaReplayJsonFixture.ToParts(roundTrip);

            Assert.That(hitObjects.Single(), Is.TypeOf<HoldNote>());
            var hold = (HoldNote)hitObjects.Single();
            Assert.That(hold.StartTime, Is.EqualTo(1500).Within(1e-9));
            Assert.That(hold.EndTime, Is.EqualTo(4000).Within(1e-9));
            Assert.That(frames, Has.Count.EqualTo(4));
            Assert.That(((ManiaReplayFrame)frames[2]).Time, Is.EqualTo(3900.25).Within(1e-9));
        }

        [Test]
        public void TestAuditChartFixturesReferenceBeatmapAndHaveFrames()
        {
            assertAuditFixture("Resources/Testing/ReplayJson/Lazer-Hanatachi.json", "Resources/Testing/Beatmaps/ManiaAudit-Hanatachi.osu");
            assertAuditFixture("Resources/Testing/ReplayJson/Lazer-PORTRAiT.json", "Resources/Testing/Beatmaps/ManiaAudit-PORTRAiT.osu");
        }

        [Test]
        public void TestToScoreAndBeatmapMatchesToParts()
        {
            var document = ManiaReplayJsonFixture.ReadResource("Resources/Testing/ReplayJson/Lazer-two-note-tap.json");
            var (score, hitObjects, columns, environment) = ManiaReplayJsonFixture.ToScoreAndBeatmap(document);
            var (env2, columns2, hitObjects2, frames2, score2) = ManiaReplayJsonFixture.ToParts(document);

            Assert.That(columns, Is.EqualTo(columns2));
            Assert.That(environment.ManiaHitMode, Is.EqualTo(env2.ManiaHitMode));
            Assert.That(hitObjects, Has.Count.EqualTo(hitObjects2.Count));
            Assert.That(score.Replay.Frames, Has.Count.EqualTo(score2.Replay.Frames.Count));
            Assert.That(score.Replay.Frames, Has.Count.EqualTo(frames2.Count));
        }

        /// <summary>
        /// CI 可跑：全谱 JSON→Session 能完整出 HitEvents（Visual Drawable≡Session 见 Explicit TestJson_*）。
        /// </summary>
        [Test]
        public void TestAuditChartSessionRunsFromJson()
        {
            runSessionFromAuditJson("Resources/Testing/ReplayJson/Lazer-Hanatachi.json");
            runSessionFromAuditJson("Resources/Testing/ReplayJson/Lazer-PORTRAiT.json");
        }

        /// <summary>
        /// 一次性从嵌入 Lazer osr 写出全谱 ReplayJson（帧为解码 double；整数档来自 osr）。
        /// 本地刷新：去掉 Explicit 后跑本测试，再把 WorkDirectory 产物拷回 Resources/Testing/ReplayJson。
        /// </summary>
        [Test]
        [Explicit("写出辅助：刷新 Hanatachi/PORTRAiT ReplayJson 夹具时手动跑。")]
        public void ExportLazerAuditChartsToReplayJson()
        {
            exportAuditChart(
                "Resources/Testing/Replays/ManiaAudit-Lazer-Hanatachi.osr",
                "Resources/Testing/Beatmaps/ManiaAudit-Hanatachi.osu",
                "Lazer-Hanatachi.json");
            exportAuditChart(
                "Resources/Testing/Replays/ManiaAudit-Lazer-PORTRAiT.osr",
                "Resources/Testing/Beatmaps/ManiaAudit-PORTRAiT.osu",
                "Lazer-PORTRAiT.json");
        }

        private static void assertAuditFixture(string jsonResource, string expectedBeatmapResource)
        {
            var document = ManiaReplayJsonFixture.ReadResource(jsonResource);
            Assert.That(document.BeatmapResource, Is.EqualTo(expectedBeatmapResource));
            Assert.That(document.FrameSource, Is.EqualTo("osr-decoded"));
            Assert.That(document.Frames, Has.Count.GreaterThan(100));
            Assert.That(document.HitMode, Is.EqualTo(nameof(EzEnumHitMode.Lazer)));
        }

        private static void runSessionFromAuditJson(string jsonResource)
        {
            var document = ManiaReplayJsonFixture.ReadResource(jsonResource);
            var (environment, _, _, frames, score) = ManiaReplayJsonFixture.ToParts(document);

            Assert.That(document.BeatmapResource, Is.Not.Null.And.Not.Empty);
            score.Replay.Frames = frames;
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);

            var playable = ManiaReplayJsonAuditLoader.LoadPlayable(document.BeatmapResource!, score);
            var hitEvents = ManiaReplaySession.RunHitEvents(score, playable, environment);
            Assert.That(hitEvents, Has.Count.GreaterThan(100), jsonResource);
        }

        private static void exportAuditChart(string osrResource, string beatmapResource, string fileName)
        {
            var (score, playable, maniaBeatmap) = ManiaReplayJsonAuditLoader.Load(osrResource, beatmapResource);
            int columns = maniaBeatmap?.TotalColumns
                          ?? playable.HitObjects.OfType<IHasColumn>().Select(h => h.Column).DefaultIfEmpty(0).Max() + 1;

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
            var document = ManiaReplayJsonFixture.FromParts(
                environment,
                columns,
                playable.HitObjects.OfType<ManiaHitObject>().ToList(),
                score.Replay.Frames,
                beatmapResource);

            // 优先写回源码 Resources（刷新夹具）；找不到源码树时退到 WorkDirectory。
            string? sourceDir = findSourceReplayJsonDir();
            string outDir = sourceDir ?? Path.Combine(TestContext.CurrentContext.WorkDirectory, "ReplayJsonExport");
            string path = Path.Combine(outDir, fileName);
            ManiaReplayJsonFixture.Write(path, document);
            TestContext.WriteLine($"Wrote {path} frames={document.Frames.Count}");
        }

        private static string? findSourceReplayJsonDir()
        {
            string dir = TestContext.CurrentContext.TestDirectory;

            for (int i = 0; i < 8; i++)
            {
                string candidate = Path.Combine(dir, "Resources", "Testing", "ReplayJson");
                if (Directory.Exists(candidate))
                    return candidate;

                string? parent = Directory.GetParent(dir)?.FullName;
                if (parent == null)
                    break;
                dir = parent;
            }

            // 从程序集路径向上找仓库内 Tests 项目
            dir = Path.GetDirectoryName(typeof(ManiaReplayJsonFixtureTest).Assembly.Location) ?? string.Empty;

            for (int i = 0; i < 10; i++)
            {
                string candidate = Path.Combine(dir, "osu.Game.Rulesets.Mania.Tests", "Resources", "Testing", "ReplayJson");
                if (Directory.Exists(candidate))
                    return candidate;

                string? parent = Directory.GetParent(dir)?.FullName;
                if (parent == null)
                    break;
                dir = parent;
            }

            return null;
        }
    }
}
