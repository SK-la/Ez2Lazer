// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings
{
    /// <summary>
    /// osu!stable mania 判定档位（classic）。
    /// 与 lazer 的 <see cref="HitResult"/> 不同：300g / 300 分值不同（320 / 300）。
    /// </summary>
    public enum StableManiaJudgement
    {
        Miss = 0,
        Meh50 = 1,
        Ok100 = 2,
        Good200 = 3,
        Great300 = 4,
        Perfect300g = 5,
    }

    /// <summary>
    /// osu!stable classic 判定 — Mode 原生名 + MapTo；Session 与 Drawable 唯一源。
    /// </summary>
    /// <remarks>
    /// 单键与长条窗由 <see cref="osu.Game.Rulesets.Mania.EzMania.Helper.JudgeModeHelper"/> 按
    /// <see cref="StableManiaWindows"/> 烘焙，本类只做档位映射，不再算 OD / mods。
    /// 单键语义与 lazer 一致（只差窗口），故三个 note 方法全部委派 <see cref="LazerNoteJudgementReplica"/>；
    /// 长条走 <see cref="StableManiaWindows.JudgeLongClassic"/>，整条只出一个判定。
    /// </remarks>
    public sealed class StableClassicJudgeModeJudgement : IManiaJudgeModeJudgement
    {
        public static StableClassicJudgeModeJudgement Instance { get; } = new StableClassicJudgeModeJudgement();

        /// <summary>
        /// 是否走 stable 原生 classic 实现（判定策略选择的唯一判定点）。
        /// </summary>
        public static bool IsSelected(EzEnumJudgeMode judgeMode) => judgeMode == EzEnumJudgeMode.StableClassic;

        /// <summary>stable 档位 → lazer <see cref="HitResult"/>：300g 与 300 分别落在 Perfect / Great。</summary>
        public static HitResult MapTo(StableManiaJudgement judgement) => judgement switch
        {
            StableManiaJudgement.Perfect300g => HitResult.Perfect,
            StableManiaJudgement.Great300 => HitResult.Great,
            StableManiaJudgement.Good200 => HitResult.Good,
            StableManiaJudgement.Ok100 => HitResult.Ok,
            StableManiaJudgement.Meh50 => HitResult.Meh,
            _ => HitResult.Miss,
        };

        /// <summary><see cref="MapTo"/> 的逆映射，供计分侧复用尾判已落定的档位。</summary>
        public static StableManiaJudgement FromHitResult(HitResult result) => result switch
        {
            HitResult.Perfect => StableManiaJudgement.Perfect300g,
            HitResult.Great => StableManiaJudgement.Great300,
            HitResult.Good => StableManiaJudgement.Good200,
            HitResult.Ok => StableManiaJudgement.Ok100,
            HitResult.Meh => StableManiaJudgement.Meh50,
            _ => StableManiaJudgement.Miss,
        };

        public ManiaNoteJudgementOutcome EvaluateAutoMiss(double timeOffset, HitWindows hitWindows)
            => LazerNoteJudgementReplica.Instance.EvaluateAutoMiss(timeOffset, hitWindows);

        public ManiaNoteJudgementOutcome EvaluatePress(double timeOffset, HitWindows hitWindows)
            => LazerNoteJudgementReplica.Instance.EvaluatePress(timeOffset, hitWindows);

        public HitResult RejudgeHitEvent(HitEvent hitEvent, HitWindows hitWindows)
            => LazerNoteJudgementReplica.Instance.RejudgeHitEvent(hitEvent, hitWindows);

        /// <summary>
        /// 长条判定：整条只出一个档位，由 <see cref="HoldTailEvaluationContext.StableClassicState"/> 提供最近按下 / 首次有效松开与断连。
        /// </summary>
        /// <remarks>
        /// 未记录的 <see cref="StableClassicHoldState.TimePress"/> 按 0 传给档位链（早于窗口的按下镜像到段末，即整段没接上）；
        /// 未记录的 <see cref="StableClassicHoldState.TimeRelease"/> 按本次求值时刻传入——按住跨过
        /// <c>EndTime - HitWindow50</c> 后即视为有效松开。
        /// 缺 <see cref="HoldTailEvaluationContext.StableClassicState"/> 时无从取这两个时刻，按 Miss 处理。
        /// </remarks>
        public HitResult EvaluateTail(in HoldTailEvaluationContext context)
        {
            var hitWindows = context.HitWindows;
            var state = context.StableClassicState;

            if (hitWindows == null || ReferenceEquals(hitWindows, HitWindows.Empty) || state == null)
                return HitResult.Miss;

            var windows = StableManiaWindows.FromRanges(
                hitWindows.WindowFor(HitResult.Perfect),
                hitWindows.WindowFor(HitResult.Great),
                hitWindows.WindowFor(HitResult.Good),
                hitWindows.WindowFor(HitResult.Ok),
                hitWindows.WindowFor(HitResult.Meh),
                hitWindows.WindowFor(HitResult.Miss));

            return MapTo(windows.JudgeLongClassic(
                state.StartTime,
                state.EndTime,
                state.TimePress ?? 0,
                state.TimeRelease ?? context.EventTime,
                state.Pressed,
                context.EventTime,
                state.HasHoldBreak));
        }

        public bool CanBeginHoldAt(double time, TailNote tail) => LazerHoldJudgementReplica.Instance.CanBeginHoldAt(time, tail);

        public bool IsHoldBreak(double rawOffset, HitWindows hitWindows) => LazerHoldJudgementReplica.Instance.IsHoldBreak(rawOffset, hitWindows);

        // classic 与 Lazer 同口径：每次提前松手各计一次断连（Ez 自有语义，非上游 ppy 行为）。
        public bool AllowsRepeatedHoldBreak => true;
    }
}
