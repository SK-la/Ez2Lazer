// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>Display name + accent for a skill chip / registry entry.</summary>
    public readonly record struct EzSkillChip(LocalisableString Name, string AccentHex)
    {
        public static readonly EzSkillChip FALLBACK = new EzSkillChip(string.Empty, "#8f6bd8");
    }
}
