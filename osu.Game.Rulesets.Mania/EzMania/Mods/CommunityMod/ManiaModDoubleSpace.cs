// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    public class ManiaModDoubleSpace : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IEzApplyOrder
    {
        public override string Name => "Double Space";

        public override string Acronym => "DSp";

        public override double ScoreMultiplier => 1.0;

        public override LocalisableString Description => DoubleSpaceStrings.DOUBLE_SPACE_DESCRIPTION;

        public override ModType Type => ModType.CommunityMod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (EzCommonModStrings.SEED_LABEL, $"{(Seed.Value == null ? "Null" : Seed.Value)}");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        private static readonly Dictionary<int, int> special_column_map = new Dictionary<int, int>
        {
            { 5, 2 },
            { 7, 3 },
            { 9, 4 },
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int currentKeys = maniaBeatmap.TotalColumns;

            if (!special_column_map.TryGetValue(currentKeys, out int sColumnIndex))
                return;
            int newColumnCount = currentKeys + 1;

            Seed.Value ??= RNG.Next();
            var rng = new Random((int)Seed.Value);

            var sColumnHitObjects = maniaBeatmap.HitObjects
                .Where(h => h.Column == sColumnIndex)
                .OrderBy(h => h.StartTime)
                .ToList();

            int totalCount = sColumnHitObjects.Count;

            if (totalCount == 0)
            {
                var shiftedObjects = maniaBeatmap.HitObjects.Select(h =>
                {
                    int newCol = h.Column > sColumnIndex ? h.Column + 1 : h.Column;
                    return createNewObject(h, newCol);
                }).ToList();

                maniaBeatmap.HitObjects = shiftedObjects;
                maniaBeatmap.Stages.Clear();
                maniaBeatmap.Stages.Add(new StageDefinition(newColumnCount));
                maniaBeatmap.Difficulty.CircleSize = newColumnCount;
                return;
            }

            int leftTarget = (totalCount + 1) / 2;
            int rightTarget = totalCount / 2;
            int leftCount = 0;
            int rightCount = 0;

            var assignToRight = new HashSet<ManiaHitObject>();

            for (int i = 0; i < totalCount; i++)
            {
                int remaining = totalCount - i;
                int leftRemaining = leftTarget - leftCount;
                int rightRemaining = rightTarget - rightCount;
                int balance = leftCount - rightCount;

                bool goRight;

                if (leftRemaining <= 0)
                {
                    goRight = true;
                }
                else if (rightRemaining <= 0)
                {
                    goRight = false;
                }
                else if (balance >= 2)
                {
                    goRight = true;
                }
                else if (balance <= -2)
                {
                    goRight = false;
                }
                else
                {
                    double leftProb = (double)leftRemaining / remaining;
                    leftProb = Math.Clamp(leftProb, 0.25, 0.75);
                    goRight = rng.NextDouble() >= leftProb;
                }

                if (goRight)
                {
                    assignToRight.Add(sColumnHitObjects[i]);
                    rightCount++;
                }
                else
                {
                    leftCount++;
                }
            }

            var newHitObjects = new List<ManiaHitObject>(maniaBeatmap.HitObjects.Count);

            foreach (var hitObject in maniaBeatmap.HitObjects)
            {
                int column = hitObject.Column;

                if (column == sColumnIndex && assignToRight.Contains(hitObject))
                    newHitObjects.Add(createNewObject(hitObject, sColumnIndex + 1));
                else if (column == sColumnIndex)
                    newHitObjects.Add(createNewObject(hitObject, sColumnIndex));
                else if (column > sColumnIndex)
                    newHitObjects.Add(createNewObject(hitObject, column + 1));
                else
                    newHitObjects.Add(createNewObject(hitObject, column));
            }

            maniaBeatmap.HitObjects = newHitObjects;

            maniaBeatmap.Stages.Clear();
            maniaBeatmap.Stages.Add(new StageDefinition(newColumnCount));
            maniaBeatmap.Difficulty.CircleSize = newColumnCount;
        }

        private ManiaHitObject createNewObject(ManiaHitObject original, int newColumn)
        {
            switch (original)
            {
                case Note note:
                    return new Note
                    {
                        Column = newColumn,
                        StartTime = note.StartTime,
                        Samples = note.Samples,
                    };

                case HoldNote holdNote:
                    return new HoldNote
                    {
                        Column = newColumn,
                        StartTime = holdNote.StartTime,
                        Duration = holdNote.Duration,
                        NodeSamples = holdNote.NodeSamples,
                    };

                default:
                    original.Column = newColumn;
                    return original;
            }
        }
    }

    public static class DoubleSpaceStrings
    {
        public static readonly LocalisableString DOUBLE_SPACE_DESCRIPTION =
            new EzLocalizationManager.EzLocalisableString(
                "将5K/7K/9K的特殊列一分为二，变成6K/8K/10K，用双拇指分担单拇指压力",
                "Split the Special column of 5K/7K/9K into two, converting to 6K/8K/10K. Share the single-thumb pressure with both thumbs.");
    }
}
