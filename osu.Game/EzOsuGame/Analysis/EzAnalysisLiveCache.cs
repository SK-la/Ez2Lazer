// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using osu.Game.EzOsuGame.Metrics;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// Session-only analysis results. Nothing here is persisted; the stored baseline is owned by the background
    /// backfill.
    /// </summary>
    /// <remarks>
    /// Results are keyed by chart, ruleset and mods, because that is what determines an analysis. Every surface
    /// that wants live values for the same chart reads the same entry instead of computing its own.
    /// </remarks>
    public sealed class EzAnalysisLiveCache
    {
        /// <summary>Inputs that determine an analysis. The key count is derived from the playable, not an input.</summary>
        public readonly record struct AnalysisKey(string BeatmapHash, int RulesetOnlineId, int ModsSignature);

        private readonly ConcurrentDictionary<AnalysisKey, EzAnalysisResult> analyses
            = new ConcurrentDictionary<AnalysisKey, EzAnalysisResult>();

        public EzAnalysisResult? Get(AnalysisKey key)
            => analyses.TryGetValue(key, out var value) ? value : null;

        public void Set(AnalysisKey key, EzAnalysisResult value)
            => analyses[key] = value;

        /// <summary>Drops the entries a bump of <paramref name="producer"/> can invalidate.</summary>
        public void Invalidate(EzMetricProducerId producer)
            => analyses.Clear();

        public void InvalidateSubject(string subject)
        {
            if (string.IsNullOrEmpty(subject))
                return;

            foreach (var key in analyses.Keys)
            {
                if (string.Equals(key.BeatmapHash, subject, System.StringComparison.Ordinal))
                    analyses.TryRemove(key, out _);
            }
        }

        public void Clear() => analyses.Clear();
    }
}
