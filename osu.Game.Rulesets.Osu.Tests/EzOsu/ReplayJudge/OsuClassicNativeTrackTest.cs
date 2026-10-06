// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    /// <summary>
    /// OSL-013：ClassicNative 轨与 Lazer 在判窗/计分上可区分；Session 经 env.OsuJudgementTrack 切换。
    /// </summary>
    [TestFixture]
    public class OsuClassicNativeTrackTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public void ClassicNativeHitWindowsDifferFromLazerAtOd5()
        {
            var classic = new OsuClassicNativeHitWindows();
            classic.SetDifficulty(5);

            var lazer = new OsuHitWindows();
            lazer.SetDifficulty(5);

            Assert.That(classic.WindowFor(HitResult.Ok), Is.Not.EqualTo(lazer.WindowFor(HitResult.Ok)));
            Assert.That(classic.WindowFor(HitResult.Meh), Is.Not.EqualTo(lazer.WindowFor(HitResult.Meh)));
        }

        [Test]
        public void SessionLateTapDivergesBetweenLazerAndClassicNativeTracks()
        {
            // 各轨独立 beatmap：ClassicNative.ApplyHitWindowsToBeatmap 就地改窗。
            var (scoreLazer, beatmapLazer, envBase) = OsuReplayFixtures.CreateSingleCircleLateTap(90);
            var (scoreClassic, beatmapClassic, _) = OsuReplayFixtures.CreateSingleCircleLateTap(90);

            var envLazer = withTrack((GameplayEnvironment)envBase, EzEnumOsuJudgementTrack.Lazer);
            var envClassic = withTrack((GameplayEnvironment)envBase, EzEnumOsuJudgementTrack.ClassicNative);

            var lazer = OsuReplaySession.Run(scoreLazer, beatmapLazer, envLazer);
            var classic = OsuReplaySession.Run(scoreClassic, beatmapClassic, envClassic);

            Assert.That(count(lazer.ScoreInfo.Statistics, HitResult.Ok), Is.EqualTo(1),
                "OD5 +90ms：Lazer Ok 窗应接住");
            Assert.That(count(classic.ScoreInfo.Statistics, HitResult.Meh), Is.EqualTo(1),
                "OD5 +90ms：ClassicNative Ok 半窗更紧 → Meh");
            Assert.That(count(classic.ScoreInfo.Statistics, HitResult.Ok), Is.EqualTo(0));
            Assert.That(classic.ScoreInfo.TotalScore, Is.Not.EqualTo(lazer.ScoreInfo.TotalScore));
        }

        private static GameplayEnvironment withTrack(GameplayEnvironment env, EzEnumOsuJudgementTrack track)
            => env with { OsuJudgementTrack = track };

        private static int count(IReadOnlyDictionary<HitResult, int> stats, HitResult result)
            => stats.TryGetValue(result, out int n) ? n : 0;
    }
}
