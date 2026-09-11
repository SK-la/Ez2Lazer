// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    /// <summary>Hub <c>patterns/categorise.js</c>.</summary>
    internal static class EzLeoBlackCategorise
    {
        public static string CategoriseChart(int keys, IReadOnlyList<EzLeoBlackInternalCluster> orderedClusters, double svAmount)
        {
            _ = keys;
            _ = svAmount;

            if (orderedClusters.Count == 0)
                return "Uncategorised";

            double firstImportance = orderedClusters[0].Importance;
            var important = new List<EzLeoBlackInternalCluster>();

            foreach (var cluster in orderedClusters)
            {
                if (cluster.Importance / firstImportance > EzLeoBlackConfig.IMPORTANT_CLUSTER_RATIO)
                    important.Add(cluster);
                else
                    break;
            }

            var cluster1 = important[0];
            bool tech = cluster1.Mixed;

            string name;

            if (cluster1.SpecificTypes.Count > 0 && cluster1.SpecificTypes[0].Ratio > 0.05)
            {
                name = cluster1.SpecificTypes[0].Name;
            }
            else if (cluster1.SpecificTypes.Count >= 2
                     && cluster1.SpecificTypes[0].Name == "Jumpstream"
                     && cluster1.SpecificTypes[1].Name == "Handstream")
            {
                double a1 = cluster1.SpecificTypes[0].Ratio;
                double a2 = cluster1.SpecificTypes[1].Ratio;
                name = a2 / a1 > EzLeoBlackConfig.CATEGORY_JS_HS_SECONDARY_RATIO
                    ? "Jumpstream/Handstream"
                    : cluster1.Pattern;
            }
            else
            {
                name = cluster1.Pattern;
            }

            // hub isHybridChart always returns false
            return $"{name}{(tech ? " Tech" : "")}";
        }
    }
}
