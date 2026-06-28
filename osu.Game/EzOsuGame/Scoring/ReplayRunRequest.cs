// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Replay Session 运行请求：统一封装 score、beatmap、environment 与用途。
    /// </summary>
    public sealed class ReplayRunRequest
    {
        public Score Score { get; }
        public IBeatmap Beatmap { get; }
        public IGameplayEnvironment? Environment { get; }
        public ReplayRunPurpose Purpose { get; }

        public ReplayRunRequest(Score score, IBeatmap beatmap, IGameplayEnvironment? environment, ReplayRunPurpose purpose = ReplayRunPurpose.ForStored)
        {
            Score = score;
            Beatmap = beatmap;
            Environment = environment;
            Purpose = purpose;
        }
    }
}
