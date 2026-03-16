// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Statistics
{
    public interface IHitEventGenerator
    {
        List<HitEvent>? Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default);
    }
}
