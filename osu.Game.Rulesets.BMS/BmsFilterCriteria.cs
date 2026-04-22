// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Rulesets.Filter;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Rulesets.BMS
{
    /// <summary>
    /// Song-select filter for BMS charts. Key count is read from <see cref="BeatmapDifficulty.CircleSize"/>
    /// (indexed during library scan), not from <see cref="Mania.Beatmaps.ManiaBeatmapConverter"/> heuristics.
    /// </summary>
    public class BmsFilterCriteria : IRulesetFilterCriteria
    {
        private readonly HashSet<int> includedKeyCounts = Enumerable.Range(1, LegacyBeatmapDecoder.MAX_MANIA_KEY_COUNT).ToHashSet();
        private FilterCriteria.OptionalRange<float> longNotePercentage;

        public bool Matches(BeatmapInfo beatmapInfo, FilterCriteria criteria)
        {
            int keyCount = Math.Max(1, (int)Math.Round(beatmapInfo.Difficulty.CircleSize));
            bool keyCountMatch = includedKeyCounts.Contains(keyCount);
            bool longNotePercentageMatch = !longNotePercentage.HasFilter
                                           || longNotePercentage.IsInRange(calculateLongNotePercentage(beatmapInfo));

            return keyCountMatch && longNotePercentageMatch;
        }

        public bool TryParseCustomKeywordCriteria(string key, Operator op, string strValues)
        {
            switch (key)
            {
                case "key":
                case "keys":
                {
                    var keyCounts = new HashSet<int>();

                    foreach (string strValue in strValues.Split(','))
                    {
                        if (!int.TryParse(strValue, out int keyCount))
                            return false;

                        keyCounts.Add(keyCount);
                    }

                    int? singleKeyCount = keyCounts.Count == 1 ? keyCounts.Single() : null;

                    switch (op)
                    {
                        case Operator.Equal:
                            includedKeyCounts.IntersectWith(keyCounts);
                            return true;

                        case Operator.NotEqual:
                            includedKeyCounts.ExceptWith(keyCounts);
                            return true;

                        case Operator.Less:
                            if (singleKeyCount == null) return false;

                            includedKeyCounts.RemoveWhere(k => k >= singleKeyCount.Value);
                            return true;

                        case Operator.LessOrEqual:
                            if (singleKeyCount == null) return false;

                            includedKeyCounts.RemoveWhere(k => k > singleKeyCount.Value);
                            return true;

                        case Operator.Greater:
                            if (singleKeyCount == null) return false;

                            includedKeyCounts.RemoveWhere(k => k <= singleKeyCount.Value);
                            return true;

                        case Operator.GreaterOrEqual:
                            if (singleKeyCount == null) return false;

                            includedKeyCounts.RemoveWhere(k => k < singleKeyCount.Value);
                            return true;

                        default:
                            return false;
                    }
                }

                case "ln":
                case "lns":
                    return FilterQueryParser.TryUpdateCriteriaRange(ref longNotePercentage, op, strValues);
            }

            return false;
        }

        public bool FilterMayChangeFromMods(FilterCriteria criteria, ValueChangedEvent<IReadOnlyList<Mod>> mods) => false;

        private static float calculateLongNotePercentage(BeatmapInfo beatmapInfo)
        {
            int holdNotes = beatmapInfo.EndTimeObjectCount;
            int totalNotes = Math.Max(1, beatmapInfo.TotalObjectCount);

            return holdNotes / (float)totalNotes * 100;
        }
    }
}
