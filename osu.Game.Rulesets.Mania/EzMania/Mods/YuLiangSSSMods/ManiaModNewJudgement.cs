// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.YuLiangSSSMods
{
    public class ManiaModNewJudgement : Mod, IApplicableToBeatmap, IManiaHitRangeProvider
    {
        public override string Name => "New Judgement";

        public override string Acronym => "NJ";

        public override LocalisableString Description => NewJudgementStrings.NEW_JUDGEMENT_DESCRIPTION;

        public override ModType Type => ModType.YuLiangSSS_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;
        public override double ScoreMultiplier => 1.0;

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

        public double BeatmapBPM;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Divide", $"{Divide.Value}");

                if (BPM.Value is null) yield return ("BPM", "Auto");
                else yield return ("BPM", $"{BPM.Value}");
                if (For14Jack.Value) yield return ("For 1/4 Jack", "On");
                if (For16Stream.Value) yield return ("For 1/6 Stream", "On");
                if (For13Jack.Value) yield return ("For 1/3 Jack", "On");
            }
        }

        public ManiaModifyHitRange ComputeHitRanges()
        {
            double perBeatLength = 60 / BeatmapBPM * 1000;
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

            return new ManiaModifyHitRange(
                perfectRange,
                greatRange,
                goodRange,
                okRange,
                mehRange,
                missRange
            );
        }

        public ManiaModifyHitRange? GetDisplayHitRange(IBeatmapInfo beatmapInfo)
        {
            BeatmapBPM = beatmapInfo.BPM;
            return ComputeHitRanges();
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            BeatmapBPM = beatmap.BeatmapInfo.BPM;

            ManiaHitWindows.SetModOverride(ComputeHitRanges());
        }

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            ManiaHitWindows.ClearModOverride();
        }
    }

    public static class NewJudgementStrings
    {
        public static readonly LocalisableString NEW_JUDGEMENT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("根据歌曲BPM设置新的判定", "New judgement set by BPM of the song.");
    }
}
