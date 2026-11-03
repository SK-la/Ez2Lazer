// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 叠键 / JudgePrecedence 专用 replay 夹具（POLICY-PARITY）。
    /// </summary>
    internal static class JudgePrecedenceReplayFixtures
    {
        /// <summary>
        /// 同列 80ms 叠键 + 第三次 note 作间隔；第一次按键落在双 note 窗口重叠区（1040ms）。
        /// </summary>
        public static (Score score, IBeatmap beatmap, GameplayEnvironment environment) CreateOverlappingJack(
            EzEnumJudgeMode judgeMode,
            EzEnumHealthMode healthMode,
            EzEnumJudgePrecedence judgePrecedence)
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 1080, Column = 0 },
                    new Note { StartTime = 2500, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1040, ManiaAction.Key1),
                    new ManiaReplayFrame(1200),
                    new ManiaReplayFrame(2500, ManiaAction.Key1),
                    new ManiaReplayFrame(2600),
                    new ManiaReplayFrame(5000),
                },
            };

            var environment = ReplayJudgeTestConfig.Create(judgeMode, healthMode, judgePrecedence);
            var score = createScore(ruleset, replay);
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);
            return (score, beatmap, environment);
        }

        private static Score createScore(ManiaRuleset ruleset, Replay replay) => new Score
        {
            ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo, Mods = Array.Empty<Mod>() },
            Replay = replay,
        };
    }
}
