// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Scoring
{
    public class LegacyDatabasedScore : Score
    {
        public LegacyDatabasedScore(ScoreInfo score, RulesetStore rulesets, BeatmapManager beatmaps, IResourceStore<byte[]> store)
        {
            ScoreInfo = score;
            bool highPrecisionReplayLoaded = false;

            string ezFramesPath = score.Files.FirstOrDefault(f => f.Filename.EndsWith(EzHighPrecisionReplayFrames.EXTENSION, StringComparison.OrdinalIgnoreCase))?.File.GetStoragePath();

            if (ezFramesPath != null)
            {
                using (var stream = store.GetStream(ezFramesPath))
                {
                    if (stream != null && EzHighPrecisionReplayFrames.TryRead(stream, out var legacyFrames))
                    {
                        var ruleset = score.Ruleset.CreateInstance();
                        var playable = beatmaps.GetWorkingBeatmap(score.BeatmapInfo).GetPlayableBeatmap(score.Ruleset, score.Mods);

                        Replay = new Replay();
                        EzHighPrecisionReplayFrames.PopulateReplay(Replay, legacyFrames, ruleset, playable);
                        highPrecisionReplayLoaded = true;
                    }
                }
            }

            string replayFilename = score.Files.FirstOrDefault(f => f.Filename.EndsWith(".osr", StringComparison.InvariantCultureIgnoreCase))?.File.GetStoragePath();

            if (replayFilename == null)
                return;

            using (var stream = store.GetStream(replayFilename))
            {
                if (stream == null)
                    return;

                Score decoded = new DatabasedLegacyScoreDecoder(rulesets, beatmaps).Parse(stream);

                if (decoded.ScoreInfo.GameplayEnvironmentSnapshot is { } environment)
                {
                    score.JudgeMode = decoded.ScoreInfo.JudgeMode;
                    score.HealthMode = decoded.ScoreInfo.HealthMode;
                    score.GameplayEnvironmentSnapshot = environment;
                }

                if (!highPrecisionReplayLoaded)
                    Replay = decoded.Replay;
            }
        }
    }
}
