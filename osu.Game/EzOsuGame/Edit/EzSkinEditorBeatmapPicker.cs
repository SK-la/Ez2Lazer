// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.EzOsuGame.Edit
{
    public sealed class EzSkinEditorBeatmapPicker
    {
        private readonly RealmAccess realm;
        private readonly BeatmapManager beatmaps;

        public EzSkinEditorBeatmapPicker(RealmAccess realm, BeatmapManager beatmaps)
        {
            this.realm = realm;
            this.beatmaps = beatmaps;
        }

        public bool TryPickRandom(RulesetInfo ruleset, out IWorkingBeatmap? workingBeatmap)
        {
            workingBeatmap = null;

            var candidates = queryCandidates(ruleset.OnlineID);

            if (candidates.Count == 0)
                return false;

            var chosen = candidates[RNG.Next(candidates.Count)];
            workingBeatmap = beatmaps.GetWorkingBeatmap(chosen);
            return true;
        }

        internal static bool IsMetadataCandidate(BeatmapInfo beatmapInfo, int rulesetOnlineId)
        {
            if (beatmapInfo.Ruleset.OnlineID != rulesetOnlineId)
                return false;

            if (beatmapInfo.BeatmapSet?.Protected == true)
                return false;

            if (beatmapInfo.TotalObjectCount == 0)
                return false;

            return beatmapInfo.TotalObjectCount > 0 || beatmapInfo.TotalObjectCount == -1;
        }

        private List<BeatmapInfo> queryCandidates(int rulesetOnlineId)
        {
            var metadataMatches = realm.Run(r => r.All<BeatmapInfo>()
                                                  .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false")
                                                  .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.Protected)} == false")
                                                  .Filter($@"{nameof(BeatmapInfo.Ruleset)}.{nameof(RulesetInfo.OnlineID)} == $0", rulesetOnlineId)
                                                  .AsEnumerable()
                                                  .Select(i => i.Detach())
                                                  .Where(b => IsMetadataCandidate(b, rulesetOnlineId))
                                                  .ToList());

            var playable = new List<BeatmapInfo>(metadataMatches.Count);

            foreach (var beatmapInfo in metadataMatches)
            {
                if (beatmapInfo.TotalObjectCount > 0)
                {
                    playable.Add(beatmapInfo);
                    continue;
                }

                if (hasPlayableObjects(beatmapInfo))
                    playable.Add(beatmapInfo);
            }

            return playable;
        }

        private bool hasPlayableObjects(BeatmapInfo beatmapInfo)
        {
            try
            {
                var working = beatmaps.GetWorkingBeatmap(beatmapInfo);
                var playable = working.GetPlayableBeatmap(beatmapInfo.Ruleset);
                return playable.HitObjects.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
