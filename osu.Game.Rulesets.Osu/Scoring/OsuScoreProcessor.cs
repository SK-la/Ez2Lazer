// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public partial class OsuScoreProcessor : ScoreProcessor
    {
        /// <summary>
        /// gameplay 开局冻结的判定轨；空则按 Lazer。禁止在无头路径回读全局配置。
        /// </summary>
        public EzEnumJudgeMode? JudgementTrackOverride { get; set; }

        /// <summary>
        /// [Ez] 单次按键是否允许同时判定多个物件。默认 false，与实机一致（一次输入只落一个物件）。
        /// 仅供 mod 等特殊路径在 Session 内标记；不参与环境解析、不落库、不上传。
        /// </summary>
        public bool AllowMultiJudgementPerPress { get; set; }

        private EzEnumJudgeMode stdJudgeMode => JudgementTrackOverride ?? EzEnumJudgeMode.Lazer;

        private readonly StableStdScoring stableScoring = new StableStdScoring();

        private bool isStableClassic => stdJudgeMode == EzEnumJudgeMode.StableClassic;

        /// <summary>
        /// stable 总分自带 mod 倍率，不能再被下游 <c>scoreMultiplier</c> 乘一次。
        /// </summary>
        protected override bool TotalScoreIsSelfContained => isStableClassic;

        public OsuScoreProcessor()
            : base(new OsuRuleset())
        {
        }

        public override void ApplyEzGameplayEnvironment()
        {
            JudgementTrackOverride ??= GlobalConfigStore.EzConfig.Get<EzEnumJudgeMode>(Ez2Setting.StdJudgeMode);

            if (OsuStableClassicScoring.ShouldUseLegacyScoreAlgorithm(stdJudgeMode))
                IsLegacyScore = true;
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            // 判定窗与 classic 滑条语义必须在全谱模拟（base.ApplyBeatmap 内部的 SimulateAutoplay）前绑定。
            if (isStableClassic)
                OsuStableClassicScoring.BindClassicSemanticsToBeatmap(beatmap);

            base.ApplyBeatmap(beatmap);
        }

        protected override void Reset(bool storeResults)
        {
            // base 先跑一次 updateScore，把全谱模拟出来的 stable 总分留在 MaximumTotalScore 上。
            base.Reset(storeResults);

            stableScoring.Reset(peppyStars, StableStdScoring.ModMultiplierFor(Mods.Value));
        }

        private int peppyStars => Beatmap.Value == null ? 1 : StableStdScoring.PeppyStarsFor(Beatmap.Value);

        /// <summary>
        /// classic 准确率只认圆圈 / 滑条 / 转盘主体：tick 与 bonus 不占分子也不占分母。
        /// </summary>
        public override int GetBaseScoreForResult(HitResult result)
        {
            if (!isStableClassic)
                return base.GetBaseScoreForResult(result);

            switch (result)
            {
                case HitResult.Great:
                case HitResult.Perfect:
                    return 300;

                case HitResult.Ok:
                    return 100;

                case HitResult.Meh:
                    return 50;

                default:
                    return 0;
            }
        }

        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            if (isStableClassic)
                return stableScoring.TotalScore;

            return base.ComputeTotalScore(comboProgress, accuracyProgress, bonusPortion);
        }

        protected override double ComputeTheoreticalPerfectJudgedTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            // stable 的乘区次序无法从帧头/进度反推，退化为按已判定比例线性投影到满分。
            if (isStableClassic)
                return MaximumTotalScore * (double)JudgedHits / Math.Max(1, MaximumJudgements);

            return base.ComputeTheoreticalPerfectJudgedTotalScore(comboProgress, accuracyProgress, bonusPortion);
        }

        public override void ResetFromReplayFrame(ReplayFrame frame)
        {
            // 帧头是同轨（classic 原生）的成绩快照：后退 / 跳帧后必须整体重建累加器，
            // 否则下一次判定会从陈旧状态继续累加，连击与总分都会跳变。
            if (isStableClassic && frame.Header != null)
            {
                stableScoring.RestoreFrom(frame.Header.Combo, frame.Header.MaxCombo, frame.Header.Statistics, frame.Header.TotalScore);

                ClassicBaseScore = stableScoring.AccuracyPoints;
                ClassicMaxBaseScore = stableScoring.JudgedCount * 300;
            }

            base.ResetFromReplayFrame(frame);
        }

        protected override void ApplyScoreChange(JudgementResult result)
        {
            if (!isStableClassic)
                return;

            stableScoring.Apply(result.HitObject, result.Type);
            syncStableState();
        }

        protected override void RemoveScoreChange(JudgementResult result)
        {
            if (!isStableClassic)
                return;

            stableScoring.Revert();
            syncStableState();
        }

        /// <summary>
        /// 连击与 classic 准确率都要按 stable 语义覆盖：lazer 把滑条主体也算一次连击，stable 算在滑条头上。
        /// <c>AccuracyClassic</c> 由 <see cref="ScoreProcessor.UpdateScoreClassic"/> 从这两个字段重算，
        /// 而 base 的 <c>ApplyResultInternal</c> / <c>RevertResultInternal</c> 之后本来就会跑一次，
        /// 故此处不再重复触发（全谱模拟时每物件一次会白跑）。
        /// </summary>
        private void syncStableState()
        {
            Combo.Value = stableScoring.Combo;
            HighestCombo.Value = stableScoring.MaxCombo;

            ClassicBaseScore = stableScoring.AccuracyPoints;
            ClassicMaxBaseScore = stableScoring.JudgedCount * 300;
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results, bool useDefaultCutoffs = false)
        {
            ScoreRank rank = base.RankFromScore(accuracy, results, useDefaultCutoffs);

            switch (rank)
            {
                case ScoreRank.S:
                case ScoreRank.X:
                    if (results.GetValueOrDefault(HitResult.Miss) > 0)
                        rank = ScoreRank.A;
                    break;
            }

            return rank;
        }

        protected override HitEvent CreateHitEvent(JudgementResult result)
            => base.CreateHitEvent(result).With((result as OsuHitCircleJudgementResult)?.CursorPositionAtHit);
    }
}
