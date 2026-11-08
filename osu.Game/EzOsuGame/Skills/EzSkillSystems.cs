// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.PPP;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Storage namespaces for skill value rows, plus the ids that are not plain axes.
    /// </summary>
    /// <remarks>
    /// These are the strings written into the value tables. The canonical producer names live in
    /// <c>EzMetricProducers</c>; the two are deliberately separate so storage can be renamed without
    /// touching identity, and vice versa.
    /// </remarks>
    public static class EzSkillSystems
    {
        public const string BEATMAP_MINA = "beatmap_msd";

        public const string PLAYER_MINA = "player_ssr";

        public const string PLAYER_PATTERN = "player_pattern";

        public const string DAN = "dan";

        /// <summary>PP+ chart axes.</summary>
        public const string BEATMAP_PPP = "beatmap_ppp";

        /// <summary>PP+ player axes.</summary>
        public const string PLAYER_PPP = "player_ppp";

        /// <summary>Chart structure axes. Persisted alongside the typed structure row so a reader can draw the
        /// long-note frame without running the structure pass again.</summary>
        public const string BEATMAP_CSI = "csi";

        /// <summary>Key count sentinel for producers that are not sliced by key mode.</summary>
        public const int UNSLICED_KEY = 0;

        /// <summary>
        /// Row tag for a chart the engine rates as a zero vector on a supported key mode. The row carries
        /// <see cref="Metrics.EzMetricState.Unrateable"/>; the tag itself is not an axis.
        /// </summary>
        public const string UNRATEABLE = "__unrateable";

        public static string DanSkillId(int keyCount, EzDanSide side) => $"{DAN}.{keyCount}k.{side.ToId()}";

        /// <summary>
        /// Default algorithm version for rows of <paramref name="systemId"/>. Unknown ids return 0, so
        /// callers must pass an explicit version for anything not listed here.
        /// </summary>
        /// <remarks>
        /// The two sides are deliberately stamped from different sources. Chart-side Mina rows carry the
        /// producer revision, which folds every declared dependency. Player rows carry the raw engine
        /// constant: they are aggregated per play and are not part of the chart chain, so a chart-side
        /// admission never retires them.
        /// </remarks>
        public static int ResolveAlgorithmVersion(string systemId) => systemId switch
        {
            BEATMAP_MINA => EzAnalysisRevision.Registry.RevisionOf(EzMetricProducers.MINA_CHART, EzLocalProfileConstants.MANIA_RULESET_ID),
            BEATMAP_CSI => EzAnalysisRevision.ChartSkillInfo,
            PLAYER_MINA or PLAYER_PATTERN => EzMinaSkillAlgorithm.VERSION,
            // PP+ rows are stamped per ruleset bundle, never by a single version constant.
            BEATMAP_PPP or PLAYER_PPP => EzPPPSkillVersions.FACET_REVISION,
            _ => 0,
        };
    }

    /// <summary>
    /// Bump when note conversion or rating aggregation semantics change.
    /// 2: unified onto the 4-18K n-key engine (was a 4/6/7K-only package).
    /// </summary>
    public static class EzMinaSkillAlgorithm
    {
        public const int VERSION = 2;
    }
}
