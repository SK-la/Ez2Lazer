// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Skills
{
    public enum EzSkillScope
    {
        Beatmap = 0,
        Player = 1,
    }

    public readonly record struct EzSkillDefinition(
        string SystemId,
        string SkillId,
        LocalisableString DisplayName,
        EzSkillScope Scope,
        string AccentHex);
}
