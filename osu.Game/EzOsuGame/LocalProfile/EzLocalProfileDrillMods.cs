// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public static class EzLocalProfileDrillMods
    {
        public static Mod[] Resolve(EzLocalProfileDrillScoreRow row, RulesetStore rulesets)
            => Resolve(row, rulesets.GetRuleset(row.RulesetId)?.CreateInstance());

        public static Mod[] Resolve(EzLocalProfileDrillScoreRow row, Ruleset? ruleset)
        {
            if (string.IsNullOrEmpty(row.ModsJson) || ruleset == null)
                return Array.Empty<Mod>();

            try
            {
                var apiMods = JsonConvert.DeserializeObject<APIMod[]>(row.ModsJson) ?? Array.Empty<APIMod>();
                var mods = new List<Mod>();

                foreach (var apiMod in apiMods)
                {
                    var mod = ruleset.CreateModFromAcronym(apiMod.Acronym);
                    if (mod != null)
                        mods.Add(mod);
                }

                return mods.ToArray();
            }
            catch
            {
                return Array.Empty<Mod>();
            }
        }
    }
}
