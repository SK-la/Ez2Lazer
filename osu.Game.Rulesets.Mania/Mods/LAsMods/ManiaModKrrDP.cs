// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModKrrDP : Mod, IApplicableToBeatmapConverter, IApplicableAfterBeatmapConversion
    {
        public override string Name => "DP";

        public override string Acronym => "DP";

        public override LocalisableString Description => "Convert to Dual Play mode";

        public override double ScoreMultiplier => 1;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource("Enable Modify Keys", "Enable key modification")]
        public BindableBool EnableModifyKeys { get; } = new BindableBool();

        [SettingSource("Modify Keys", "Target number of keys for each side")]
        public BindableInt ModifyKeys { get; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 9,
        };

        [SettingSource("Left Mirror", "Mirror left side")]
        public BindableBool LMirror { get; set; } = new BindableBool(false);

        [SettingSource("Left Density", "Adjust left side density")]
        public BindableBool LDensity { get; set; } = new BindableBool(false);

        [SettingSource("Left Remove", "Remove left side")]
        public BindableBool LRemove { get; set; } = new BindableBool(false);

        [SettingSource("Left Max Keys", "Max keys for left density")]
        public BindableNumber<int> LMaxKeys { get; set; } = new BindableInt(5)
        {
            MinValue = 1,
            MaxValue = 9,
        };

        [SettingSource("Left Min Keys", "Min keys for left density")]
        public BindableNumber<int> LMinKeys { get; set; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 9,
        };

        [SettingSource("Right Mirror", "Mirror right side")]
        public BindableBool RMirror { get; set; } = new BindableBool(false);

        [SettingSource("Right Density", "Adjust right side density")]
        public BindableBool RDensity { get; set; } = new BindableBool(false);

        [SettingSource("Right Remove", "Remove right side")]
        public BindableBool RRemove { get; set; } = new BindableBool(false);

        [SettingSource("Right Max Keys", "Max keys for right density")]
        public BindableNumber<int> RMaxKeys { get; set; } = new BindableInt(5)
        {
            MinValue = 1,
            MaxValue = 9,
        };

        [SettingSource("Right Min Keys", "Min keys for right density")]
        public BindableNumber<int> RMinKeys { get; set; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 9,
        };

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;
            mbc.TargetColumns = mbc.TotalColumns * 2;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int totalKeys = maniaBeatmap.TotalColumns; // already doubled by converter
            int originalKeys = totalKeys / 2;

            var rng = new System.Random();

            var newObjects = new List<ManiaHitObject>();

            foreach (var hitObject in maniaBeatmap.HitObjects)
            {
                // Since converter mapped column * 2, original column = hitObject.Column / 2
                int originalColumn = hitObject.Column / 2;

                // Left side
                if (!LRemove.Value)
                {
                    int leftCol = LMirror.Value ? originalKeys - 1 - originalColumn : originalColumn;
                    if (EnableModifyKeys.Value)
                    {
                        leftCol = rng.Next(ModifyKeys.Value);
                    }
                    else if (LDensity.Value)
                    {
                        // Simple density adjustment
                        leftCol = rng.Next(LMinKeys.Value, LMaxKeys.Value + 1);
                    }
                    ManiaHitObject leftObject = hitObject is HoldNote hold ? new HoldNote
                    {
                        StartTime = hold.StartTime,
                        EndTime = hold.EndTime,
                        Column = leftCol,
                        Samples = hold.Samples.ToList()
                    } : new Note
                    {
                        StartTime = hitObject.StartTime,
                        Column = leftCol,
                        Samples = hitObject.Samples.ToList()
                    };
                    newObjects.Add(leftObject);
                }

                // Right side
                if (!RRemove.Value)
                {
                    int rightCol = RMirror.Value ? originalKeys - 1 - originalColumn : originalColumn;
                    if (EnableModifyKeys.Value)
                    {
                        rightCol = ModifyKeys.Value + rng.Next(ModifyKeys.Value);
                    }
                    else if (RDensity.Value)
                    {
                        rightCol = originalKeys + rng.Next(RMinKeys.Value, RMaxKeys.Value + 1);
                    }
                    else
                    {
                        rightCol += originalKeys;
                    }
                    ManiaHitObject rightObject = hitObject is HoldNote hold2 ? new HoldNote
                    {
                        StartTime = hold2.StartTime,
                        EndTime = hold2.EndTime,
                        Column = rightCol,
                        Samples = hold2.Samples.ToList()
                    } : new Note
                    {
                        StartTime = hitObject.StartTime,
                        Column = rightCol,
                        Samples = hitObject.Samples.ToList()
                    };
                    newObjects.Add(rightObject);
                }
            }

            maniaBeatmap.HitObjects.Clear();
            maniaBeatmap.HitObjects.AddRange(newObjects.OrderBy(h => h.StartTime).ThenBy(h => h.Column));

            // Update metadata
            var metadata = maniaBeatmap.BeatmapInfo.Metadata;
            int targetKeys = EnableModifyKeys.Value ? ModifyKeys.Value * 2 : totalKeys;
            string DPVersionName = $"[{originalKeys}to{targetKeys}DP]";

            // Modify creator
            metadata.Author.Username = AddTagToCreator(metadata.Author.Username, "DP");

            // Modify version
            metadata.Title = DPVersionName + " " + metadata.Title;

            // Modify tags
            var tags = metadata.Tags.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (!tags.Contains("Converter")) tags.Add("Converter");
            if (!tags.Contains("DP")) tags.Add("DP");
            if (!tags.Contains("Krr")) tags.Add("Krr");
            metadata.Tags = string.Join(" ", tags);

            // Reset beatmap ID
            maniaBeatmap.BeatmapInfo.OnlineID = -1;
        }

        private string AddTagToCreator(string creator, string tag)
        {
            if (string.IsNullOrEmpty(creator)) return tag;
            if (creator.Contains(tag)) return creator;
            return $"{creator} ({tag})";
        }
    }
}
