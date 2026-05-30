// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps.ExternalLibraries;

namespace osu.Game.Rulesets
{
    /// <summary>
    /// Ruleset-provided scanner for external on-disk libraries. Invoked only during startup indexing, not during gameplay.
    /// </summary>
    public interface IRulesetExternalBeatmapLibraryContributor
    {
        IEnumerable<ExternalBeatmapSetImportModel> ScanPath(string absolutePath, CancellationToken cancellationToken);
    }
}
