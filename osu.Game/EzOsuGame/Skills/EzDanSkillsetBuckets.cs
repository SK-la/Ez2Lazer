// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Skills.Dan;

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
    ///     Hub <c>danSkillsetBuckets</c> slot table + thin 4K RC filing.
    ///     <para>
    ///         Capability matrix (do not silently drop keys in UI — always call <see cref="Slots" />):
    ///         4K RC: jack/tech/speed/stamina (MSD DominantAxis filing — this PR);
    ///         6/7K RC: jack/tech/speed/stream — TODO(data): pattern-tag / cluster filing;
    ///         4K/6K LN: empty slots (side aggregate only);
    ///         7K LN: lngeneral/lntech/lninverse/lnrelease — TODO(data): LN pattern filing;
    ///         LeoBlack jackDemand / Jumpstream arbitration — TODO(data).
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
        ///     Thin 4K RC skillset dans from clears + MSD dominant axis.
        ///     Other key×side: empty (slots still from <see cref="Slots" />).
        /// </summary>
        public static IReadOnlyDictionary<string, EzDanSkillsetVerdict> ComputeFromClears(
            int keyCount,
            EzDanSide side,
            IReadOnlyList<EzDanClearEvidenceRow> clears,
            Func<string, EzMinaSkillAxis?> resolveDominantAxis)
        {
            var result = new Dictionary<string, EzDanSkillsetVerdict>(StringComparer.Ordinal);

            if (side != EzDanSide.Rc || keyCount != 4 || clears.Count == 0)
            {
                // TODO(data): 6/7K RC pattern-tag / cluster filing
                // TODO(data): 7K LN pattern (lngeneral/lntech/lninverse/lnrelease) filing
                return result;
            }

            var buckets = new Dictionary<string, List<double>>(StringComparer.Ordinal)
            {
                [JACK] = new List<double>(),
                [TECH] = new List<double>(),
                [SPEED] = new List<double>(),
                [STAMINA] = new List<double>()
            };

            foreach (var clear in clears)
            {
                if (clear.KeyCount != keyCount)
                    continue;
                if (!string.Equals(clear.Side, side.ToId(), StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrEmpty(clear.BeatmapHash))
                    continue;

                var axis = resolveDominantAxis(clear.BeatmapHash);
                if (axis is not EzMinaSkillAxis a)
                    continue;

                string? skillsetId = TryMapMinaAxisToSkillset(a);
                if (skillsetId == null)
                    continue;

                buckets[skillsetId].Add(clear.CreditedDan);
            }

            var ladder = EzDanLadders.For(keyCount, EzDanSide.Rc);

            foreach ((string id, var values) in buckets)
            {
                if (values.Count < EzDanAlgorithm.CLEAR_QUORUM)
                    continue;

                var window = values
                             .OrderByDescending(v => v)
                             .Take(EzDanAlgorithm.CLEAR_WINDOW)
                             .ToList();

                double rawDan = window.Average();
                result[id] = new EzDanSkillsetVerdict(id, rawDan, ladder.ParseLabel(rawDan), values.Count);
            }

            return result;
        }
    }
}
