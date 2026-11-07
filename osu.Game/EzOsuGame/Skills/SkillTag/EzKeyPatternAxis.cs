// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    /// Key pattern radar axes. Wire labels are Title Case from ruleset analysis.
    /// Distinct from the Mina rating axes and from the player pattern rating axes.
    /// </summary>
    public enum EzKeyPatternAxis
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

    public static class EzKeyPatternAxisExtensions
    {
        public static EzKeyPatternAxis[] All => EzEnumMetaCache<EzKeyPatternAxis>.ALL;

        public static EzSkillMetaAttribute Meta(this EzKeyPatternAxis axis)
            => EzEnumMetaCache<EzKeyPatternAxis>.Meta(axis);

        public static string ToWireLabel(this EzKeyPatternAxis axis) => axis.Meta().Id;

        public static LocalisableString DisplayName(this EzKeyPatternAxis axis) => axis.Meta().DisplayName;

        public static bool TryParse(string? wire, out EzKeyPatternAxis axis)
            => EzEnumMetaCache<EzKeyPatternAxis>.TryParse(wire, out axis);
    }
}
