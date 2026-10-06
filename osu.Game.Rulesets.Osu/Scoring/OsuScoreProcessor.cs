// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public partial class OsuScoreProcessor : ScoreProcessor
    {
        /// <summary>
        /// gameplay 开局冻结的判定轨；空则按 Lazer。禁止在无头路径回读全局配置。
        /// </summary>
        public EzEnumOsuJudgementTrack? JudgementTrackOverride { get; set; }

        private EzEnumOsuJudgementTrack judgementTrack => JudgementTrackOverride ?? EzEnumOsuJudgementTrack.Lazer;

        public OsuScoreProcessor()
            : base(new OsuRuleset())
        {
        }

        public override void ApplyEzGameplayEnvironment()
        {
            JudgementTrackOverride ??= GlobalConfigStore.EzConfig.Get<EzEnumOsuJudgementTrack>(Ez2Setting.OsuJudgementTrack);

            if (OsuClassicNativeScoring.ShouldUseLegacyScoreAlgorithm(judgementTrack))
                IsLegacyScore = true;
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            if (judgementTrack == EzEnumOsuJudgementTrack.ClassicNative)
                OsuClassicNativeScoring.ApplyHitWindowsToBeatmap(beatmap);

            base.ApplyBeatmap(beatmap);
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
