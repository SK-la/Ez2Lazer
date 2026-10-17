// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.IO.Archives;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Scores.IO;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    /// <summary>
    /// 38（已淘汰的 mania Classic）→ StableClassic 数据迁移的契约：
    /// 无 replay 时不得回落到官方算法，可转换时必须幂等且产出与 stable classic Session 同形的行。
    /// </summary>
    [TestFixture]
    public class EzLegacyClassicMigrationTest : ImportTest
    {
        [Test]
        public void TestPinnedMigrationIsIdempotent()
        {
            using var host = new CleanRunHeadlessGameHost();

            try
            {
                var osu = LoadOsuIntoHost(host, withBeatmap: false);

                GlobalConfigStore.EnsureInitialized();

                var scoreManager = osu.Dependencies.Get<ScoreManager>();
                var beatmapManager = osu.Dependencies.Get<BeatmapManager>();
                var replaySession = osu.Dependencies.Get<IEzReplaySession>();

                var ruleset = new ManiaRuleset();
                var (score, playableBeatmap) = createTinyScore(ruleset);

                var importedSet = beatmapManager.Import(new ImportTask(createManiaOsz())).GetResultSafely();
                Assert.That(importedSet, Is.Not.Null);

                var beatmapInfo = importedSet!.PerformRead(set => set.Beatmaps.First(b => b.Ruleset.ShortName == ruleset.RulesetInfo.ShortName).Detach());

                score.ScoreInfo.BeatmapInfo = beatmapInfo;
                score.ScoreInfo.Ruleset = ruleset.RulesetInfo;
                score.ScoreInfo.User = new APIUser { Id = 2303, Username = "legacy_classic_migration" };

                using var replayStream = new MemoryStream();
                new LegacyScoreEncoder(score, playableBeatmap).Encode(replayStream);
                var imported = ImportScoreTest.LoadScoreIntoOsu(osu, score.ScoreInfo, new ByteArrayArchiveReader(replayStream.ToArray(), "replay.osr"));

                osu.Realm.Write(r => r.Find<ScoreInfo>(imported.ID)!.JudgeMode = RealmAccess.LEGACY_CLASSIC_JUDGE_MODE);

                bool deferredApplied = true;
                int deferredJudgeMode = 0;
                bool firstApplied = false;
                bool secondApplied = false;
                int detachedHitEvents = 0;
                string? statisticsJson = null;
                ScoreSnapshot? first = null;
                ScoreSnapshot? second = null;
                bool done = false;
                Exception? error = null;

                host.UpdateThread.Scheduler.Add(() =>
                {
                    try
                    {
                        // 无 replay：地址解析不出来时必须原样保留（下一次启动重试），不能回落官方重算。
                        ScoreInfo deferred = null!;

                        osu.Realm.Write(r =>
                        {
                            var managedBeatmap = r.All<BeatmapInfo>().AsEnumerable().First(b => b.Ruleset.ShortName == ruleset.RulesetInfo.ShortName);

                            r.Add(deferred = new ScoreInfo(ruleset: managedBeatmap.Ruleset, beatmap: managedBeatmap)
                            {
                                JudgeMode = RealmAccess.LEGACY_CLASSIC_JUDGE_MODE,
                                TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION,
                            });
                        });

                        deferredApplied = EzScoreRecalculationService.RecalculateWithPinnedJudgeModeAsync(
                                scoreManager, beatmapManager, replaySession, deferred, EzEnumJudgeMode.StableClassic)
                            .GetResultSafely();

                        deferredJudgeMode = osu.Realm.Run(r =>
                        {
                            r.Refresh();
                            return r.Find<ScoreInfo>(deferred.ID)!.JudgeMode;
                        });

                        var stored = scoreManager.GetScore(imported)!;

                        firstApplied = EzScoreRecalculationService.RecalculateWithPinnedJudgeModeAsync(
                                scoreManager, beatmapManager, replaySession, stored.ScoreInfo, EzEnumJudgeMode.StableClassic)
                            .GetResultSafely();

                        first = snapshot(osu.Realm, imported.ID);
                        detachedHitEvents = stored.ScoreInfo.HitEvents.Count;
                        statisticsJson = osu.Realm.Run(r =>
                        {
                            r.Refresh();
                            return r.Find<ScoreInfo>(imported.ID)!.StatisticsJson;
                        });

                        secondApplied = EzScoreRecalculationService.RecalculateWithPinnedJudgeModeAsync(
                                scoreManager, beatmapManager, replaySession, stored.ScoreInfo, EzEnumJudgeMode.StableClassic)
                            .GetResultSafely();

                        second = snapshot(osu.Realm, imported.ID);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        done = true;
                    }
                });

                waitForOrAssert(() => done, "legacy classic migration test did not complete");

                if (error != null)
                    throw error;

                Assert.Multiple(() =>
                {
                    Assert.That(deferredApplied, Is.False, "无 replay 时必须返回 false，不得回落官方 Recalculate");
                    Assert.That(deferredJudgeMode, Is.EqualTo(RealmAccess.LEGACY_CLASSIC_JUDGE_MODE), "无 replay 的行必须原样保留，作为下次重试的标记");
                });

                Assert.That(firstApplied, Is.True);
                Assert.That(first, Is.Not.Null);

                Assert.Multiple(() =>
                {
                    Assert.That(first!.JudgeMode, Is.EqualTo((int)EzEnumJudgeMode.StableClassic), "迁移后判定档必须落 1");
                    Assert.That(first.HealthMode, Is.EqualTo((int)EzEnumHealthMode.Lazer), "迁移使用规范化环境，血量档应落 lazer");
                    Assert.That(first.TotalScoreVersion, Is.EqualTo(LegacyScoreEncoder.LATEST_VERSION), "重算后应盖到最新分数版本");
                    Assert.That(detachedHitEvents, Is.GreaterThan(0), "迁移产物必须带 HitEvents（Realm 不持久化 HitEvents，只能检查回写后的内存快照）");
                    Assert.That(first.Statistics, Is.Not.Empty, "迁移产物必须带判定统计");
                    Assert.That(statisticsJson, Is.Not.Empty, "StatisticsJson 必须与 Statistics 同步，否则列表重载会读到旧值");
                });

                Assert.That(secondApplied, Is.True, "已迁移行再次重算必须成功（幂等，不依赖 38 的选行条件）");
                Assert.That(second, Is.EqualTo(first), "重复迁移不得改变任何字段");
            }
            finally
            {
                host.Exit();
            }
        }

        private static string createManiaOsz()
        {
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.osz");

            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("sample-beatmap-mania.osu");

                using var source = TestResources.OpenResource("sample-beatmap-mania.osu");
                using var target = entry.Open();
                source.CopyTo(target);
            }

            return path;
        }

        private static ScoreSnapshot snapshot(RealmAccess realm, Guid id) => realm.Run(r =>
        {
            r.Refresh();
            var score = r.Find<ScoreInfo>(id)!;

            return new ScoreSnapshot(
                score.JudgeMode,
                score.HealthMode,
                score.TotalScoreVersion,
                score.IsLegacyScore,
                score.TotalScore,
                score.Accuracy,
                score.MaxCombo,
                string.Join(",", score.Statistics.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")));
        });

        private static (Score score, IBeatmap beatmap) createTinyScore(ManiaRuleset ruleset)
        {
            var hitObjects = new List<HitObject>
            {
                new Note { StartTime = 1000, Column = 0 },
                new Note { StartTime = 2000, Column = 1 },
            };

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = hitObjects,
                ControlPointInfo = new ControlPointInfo(),
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            beatmap.Difficulty.OverallDifficulty = 7;

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key2),
                    new ManiaReplayFrame(2100),
                },
            };

            return (new Score
            {
                ScoreInfo = new ScoreInfo
                {
                    Ruleset = ruleset.RulesetInfo,
                    BeatmapInfo = beatmap.BeatmapInfo,
                    ID = Guid.NewGuid(),
                    Hash = Guid.NewGuid().ToString("N"),
                },
                Replay = replay,
            }, beatmap);
        }

        private static void waitForOrAssert(Func<bool> result, string failureMessage, int timeout = 60000)
        {
            Task task = Task.Run(() =>
            {
                while (!result()) Thread.Sleep(200);
            });

            ClassicAssert.True(task.Wait(timeout), failureMessage);
        }

        private sealed record ScoreSnapshot(
            int JudgeMode,
            int HealthMode,
            int TotalScoreVersion,
            bool IsLegacyScore,
            long TotalScore,
            double Accuracy,
            int MaxCombo,
            string Statistics);
    }
}
