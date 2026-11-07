// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Realm <c>SystemId</c> namespaces and meta skill ids.
    /// Mania (Mina / pattern / dan) plugins register under <see cref="EzSkillRegistry"/>;
    /// ruleset wiring is <see cref="EzSkillProfile"/>.
    /// </summary>
    public static class EzSkillSystems
    {
        public const string BEATMAP_MINA = "beatmap_msd";
        public const string PLAYER_MINA = "player_ssr";

        /// <summary>
        /// Hub pattern ratings (Overall SSR aggregated per chart pattern tag).
        /// Persisted in existing <see cref="EzPlayerSkillValue"/> rows — see DATA-Skills-PatternRatings.
        /// </summary>
        public const string PLAYER_PATTERN = "player_pattern";

        public const string DAN = "dan";

        /// <summary>PP+ chart axes.</summary>
        public const string BEATMAP_PPP = "beatmap_ppp";

        /// <summary>PP+ player axes.</summary>
        public const string PLAYER_PPP = "player_ppp";

        /// <summary>Key count sentinel for producers that are not sliced by key mode.</summary>
        public const int UNSLICED_KEY = 0;

        /// <summary>Persisted beside MSD axes when computed (not a Mina axis).</summary>
        public const string HOLD_RATIO = "__hold_ratio";

        /// <summary>
        /// Settled miss for charts MinaCalc rates as a zero vector on a supported keymode.
        /// Keyed by beatmap content hash — editing the chart changes the hash and allows recompute.
        /// Not a valid MSD cache (<see cref="EzBeatmapMinaComputer.IsCurrentMinaCache"/> stays false).
        /// </summary>
        public const string UNRATEABLE = "__unrateable";

        public static string MinaHoldRatioSkillId => $"{BEATMAP_MINA}.{HOLD_RATIO}";

        public static string MinaUnrateableSkillId => $"{BEATMAP_MINA}.{UNRATEABLE}";

        public static string DanSkillId(int keyCount, EzDanSide side)
            => $"{DAN}.{keyCount}k.{side.ToId()}";

        /// <summary>
        /// Default algorithm version for skill-value rows of <paramref name="systemId"/>.
        /// Unknown ids return 0 (caller should pass an explicit version).
        /// </summary>
        public static int ResolveAlgorithmVersion(string systemId) => systemId switch
        {
            BEATMAP_MINA or PLAYER_MINA or PLAYER_PATTERN => EzMinaSkillAlgorithm.VERSION,
            _ => 0,
        };
    }

    /// <summary>
    /// Bump when MinaCalc note conversion or aggregation semantics change.
    /// 2: unified onto the 4-18K n-key MinaCalc wasm (was NuGet MinaCalc 0.4.2, 4/6/7K only).
    /// </summary>
    public static class EzMinaSkillAlgorithm
    {
        public const int VERSION = 2;
    }
}
