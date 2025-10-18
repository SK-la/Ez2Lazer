// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.SelectV2;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModNewJudgement : Mod, IApplicableToDifficulty
    {
        public override string Name => "New Judgement";

        public override string Acronym => "NJ";

        public override LocalisableString Description => "New judgement set by BPM of the song.";

        public override ModType Type => ModType.CustomMod;

        public override double ScoreMultiplier => 1.0;

        public ManiaHitWindows HitWindows { get; set; } = new ManiaHitWindows();

        public double NowBeatmapBPM
        {
            get
            {
                double result;
                if (BeatmapTitleWedge.SelectedWorkingBeatmap is not null)
                    result = BeatmapTitleWedge.SelectedWorkingBeatmap.BeatmapInfo.BPM;
                else
                    result = 200;
                return result;
            }
        }

        [SettingSource("Custom BPM", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> BPM { get; set; } = new Bindable<int?>();

        [SettingSource("Divide")]
        public BindableDouble Divide { get; set; } = new BindableDouble(7.5)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 0.5
        };

        [SettingSource("For 1/4 Jack")]
        public BindableBool For14Jack { get; set; } = new BindableBool();

        [SettingSource("For 1/6 Stream")]
        public BindableBool For16Stream { get; set; } = new BindableBool();

        [SettingSource("For 1/3 Jack")]
        public BindableBool For13Jack { get; set; } = new BindableBool();

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            double perBeatLength = 60 / NowBeatmapBPM * 1000;
            if (BPM.Value is not null) perBeatLength = 60 / (double)BPM.Value * 1000;

            if (For14Jack.Value) perBeatLength /= 2;

            if (For16Stream.Value) perBeatLength /= 1.5;

            if (For13Jack.Value) perBeatLength = perBeatLength * 4 / 6;
            double perfectRange = perBeatLength / Divide.Value;
            double greatRange = perBeatLength / (Divide.Value / 1.5);
            double goodRange = perBeatLength / (Divide.Value / 2);
            double okRange = perBeatLength / (Divide.Value / 2.5);
            double mehRange = perBeatLength / (Divide.Value / 3);
            double missRange = perBeatLength / (Divide.Value / 3.5);
            // difficulty.OverallDifficulty = 0;
            HitWindows.SetDifficulty(difficulty.OverallDifficulty);

            HitWindows.SetSpecialDifficultyRange(perfectRange, greatRange, goodRange, okRange, mehRange, missRange);
        }

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            HitWindows.ResetRange();
        }
    }
}
