// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Default MinaCalc / Etterna skillset axes (RcMina module).
    /// Metadata lives on each field via <see cref="EzSkillMetaAttribute"/>; use <see cref="EzMinaSkillAxisExtensions.All"/>.
    /// Per-keymode alternate axis sets register via <see cref="IEzSkillSystem"/> / <see cref="EzSkillRegistry"/>.
    /// LN pattern skills are a separate future module — do not merge into this enum.
    /// </summary>
    public enum EzMinaSkillAxis
    {
        [EzSkillMeta("overall", "综合", "Overall", "#c9cfdd", InRadar = false)]
        Overall,

        [EzSkillMeta("stream", "切", "Stream", "#8f6bd8")]
        Stream,

        [EzSkillMeta("jumpstream", "滑切", "Jumpstream", "#6f87d8")]
        Jumpstream,

        [EzSkillMeta("handstream", "多切", "Handstream", "#b06bc0")]
        Handstream,

        [EzSkillMeta("stamina", "耐力", "Stamina", "#ad6b5d")]
        Stamina,

        [EzSkillMeta("jack", "叠", "Jack", "#ec6a9c")]
        JackSpeed,

        [EzSkillMeta("chordjack", "大叠", "Chordjack", "#c59a5c")]
        Chordjack,

        [EzSkillMeta("tech", "技", "Tech", "#83cf6b")]
        Technical,
    }

    public static class EzMinaSkillAxisExtensions
    {
        /// <summary>All axes from <see cref="Enum.GetValues{TEnum}"/> (cached).</summary>
        public static EzMinaSkillAxis[] All => EzEnumMetaCache<EzMinaSkillAxis>.All;

        /// <summary>Axes with <see cref="EzSkillMetaAttribute.InRadar"/> (excludes Overall).</summary>
        public static EzMinaSkillAxis[] RadarAxes { get; } = All.Where(a => a.Meta().InRadar).ToArray();

        public static EzSkillMetaAttribute Meta(this EzMinaSkillAxis axis)
            => EzEnumMetaCache<EzMinaSkillAxis>.Meta(axis);

        public static string ToId(this EzMinaSkillAxis axis) => axis.Meta().Id;

        public static string ToMsdSkillId(this EzMinaSkillAxis axis)
            => $"{EzSkillSystems.BEATMAP_MSD}.{axis.ToId()}";

        public static string ToSsrSkillId(this EzMinaSkillAxis axis)
            => $"{EzSkillSystems.PLAYER_SSR}.{axis.ToId()}";

        public static EzSkillChip Chip(this EzMinaSkillAxis axis) => axis.Meta().Chip;

        public static bool TryParse(string? axisId, out EzMinaSkillAxis axis)
        {
            axis = default;

            if (string.IsNullOrEmpty(axisId))
                return false;

            string bare = axisId;
            int dot = axisId.LastIndexOf('.');
            if (dot >= 0 && dot < axisId.Length - 1)
                bare = axisId[(dot + 1)..];

            return EzEnumMetaCache<EzMinaSkillAxis>.TryParseIgnoreCase(bare, out axis);
        }
    }
}
