// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Skills.Dan
{
    /// <summary>
    /// RC vs LN dan track (赛道). Wire id on <see cref="EzSkillMetaAttribute"/>.
    /// Not a skill module — orthogonal to the rating axes.
    /// </summary>
    public enum EzDanSide
    {
        [EzSkillMeta("rc", "RC", "RC")]
        Rc,

        [EzSkillMeta("ln", "LN", "LN")]
        Ln,
    }

    public static class EzDanSideExtensions
    {
        public static EzDanSide[] All => EzEnumMetaCache<EzDanSide>.ALL;

        public static EzSkillMetaAttribute Meta(this EzDanSide side) => EzEnumMetaCache<EzDanSide>.Meta(side);

        public static string ToId(this EzDanSide side) => side.Meta().Id;

        public static bool TryParse(string? id, out EzDanSide side) => EzEnumMetaCache<EzDanSide>.TryParseIgnoreCase(id, out side);

        public static EzDanSide ParseOrRc(string? id) => TryParse(id, out var side) ? side : EzDanSide.Rc;

        public static bool IsLn(this EzDanSide side) => side == EzDanSide.Ln;

        public static LocalisableString TrackDisplayName(this EzDanSide side, int keyCount)
        {
            return side == EzDanSide.Ln
                ? new EzLocalizationManager.EzLocalisableString($"{keyCount}K LN 段位", $"{keyCount}K LN dan")
                : new EzLocalizationManager.EzLocalisableString($"{keyCount}K 段位", $"{keyCount}K Regular dan");
        }
    }
}
