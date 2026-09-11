// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Realm <c>SystemId</c> namespaces and meta skill ids (not Mina axes).
    /// Topology (lean hybrid C):
    /// <list type="number">
    /// <item><b>Skills now</b> — <see cref="EzMinaSkillAxis"/> (RcMina). LN pattern skills are a future separate module.</item>
    /// <item><b>Dan</b> — parallel to metrics: <c>key×</c><see cref="EzDanSide"/> → <see cref="Dan.IEzDanLadder"/> + estimator;
    /// not under skills. Profile dan uses its own estimate table/algorithm.</item>
    /// <item><b>Chip</b> — optional UX axis (<see cref="EzChartDanVerdict.DominantAxis"/>); do not invent a parallel family enum.</item>
    /// </list>
    /// Catalogs register via <see cref="IEzSkillSystem"/> / <see cref="EzSkillRegistry"/>.
    /// Axis bare ids: <see cref="EzMinaSkillAxisExtensions.ToId"/>. Reads: <see cref="EzSkillProvider"/>; writers are separate DI services.
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
    }

    /// <summary>
    /// Pre-release: corrective formula fixes keep this at 1 (no client shipped yet).
    /// After public release, bump when MinaCalc note conversion or aggregation semantics change.
    /// </summary>
    public static class EzManiaSkillAlgorithm
    {
        public const int VERSION = 1;
    }
}
