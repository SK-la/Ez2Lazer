// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Realm <c>SystemId</c> namespaces and meta skill ids.
    /// Mania (Mina / pattern / dan) and osu (PP+ chart / player) are peer plugins under <see cref="EzSkillRegistry"/>;
    /// ruleset wiring is <see cref="EzSkillProfile"/>.
    /// </summary>
    public static class EzSkillSystems
    {
        public const string BEATMAP_MSD = "beatmap_msd";
        public const string PLAYER_SSR = "player_ssr";

        /// <summary>
        /// Hub pattern ratings (Overall SSR aggregated per chart pattern tag).
        /// Persisted in existing <see cref="EzPlayerSkillValue"/> rows — see DATA-Skills-PatternRatings.
        /// </summary>
        public const string PLAYER_PATTERN = "player_pattern";

        public const string DAN = "dan";

        /// <summary>osu!standard PP+-shaped chart axes (<see cref="EzOsuSkillAxis"/>).</summary>
        public const string BEATMAP_PPPLUS = "beatmap_ppplus";

        /// <summary>osu!standard PP+-shaped player axes (aggregated plays).</summary>
        public const string PLAYER_PPPLUS = "player_ppplus";

        /// <summary>
        /// Realm <see cref="EzPlayerSkillValue.KeyCount"/> sentinel for rulesets without keymode slices (osu).
        /// </summary>
        public const int OSU_SLICE_KEY = 0;

        /// <summary>Persisted beside MSD axes when computed (not a Mina axis).</summary>
        public const string HOLD_RATIO = "__hold_ratio";

        /// <summary>
        /// Settled miss for charts MinaCalc rates as a zero vector on a supported keymode.
        /// Keyed by beatmap content hash — editing the chart changes the hash and allows recompute.
        /// Not a valid MSD cache (<see cref="EzBeatmapMsdComputer.IsCurrentMsdCache"/> stays false).
        /// </summary>
        public const string UNRATEABLE = "__unrateable";

        public static string MsdHoldRatioSkillId => $"{BEATMAP_MSD}.{HOLD_RATIO}";

        public static string MsdUnrateableSkillId => $"{BEATMAP_MSD}.{UNRATEABLE}";

        public static string DanSkillId(int keyCount, EzDanSide side)
            => $"{DAN}.{keyCount}k.{side.ToId()}";

        /// <summary>
        /// Default algorithm version for skill-value rows of <paramref name="systemId"/>.
        /// Unknown ids return 0 (caller should pass an explicit version).
        /// </summary>
        public static int ResolveAlgorithmVersion(string systemId) => systemId switch
        {
            BEATMAP_MSD or PLAYER_SSR or PLAYER_PATTERN => EzManiaSkillAlgorithm.VERSION,
            BEATMAP_PPPLUS or PLAYER_PPPLUS => EzOsuSkillAlgorithm.VERSION,
            _ => 0,
        };
    }

    /// <summary>
    /// Bump when MinaCalc note conversion or aggregation semantics change.
    /// 2: unified onto the 4-18K n-key MinaCalc wasm (was NuGet MinaCalc 0.4.2, 4/6/7K only).
    /// </summary>
    public static class EzManiaSkillAlgorithm
    {
        public const int VERSION = 2;
    }

    /// <summary>
    /// Bump when osu chart/player skill mapping or aggregation semantics change.
    /// 2: PP+-shaped axes + stub engine (was official Diff aim/speed/FL/reading).
    /// </summary>
    public static class EzOsuSkillAlgorithm
    {
        public const int VERSION = 2;
    }
}
