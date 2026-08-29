// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Legacy wrapper; bars only. Prefer <see cref="EzLocalProfileBucketBars.FromStarPlayCounts"/>.
    /// </summary>
    public partial class EzLocalProfileStarBars : EzLocalProfileBucketBars
    {
        public EzLocalProfileStarBars(IEnumerable<EzLocalProfileStarPlayCount> stars)
            : base(stars.Select(s => (s.StarBucket, s.Count)), bucket => $"{bucket}★–{bucket + 1}★")
        {
        }
    }
}
