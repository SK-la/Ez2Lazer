// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 单局 Mania 判定上下文：开局冻结环境、策略与派生标志，热路径禁止再解析全局配置。
    /// </summary>
    public sealed class ManiaJudgementRound
    {
        public GameplayEnvironment Environment { get; }

        public IManiaHitModeJudgement? Strategy { get; }

        public bool PoorEnabled { get; }

        public bool PillModeEnabled { get; }

        public EzEnumJudgePrecedence JudgePrecedence { get; }

        public ManiaReplayJudgementState MutableState { get; }

        public bool IsO2Jam { get; }

        private readonly IBeatmap? beatmap;
        private long o2AutoMissFrame = -1;
        private double o2AutoMissBpm;

        private ManiaJudgementRound(
            GameplayEnvironment environment,
            IManiaHitModeJudgement? strategy,
            bool poorEnabled,
            bool pillModeEnabled,
            ManiaReplayJudgementState mutableState,
            IBeatmap? beatmap)
        {
            Environment = environment;
            Strategy = strategy;
            PoorEnabled = poorEnabled;
            PillModeEnabled = pillModeEnabled;
            JudgePrecedence = environment.JudgePrecedence;
            MutableState = mutableState;
            IsO2Jam = environment.ManiaHitMode == EzEnumHitMode.O2Jam;
            this.beatmap = beatmap;
        }

        public static ManiaJudgementRound Create(GameplayEnvironment environment, IBeatmap? beatmap = null)
        {
            var strategy = ManiaJudgementRegistry.GetHitModeJudgement(environment.ManiaHitMode);
            bool poorEnabled = HealthModeHelper.ComputeKPoorEnabled(environment.ManiaHealthMode, environment.BmsPoorHitResultEnable);
            bool pillModeEnabled = environment.ManiaHealthMode.ToString().Contains("O2Jam");

            return new ManiaJudgementRound(
                environment,
                strategy,
                poorEnabled,
                pillModeEnabled,
                new ManiaReplayJudgementState(),
                beatmap);
        }

        public bool IsEzHitMode => ManiaJudgementRegistry.IsEzHitMode(Environment.ManiaHitMode);

        /// <summary>
        /// 列级按键：缓存本按 press-time BPM（O2 每输入至多查表一次）。
        /// </summary>
        public void NotifyO2InputAt(double time)
        {
            O2PressBpm = resolveO2Bpm(time);
            ManiaJudgeHotPathTrace.RecordO2BpmLookup();
        }

        public double O2PressBpm { get; private set; }

        /// <summary>
        /// 每帧 auto-miss：同帧共享一次 BPM 查表。
        /// </summary>
        public double GetO2BpmForAutoMiss(double time, long frameStableId)
        {
            if (frameStableId != o2AutoMissFrame)
            {
                o2AutoMissBpm = resolveO2Bpm(time);
                o2AutoMissFrame = frameStableId;
            }

            return o2AutoMissBpm;
        }

        public static double GetTotalMultiplier(HitWindows hitWindows)
            => hitWindows is ManiaHitWindows mania ? mania.TotalMultiplier : 1;

        private double resolveO2Bpm(double time)
        {
            if (beatmap != null)
                return O2HitModeExtension.SafeBpm(beatmap.ControlPointInfo.TimingPointAt(time).BPM);

            // Detached compatibility only. Live and Session always provide their beatmap.
            return O2HitModeExtension.GetBPMAtTime(time);
        }
    }
}
