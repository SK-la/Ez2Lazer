// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// RC vs LN dan track (赛道). Wire id on <see cref="EzSkillMetaAttribute"/>.
    /// Not a skill module — orthogonal to RcMina / future LnPattern axes.
    /// </summary>
    public enum EzDanSide
    {
        [EzSkillMeta("rc", "RC", "RC")]
        Rc,

        [EzSkillMeta("ln", "LN", "LN")]
        Ln,
    }

    /// <summary>
    /// xxy / KeyPattern radar axes. Wire labels are Title Case from ruleset analysis.
    /// </summary>
    public enum EzPatternAxis
    {
        [EzSkillMeta("Bracket", "衩", "Bracket")]
        Bracket,

        [EzSkillMeta("Chord", "合键", "Chord")]
        Chord,

        [EzSkillMeta("Chordstream", "大切", "Chordstream")]
        Chordstream,

        [EzSkillMeta("Jack", "叠", "Jack")]
        Jack,

        [EzSkillMeta("Delay", "延迟", "Delay")]
        Delay,

        [EzSkillMeta("Dump", "乱切", "Dump")]
        Dump,

        [EzSkillMeta("Burst", "爆发", "Burst")]
        Burst,

        [EzSkillMeta("Anchor", "锚点", "Anchor")]
        Anchor,

        [EzSkillMeta("LN Hold", "长条按住", "LN Hold")]
        LnHold,

        [EzSkillMeta("LN Release", "放手", "Release")]
        LnRelease,

        [EzSkillMeta("LN%", "长条%", "LN%")]
        LnPercent,

        [EzSkillMeta("Tech", "技", "Tech")]
        Tech,

        [EzSkillMeta("Stream", "切", "Stream")]
        Stream,
    }

    public static class EzDanSideExtensions
    {
        public static EzDanSide[] All => EzEnumMetaCache<EzDanSide>.ALL;

        public static EzSkillMetaAttribute Meta(this EzDanSide side)
            => EzEnumMetaCache<EzDanSide>.Meta(side);

        public static string ToId(this EzDanSide side) => side.Meta().Id;

        public static bool TryParse(string? id, out EzDanSide side)
            => EzEnumMetaCache<EzDanSide>.TryParseIgnoreCase(id, out side);

        public static EzDanSide ParseOrRc(string? id)
            => TryParse(id, out var side) ? side : EzDanSide.Rc;

        public static bool IsLn(this EzDanSide side) => side == EzDanSide.Ln;

        public static LocalisableString TrackDisplayName(this EzDanSide side, int keyCount)
        {
            return side == EzDanSide.Ln
                ? new EzLocalizationManager.EzLocalisableString($"{keyCount}K LN 段位", $"{keyCount}K LN dan")
                : new EzLocalizationManager.EzLocalisableString($"{keyCount}K 段位", $"{keyCount}K Regular dan");
        }
    }

    public static class EzPatternAxisExtensions
    {
        public static EzPatternAxis[] All => EzEnumMetaCache<EzPatternAxis>.ALL;

        public static EzSkillMetaAttribute Meta(this EzPatternAxis axis)
            => EzEnumMetaCache<EzPatternAxis>.Meta(axis);

        public static string ToWireLabel(this EzPatternAxis axis) => axis.Meta().Id;

        public static LocalisableString DisplayName(this EzPatternAxis axis) => axis.Meta().DisplayName;

        public static bool TryParse(string? wire, out EzPatternAxis axis)
            => EzEnumMetaCache<EzPatternAxis>.TryParse(wire, out axis);
    }
}
