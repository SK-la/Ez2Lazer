// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ExternalLibraries;
using osu.Game.Database;
using osu.Game.Rulesets.BMS.Beatmaps;
using Realms;

namespace osu.Game.Rulesets.BMS.Database
{
    /// <summary>
    /// Rewrites legacy <c>bms-ext:set:</c> set hashes to the shared <see cref="ExternalBeatmapPathEncoding"/> format.
    /// </summary>
    public sealed class BmsLegacyExternalBeatmapEzMigration : IEzRealmMigrationContributor
    {
        public IReadOnlyList<int> TargetEzVersions { get; } = new[] { 7 };

        public void Apply(Migration migration, int targetEzVersion)
        {
            foreach (var set in migration.NewRealm.All<BeatmapSetInfo>())
            {
                if (!set.Beatmaps.Any(b => string.Equals(b.Ruleset.ShortName, "bms", StringComparison.Ordinal)))
                    continue;

                if (!BMSExternalPath.TryDecodeLegacyHash(set.Hash, out string folderPath))
                    continue;

                set.HostingKind = BeatmapSetHostingKind.External;
                set.ExternalContentRoot = Path.GetFullPath(folderPath);
                set.Hash = ExternalBeatmapPathEncoding.Encode(folderPath);
            }
        }
    }
}
