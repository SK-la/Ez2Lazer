using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.LAsEzExtensions.Analysis
{
    public interface IHitEventGenerator
    {
        List<HitEvent>? Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default);
    }
}
