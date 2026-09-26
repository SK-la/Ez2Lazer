// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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
    internal static class LazerTapReplayFixtures
    {
        public static (Score score, IBeatmap beatmap, GameplayEnvironment environment) CreateTwoNoteColumnTap()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(900, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(1900, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                },
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        public static (Score score, IBeatmap beatmap, GameplayEnvironment environment) CreateSingleHoldPerfect()
        {
            const double head = 1500;
            const double tail = 4000;

            var ruleset = new ManiaRuleset();
            var hold = new HoldNote
            {
                StartTime = head,
                Duration = tail - head,
                Column = 0,
            };

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { hold },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                },
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        public static (Score score, IBeatmap beatmap, GameplayEnvironment environment) CreateSingleHoldLateTailRelease(int lateMs = 30)
        {
            const double head = 1500;
            const double tail = 4000;

            var ruleset = new ManiaRuleset();
            var hold = new HoldNote
            {
                StartTime = head,
                Duration = tail - head,
                Column = 0,
            };

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { hold },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(tail + lateMs),
                },
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        public static (Score score, IBeatmap beatmap, GameplayEnvironment environment) CreateSingleHoldLateRelease()
        {
            const double head = 1500;
            const double tail = 4000;
            const double after_tail = 5250;

            var ruleset = new ManiaRuleset();
            var hold = new HoldNote
            {
                StartTime = head,
                Duration = tail - head,
                Column = 0,
            };

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { hold },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(after_tail),
                },
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        /// <summary>
        /// 头命中后于 <paramref name="releaseTime"/> 松手（可早于或晚于尾），用于 Lazer / Classic 对账
        /// 「中途松手断连 + 尾窗内松手仍可松判」。
        /// </summary>
        public static (Score score, IBeatmap beatmap, GameplayEnvironment environment) CreateSingleHoldReleaseAt(double releaseTime)
        {
            const double head = 1500;
            const double tail = 4000;

            var ruleset = new ManiaRuleset();
            var hold = new HoldNote
            {
                StartTime = head,
                Duration = tail - head,
                Column = 0,
            };

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { hold },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(releaseTime),
                },
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        /// <summary>
        /// 单条 LN，按给定的 (时间, 是否按下) 序列生成输入，用于复现「断连后重按再松手」等组合。
        /// </summary>
        public static (Score score, IBeatmap beatmap, GameplayEnvironment environment) CreateSingleHoldWithInputs(params (double time, bool press)[] inputs)
        {
            const double head = 1500;
            const double tail = 4000;

            var ruleset = new ManiaRuleset();
            var hold = new HoldNote
            {
                StartTime = head,
                Duration = tail - head,
                Column = 0,
            };

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { hold },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = inputs
                         .Select(i => (ReplayFrame)(i.press ? new ManiaReplayFrame(i.time, ManiaAction.Key1) : new ManiaReplayFrame(i.time)))
                         .ToList(),
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        private static GameplayEnvironment createEnvironment() => ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);

        private static Score createScore(ManiaRuleset ruleset, Replay replay) => new Score
        {
            ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo, Mods = Array.Empty<Mod>() },
            Replay = replay,
        };
    }
}
