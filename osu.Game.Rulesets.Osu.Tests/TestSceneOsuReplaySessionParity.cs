// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge;
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
        private IBeatmap playableBeatmap = null!;
        private Score replayScore = null!;
        private IGameplayEnvironment parityEnvironment = null!;

        [Test]
        public void TestTwoCircleTapDrawableMatchesSession()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateTwoCircleTap();
            parityEnvironment = environment;
            runDrawableParityTest(beatmap, score.Replay!.Frames);
        }

        [Test]
        public void TestSliderBothKeysDrawableMatchesSession()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateSliderBothKeysTracking();
            parityEnvironment = environment;
            runDrawableParityTest(beatmap, score.Replay!.Frames);
        }

        [Test]
        public void TestSpinnerSingleSpinDrawableMatchesSession()
        {
            var (score, beatmap, environment) = OsuReplayFixtures.CreateSpinnerSingleSpin();
            parityEnvironment = environment;
            runDrawableParityTest(beatmap, score.Replay!.Frames);
        }

        private void runDrawableParityTest(IBeatmap beatmap, IReadOnlyList<ReplayFrame> frames)
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
                    },
                    Replay = new Replay { Frames = new List<ReplayFrame>(frames) },
                };

                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            AddUntilStep("wait for completion", () => currentPlayer.ScoreProcessor.HasCompleted.Value == true);

            AddStep("capture drawable hit events", () =>
            {
                drawableHitEvents = currentPlayer.ScoreProcessor.HitEvents.ToList();
                playableBeatmap = Beatmap.Value.GetPlayableBeatmap(new OsuRuleset().RulesetInfo);
            });

            AddAssert("session hit events match drawable replay path", () =>
            {
                var sessionEvents = OsuReplaySession.RunHitEvents(replayScore, playableBeatmap, parityEnvironment);

                if (OsuReplayParityHelper.AreHitEventsEquivalent(drawableHitEvents, sessionEvents))
                    return true;

                throw new AssertionException(
                    $"drawable=[{OsuReplayParityHelper.DescribeHitEvents(drawableHitEvents)}] session=[{OsuReplayParityHelper.DescribeHitEvents(sessionEvents)}]");
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
