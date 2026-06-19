// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    internal static class HitModeReplayFixtures
    {
        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateBmsEarlyBadWithPostBadKPoor()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 5000, Column = 0 },
                },
                ControlPointInfo = createTiming120(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            beatmap.Difficulty.OverallDifficulty = 5;

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(850, ManiaAction.Key1),
                    new ManiaReplayFrame(900),
                    new ManiaReplayFrame(1180, ManiaAction.Key1),
                    new ManiaReplayFrame(1300),
                },
            };

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD, bmsPoorHitResultEnable: true);

            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            return (createScore(ruleset, replay), beatmap, environment);
        }

        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateO2TwoNoteTap()
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
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                },
            };

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);

            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            return (createScore(ruleset, replay), beatmap, environment);
        }

        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateEz2AcHoldHeadPerfect()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HoldNote { StartTime = 1000, Duration = 1000, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(2000),
                },
            };

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            return (createScore(ruleset, replay), beatmap, environment);
        }

        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateEz2AcManyNoteTap(int noteCount = 20)
        {
            var ruleset = new ManiaRuleset();
            var hitObjects = new List<HitObject>();
            var frames = new List<ReplayFrame>();

            for (int i = 0; i < noteCount; i++)
            {
                double time = 1000 + i * 500;
                hitObjects.Add(new Note { StartTime = time, Column = 0 });
                frames.Add(new ManiaReplayFrame(time, ManiaAction.Key1));
                frames.Add(new ManiaReplayFrame(time + 50));
            }

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = hitObjects,
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay { Frames = frames };

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            return (createScore(ruleset, replay), beatmap, environment);
        }

        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateMalodyHoldPerfect(EzEnumHitMode hitMode = EzEnumHitMode.Malody_E)
        {
            const double head = 1500;
            const double tail = 4000;

            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
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

            var environment = createMalodyEnvironment(hitMode);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            return (createScore(ruleset, replay), beatmap, environment);
        }

        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateMalodyHoldEarlyRelease(EzEnumHitMode hitMode = EzEnumHitMode.Malody_E)
        {
            const double head = 1500;
            const double tail = 4000;
            const double early_release = 2500;

            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head, ManiaAction.Key1),
                    new ManiaReplayFrame(early_release),
                },
            };

            var environment = createMalodyEnvironment(hitMode);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            return (createScore(ruleset, replay), beatmap, environment);
        }

        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateO2HoldPerfect()
        {
            const double head = 1500;
            const double tail = 4000;

            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
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

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);

            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            return (createScore(ruleset, replay), beatmap, environment);
        }

        public static BmsJudge ToBmsJudge(HitResult result) => BmsHitModeJudgement.FromHitResult(result);

        public static O2Judge ToO2Judge(HitResult result) => O2HitModeJudgement.FromHitResult(result);

        public static Ez2AcJudge ToEz2AcJudge(HitResult result) => Ez2AcHitModeJudgement.FromHitResult(result);

        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateBmsEarlyKPoorPress()
        {
            double pressOffset = findBmsEarlyKPoorPressOffset();

            if (pressOffset == 0)
                throw new InvalidOperationException("IIDX early KPoor window not available for fixture");

            const double note_time = 1000;
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { new Note { StartTime = note_time, Column = 0 } },
                ControlPointInfo = createTiming120(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            beatmap.Difficulty.OverallDifficulty = 5;

            var environment = createIidxEnvironment();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(note_time + pressOffset, ManiaAction.Key1),
                    new ManiaReplayFrame(note_time + 200),
                },
            };

            return (createScore(ruleset, replay), beatmap, environment);
        }

        /// <summary>
        /// 15 连 Cool 后第 16 颗在 pill Bad 带应被 pill 升为 Cool。
        /// </summary>
        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateO2PillUpgradeOnBadRange()
        {
            const double spacing = 400;
            const double first = 1000;
            const int cool_streak = 15;
            const double bpm = 120;
            const double pill_note_time = first + cool_streak * spacing;
            double pillHitOffset = findO2PillConsumingOffset(bpm);

            var ruleset = new ManiaRuleset();
            var hitObjects = new List<HitObject>();

            for (int i = 0; i <= cool_streak; i++)
                hitObjects.Add(new Note { StartTime = first + i * spacing, Column = 0 });

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = hitObjects,
                ControlPointInfo = createTiming120(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var frames = new List<ReplayFrame>();

            for (int i = 0; i < cool_streak; i++)
            {
                double t = first + i * spacing;
                frames.Add(new ManiaReplayFrame(t, ManiaAction.Key1));
                frames.Add(new ManiaReplayFrame(t + 50));
            }

            frames.Add(new ManiaReplayFrame(pill_note_time + pillHitOffset, ManiaAction.Key1));
            frames.Add(new ManiaReplayFrame(pill_note_time + pillHitOffset + 50));

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);

            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);
            return (createScore(ruleset, new Replay { Frames = frames }), beatmap, environment);
        }

        /// <summary>
        /// LN 头在 Great 窗口应经 SoftenLnJudge 升为 Perfect。
        /// </summary>
        public static (Score score, IBeatmap beatmap, ManiaGameplayEnvironment environment) CreateEz2AcHoldHeadGreatSoftened()
        {
            var ruleset = new ManiaRuleset();
            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            ReplayJudgeTestConfig.ApplyToGlobalConfig(environment);

            double greatOffset = findFirstOffsetWithResult(EzEnumHitMode.EZ2AC, HitResult.Great);

            if (greatOffset == 0)
                throw new InvalidOperationException("EZ2AC Great window not found for fixture");

            const double head = 2000;
            const double tail = 3500;

            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HoldNote { StartTime = head, Duration = tail - head, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(head + greatOffset, ManiaAction.Key1),
                    new ManiaReplayFrame(tail),
                },
            };

            return (createScore(ruleset, replay), beatmap, environment);
        }

        private static double findFirstOffsetWithResult(EzEnumHitMode hitMode, HitResult desired)
        {
            var windows = new ManiaHitWindows(hitMode);

            for (double offset = 1; offset <= 250; offset++)
            {
                if (windows.ResultFor(offset) == desired)
                    return offset;

                if (windows.ResultFor(-offset) == desired)
                    return -offset;
            }

            return 0;
        }

        private static double findO2PillConsumingOffset(double bpm)
        {
            double goodRange = O2HitModeExtension.BASE_GOOD / bpm;
            double badRange = O2HitModeExtension.BASE_BAD / bpm;
            var windows = new ManiaHitWindows(EzEnumHitMode.O2Jam);

            for (double offset = goodRange + 1; offset <= badRange; offset++)
            {
                if (windows.ResultFor(offset) != HitResult.None)
                    return offset;
            }

            throw new InvalidOperationException("No hittable O2 pill bad-range offset found");
        }

        private static double findBmsEarlyKPoorPressOffset()
        {
            var windows = new ManiaHitWindows(EzEnumHitMode.IIDX_HD);
            double badEarly = BmsHitModeJudgement.WindowFor(windows, BmsJudge.Bad, true);
            double kPoorEarly = BmsHitModeJudgement.WindowFor(windows, BmsJudge.KPoor, true);

            if (kPoorEarly <= badEarly)
                return 0;

            return -(badEarly + (kPoorEarly - badEarly) * 0.5);
        }

        private static ManiaGameplayEnvironment createIidxEnvironment() => ReplayJudgeTestConfig.Create(
            EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD, bmsPoorHitResultEnable: true);

        private static ManiaGameplayEnvironment createMalodyEnvironment(EzEnumHitMode hitMode) => ReplayJudgeTestConfig.Create(hitMode, EzEnumHealthMode.Lazer);

        private static ControlPointInfo createTiming120()
        {
            var cpi = new ControlPointInfo();
            cpi.Add(0, new TimingControlPoint { BeatLength = 500 });
            return cpi;
        }

        private static Score createScore(ManiaRuleset ruleset, Replay replay) => new Score
        {
            ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo, Mods = Array.Empty<Mod>() },
            Replay = replay,
        };
    }
}
