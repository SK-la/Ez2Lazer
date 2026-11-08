// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Skills;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// The channels one analysis pass fills in <see cref="EzAnalysisResult.Axes"/>.
    /// </summary>
    /// <remarks>
    /// A channel is just the name a pass groups its axis values under; it carries no declaration, no revision
    /// and no applicability rule. Which channel a ruleset reads for its panel is decided in
    /// <see cref="EzRulesetReadout"/>.
    /// </remarks>
    public static class EzAnalysisChannels
    {
        public const string OFFICIAL = "official";

        public const string OFFICIAL_PERFORMANCE = "official_performance";

        public const string SUNNY = "ssr";

        public const string SUNNY_PERFORMANCE = "ssr_performance";

        public const string PPP = "ppp";

        public const string PPP_PERFORMANCE = "ppp_performance";

        public const string MINA = "mina";

        public const string CSI = "csi";

        /// <summary>Axis id a star rating is stored under, whichever system produced it.</summary>
        public const string STAR_RATING = "star_rating";

        /// <summary>Axis id a perfect-play performance number is stored under.</summary>
        public const string PERFORMANCE_POINTS = "performance_points";

        /// <summary>Channel a stored skill namespace is read back under.</summary>
        public static string ChannelOfSystem(string systemId) => systemId switch
        {
            EzSkillSystems.BEATMAP_MINA => MINA,
            EzSkillSystems.BEATMAP_PPP => PPP,
            EzSkillSystems.BEATMAP_CSI => CSI,
            _ => systemId,
        };

        /// <summary>Rulesets the analysis layer knows by online id.</summary>
        public static readonly int[] ALL_RULESETS =
        {
            LocalProfile.EzLocalProfileConstants.OSU_RULESET_ID,
            LocalProfile.EzLocalProfileConstants.TAIKO_RULESET_ID,
            LocalProfile.EzLocalProfileConstants.CATCH_RULESET_ID,
            LocalProfile.EzLocalProfileConstants.MANIA_RULESET_ID,
        };
    }
}
