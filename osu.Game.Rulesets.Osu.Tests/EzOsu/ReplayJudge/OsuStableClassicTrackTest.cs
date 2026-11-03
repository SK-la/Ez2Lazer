// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    /// <summary>
    /// OSL-013：StableClassic 轨与 Lazer 在判窗/计分上可区分；Session 经 env.JudgeMode 切换。
    /// </summary>
    [TestFixture]
    public class OsuStableClassicTrackTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public void StableHitWindowsFollowStableOdFormula()
        {
            var stable = new StableStdHitWindows();
            stable.SetDifficulty(5);

            // stable 半窗：300 = 80-6*OD，100 = 140-8*OD，50 = 200-10*OD，miss 固定 400。
            Assert.That(stable.WindowFor(HitResult.Great), Is.EqualTo(50));
            Assert.That(stable.WindowFor(HitResult.Ok), Is.EqualTo(100));
            Assert.That(stable.WindowFor(HitResult.Meh), Is.EqualTo(150));
            Assert.That(stable.WindowFor(HitResult.Miss), Is.EqualTo(400));

            // 边界取严格小于：恰好压线落到下一档。
            Assert.That(stable.ResultFor(49.9), Is.EqualTo(HitResult.Great));
            Assert.That(stable.ResultFor(50), Is.EqualTo(HitResult.Ok));
            Assert.That(stable.ResultFor(150), Is.EqualTo(HitResult.Miss));
            Assert.That(stable.ResultFor(400), Is.EqualTo(HitResult.Miss));
            Assert.That(stable.ResultFor(400.1), Is.EqualTo(HitResult.None));
        }

        [Test]
        public void SessionLateTapDivergesBetweenLazerAndClassicTracks()
        {
            // 各轨独立 beatmap：OsuStableClassic.BindSemantics 就地改窗。
            var (scoreLazer, beatmapLazer, envBase) = OsuReplayFixtures.CreateSingleCircleLateTap(90);
            var (scoreClassic, beatmapClassic, _) = OsuReplayFixtures.CreateSingleCircleLateTap(90);

            var envLazer = withTrack((GameplayEnvironment)envBase, EzEnumJudgeMode.Lazer);
            var envClassic = withTrack((GameplayEnvironment)envBase, EzEnumJudgeMode.StableClassic);

            var lazer = OsuReplaySession.Run(scoreLazer, beatmapLazer, envLazer);
            var classic = OsuReplaySession.Run(scoreClassic, beatmapClassic, envClassic);

            // stable 判定窗与 lazer 同式（OD5 +90ms 落 Ok 窗）；两轨的分野在计分：
            Assert.That(count(lazer.ScoreInfo.Statistics, HitResult.Ok), Is.EqualTo(1));
            Assert.That(count(classic.ScoreInfo.Statistics, HitResult.Ok), Is.EqualTo(1));

            // lazer 走 500k/500k 标准化曲线，stable 轨走纯 PeppyStars 累加，两者不该相等。
            Assert.That(classic.ScoreInfo.TotalScore, Is.LessThan(lazer.ScoreInfo.TotalScore));
        }

        private static GameplayEnvironment withTrack(GameplayEnvironment env, EzEnumJudgeMode track)
            => env with { JudgeMode = track };

        private static int count(IReadOnlyDictionary<HitResult, int> stats, HitResult result)
            => stats.TryGetValue(result, out int n) ? n : 0;
    }
}
