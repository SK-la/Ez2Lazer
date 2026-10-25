// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps.ExternalLibraries;
using osu.Game.Database;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.BeatmapPools
{
    /// <summary>
    /// Scans osu!-style folders and aligns Realm external sets for one built-in ruleset.
    /// Catalog align only — never triggers analysis rebuild.
    /// </summary>
    public sealed partial class OsuFolderBeatmapPoolProvider : RulesetBeatmapPoolProvider
    {
        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        public OsuFolderBeatmapPoolProvider(Ruleset ruleset)
            : base(ruleset)
        {
        }

        public override bool CanApply => true;

        public override LocalisableString ApplyButtonText => "重建外部库";

        public override Task ApplyAsync(int variant, IReadOnlyList<string> enabledPaths, CancellationToken cancellationToken = default)
        {
            string shortName = Ruleset.ShortName;
            var rulesetInfo = rulesets.AvailableRulesets.FirstOrDefault(r => string.Equals(r.ShortName, shortName, StringComparison.OrdinalIgnoreCase));

            if (rulesetInfo == null)
                return Task.CompletedTask;

            var rulesetsByShortName = new Dictionary<string, RulesetInfo>(StringComparer.Ordinal)
            {
                [shortName] = (RulesetInfo)rulesetInfo,
            };

            return Task.Run(() =>
            {
                var scanned = new List<ExternalBeatmapSetImportModel>();

                foreach (string path in enabledPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (ExternalBeatmapSetImportModel set in OsuFolderExternalLibraryScanner.Scan(path, rulesetsByShortName, cancellationToken))
                    {
                        if (string.Equals(set.Ruleset.ShortName, shortName, StringComparison.OrdinalIgnoreCase))
                            scanned.Add(set);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Empty enabledPaths still aligns (removes all external sets for this ruleset).
                new ExternalBeatmapLibraryRealmAligner(realm, storage).Align(scanned, shortName);
            }, cancellationToken);
        }
    }
}
