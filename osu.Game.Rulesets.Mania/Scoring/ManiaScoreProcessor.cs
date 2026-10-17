// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
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
    public partial class ManiaScoreProcessor : ScoreProcessor, IStableScoreSource
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

        private readonly BindableBool stableAvailable = new BindableBool();

        public IBindable<double> StableAccuracy => AccuracyClassic;

        public IBindable<long> StableScore => TotalScore;

        public IBindable<int> StableMaxCombo => HighestCombo;

        public IBindable<bool> StableAvailable => stableAvailable;

        /// <summary>
        /// stable classic 长条持有期的连击增量：每 100ms 一枚。
        /// </summary>
        /// <remarks>
        /// 只推 <see cref="ScoreProcessor.Combo"/> 与 <see cref="ScoreProcessor.HighestCombo"/>：
        /// 该分支不给判定计数、不加分、不进 <c>HitEvents</c>，因此
        /// <c>ScoreResultCounts</c> / acc / rank / <c>MaximumStatistics</c> 均不受影响。
        /// 增量数取决于实际按住时长（无法从谱面枚举），故也不进 <c>MaximumCombo</c>。
        /// 在自增前按当前 combo 做 384 的 <c>ComboAddition</c> 重置。
        /// </remarks>
        internal void ApplyClassicHoldCombo()
        {
            if (IsSimulating)
                return;

            stableScoring.ApplyHoldTick(Combo.Value);
            Combo.Value++;
            HighestCombo.Value = Math.Max(HighestCombo.Value, Combo.Value);
        }

        protected override bool TotalScoreIsSelfContained => isStableClassic;

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

            return (double)StableManiaScoring.SCORE_MAX / noteCount * StableManiaScoring.ModMultiplier(Mods.Value);
        }

        /// <summary>
        /// 局内 stable 槽位只在判定档本身就是 StableClassic 时可显示；其它档没有等价的实时结果。
        /// </summary>
        /// <remarks>
        /// TODO(Ez live stable 影子)：非 stable 档应转发 Session 可推进引擎的输出，届时这里改为恒 true。
        /// 引擎改造面见 <c>ManiaReplaySessionSimulator.Simulate</c> 头部的 TODO。
        /// </remarks>
        private void refreshStableAvailability() => stableAvailable.Value = isStableClassic;

        /// <summary>
        /// gameplay 入口（Player / 皮肤编辑器预览）冻结当前全局 JudgeMode。
        /// 必须在 <see cref="ScoreProcessor.ApplyBeatmap"/> 之前调用，否则全谱模拟与实际判定权重不一致。
        /// </summary>
        public override void ApplyEzGameplayEnvironment()
        {
            JudgeModeOverride ??= GlobalConfigStore.EzConfig.Get<EzEnumJudgeMode>(Ez2Setting.ManiaJudgeMode);
            refreshStableAvailability();
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            refreshStableAvailability();

            // Tail/Tick 的 Ignore 系 Judgement 必须在全谱模拟前绑定，否则 EZ2AC/Malody 会把官方尾 Perfect 计入 MaximumBaseScore。
            ManiaBeatmapBinding.BindJudgements(beatmap, judgeMode);
            base.ApplyBeatmap(beatmap);
        }

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);
            stableScoring.Reset(stableScoreMultiplier(), StableManiaScoring.ScoreIncreaseMultiplier(Mods.Value));
        }

        public override void ResetFromReplayFrame(ReplayFrame frame)
        {
            // 帧头是同轨（classic 原生）的成绩快照：后退 / 跳帧后必须整体重建累加器，
            // 否则下一次判定会从陈旧状态继续累加，总分与 classic 准确率都会跳变。
            if (isStableClassic && frame.Header != null)
            {
                stableScoring.RestoreFrom(frame.Header.TotalScore);

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
            int perfect = JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.StableClassic, HitResult.Perfect);
            int judged = 0;
            long baseScore = 0;

            foreach (var (result, count) in statistics)
            {
                judged += count;
                baseScore += (long)count * JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.StableClassic, result);
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
            // stable 原生 classic：总分由 <c>ComboAddition</c> 状态机逐物件累加，不再走 combo/acc 曲线。
            if (isStableClassic)
                return StableManiaScoring.Finalize(stableScoring.TotalScoreDouble);

            if (IsLegacyScore)
            {
                return 150000 * comboProgress
                       + 850000 * Math.Pow(AccuracyClassic.Value, 2 + 2 * AccuracyClassic.Value) * accuracyProgress
                       + bonusPortion;
            }

            if (judgeMode != EzEnumJudgeMode.Lazer)
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

            if (IsLegacyScore)
            {
                return 150000 * comboProgress
                       + 850000 * accuracyProgress
                       + bonusPortion;
            }

            if (judgeMode != EzEnumJudgeMode.Lazer)
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
            if (!isStableClassic)
                return;

            applyStableClassicScoreChange(judgement, judgement.ComboAtJudgement);
            UpdateScoreClassic();
        }

        /// <summary>
        /// stable classic 的计分 / classic 准确率都取<strong>已落定</strong>的判定档位。
        /// </summary>
        /// <remarks>
        /// 不能用 <c>TimeOffset</c> 重判：被动 / 强制 Miss 的 stored offset 来自同列最近一次按键，
        /// 偏移小时按窗口重判会得回 300g，于是 Miss 照样加基础分、还把 <c>ComboAddition</c> 加 2；
        /// 列内从未按过键时偏移为 0，更是必然判成 300g。
        /// </remarks>
        private void applyStableClassicScoreChange(JudgementResult judgement, int comboAtJudgement)
        {
            var hitObject = judgement.HitObject;

            // LN 头在 stable 里不进计分（也没有独立档位），头判定被绑定成 Ignore。
            if (hitObject is not Note || hitObject is HeadNote)
                return;

            ClassicMaxBaseScore += JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.StableClassic, HitResult.Perfect);
            ClassicBaseScore += JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.StableClassic, judgement.Type);
            stableScoring.Apply(StableClassicJudgeModeJudgement.FromHitResult(judgement.Type), comboAtJudgement);
        }

        protected override void RemoveScoreChange(JudgementResult judgement)
        {
            if (!isStableClassic)
                return;

            removeStableClassicScoreChange(judgement);
            UpdateScoreClassic();
        }

        /// <summary><see cref="applyStableClassicScoreChange"/> 的逆操作（rewind / revert）。</summary>
        private void removeStableClassicScoreChange(JudgementResult judgement)
        {
            if (judgement.HitObject is not Note || judgement.HitObject is HeadNote)
                return;

            ClassicMaxBaseScore -= JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.StableClassic, HitResult.Perfect);
            ClassicBaseScore -= JudgeModeHelper.GetBaseScoreForResult(EzEnumJudgeMode.StableClassic, judgement.Type);
            stableScoring.Revert();
        }
    }
}
