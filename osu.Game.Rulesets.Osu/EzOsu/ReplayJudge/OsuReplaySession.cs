// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge
{
    /// <summary>
    /// 无绘制 Osu replay Session：影子判定 → <see cref="JudgementProcessor.ApplyResult"/> → PopulateScore。
    /// </summary>
    /// <remarks>
    /// 判定由 <see cref="Shadow.OsuReplayShadowEngine"/> 驱动（OSL-010 Shadow Judgement）。
    /// </remarks>
    public static class OsuReplaySession
    {
        public static Score Run(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            var (scoreProcessor, _) = run(score, beatmap, environment, recordTimeline: false, cancellationToken);

            score.ScoreInfo.HitEvents = scoreProcessor.HitEvents.ToList();
            scoreProcessor.PopulateScore(score.ScoreInfo);

            return score;
        }

        public static IReadOnlyList<HitEvent> RunHitEvents(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
            => Run(score, beatmap, environment, cancellationToken).ScoreInfo.HitEvents;

        public static EzScoreTimeline RunTimeline(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default)
        {
            var (_, timeline) = run(score, beatmap, environment, recordTimeline: true, cancellationToken);
            return timeline ?? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>());
        }

        public static (Score Score, EzScoreTimeline Timeline) RunWithTimeline(Score score, IBeatmap beatmap, IGameplayEnvironment environment,
                                                                              CancellationToken cancellationToken = default)
        {
            var (scoreProcessor, timeline) = run(score, beatmap, environment, recordTimeline: true, cancellationToken);

            score.ScoreInfo.HitEvents = scoreProcessor.HitEvents.ToList();
            scoreProcessor.PopulateScore(score.ScoreInfo);

            return (score, timeline ?? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>()));
        }

        private static (ScoreProcessor scoreProcessor, EzScoreTimeline? timeline) run(
            Score score,
            IBeatmap beatmap,
            IGameplayEnvironment environment,
            bool recordTimeline,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score);
            ArgumentNullException.ThrowIfNull(score.Replay);
            ArgumentNullException.ThrowIfNull(beatmap);

            var ruleset = score.ScoreInfo.Ruleset.CreateInstance();
            var scoreProcessor = ruleset.CreateScoreProcessor();
            // [Ez] Defence in depth: ScoreInfo.Mods already strips UnknownMod; keep Session free of unresolved mods.
            var resolvedMods = EzModCompatibility.StripUnknown(score.ScoreInfo.Mods);
            scoreProcessor.Mods.Value = resolvedMods;

            var beatmapProcessor = ruleset.CreateBeatmapProcessor(beatmap);
            beatmapProcessor?.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, cancellationToken);

            beatmapProcessor?.PostProcess();

            scoreProcessor.ApplyBeatmap(beatmap);

            if (score.ScoreInfo.IsLegacyScore)
                scoreProcessor.IsLegacyScore = true;

            foreach (var mod in resolvedMods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(scoreProcessor);

            var recorder = recordTimeline ? new OsuReplayTimelineRecorder() : null;
            double gameplayRate = ModUtils.CalculateRateWithMods(resolvedMods);

            if (score.Replay.Frames.Count == 0)
            {
                scoreProcessor.PopulateScore(score.ScoreInfo);
                return (scoreProcessor, recordTimeline ? new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>()) : null);
            }

            OsuReplaySessionSimulator.Simulate(score, beatmap, environment, scoreProcessor, gameplayRate, recorder, cancellationToken);

            return (scoreProcessor, recorder?.Build());
        }
    }
}
