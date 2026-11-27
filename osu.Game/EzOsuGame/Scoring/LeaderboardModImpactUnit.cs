// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Scoring
{
    internal static class LeaderboardModImpactUnit
    {
        private readonly record struct ModIdentity(string Acronym, ModType Type);

        private readonly record struct SettingIdentity(ModIdentity Mod, string Name);

        private sealed record ModImpact(Mod Mod, ModIdentity Identity, IReadOnlyDictionary<string, object> Settings);

        // Use stable mod identifiers because ruleset-specific projects depend on osu.Game.
        private static readonly IReadOnlyDictionary<LeaderboardModFilter, HashSet<ModIdentity>> mod_exceptions = new Dictionary<LeaderboardModFilter, HashSet<ModIdentity>>
        {
            [LeaderboardModFilter.Rate] = new HashSet<ModIdentity>
            {
                new ModIdentity("NM", ModType.System),
                new ModIdentity("CS", ModType.Conversion), // Scroll visualisation only.
            },
            [LeaderboardModFilter.Difficulty] = new HashSet<ModIdentity>
            {
                new ModIdentity("NM", ModType.System),
                new ModIdentity("CL", ModType.Conversion), // Stable-style judgement behaviour.
            },
            [LeaderboardModFilter.Conversion] = new HashSet<ModIdentity>
            {
                new ModIdentity("NM", ModType.System),
                // new ModIdentity("MR", ModType.Conversion),
                new ModIdentity("CS", ModType.Conversion),
                new ModIdentity("CL", ModType.Conversion),
                new ModIdentity("OH", ModType.CommunityMod), // Deprecated fail-condition mod.
            },
        };

        // Mania Adjust's constant-speed option changes only scroll visualisation.
        private static readonly HashSet<SettingIdentity> setting_exceptions = new HashSet<SettingIdentity>
        {
            new SettingIdentity(new ModIdentity("AJ", ModType.CommunityMod), "constant_speed"),
        };

        private static readonly LeaderboardModFilter[] categories =
        {
            LeaderboardModFilter.Rate,
            LeaderboardModFilter.Difficulty,
            LeaderboardModFilter.Conversion,
        };

        public static Mod[] GetRelevantMods(IEnumerable<Mod> mods, LeaderboardModFilter filter)
            => getRelevantModImpacts(mods, filter).Select(impact => impact.Mod).ToArray();

        public static bool ModsMatch(IReadOnlyList<Mod> scoreMods, IReadOnlyList<Mod> selectedMods, LeaderboardModFilter filter)
        {
            if (filter == LeaderboardModFilter.None)
                return true;

            var unmatchedSelectedMods = getRelevantModImpacts(selectedMods, filter).ToList();
            var relevantScoreMods = getRelevantModImpacts(scoreMods, filter);

            if (unmatchedSelectedMods.Count != relevantScoreMods.Length)
                return false;

            foreach (var scoreMod in relevantScoreMods)
            {
                int matchingIndex = unmatchedSelectedMods.FindIndex(selectedMod => impactsMatch(scoreMod, selectedMod));

                if (matchingIndex < 0)
                    return false;

                unmatchedSelectedMods.RemoveAt(matchingIndex);
            }

            return unmatchedSelectedMods.Count == 0;
        }

        private static ModImpact[] getRelevantModImpacts(IEnumerable<Mod> mods, LeaderboardModFilter filter)
        {
            return mods.Select(createImpact)
                       .Where(impact => isRelevant(impact.Mod, filter) && !isExcepted(impact, filter))
                       .ToArray();
        }

        private static ModImpact createImpact(Mod mod)
        {
            var identity = new ModIdentity(mod.Acronym, mod.Type);
            var settings = mod.SettingsMap.ToDictionary(setting => setting.Key, setting => setting.Value.GetUnderlyingSettingValue());
            return new ModImpact(mod, identity, settings);
        }

        private static bool isRelevant(Mod mod, LeaderboardModFilter filter)
            => filter == LeaderboardModFilter.All
               || categories.Any(category => filter.HasFlag(category) && getCategories(mod).HasFlag(category));

        private static bool isExcepted(ModImpact impact, LeaderboardModFilter filter)
            => filter == LeaderboardModFilter.All
                ? categories.Any(category => mod_exceptions[category].Contains(impact.Identity))
                : categories.Any(category => filter.HasFlag(category)
                                             && getCategories(impact.Mod).HasFlag(category)
                                             && mod_exceptions[category].Contains(impact.Identity));

        private static LeaderboardModFilter getCategories(Mod mod)
        {
            var result = LeaderboardModFilter.None;

            if (mod is IApplicableToRate)
                result |= LeaderboardModFilter.Rate;

            if (mod is IApplicableToDifficulty)
                result |= LeaderboardModFilter.Difficulty;

            if (mod is IApplicableToBeatmapConverter
                || mod is IApplicableAfterBeatmapConversion and not IApplicableToDifficulty
                || mod.Type == ModType.Conversion)
                result |= LeaderboardModFilter.Conversion;

            return result;
        }

        private static bool impactsMatch(ModImpact scoreMod, ModImpact selectedMod)
        {
            if (scoreMod.Mod.GetType() != selectedMod.Mod.GetType() || scoreMod.Identity != selectedMod.Identity)
                return false;

            foreach (var (settingName, scoreSetting) in scoreMod.Settings)
            {
                if (setting_exceptions.Contains(new SettingIdentity(scoreMod.Identity, settingName)))
                    continue;

                if (!selectedMod.Settings.TryGetValue(settingName, out object? selectedSetting)
                    || !EqualityComparer<object>.Default.Equals(scoreSetting, selectedSetting))
                    return false;
            }

            return scoreMod.Settings.Keys.Count(settingName => !setting_exceptions.Contains(new SettingIdentity(scoreMod.Identity, settingName)))
                   == selectedMod.Settings.Keys.Count(settingName => !setting_exceptions.Contains(new SettingIdentity(selectedMod.Identity, settingName)));
        }
    }
}
