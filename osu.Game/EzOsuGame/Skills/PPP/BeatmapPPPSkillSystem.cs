// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// Chart-side PPP skills for osu, taiko, catch, and mania.
    /// Mania keeps MSD as the default radar; the other three rulesets default to PPP.
    /// </summary>
    public sealed class BeatmapPPPSkillSystem : IEzSkillSystem
    {
        public string SystemId => EzSkillSystems.BEATMAP_PPP;

        public EzSkillScope Scope => EzSkillScope.Beatmap;

        public bool IsDefaultRadar => false;

        public bool IsDefaultRadarFor(int rulesetOnlineId)
            => AppliesToRuleset(rulesetOnlineId) && rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID;

        public IReadOnlyList<EzSkillDefinition> Skills { get; } = EzPPPSkillCatalog.CreateDefinitions(EzSkillScope.Beatmap);

        public bool AppliesToRuleset(int rulesetOnlineId)
            => EzPPPSkillCatalog.SupportsRuleset(rulesetOnlineId);
    }
}
