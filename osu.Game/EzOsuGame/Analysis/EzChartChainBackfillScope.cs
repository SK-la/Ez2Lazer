// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Which chart-chain stages a Realm metadata pass must run when upstream systems are invalidated.
    /// </summary>
    internal static class EzChartChainBackfillScope
    {
        public static void Resolve(in EzAnalysisSystem scope, out bool msd, out bool csi, out bool dan, out bool ppp, out bool needsChartChain)
        {
            msd = scope.HasFlag(EzAnalysisSystem.Mina);
            // Mina invalidation retires CSI/Dan rows but the user often selects only Realm Mina — still refill downstream.
            csi = scope.HasFlag(EzAnalysisSystem.CSI) || msd;
            // Dan reads Sunny too; SSR-only maintenance must not leave ChartDan deleted with no stage enabled.
            dan = scope.HasFlag(EzAnalysisSystem.Dan) || csi || scope.HasFlag(EzAnalysisSystem.SSR);
            ppp = scope.HasFlag(EzAnalysisSystem.PPP);
            needsChartChain = msd || csi || dan || ppp;
        }
    }
}