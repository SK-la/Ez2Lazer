// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.EzOsuGame.Edit
{
    public sealed class EzSkinEditorBeatmapPicker
    {
        private const int unknown_object_count_attempts = 8;

        private readonly RealmAccess realm;
        private readonly BeatmapManager beatmaps;

        public EzSkinEditorBeatmapPicker(RealmAccess realm, BeatmapManager beatmaps)
        {
            this.realm = realm;
            this.beatmaps = beatmaps;
        }

        public static bool CanUseAsPreview(IWorkingBeatmap working)
        {
            if (working is DummyWorkingBeatmap)
                return false;

            if (working is WorkingBeatmap playable && playable.BeatmapSetInfo.Protected)
                return false;

            if (working.BeatmapInfo.Ruleset is not RulesetInfo ruleset)
                return false;

            if (!EzBeatmapPreviewModes.SupportsBeatmapPreview(ruleset))
                return false;

            return working.BeatmapInfo.TotalObjectCount != 0;
        }

        public bool TryPickRandom(RulesetInfo ruleset, out IWorkingBeatmap? workingBeatmap)
        {
            workingBeatmap = null;

            if (tryPickRandomWithKnownObjectCount(ruleset, out var beatmapInfo))
            {
                workingBeatmap = beatmaps.GetWorkingBeatmap(beatmapInfo);
                return true;
            }

            if (tryPickRandomWithUnknownObjectCount(ruleset, out beatmapInfo))
            {
                workingBeatmap = beatmaps.GetWorkingBeatmap(beatmapInfo);
                return true;
            }

            return false;
        }

        private bool tryPickRandomWithKnownObjectCount(RulesetInfo ruleset, out BeatmapInfo beatmapInfo)
        {
            beatmapInfo = null!;

            int count = realm.Run(r => buildFastQuery(r, ruleset.ShortName).Count());

            if (count == 0)
                return false;

            int index = RNG.Next(0, count);

            beatmapInfo = realm.Run(r => buildFastQuery(r, ruleset.ShortName)
                                         .AsEnumerable()
                                         .ElementAt(index)
                                         .Detach());

            return true;
        }

        private bool tryPickRandomWithUnknownObjectCount(RulesetInfo ruleset, out BeatmapInfo beatmapInfo)
        {
            beatmapInfo = null!;

            int count = realm.Run(r => buildUnknownObjectCountQuery(r, ruleset.ShortName).Count());

            if (count == 0)
                return false;

            for (int attempt = 0; attempt < Math.Min(unknown_object_count_attempts, count); attempt++)
            {
                int index = RNG.Next(0, count);

                var candidate = realm.Run(r => buildUnknownObjectCountQuery(r, ruleset.ShortName)
                                               .AsEnumerable()
                                               .ElementAt(index)
                                               .Detach());

                if (!IsMetadataCandidate(candidate, ruleset))
                    continue;

                try
                {
                    var working = beatmaps.GetWorkingBeatmap(candidate);

                    if (working.GetPlayableBeatmap(ruleset).HitObjects.Count > 0)
                    {
                        beatmapInfo = candidate;
                        return true;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            return false;
        }

        internal static bool IsMetadataCandidate(BeatmapInfo beatmapInfo, RulesetInfo ruleset)
        {
            if (!ruleset.Equals(beatmapInfo.Ruleset))
                return false;

            if (beatmapInfo.BeatmapSet?.Protected == true)
                return false;

            return beatmapInfo.TotalObjectCount != 0;
        }

        private static IQueryable<BeatmapInfo> buildFastQuery(Realm r, string rulesetShortName) =>
            r.All<BeatmapInfo>()
             .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false")
             .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.Protected)} == false")
             .Filter($@"{nameof(BeatmapInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0", rulesetShortName)
             .Filter($@"{nameof(BeatmapInfo.TotalObjectCount)} > 0");

        private static IQueryable<BeatmapInfo> buildUnknownObjectCountQuery(Realm r, string rulesetShortName) =>
            r.All<BeatmapInfo>()
             .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false")
             .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.Protected)} == false")
             .Filter($@"{nameof(BeatmapInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0", rulesetShortName)
             .Filter($@"{nameof(BeatmapInfo.TotalObjectCount)} == -1");
    }
}
