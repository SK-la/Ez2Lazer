// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Scoring
{
    internal sealed class LeaderboardModImpactUnit
    {
        public const string FILENAME = "LeaderboardModImpactExceptions.json";

        private const int current_document_version = 1;

        private static readonly JsonSerializerSettings json_settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private sealed record ModImpact(Mod Mod, string ModType, IReadOnlyDictionary<string, object> Settings);

        private readonly IReadOnlyDictionary<LeaderboardModFilter, HashSet<string>> modExceptions;

        private static readonly LeaderboardModFilter[] categories =
        {
            LeaderboardModFilter.Rate,
            LeaderboardModFilter.Difficulty,
            LeaderboardModFilter.Conversion,
        };

        private LeaderboardModImpactUnit(LeaderboardModImpactExceptionsDocument document)
        {
            if (document.Version != current_document_version)
                throw new JsonSerializationException($"Unsupported exception list version {document.Version}.");

            modExceptions = new Dictionary<LeaderboardModFilter, HashSet<string>>
            {
                [LeaderboardModFilter.Rate] = new HashSet<string>(document.Rate ?? [], StringComparer.Ordinal),
                [LeaderboardModFilter.Difficulty] = new HashSet<string>(document.Difficulty ?? [], StringComparer.Ordinal),
                [LeaderboardModFilter.Conversion] = new HashSet<string>(document.Conversion ?? [], StringComparer.Ordinal),
            };
        }

        public static LeaderboardModImpactUnit CreateDefault() => new LeaderboardModImpactUnit(createDefaultDocument());

        public static LeaderboardModImpactUnit Load(Storage configStorage)
        {
            var defaults = createDefaultDocument();

            try
            {
                if (!configStorage.Exists(FILENAME))
                {
                    writeDocument(configStorage, defaults);
                    return new LeaderboardModImpactUnit(defaults);
                }

                using (var stream = configStorage.GetStream(FILENAME))
                {
                    if (stream == null)
                        throw new IOException($"Could not open {FILENAME}.");

                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        var document = JsonConvert.DeserializeObject<LeaderboardModImpactExceptionsDocument>(json, json_settings);

                        if (document == null)
                            throw new JsonSerializationException($"{FILENAME} did not contain an exception list.");

                        return new LeaderboardModImpactUnit(document);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to load leaderboard mod exceptions {FILENAME}");
                return new LeaderboardModImpactUnit(defaults);
            }
        }

        private static void writeDocument(Storage configStorage, LeaderboardModImpactExceptionsDocument document)
        {
            using (var stream = configStorage.CreateFileSafely(FILENAME))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
                writer.Write(JsonConvert.SerializeObject(document, json_settings));
        }

        private static LeaderboardModImpactExceptionsDocument createDefaultDocument() => new LeaderboardModImpactExceptionsDocument
        {
            Version = current_document_version,
            Rate =
            [
                "osu.Game.Rulesets.Mods.ModNoMod",
                "osu.Game.Rulesets.Mods.ModScoreV2",
                "osu.Game.Rulesets.Mania.Mods.ManiaModScoreV2",
                "osu.Game.Rulesets.Mania.Mods.ManiaModConstantSpeed",
                "osu.Game.Rulesets.Taiko.Mods.TaikoModConstantSpeed",
            ],
            Difficulty =
            [
                "osu.Game.Rulesets.Mods.ModNoMod",
                "osu.Game.Rulesets.Mods.ModScoreV2",
                "osu.Game.Rulesets.Mania.Mods.ManiaModScoreV2",
                "osu.Game.Rulesets.Osu.Mods.OsuModClassic",
                "osu.Game.Rulesets.Taiko.Mods.TaikoModClassic",
                "osu.Game.Rulesets.Catch.Mods.CatchModClassic",
                "osu.Game.Rulesets.Mania.Mods.ManiaModClassic",
            ],
            Conversion =
            [
                "osu.Game.Rulesets.Mods.ModNoMod",
                "osu.Game.Rulesets.Mods.ModScoreV2",
                "osu.Game.Rulesets.Mania.Mods.ManiaModScoreV2",
                "osu.Game.Rulesets.Mania.Mods.ManiaModConstantSpeed",
                "osu.Game.Rulesets.Taiko.Mods.TaikoModConstantSpeed",
                "osu.Game.Rulesets.Osu.Mods.OsuModClassic",
                "osu.Game.Rulesets.Taiko.Mods.TaikoModClassic",
                "osu.Game.Rulesets.Catch.Mods.CatchModClassic",
                "osu.Game.Rulesets.Mania.Mods.ManiaModClassic",
                "osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod.ManiaModO2Health",
            ],
        };

        public Mod[] GetRelevantMods(IEnumerable<Mod> mods, LeaderboardModFilter filter)
            => getRelevantModImpacts(mods, filter).Select(impact => impact.Mod).ToArray();

        public bool ModsMatch(IReadOnlyList<Mod> scoreMods, IReadOnlyList<Mod> selectedMods, LeaderboardModFilter filter)
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

        private ModImpact[] getRelevantModImpacts(IEnumerable<Mod> mods, LeaderboardModFilter filter)
        {
            return mods.Select(createImpact)
                       .Where(impact => isRelevant(impact.Mod, filter) && !isExcepted(impact, filter))
                       .ToArray();
        }

        private static ModImpact createImpact(Mod mod)
        {
            var settings = mod.SettingsMap.ToDictionary(setting => setting.Key, setting => setting.Value.GetUnderlyingSettingValue());
            return new ModImpact(mod, mod.GetType().FullName ?? mod.GetType().Name, settings);
        }

        private static bool isRelevant(Mod mod, LeaderboardModFilter filter)
            => filter == LeaderboardModFilter.All
               || categories.Any(category => filter.HasFlag(category) && getCategories(mod).HasFlag(category));

        private bool isExcepted(ModImpact impact, LeaderboardModFilter filter)
            => filter == LeaderboardModFilter.All
                ? categories.Any(category => modExceptions[category].Contains(impact.ModType))
                : categories.Any(category => filter.HasFlag(category)
                                             && getCategories(impact.Mod).HasFlag(category)
                                             && modExceptions[category].Contains(impact.ModType));

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

        private bool impactsMatch(ModImpact scoreMod, ModImpact selectedMod)
        {
            if (scoreMod.ModType != selectedMod.ModType)
                return false;

            foreach (var (settingName, scoreSetting) in scoreMod.Settings)
            {
                if (!selectedMod.Settings.TryGetValue(settingName, out object? selectedSetting)
                    || !EqualityComparer<object>.Default.Equals(scoreSetting, selectedSetting))
                    return false;
            }

            return scoreMod.Settings.Count == selectedMod.Settings.Count;
        }
    }

    public sealed class LeaderboardModImpactExceptionsDocument
    {
        public int Version { get; set; } = 1;

        public List<string>? Rate { get; set; }

        public List<string>? Difficulty { get; set; }

        public List<string>? Conversion { get; set; }
    }
}
