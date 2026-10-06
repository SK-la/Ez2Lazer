// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 从嵌入 osr+osu 解码 Score / playable（帧保持解码 double，不经 Legacy Round）。
    /// 供 ReplayJson 导出与全谱 Drawable≡Session 共用。
    /// </summary>
    internal static class ManiaReplayJsonAuditLoader
    {
        private static readonly DllResourceStore resources = new DllResourceStore(typeof(ManiaReplayJsonAuditLoader).Assembly);

        public static (Score Score, IBeatmap Playable, ManiaBeatmap? ManiaBeatmap) Load(string osrResource, string beatmapResource)
        {
            var decoder = new HarnessScoreDecoder(beatmapResource);
            Score score;

            using (var stream = resources.GetStream(osrResource)
                                ?? throw new FileNotFoundException($"osr resource missing: {osrResource}"))
                score = decoder.Parse(stream);

            var playable = decoder.LastWorkingBeatmap!.GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
            return (score, playable, playable as ManiaBeatmap);
        }

        public static Stream? OpenBeatmap(string beatmapResource) => resources.GetStream(beatmapResource);

        public static IBeatmap LoadPlayable(string beatmapResource, Score score)
        {
            using var stream = OpenBeatmap(beatmapResource)
                               ?? throw new FileNotFoundException($"beatmap resource missing: {beatmapResource}");
            IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream));
            decoded.BeatmapInfo.Ruleset = score.ScoreInfo.Ruleset;
            return new TestWorkingBeatmap(decoded).GetPlayableBeatmap(score.ScoreInfo.Ruleset, score.ScoreInfo.Mods);
        }

        private sealed class HarnessScoreDecoder : LegacyScoreDecoder
        {
            public WorkingBeatmap? LastWorkingBeatmap { get; private set; }
            private readonly string beatmapResource;

            public HarnessScoreDecoder(string beatmapResource)
            {
                this.beatmapResource = beatmapResource;
            }

            protected override Ruleset GetRuleset(int rulesetId) => new ManiaRuleset();

            protected override WorkingBeatmap GetBeatmap(string md5Hash)
            {
                using var stream = resources.GetStream(beatmapResource)
                                   ?? throw new FileNotFoundException($"beatmap resource missing: {beatmapResource}");
                IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream));
                decoded.BeatmapInfo.MD5Hash = md5Hash;
                LastWorkingBeatmap = new TestWorkingBeatmap(decoded);
                return LastWorkingBeatmap;
            }
        }
    }
}
