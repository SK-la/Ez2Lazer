// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Skills.LeoBlack
{
    /// <summary>Hub <c>player-skills.ts</c> cluster share / category helpers.</summary>
    public static class EzLeoBlackClusterMetrics
    {
        /// <summary>
        /// Matched importance / total importance over clusters; <c>null</c> when total is 0.
        /// </summary>
        public static double? ClusterShare(IReadOnlyList<EzLeoBlackCluster> clusters, Func<string, bool> patternMatch)
        {
            ArgumentNullException.ThrowIfNull(clusters);
            ArgumentNullException.ThrowIfNull(patternMatch);

            double total = 0;
            double matched = 0;

            for (int i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                double importance = cluster.Importance;

                if (!double.IsFinite(importance) || importance <= 0)
                    continue;

                total += importance;

                if (patternMatch(cluster.Pattern))
                    matched += importance;
            }

            return total > 0 ? matched / total : null;
        }

        /// <summary>Share of clusters whose pattern matches <c>/jack/i</c>.</summary>
        public static double? JackShare(IReadOnlyList<EzLeoBlackCluster> clusters)
            => ClusterShare(clusters, p => p.Contains("jack", StringComparison.OrdinalIgnoreCase));

        /// <summary>Share of clusters whose pattern matches <c>/stream/i</c>.</summary>
        public static double? StreamShare(IReadOnlyList<EzLeoBlackCluster> clusters)
            => ClusterShare(clusters, p => p.Contains("stream", StringComparison.OrdinalIgnoreCase));

        /// <summary>Whether category matches <c>/tech/i</c>.</summary>
        public static bool TechCategory(string? category)
            => (category ?? "").Contains("tech", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether category matches <c>/trill/i</c>.</summary>
        public static bool ClusterTrill(string? category)
            => (category ?? "").Contains("trill", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether category matches <c>/handstream/i</c>.</summary>
        public static bool HandstreamCluster(string? category)
            => (category ?? "").Contains("handstream", StringComparison.OrdinalIgnoreCase);
    }
}
