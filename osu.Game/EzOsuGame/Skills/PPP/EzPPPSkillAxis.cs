// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// osu!standard PP+-shaped skill axes (Jump / Flow / Precision / Speed / Stamina / Accuracy).
    /// These are the osu calculator's wire ids; other rulesets use <see cref="EzPPPSkillCatalog"/> directly.
    /// <see cref="Aim"/> is Aim Total (non-radar), like mania Overall.
    /// </summary>
    public enum EzPPPSkillAxis
    {
        [EzSkillMeta("aim", "综合", "Aim", "#c9cfdd", InRadar = false)]
        Aim,

        [EzSkillMeta("jump_aim", "跳", "Jump", "#8f6bd8")]
        JumpAim,

        [EzSkillMeta("flow_aim", "串", "Flow", "#6f87d8")]
        FlowAim,

        /// <summary>Jump term at raw (unscaled) distance, without the small-circle bonus. Only Precision reads it.</summary>
        [EzSkillMeta("raw_aim", "裸瞄", "Raw", "#6b7f9c", InRadar = false)]
        RawAim,

        [EzSkillMeta("precision", "小圈", "Precision", "#b06bc0")]
        Precision,

        [EzSkillMeta("speed", "速度", "Speed", "#ec6a9c")]
        Speed,

        [EzSkillMeta("stamina", "耐力", "Stamina", "#ad6b5d")]
        Stamina,

        /// <summary>Rhythm-complexity accuracy difficulty (not part of the star-rating sum).</summary>
        [EzSkillMeta("accuracy", "准度", "Accuracy", "#c59a5c")]
        Accuracy,

        [EzSkillMeta("star_rating", "星级", "Stars", "#c9cfdd", InRadar = false)]
        StarRating,
    }

    public static class EzOsuSkillAxisExtensions
    {
        public static EzPPPSkillAxis[] All => EzEnumMetaCache<EzPPPSkillAxis>.ALL;

        /// <summary>Radar polygon axes (excludes Aim Total).</summary>
        public static EzPPPSkillAxis[] RadarAxes { get; } = All.Where(a => a.Meta().InRadar).ToArray();

        public static EzSkillMetaAttribute Meta(this EzPPPSkillAxis axis)
            => EzEnumMetaCache<EzPPPSkillAxis>.Meta(axis);

        public static string ToId(this EzPPPSkillAxis axis) => axis.Meta().Id;

        public static string ToChartSkillId(this EzPPPSkillAxis axis) => axis.ToId();

        public static string ToPlayerSkillId(this EzPPPSkillAxis axis) => axis.ToId();

        public static EzSkillChip Chip(this EzPPPSkillAxis axis) => axis.Meta().Chip;

        public static bool TryParse(string? axisId, out EzPPPSkillAxis axis)
        {
            axis = default;

            if (string.IsNullOrEmpty(axisId))
                return false;

            string bare = axisId;
            int dot = axisId.LastIndexOf('.');
            if (dot >= 0 && dot < axisId.Length - 1)
                bare = axisId[(dot + 1)..];

            return EzEnumMetaCache<EzPPPSkillAxis>.TryParseIgnoreCase(bare, out axis);
        }
    }
}
