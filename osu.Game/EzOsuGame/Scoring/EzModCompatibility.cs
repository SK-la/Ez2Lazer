// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Helpers for scores that still reference deleted/obsolete mod acronyms in <see cref="ScoreInfo.ModsJson"/>.
    /// Prefer reading <see cref="ScoreInfo.Mods"/> (which already strips <see cref="UnknownMod"/>);
    /// use these when you hold a raw <see cref="Mod"/> array or need to resolve from <see cref="APIMod"/> without UnknownMod.
    /// </summary>
    public static class EzModCompatibility
    {
        /// <summary>
        /// Removes <see cref="UnknownMod"/> entries from <paramref name="mods"/>.
        /// </summary>
        public static Mod[] StripUnknown(IEnumerable<Mod> mods)
        {
            ArgumentNullException.ThrowIfNull(mods);

            return mods.Where(m => m is not UnknownMod).ToArray();
        }

        /// <summary>
        /// Resolves <paramref name="score"/> mods, skipping unknown acronyms (does not instantiate <see cref="UnknownMod"/>).
        /// </summary>
        public static Mod[] ResolveFromScore(ScoreInfo score)
        {
            ArgumentNullException.ThrowIfNull(score);

            // ScoreInfo.Mods already strips UnknownMod and caches; reuse when possible.
            return StripUnknown(score.Mods);
        }

        /// <summary>
        /// Instantiates mods from API payloads for <paramref name="ruleset"/>, skipping unknown acronyms
        /// (aligned with <see cref="ModUtils.InstantiateValidModsForRuleset"/> / branch-library restore).
        /// </summary>
        public static Mod[] ResolveFromApiMods(Ruleset ruleset, IEnumerable<APIMod> apiMods, bool logSkipped = false)
        {
            ArgumentNullException.ThrowIfNull(ruleset);
            ArgumentNullException.ThrowIfNull(apiMods);

            var valid = new List<Mod>();
            List<string>? skipped = null;

            foreach (var apiMod in apiMods)
            {
                var mod = apiMod.ToMod(ruleset);

                if (mod is UnknownMod unknown)
                {
                    skipped ??= new List<string>();
                    skipped.Add(unknown.OriginalAcronym);
                    continue;
                }

                valid.Add(mod);
            }

            if (logSkipped && skipped != null)
            {
                Logger.Log(
                    $"Skipped unresolved score mods: {string.Join(", ", skipped.Distinct())}",
                    Ez2ConfigManager.LOGGER_NAME,
                    LogLevel.Debug);
            }

            return valid.ToArray();
        }
    }
}
