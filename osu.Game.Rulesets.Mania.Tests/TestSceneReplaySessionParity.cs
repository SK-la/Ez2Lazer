// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests
{
    /// <summary>
    /// Drawable replay 路径与 <see cref="ManiaReplaySession"/> 的 parity（Lazer + Ez HitMode）。
    /// </summary>
    public partial class TestSceneReplaySessionParity : RateAdjustedBeatmapTestScene
    {
        protected override Ruleset CreateRuleset() => new ManiaRuleset();

        private ScoreAccessibleReplayPlayer currentPlayer = null!;
        private IReadOnlyList<HitEvent> drawableHitEvents = null!;
        private IBeatmap playableBeatmap = null!;
        private Score replayScore = null!;
        private ManiaGameplayEnvironment parityEnvironment = null!;

        [TearDown]
        public void TearDown()
        {
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.BmsPoorHitResultEnable, false);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHitMode, EzEnumHitMode.Lazer);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHealthMode, EzEnumHealthMode.Lazer);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.JudgePrecedence, EzEnumJudgePrecedence.Earliest);
        }

        [Test]
        public void TestLazerTapDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(900, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(1900, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                });
        }

        [Test]
        public void TestLazerHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);

            const double head = 1500;
            const double tail = 4000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote
                    {
                        StartTime = head,
                        Duration = tail - head,
                        Column = 0,
                    },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        [Test]
        public void TestIidxTapDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                });
        }

        [Test]
        public void TestIidxHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD);

            const double head = 1500;
            const double tail = 4000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        [Test]
        public void TestO2TapDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                });
        }

        [Test]
        public void TestMalodyHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.Malody_E, EzEnumHealthMode.Lazer);

            const double head = 1500;
            const double tail = 4000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        [Test]
        public void TestO2HoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);

            const double head = 1500;
            const double tail = 4000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        [Test]
        public void TestO2PillDrawableMatchesSession()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2PillUpgradeOnBadRange();
            parityEnvironment = environment;

            runDrawableParityTest(
                beatmap.HitObjects.Cast<ManiaHitObject>().ToList(),
                score.Replay.Frames);
        }

        [Test]
        public void TestEz2AcHoldDrawableMatchesSession()
        {
            parityEnvironment = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            const double head = 1000;
            const double tail = 2000;

            runDrawableParityTest(
                new List<ManiaHitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                });
        }

        private void runDrawableParityTest(List<ManiaHitObject> hitObjects, List<ReplayFrame> frames)
        {
            AddStep("configure environment", () => ReplayJudgeTestConfig.ApplyToGlobalConfig(parityEnvironment));

            AddStep("load player", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new ManiaBeatmap(new StageDefinition(4))
                {
                    HitObjects = hitObjects,
                    BeatmapInfo =
                    {
                        Ruleset = new ManiaRuleset().RulesetInfo,
                    },
                });

                Beatmap.Value.Beatmap.ControlPointInfo.Add(0, new EffectControlPoint { ScrollSpeed = 0.1f });

                replayScore = new Score { Replay = new Replay { Frames = frames } };
                LoadScreen(currentPlayer = new ScoreAccessibleReplayPlayer(replayScore));
            });

            AddUntilStep("wait for completion", () => currentPlayer?.ScoreProcessor?.HasCompleted.Value == true);

            AddStep("capture drawable hit events", () =>
            {
                drawableHitEvents = currentPlayer.ScoreProcessor.HitEvents.ToList();
                playableBeatmap = Beatmap.Value.GetPlayableBeatmap(new ManiaRuleset().RulesetInfo);
            });

            AddAssert("session hit events match drawable replay path", () =>
            {
                var sessionEvents = ManiaReplaySession.RunHitEvents(replayScore, playableBeatmap, parityEnvironment);

                if (ManiaReplayParityHelper.AreHitEventsEquivalent(drawableHitEvents, sessionEvents))
                    return true;

                throw new AssertionException(
                    $"drawable=[{ManiaReplayParityHelper.DescribeHitEvents(drawableHitEvents)}] session=[{ManiaReplayParityHelper.DescribeHitEvents(sessionEvents)}]");
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
