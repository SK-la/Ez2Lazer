// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    ///     Hub-aligned dan skillset slot (evidence-window tiles). Layout is keyed by this, not Mina axes.
    /// </summary>
    public readonly record struct EzDanSkillsetSlot(string Id, LocalisableString DisplayName, string AccentHex);

    /// <summary>
    ///     One skillset dan verdict (clear-bucket average). Absent from <see cref="EzSkillProvider.GetDanSkillsets" /> when
    ///     under quorum / no filing data.
    /// </summary>
    public readonly record struct EzDanSkillsetVerdict(string SkillsetId, double RawDan, string Label, int Clears);

    /// <summary>
    ///     Hub <c>danSkillsetBuckets</c> slot table for DualPanel layout.
    ///     Filing / verdicts: <see cref="EzDanSkillsetFiling"/>.
    ///     <para>
    ///         Capability matrix (always call <see cref="Slots(int, EzDanSide)"/> for layout):
        ///         4K RC: jack/tech/speed/stamina (play SSR + chart overrides);
        ///         6/7K RC: jack/tech/speed/stream (pattern tags; LeoBlack clusters when filled — see DATA-LeoBlack-Clusters);
    ///         4K/6K LN: empty slots (side aggregate only);
    ///         7K LN: lngeneral/lntech/lninverse/lnrelease (LN pattern tags when chart present).
    ///     </para>
    /// </summary>
    public static class EzDanSkillsetBuckets
    {
        public const string JACK = "jack";
        public const string TECH = "tech";
        public const string SPEED = "speed";
        public const string STAMINA = "stamina";
        public const string STREAM = "stream";
        public const string LN_GENERAL = "lngeneral";
        public const string LN_TECH = "lntech";
        public const string LN_INVERSE = "lninverse";
        public const string LN_RELEASE = "lnrelease";

        /// <summary>
        /// Persisted when a key×side was recomputed but produced no skillset tiles
        /// (so readers can distinguish cache hit-empty from never cached).
        /// </summary>
        public const string CACHE_EMPTY_SENTINEL = "__empty__";

        private static readonly EzDanSkillsetSlot[] rc_4k =
        {
            new EzDanSkillsetSlot(JACK, "Jack", "#ec6a9c"),
            new EzDanSkillsetSlot(TECH, "Tech", "#83cf6b"),
            new EzDanSkillsetSlot(SPEED, "Speed", "#5ab2f2"),
            new EzDanSkillsetSlot(STAMINA, "Stamina", "#ad6b5d")
        };

        private static readonly EzDanSkillsetSlot[] rc_pattern =
        {
            new EzDanSkillsetSlot(JACK, "Jack", "#ec6a9c"),
            new EzDanSkillsetSlot(TECH, "Tech", "#83cf6b"),
            new EzDanSkillsetSlot(SPEED, "Speed", "#5ab2f2"),
            new EzDanSkillsetSlot(STREAM, "Stream", "#8f6bd8")
        };

        private static readonly EzDanSkillsetSlot[] ln_7k =
        {
            new EzDanSkillsetSlot(LN_GENERAL, "General", "#f07474"),
            new EzDanSkillsetSlot(LN_TECH, "Tech", "#83cf6b"),
            new EzDanSkillsetSlot(LN_INVERSE, "Inverse", "#c59a5c"),
            new EzDanSkillsetSlot(LN_RELEASE, "Release", "#46c7b8")
        };

        /// <summary>Ordered slots for DualPanel layout — returned even when verdicts are empty.</summary>
        public static IReadOnlyList<EzDanSkillsetSlot> Slots(int keyCount, EzDanSide side)
        {
            if (side == EzDanSide.Ln)
            {
                // Hub: only 7K LN publishes skillset tiles.
                return keyCount == 7 ? ln_7k : Array.Empty<EzDanSkillsetSlot>();
            }

            return keyCount == 4 ? rc_4k : rc_pattern;
        }

        public static IReadOnlyList<EzDanSkillsetSlot> Slots(int keyCount, string sideId)
        {
            return Slots(keyCount, EzDanSideExtensions.ParseOrRc(sideId));
        }

        /// <summary>4K RC: map Mina DominantAxis → skillset id. Null when unmapped.</summary>
        public static string? TryMapMinaAxisToSkillset(EzMinaSkillAxis axis)
        {
            return axis switch
            {
                EzMinaSkillAxis.JackSpeed or EzMinaSkillAxis.Chordjack => JACK,
                EzMinaSkillAxis.Technical or EzMinaSkillAxis.Jumpstream => TECH,
                EzMinaSkillAxis.Stream => SPEED,
                EzMinaSkillAxis.Handstream or EzMinaSkillAxis.Stamina => STAMINA,
                _ => null
            };
        }

        /// <summary>
        ///     Skillset dans from clears via hub-aligned filing (<see cref="EzDanSkillsetFiling.ComputeVerdicts"/>).
        /// </summary>
        /// <param name="resolvePlayValues">Hub <c>play.values</c> per clear (Mina SSR), not beatmap MSD.</param>
        public static IReadOnlyDictionary<string, EzDanSkillsetVerdict> ComputeFromClears(
            int keyCount,
            EzDanSide side,
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            Func<EzDanClearEvidenceRow, IReadOnlyDictionary<string, double>?> resolvePlayValues,
            Func<string, EzChartSkillInfo?>? resolveChart = null)
            => EzDanSkillsetFiling.ComputeVerdicts(
                keyCount,
                side,
                clears,
                resolvePlayValues,
                resolveChart ?? (_ => null));

        /// <summary>
        /// Test / chart-label helper: resolve play values by beatmap hash only (same vector for every clear on that chart).
        /// </summary>
        public static IReadOnlyDictionary<string, EzDanSkillsetVerdict> ComputeFromClears(
            int keyCount,
            EzDanSide side,
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            Func<string, IReadOnlyDictionary<string, double>?> resolvePlayValuesByHash,
            Func<string, EzChartSkillInfo?>? resolveChart = null)
            => ComputeFromClears(
                keyCount,
                side,
                clears,
                clear => resolvePlayValuesByHash(clear.BeatmapHash),
                resolveChart);
    }
}
