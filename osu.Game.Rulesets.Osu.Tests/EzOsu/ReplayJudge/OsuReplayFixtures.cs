// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    internal static class OsuReplayFixtures
    {
        public static (Score score, IBeatmap beatmap, IGameplayEnvironment environment) CreateTwoCircleTap()
        {
            var ruleset = new OsuRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) },
                    new HitCircle { StartTime = 2000, Position = new Vector2(300, 200) },
                },
            };

            var beatmap = prepareBeatmap(ruleset, testBeatmap);
            var circle1 = (HitCircle)beatmap.HitObjects[0];
            var circle2 = (HitCircle)beatmap.HitObjects[1];

            var scoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
            };

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new OsuReplayFrame(1000, circle1.StackedPosition, OsuAction.LeftButton),
                    new OsuReplayFrame(1100, circle1.StackedPosition),
                    new OsuReplayFrame(2000, circle2.StackedPosition, OsuAction.LeftButton),
                },
            };

            var score = new Score { ScoreInfo = scoreInfo, Replay = replay };
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);

            return (score, beatmap, environment);
        }

        /// <summary>
        /// 对齐 <see cref="TestSceneSliderInput.TestPressBothKeysSimultaneouslyAndReleaseOne"/> 的简化 slider tracking 场景。
        /// </summary>
        public static (Score score, IBeatmap beatmap, IGameplayEnvironment environment) CreateSliderBothKeysTracking()
        {
            const double time_slider_start = 1500;
            const double time_during_slide_1 = 2500;
            const float slider_path_length = 25;

            var ruleset = new OsuRuleset();
            var slider = new Slider
            {
                StartTime = time_slider_start,
                Position = new Vector2(0, 0),
                SliderVelocityMultiplier = 0.1f,
                Path = new SliderPath(PathType.PERFECT_CURVE, new[]
                {
                    Vector2.Zero,
                    new Vector2(slider_path_length, 0),
                }, slider_path_length),
            };

            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { slider },
            };

            var beatmap = prepareBeatmap(ruleset, testBeatmap);
            var preparedSlider = (Slider)beatmap.HitObjects[0];
            var head = preparedSlider.NestedHitObjects.OfType<SliderHeadCircle>().Single();

            var scoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
            };

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new OsuReplayFrame(time_slider_start, head.StackedPosition, OsuAction.LeftButton, OsuAction.RightButton),
                    new OsuReplayFrame(time_during_slide_1, head.StackedPosition, OsuAction.RightButton),
                    new OsuReplayFrame(preparedSlider.EndTime + 100, head.StackedPosition),
                },
            };

            var score = new Score { ScoreInfo = scoreInfo, Replay = replay };
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);

            return (score, beatmap, environment);
        }

        /// <summary>
        /// 对齐 <see cref="SpinFramesGenerator"/> 单方向 360° 旋转（1 tick）。
        /// </summary>
        public static (Score score, IBeatmap beatmap, IGameplayEnvironment environment) CreateSpinnerSingleSpin()
        {
            const double time_spinner_start = 1500;
            const double time_spinner_end = 8000;

            var ruleset = new OsuRuleset();
            var spinner = new Spinner
            {
                StartTime = time_spinner_start,
                Duration = time_spinner_end - time_spinner_start,
                Position = new Vector2(256, 192),
            };

            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { spinner },
            };

            var beatmap = prepareBeatmap(ruleset, testBeatmap);

            var frames = new SpinFramesGenerator(time_spinner_start)
                         .Spin(360, 500)
                         .Build();

            var scoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
            };

            var score = new Score { ScoreInfo = scoreInfo, Replay = new Replay { Frames = frames } };
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);

            return (score, beatmap, environment);
        }

        /// <summary>
        /// 单圈迟到 tap：OD5 下约 90ms 晚于起拍 — Lazer 多为 Ok，ClassicNative 多为 Meh（OK 半窗更紧）。
        /// </summary>
        public static (Score score, IBeatmap beatmap, IGameplayEnvironment environment) CreateSingleCircleLateTap(double lateMs = 90)
        {
            var ruleset = new OsuRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) },
                },
            };

            var beatmap = prepareBeatmap(ruleset, testBeatmap);
            var circle = (HitCircle)beatmap.HitObjects[0];

            var scoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
            };

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new OsuReplayFrame(1000 + lateMs, circle.StackedPosition, OsuAction.LeftButton),
                    new OsuReplayFrame(1000 + lateMs + 50, circle.StackedPosition),
                },
            };

            var score = new Score { ScoreInfo = scoreInfo, Replay = replay };
            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);

            return (score, beatmap, environment);
        }

        private static IBeatmap prepareBeatmap(OsuRuleset ruleset, TestBeatmap testBeatmap)
        {
            var beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var beatmapProcessor = ruleset.CreateBeatmapProcessor(beatmap);
            beatmapProcessor.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

            beatmapProcessor.PostProcess();
            return beatmap;
        }
    }
}
