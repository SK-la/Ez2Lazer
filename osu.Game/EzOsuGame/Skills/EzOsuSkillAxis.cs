// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// osu!standard PP+-shaped skill axes (Jump / Flow / Precision / Speed / Stamina / Accuracy).
    /// <see cref="Aim"/> is Aim Total (non-radar), like mania Overall.
    /// </summary>
    public enum EzOsuSkillAxis
    {
        [EzSkillMeta("aim", "综合", "Aim", "#c9cfdd", InRadar = false)]
        Aim,

        [EzSkillMeta("jump_aim", "跳", "Jump", "#8f6bd8")]
        JumpAim,

        [EzSkillMeta("flow_aim", "串", "Flow", "#6f87d8")]
        FlowAim,

        [EzSkillMeta("precision", "小圈", "Precision", "#b06bc0")]
        Precision,

        [EzSkillMeta("speed", "速度", "Speed", "#ec6a9c")]
        Speed,

        [EzSkillMeta("stamina", "耐力", "Stamina", "#ad6b5d")]
        Stamina,

        [EzSkillMeta("accuracy", "准度", "Accuracy", "#c59a5c")]
        Accuracy,
    }

    public static class EzOsuSkillAxisExtensions
    {
        public static EzOsuSkillAxis[] All => EzEnumMetaCache<EzOsuSkillAxis>.ALL;

        /// <summary>Radar polygon axes (excludes Aim Total).</summary>
        public static EzOsuSkillAxis[] RadarAxes { get; } = All.Where(a => a.Meta().InRadar).ToArray();

        /// <summary>Chart catalog = all axes including Aim Total.</summary>
        public static EzOsuSkillAxis[] ChartAxes => All;

        /// <summary>Player catalog = all axes including Aim Total.</summary>
        public static EzOsuSkillAxis[] PlayerAxes => All;

        public static EzSkillMetaAttribute Meta(this EzOsuSkillAxis axis)
            => EzEnumMetaCache<EzOsuSkillAxis>.Meta(axis);

        public static string ToId(this EzOsuSkillAxis axis) => axis.Meta().Id;

        public static string ToChartSkillId(this EzOsuSkillAxis axis)
            => $"{EzSkillSystems.BEATMAP_PPPLUS}.{axis.ToId()}";

        public static string ToPlayerSkillId(this EzOsuSkillAxis axis)
            => $"{EzSkillSystems.PLAYER_PPPLUS}.{axis.ToId()}";

        public static EzSkillChip Chip(this EzOsuSkillAxis axis) => axis.Meta().Chip;

        public static bool TryParse(string? axisId, out EzOsuSkillAxis axis)
        {
            axis = default;

            if (string.IsNullOrEmpty(axisId))
                return false;

            string bare = axisId;
            int dot = axisId.LastIndexOf('.');
            if (dot >= 0 && dot < axisId.Length - 1)
                bare = axisId[(dot + 1)..];

            return EzEnumMetaCache<EzOsuSkillAxis>.TryParseIgnoreCase(bare, out axis);
        }
    }
}
