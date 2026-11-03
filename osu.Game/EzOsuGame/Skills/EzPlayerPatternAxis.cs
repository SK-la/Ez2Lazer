// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Hub <c>PATTERN_RATING_META</c> axes for 6/7/8K player pattern ratings
    /// (<c>player_pattern.*</c>). Distinct from <see cref="EzMinaSkillAxis"/> (Mina)
    /// and <see cref="EzPatternAxis"/> (Key Pattern / xxy Title-Case wires).
    /// </summary>
    public enum EzPlayerPatternAxis
    {
        [EzSkillMeta("chordstream", "大切", "Chordstream", "#5ab2f2")]
        Chordstream,

        [EzSkillMeta("bracket", "衩", "Bracket", "#f3c24a")]
        Bracket,

        [EzSkillMeta("delay", "延迟", "Delay", "#46c7b8")]
        Delay,

        [EzSkillMeta("stream", "切", "Stream", "#8f6bd8")]
        Stream,

        [EzSkillMeta("jack", "叠", "Jack", "#ec6a9c")]
        Jack,

        [EzSkillMeta("tech", "技", "Tech", "#83cf6b")]
        Tech,

        [EzSkillMeta("ln", "LN", "LN", "#f07474")]
        Ln,
    }

    public static class EzPlayerPatternAxisExtensions
    {
        public static EzPlayerPatternAxis[] All => EzEnumMetaCache<EzPlayerPatternAxis>.ALL;

        public static EzSkillMetaAttribute Meta(this EzPlayerPatternAxis axis)
            => EzEnumMetaCache<EzPlayerPatternAxis>.Meta(axis);

        public static string ToId(this EzPlayerPatternAxis axis) => axis.Meta().Id;

        public static string ToSkillId(this EzPlayerPatternAxis axis)
            => $"{EzSkillSystems.PLAYER_PATTERN}.{axis.ToId()}";

        public static EzSkillChip Chip(this EzPlayerPatternAxis axis) => axis.Meta().Chip;

        public static bool TryParse(string? patternId, out EzPlayerPatternAxis axis)
        {
            axis = default;

            if (string.IsNullOrEmpty(patternId))
                return false;

            string bare = patternId;
            int dot = patternId.LastIndexOf('.');
            if (dot >= 0 && dot < patternId.Length - 1)
                bare = patternId[(dot + 1)..];

            return EzEnumMetaCache<EzPlayerPatternAxis>.TryParseIgnoreCase(bare, out axis);
        }
    }
}
