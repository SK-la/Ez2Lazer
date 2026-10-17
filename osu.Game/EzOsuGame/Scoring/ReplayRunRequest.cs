// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Replay Session 运行请求：由 <see cref="ReplayRunPurpose"/> 驱动环境解析。
    /// </summary>
    public sealed class ReplayRunRequest
    {
        public Score Score { get; }
        public IBeatmap Beatmap { get; }
        public ReplayRunPurpose Purpose { get; }
        public bool IncludeGlobalManiaOffset { get; init; }

        /// <summary>
        /// 固定判定档位；其余环境字段（offset、frameShift、血量、优先级）仍按 <see cref="Purpose"/> 解析。
        /// </summary>
        /// <remarks>
        /// 只换档位不换整份环境：偏移等字段必须与对照运行一致，否则两次结果无法比较，缓存键也会分叉。
        /// </remarks>
        public EzEnumJudgeMode? JudgeModeOverride { get; init; }

        public ReplayRunRequest(Score score, IBeatmap beatmap, ReplayRunPurpose purpose)
        {
            Score = score;
            Beatmap = beatmap;
            Purpose = purpose;
        }
    }
}
