// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Analysis;

namespace osu.Game.Rulesets.BMS.EzBms
{
    /// <summary>
    /// Routes BMS beatmaps through mania conversion before delegating to <see cref="EzManiaAnalysisProvider"/>.
    /// </summary>
    public sealed class BmsEzAnalysisProvider : IEzAnalysisProvider, IEzXxyStarRatingSupport
    {
        private readonly EzManiaAnalysisProvider inner = new EzManiaAnalysisProvider();
        public int XxyStarRatingVersion => inner.XxyStarRatingVersion;

        public bool SupportsXxyStarRating(IBeatmap beatmap)
        {
            if (beatmap is BMSBeatmap || beatmap.HitObjects.Any(h => h is BMSHitObject))
                return true;

            return inner.SupportsXxyStarRating(beatmap);
        }

        public bool TryCompute(in EzAnalysisRequest request, CancellationToken cancellationToken, out IEzAnalysis analysis)
        {
            if (tryGetManiaBeatmap(request.Beatmap, out ManiaBeatmap? maniaBeatmap) && maniaBeatmap != null)
            {
                var maniaRequest = new EzAnalysisRequest(maniaBeatmap, request.ClockRate, request.RequestedScopes);
                return inner.TryCompute(maniaRequest, cancellationToken, out analysis);
            }

            return inner.TryCompute(request, cancellationToken, out analysis);
        }

        private static bool tryGetManiaBeatmap(IBeatmap beatmap, out ManiaBeatmap? maniaBeatmap)
        {
            maniaBeatmap = null;

            if (beatmap is ManiaBeatmap existing)
            {
                maniaBeatmap = existing;
                return true;
            }

            if (beatmap is BMSBeatmap || beatmap.HitObjects.Any(h => h is BMSHitObject))
            {
                maniaBeatmap = ManiaConvertedWorkingBeatmap.ConvertToManiaBeatmap(beatmap);
                return true;
            }

            return false;
        }
    }
}
