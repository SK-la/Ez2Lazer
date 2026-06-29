// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania
{
    public partial class ManiaRuleset
    {
        /// <summary>
        /// 无绘制 replay 判定：返回经 ScoreProcessor.PopulateScore 填充的 <see cref="Score"/>。
        /// 成绩统计页经 <see cref="EzMania.Statistics.ManiaScoreHitEventGenerator"/> 调用同一 Session 路径。
        /// </summary>
        public Task<Score> RunReplayAsync(Score score, IBeatmap playable, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => ManiaReplaySession.Run(score, playable, environment, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Mania replay 环境唯一解析入口（替代分散的 FromScore / FromLive）。
        /// </summary>
        public static GameplayEnvironment ResolveEnvironment(ScoreInfo? score, ReplayRunPurpose purpose)
        {
            var config = GlobalConfigStore.EzConfig;
            var live = readLive(config);

            switch (purpose)
            {
                case ReplayRunPurpose.ForStored:

                    if (score == null || !score.TryGetManiaGameplayModes(out int hitMode, out int healthMode))
                        return live;

                    return live with
                    {
                        ManiaHitMode = (EzEnumHitMode)hitMode,
                        ManiaHealthMode = (EzEnumHealthMode)healthMode,
                    };

                default:
                    return live;
            }
        }

        private static GameplayEnvironment readLive(Ez2ConfigManager config) => new GameplayEnvironment
        {
            ManiaHitMode = config.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode),
            ManiaHealthMode = config.Get<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode),
            JudgePrecedence = config.Get<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence),
            OffsetPlusMania = config.Get<double>(Ez2Setting.OffsetPlusMania),
            BmsPoorHitResultEnable = config.Get<bool>(Ez2Setting.BmsPoorHitResultEnable),
        };
    }
}
