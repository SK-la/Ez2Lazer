// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public partial class OsuScoreProcessor : ScoreProcessor, IStableScoreSource
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

        /// <summary>
        /// 本局冻结的判定轨；未冻结时按 Lazer，与全谱模拟使用同一口径。
        /// </summary>
        public EzEnumJudgeMode JudgeMode => stdJudgeMode;

        private readonly StableStdScoring stableScoring = new StableStdScoring();

        private bool isStableClassic => OsuStableClassic.IsActive(stdJudgeMode);

        private readonly BindableBool stableAvailable = new BindableBool();

        public IBindable<double> StableAccuracy => AccuracyClassic;

        public IBindable<long> StableScore => TotalScoreWithoutMods;

        public IBindable<int> StableMaxCombo => HighestCombo;

        public IBindable<bool> StableAvailable => stableAvailable;

        /// <summary>
        /// 局内 stable 槽位只在判定轨本身就是 StableClassic 时可显示；其它档没有等价的实时结果。
        /// </summary>
        /// <remarks>
        /// TODO(Ez live stable 影子)：非 stable 档应转发 Session 可推进引擎的输出，届时这里改为恒 true。
        /// 引擎改造面见 <c>OsuReplaySessionSimulator</c> 头部的 TODO。
        /// </remarks>
        private void refreshStableAvailability() => stableAvailable.Value = isStableClassic;

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
            refreshStableAvailability();

            if (OsuStableClassic.IsActive(stdJudgeMode))
                IsLegacyScore = true;
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            refreshStableAvailability();

            // 判定窗与 classic 滑条语义必须在全谱模拟（base.ApplyBeatmap 内部的 SimulateAutoplay）前绑定。
            // 共享 cache 实例对所有消费者只读：就地绑定会把本档窗口泄漏到其它判定档，故跳过（仿真走按档隔离的副本）。
            if (isStableClassic && !EzPlayableBeatmapCache.IsSharedInstance(beatmap))
                OsuStableClassic.BindSemantics(beatmap);

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
            => isStableClassic ? StableStdScoring.BaseScoreForResult(result) : base.GetBaseScoreForResult(result);

        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            if (isStableClassic)
                return stableScoring.TotalScore;

            return base.ComputeTotalScore(comboProgress, accuracyProgress, bonusPortion);
        }

        protected override double ComputeTheoreticalPerfectJudgedTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
        {
            if (isStableClassic)
                return StableStdScoring.ProjectPerfectJudged(MaximumTotalScore, JudgedHits, MaximumJudgements);

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
