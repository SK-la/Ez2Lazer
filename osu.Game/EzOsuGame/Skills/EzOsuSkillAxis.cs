// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// osu!standard skill axes (official difficulty / performance portions).
    /// Chart systems omit <see cref="Accuracy"/>; player systems include it.
    /// </summary>
    public enum EzOsuSkillAxis
    {
        [EzSkillMeta("aim", "准度", "Aim", "#8f6bd8")]
        Aim,

        [EzSkillMeta("speed", "速度", "Speed", "#6f87d8")]
        Speed,

        [EzSkillMeta("flashlight", "暗场", "Flashlight", "#b06bc0")]
        Flashlight,

        [EzSkillMeta("reading", "读图", "Reading", "#83cf6b")]
        Reading,

        /// <summary>Performance-only axis (pp_acc); not present on chart difficulty attrs.</summary>
        [EzSkillMeta("accuracy", "准确", "Accuracy", "#c59a5c")]
        Accuracy,
    }

    public static class EzOsuSkillAxisExtensions
    {
        public static EzOsuSkillAxis[] All => EzEnumMetaCache<EzOsuSkillAxis>.ALL;

        /// <summary>Chart / Diff radar axes (excludes Accuracy).</summary>
        public static EzOsuSkillAxis[] ChartAxes { get; } =
            All.Where(a => a != EzOsuSkillAxis.Accuracy).ToArray();

        /// <summary>Player / Perf radar axes (all, including Accuracy).</summary>
        public static EzOsuSkillAxis[] PlayerAxes => All;

        public static EzOsuSkillAxis[] RadarAxes { get; } = All.Where(a => a.Meta().InRadar).ToArray();

        public static EzSkillMetaAttribute Meta(this EzOsuSkillAxis axis)
            => EzEnumMetaCache<EzOsuSkillAxis>.Meta(axis);

        public static string ToId(this EzOsuSkillAxis axis) => axis.Meta().Id;

        public static string ToDiffSkillId(this EzOsuSkillAxis axis)
            => $"{EzSkillSystems.BEATMAP_OSU_DIFF}.{axis.ToId()}";

        public static string ToPerfSkillId(this EzOsuSkillAxis axis)
            => $"{EzSkillSystems.PLAYER_OSU_PERF}.{axis.ToId()}";

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
