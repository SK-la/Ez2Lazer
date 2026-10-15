// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Scoring
{
    public partial class ManiaScoreProcessor : ScoreProcessor
    {
        private const double combo_base = 4;
        private double calOD = 8;

        /// <summary>
        /// 显式注入的 JudgeMode（gameplay 开局冻结 / Session / Ghost 时间线）。
        /// 为空时按 <see cref="EzEnumJudgeMode.Lazer"/> 官方语义计算。
        /// </summary>
        /// <remarks>
        /// 禁止在此回读全局配置：无头路径（官方 legacy 转换、后台升级、重算、PP/难度缓存等）
        /// 必须保持 ppy 上游行为，否则会被玩家当前 JudgeMode 污染（曾导致 stable 导入成绩被错转为 D + 超低分）。
        /// gameplay 侧由 <see cref="ApplyEzGameplayEnvironment"/> 在 <see cref="ScoreProcessor.ApplyBeatmap"/> 前冻结注入。
        /// </remarks>
        public EzEnumJudgeMode? JudgeModeOverride { get; set; }

        private EzEnumJudgeMode judgeMode => JudgeModeOverride ?? EzEnumJudgeMode.Lazer;

        private readonly StableManiaScoring stableScoring = new StableManiaScoring();

        private bool isStableClassic => StableClassicJudgeModeJudgement.IsSelected(judgeMode);

        /// <summary>
        /// stable classic 长条持有期的连击增量（<c>HitCircleManiaLong.Holding</c> 的
        /// <c>IncreaseScoreType</c> 7）。
        /// </summary>
        /// <remarks>
        /// 只推 <see cref="ScoreProcessor.Combo"/> 与 <see cref="ScoreProcessor.HighestCombo"/>：
        /// stable 该分支不给判定计数、不加分、不进 <c>HitEvents</c>，因此
        /// <c>ScoreResultCounts</c> / acc / rank / <c>MaximumStatistics</c> 均不受影响。
        /// 增量数取决于实际按住时长（无法从谱面枚举），故也不进 <c>MaximumCombo</c>。
        /// </remarks>
        internal void ApplyClassicHoldCombo()
        {
            if (IsSimulating)
                return;

            Combo.Value++;
            HighestCombo.Value = Math.Max(HighestCombo.Value, Combo.Value);
        }

        protected override bool TotalScoreIsSelfContained => isStableClassic;

        private StableManiaWindows stableWindows => StableManiaWindows.Create(
            calOD,
            StableManiaScoring.WindowMultiplier(Mods.Value),
            Beatmap.Value?.BeatmapInfo.Ruleset.OnlineID != 3);

        private double stableScoreMultiplier()
        {
            var beatmap = Beatmap.Value;

            if (beatmap == null)
                return 1;

            int noteCount = 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is Note || hitObject is HoldNote)
                    noteCount++;
            }

            if (noteCount <= 0)
                return 1;

            return (double)StableManiaScoring.ScoreMax / noteCount * StableManiaScoring.ModMultiplier(Mods.Value);
        }

        /// <summary>
        /// gameplay 入口（Player / 皮肤编辑器预览）冻结当前全局 JudgeMode。
        /// 必须在 <see cref="ScoreProcessor.ApplyBeatmap"/> 之前调用，否则全谱模拟与实际判定权重不一致。
        /// </summary>
        public override void ApplyEzGameplayEnvironment()
        {
            JudgeModeOverride ??= GlobalConfigStore.EzConfig.Get<EzEnumJudgeMode>(Ez2Setting.ManiaJudgeMode);
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            // Tail/Tick 的 Ignore 系 Judgement 必须在全谱模拟前绑定，否则 EZ2AC/Malody 会把官方尾 Perfect 计入 MaximumBaseScore。
            ManiaBeatmapBinding.BindJudgements(beatmap, judgeMode);
            base.ApplyBeatmap(beatmap);
        }

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);
            stableScoring.Reset(stableScoreMultiplier());
        }

        public override void ResetFromReplayFrame(ReplayFrame frame)
        {
            // 帧头是同轨（classic 原生）的成绩快照：后退 / 跳帧后必须整体重建累加器，
            // 否则下一次判定会从陈旧状态继续累加，总分与 classic 准确率都会跳变。
            if (isStableClassic && frame.Header != null)
            {
                stableScoring.RestoreFrom(frame.Header.TotalScore);

                if (frame.Header.Statistics != null)
                    restoreClassicBaseScore(frame.Header.Statistics);
            }

            base.ResetFromReplayFrame(frame);
        }

        /// <summary>
        /// 帧头只带判定计数，按 classic 档位把 <see cref="ScoreProcessor.ClassicBaseScore"/> /
        /// <see cref="ScoreProcessor.ClassicMaxBaseScore"/> 反推回同一量纲（长条尾按 Perfect 档估），
        /// 使回退后的 <see cref="ScoreProcessor.AccuracyClassic"/> 不致停在跳帧前的旧值。
        /// </summary>
        private void restoreClassicBaseScore(IReadOnlyDictionary<HitResult, int> statistics)
        {
            int perfect = JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, HitResult.Perfect);
            int judged = 0;
            long baseScore = 0;

            foreach (var (result, count) in statistics)
            {
                judged += count;
                baseScore += (long)count * JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, result);
            }

            ClassicMaxBaseScore = (double)judged * perfect;
            ClassicBaseScore = baseScore;
        }

        public ManiaScoreProcessor()
            : base(new ManiaRuleset())
        {
        }

        protected override IEnumerable<HitObject> EnumerateHitObjects(IBeatmap beatmap)
        {
            calOD = Mods.Value.OfType<ManiaModAdjust>().FirstOrDefault(m => m.OverallDifficulty.Value != beatmap.Difficulty.OverallDifficulty)
                        ?.OverallDifficulty.Value ?? beatmap.Difficulty.OverallDifficulty;

            return base.EnumerateHitObjects(beatmap).Order(JudgementOrderComparer.DEFAULT);
        }

        // TODO(Ez 计分 v2)：Ez 模式目前仍是 EX 占比（1_000_000 * MinimumAccuracy），与 acc 显示重复，
        // 且不携带 combo / 难度信息。换版时要引入难度 / 密度表现：
        //  - 同为 miss，稀疏段的 miss 与全谱最难段的 miss 价值应不同；
        //  - combo 加成随该处 note 密度递减：密度越低，combo 项贡献越小，避免稀疏段白拿 combo 分。
        // 需要每个物件或时间窗的局部密度 / 难度权重（可基于现有 strain 分段或 Ez 自己的密度统计），
        // 并保证 live、Session、成绩图三方共用同一实现。
        // 换版必须同时给存量成绩定版本策略：算法一改，历史成绩与重算结果就会对不齐。
        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            // stable 原生 classic：总分由 ComboAddition 状态机逐物件累加，不再走 combo/acc 曲线。
            if (isStableClassic)
                return StableManiaScoring.Finalize(stableScoring.TotalScoreDouble);

            if (IsLegacyScore || judgeMode == EzEnumJudgeMode.Classic)
            {
                return 150000 * comboProgress
                       + 850000 * Math.Pow(AccuracyClassic.Value, 2 + 2 * AccuracyClassic.Value) * accuracyProgress
                       + bonusPortion;
            }

            if (judgeMode != EzEnumJudgeMode.Lazer && judgeMode != EzEnumJudgeMode.Classic)
            {
                // 满分制：TotalScore = 已获得 ex / 整谱最大 ex，从 0 随判定累积（非已判子集 acc 反算）
                return 1_000_000 * MinimumAccuracy.Value;
            }

            return 150000 * comboProgress
                   + 850000 * Math.Pow(Accuracy.Value, 2 + 2 * Accuracy.Value) * accuracyProgress
                   + bonusPortion;
        }

        protected override double ComputeTheoreticalPerfectJudgedTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            if (isStableClassic)
                return MAX_SCORE * StableManiaScoring.ModMultiplier(Mods.Value);

            if (IsLegacyScore || judgeMode == EzEnumJudgeMode.Classic)
            {
                return 150000 * comboProgress
                       + 850000 * accuracyProgress
                       + bonusPortion;
            }

            if (judgeMode != EzEnumJudgeMode.Lazer && judgeMode != EzEnumJudgeMode.Classic)
            {
                var stats = GetScoreProcessorStatistics();
                double maximumBaseScoreOnChart = MinimumAccuracy.Value > 0 ? stats.BaseScore / MinimumAccuracy.Value : 0;

                if (maximumBaseScoreOnChart <= 0)
                    return 0;

                return MAX_SCORE * stats.MaximumBaseScore / maximumBaseScoreOnChart;
            }

            return 150000 * comboProgress
                   + 850000 * accuracyProgress
                   + bonusPortion;
        }

        protected override double GetComboScoreChange(JudgementResult result)
        {
            return getBaseComboScoreForResult(result.Type) * Math.Min(Math.Max(0.5, Math.Log(result.ComboAfterJudgement, combo_base)), Math.Log(400, combo_base));
        }

        public override int GetBaseScoreForResult(HitResult result)
        {
            if (judgeMode != EzEnumJudgeMode.Lazer)
                return JudgeModeHelper.GetBaseScoreForResult(judgeMode, result);

            switch (result)
            {
                case HitResult.Perfect:
                    return 305;
            }

            return base.GetBaseScoreForResult(result);
        }

        private int getBaseComboScoreForResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return 300;
            }

            return GetBaseScoreForResult(result);
        }

        public override void PopulateScore(ScoreInfo score)
        {
            base.PopulateScore(score);

            if (!isStableClassic)
                return;

            // stable classic 的 acc 分母是「单键数 + 长条数」（一条长条只占一个 300 槽），rank 也按这条 acc 分档。
            // lazer 的 Accuracy 仍把长条头算进分母（头不落判定却占 MaxResult 槽），会虚高，故 classic 整体改用 AccuracyClassic。
            score.Accuracy = AccuracyClassic.Value;
            score.Rank = RankFromScore(AccuracyClassic.Value, ScoreResultCounts);
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results, bool useDefaultCutoffs = false)
        {
            ScoreRank rank = base.RankFromScore(accuracy, results, useDefaultCutoffs);

            if (rank != ScoreRank.S)
                return rank;

            // SS is expected as long as all hitobjects have been hit with either a GREAT or PERFECT result.

            bool anyImperfect = results.GetValueOrDefault(HitResult.Good) > 0
                                || results.GetValueOrDefault(HitResult.Ok) > 0
                                || results.GetValueOrDefault(HitResult.Meh) > 0
                                || results.GetValueOrDefault(HitResult.Miss) > 0;

            return anyImperfect ? rank : ScoreRank.X;
        }

        private class JudgementOrderComparer : IComparer<HitObject>
        {
            public static readonly JudgementOrderComparer DEFAULT = new JudgementOrderComparer();

            public int Compare(HitObject? x, HitObject? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (ReferenceEquals(x, null)) return -1;
                if (ReferenceEquals(y, null)) return 1;

                int result = x.GetEndTime().CompareTo(y.GetEndTime());
                if (result != 0)
                    return result;

                // due to the way input is handled in mania, notes take precedence over ticks in judging order.
                if (x is Note && y is not Note) return -1;
                if (x is not Note && y is Note) return 1;

                return x is ManiaHitObject maniaX && y is ManiaHitObject maniaY
                    ? maniaX.Column.CompareTo(maniaY.Column)
                    : 0;
            }
        }

        protected override void ApplyScoreChange(JudgementResult judgement)
        {
            var hitWindows = new JudgeModeHelper(EzEnumJudgeMode.Classic)
            {
                OverallDifficulty = calOD
            };
            double offset = Math.Abs(judgement.TimeOffset);
            var result = hitWindows.ResultForClassic(offset);
            var hitObject = (ManiaHitObject)judgement.HitObject;

            if (hitObject is HeadNote)
            {
                headOffsets[hitObject.Column] = offset;
            }
            else if (hitObject is TailNote)
            {
                ClassicMaxBaseScore += JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, HitResult.Perfect);

                if (isStableClassic)
                {
                    // 尾已经带着 stable classic 档位落地（StableClassicJudgeModeJudgement），计分侧直接复用，
                    // 否则统计、acc、总分三处会各算一遍、又各自分叉。
                    ClassicBaseScore += JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, judgement.Type);
                    stableScoring.Apply(StableClassicJudgeModeJudgement.FromHitResult(judgement.Type), judgement.ComboAtJudgement);
                }
                else
                {
                    ClassicBaseScore += hitWindows.GetClassicLNScore(headOffsets[hitObject.Column], offset);
                }

                headOffsets[hitObject.Column] = 0;
            }
            else if (hitObject is Note)
            {
                ClassicMaxBaseScore += JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, HitResult.Perfect);
                ClassicBaseScore += JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, result);

                if (isStableClassic)
                    stableScoring.Apply(stableWindows.Judge(offset), judgement.ComboAtJudgement);
            }

            UpdateScoreClassic();
        }

        private readonly double[] headOffsets = new double[18];

        protected override void RemoveScoreChange(JudgementResult judgement)
        {
            var hitWindows = new JudgeModeHelper(EzEnumJudgeMode.Classic)
            {
                OverallDifficulty = calOD
            };
            double offset = Math.Abs(judgement.TimeOffset);
            var result = hitWindows.ResultForClassic(offset);
            var hitObject = (ManiaHitObject)judgement.HitObject;

            if (hitObject is HeadNote)
            {
                headOffsets[hitObject.Column] = offset;
            }
            else if (hitObject is TailNote)
            {
                ClassicMaxBaseScore -= JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, HitResult.Perfect);

                if (isStableClassic)
                {
                    ClassicBaseScore -= JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, judgement.Type);
                    stableScoring.Revert();
                }
                else
                {
                    ClassicBaseScore -= hitWindows.GetClassicLNScore(headOffsets[hitObject.Column], offset);
                }

                headOffsets[hitObject.Column] = 0;
            }
            else if (hitObject is Note)
            {
                ClassicMaxBaseScore -= JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, HitResult.Perfect);
                ClassicBaseScore -= JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.Classic, result);

                if (isStableClassic)
                    stableScoring.Revert();
            }

            UpdateScoreClassic();
        }
    }
}
