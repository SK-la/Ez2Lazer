// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Drawable replay 路径与 <see cref="OsuReplaySession"/> 的 parity（OSL-010 S4）。
    /// </summary>
    public partial class TestSceneOsuReplaySessionParity : RateAdjustedBeatmapTestScene
    {
        protected override Ruleset CreateRuleset() => new OsuRuleset();

        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private IReadOnlyList<HitEvent> drawableHitEvents = null!;
        private IReadOnlyList<HitEvent> sessionHitEvents = null!;
        private IBeatmap playableBeatmap = null!;
        private Score replayScore = null!;
        private IGameplayEnvironment parityEnvironment = null!;

        [Test]
        public void TestTwoCircleTapDrawableMatchesSession()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();
            parityEnvironment = environment;
            runDrawableParityTest(score, beatmap);
        }

        [Test]
        public void TestSliderBothKeysDrawableMatchesSession()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateSliderBothKeysTracking();
            parityEnvironment = environment;
            runDrawableParityTest(score, beatmap);
        }

        [Test]
        public void TestSpinnerSingleSpinDrawableMatchesSession()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateSpinnerSingleSpin();
            parityEnvironment = environment;
            runDrawableParityTest(score, beatmap);
        }

        [Test]
        public void TestSliderTicksDrawableMatchesSession()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateSliderTrackingWithTicks();
            parityEnvironment = environment;
            runDrawableParityTest(score, beatmap);

            AddAssert("scene really produced ticks",
                () => sessionHitEvents.Count(e => e.HitObject is SliderTick && e.Result.IsHit()) >= 2);
        }

        /// <summary>
        /// 抢拍后圆、按在前圆起拍：lazer 顺序策略放行（输入不早于前圆起拍），CL 顺序策略因前圆未判挡下。
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void TestNoteLockNextCircleAtBlockerStartMatchesSession(bool classicNoteLock)
            => runNoteLockParityTest(classicNoteLock, pressAtBlockerStart: true);

        /// <summary>
        /// 抢拍后圆、按在后圆起拍：两档取舍与上一例相同，只是输入更晚、判定结果不同（Great 对 Miss）。
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void TestNoteLockNextCircleAtTargetStartMatchesSession(bool classicNoteLock)
            => runNoteLockParityTest(classicNoteLock, pressAtBlockerStart: false);

        private void runNoteLockParityTest(bool classicNoteLock, bool pressAtBlockerStart)
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateNoteLockNextCircle(classicNoteLock, pressAtBlockerStart);
            parityEnvironment = withLazerTrack(environment);
            runDrawableParityTest(score, beatmap);

            // 断言场景本身有效：CL 挡下时后圆只能过期成 Miss，lazer 放行时输入真的落在后圆上。
            AddAssert("drawable note lock matches classic switch", () => lateCircleWasBlocked(drawableHitEvents) == classicNoteLock);
            AddAssert("session note lock matches classic switch", () => lateCircleWasBlocked(sessionHitEvents) == classicNoteLock);
        }

        /// <summary>后圆是否被挡下（输入没落上去，只能过期成 Miss）。</summary>
        private static bool lateCircleWasBlocked(IReadOnlyList<HitEvent> hitEvents)
            => OsuReplayParityHelper.OrderedJudgementResults(hitEvents).Last() == HitResult.Miss;

        /// <summary>
        /// 判定轨由 CL 模决定 note lock 策略（与 Drawable 同源），故把轨固定为 Lazer，避免受本机配置影响。
        /// </summary>
        private static IGameplayEnvironment withLazerTrack(IGameplayEnvironment environment)
            => ((GameplayEnvironment)environment) with { JudgeMode = EzEnumJudgeMode.Lazer };

        private void runDrawableParityTest(Score fixtureScore, IBeatmap beatmap)
        {
            AddStep("load player", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(beatmap);

                replayScore = new Score
                {
                    ScoreInfo = new ScoreInfo
                    {
                        Ruleset = new OsuRuleset().RulesetInfo,
                        BeatmapInfo = beatmap.BeatmapInfo,
                        Mods = fixtureScore.ScoreInfo.Mods,
                    },
                    Replay = new Replay { Frames = new List<ReplayFrame>(fixtureScore.Replay!.Frames) },
                };

                SelectedMods.Value = replayScore.ScoreInfo.Mods;
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            AddUntilStep("wait for completion", () => currentPlayer.IsLoaded && currentPlayer.ScoreProcessor.HasCompleted.Value);

            AddStep("capture drawable hit events", () =>
            {
                drawableHitEvents = currentPlayer.ScoreProcessor.HitEvents.ToList();
                playableBeatmap = Beatmap.Value.GetPlayableBeatmap(new OsuRuleset().RulesetInfo);
            });

            AddAssert("session hit events match drawable replay path", () =>
            {
                sessionHitEvents = OsuReplaySession.RunHitEvents(replayScore, playableBeatmap, parityEnvironment);

                if (OsuReplayParityHelper.AreHitEventsEquivalent(drawableHitEvents, sessionHitEvents))
                    return true;

                throw new AssertionException(
                    $"drawable=[{OsuReplayParityHelper.DescribeHitEvents(drawableHitEvents)}] session=[{OsuReplayParityHelper.DescribeHitEvents(sessionHitEvents)}]");
            });
        }

        private partial class ScoreAccessibleReplayPlayer : ReplayPlayer
        {
            public new ScoreProcessor ScoreProcessor => base.ScoreProcessor;

            protected override bool PauseOnFocusLost => false;

            public ScoreAccessibleReplayPlayer(Score score)
                : base(score, new PlayerConfiguration
                {
                    AllowPause = false,
                    ShowResults = false,
                })
            {
            }
        }
    }
}
