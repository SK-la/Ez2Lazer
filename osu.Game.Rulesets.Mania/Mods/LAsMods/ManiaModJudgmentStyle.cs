// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModJudgmentStyle : Mod, IApplicableToDifficulty
    {
        public override string Name => "Other MUG-Game Judgment Style";
        public override string Acronym => "CH";
        public override double ScoreMultiplier => 1;
        public override bool Ranked => false;
        public override ModType Type => ModType.CustomMod;
        public override LocalisableString Description => @"LaMod: Custom HitWindows. Free Hit ms.";

        [SettingSource("Adaptive Judgement(No Active)")]
        public BindableBool AdaptiveJudgement { get; } = new BindableBool();

        [SettingSource("Easy Style Judgement")]
        public BindableBool Ez2AcTemplate { get; } = new BindableBool();

        [SettingSource("Hard Style Judgement")]
        public BindableBool UseHardTemplate { get; } = new BindableBool();

        [SettingSource("Custom Hit Range", "Adjust the hit range of notes.")]
        public BindableBool CustomHitRange { get; set; } = new BindableBool(true);

        [SettingSource("Perfect Offset (ms)")]
        public BindableNumber<double> PerfectOffset { get; } = new BindableDouble(22)
        {
            MinValue = 1,
            MaxValue = 60,
            Precision = 1
        };

        [SettingSource("Great Offset (ms)")]
        public BindableNumber<double> GreatOffset { get; } = new BindableDouble(42)
        {
            MinValue = 10,
            MaxValue = 120,
            Precision = 1
        };

        [SettingSource("Good Offset (ms)")]
        public BindableNumber<double> GoodOffset { get; } = new BindableDouble(82)
        {
            MinValue = 20,
            MaxValue = 180,
            Precision = 1
        };

        [SettingSource("Ok Offset (ms)")]
        public BindableNumber<double> OkOffset { get; } = new BindableDouble(120)
        {
            MinValue = 40,
            MaxValue = 240,
            Precision = 1
        };

        [SettingSource("Meh Offset (ms)")]
        public BindableNumber<double> MehOffset { get; } = new BindableDouble(150)
        {
            MinValue = 60,
            MaxValue = 300,
            Precision = 1
        };

        [SettingSource("Miss Offset (ms)")]
        public BindableNumber<double> MissOffset { get; } = new BindableDouble(180)
        {
            MinValue = 80,
            MaxValue = 500,
            Precision = 1
        };

        public ManiaModJudgmentStyle()
        {
            Ez2AcTemplate.BindValueChanged(e =>
            {
                if (e.NewValue)
                {
                    ApplyTemplate(HitWindowTemplateDictionary.EASY);
                    UseHardTemplate.Value = false;
                    AdaptiveJudgement.Value = false;
                }
            });
            UseHardTemplate.BindValueChanged(e =>
            {
                if (e.NewValue)
                {
                    ApplyTemplate(HitWindowTemplateDictionary.HARD);
                    Ez2AcTemplate.Value = false;
                    AdaptiveJudgement.Value = false;
                }
            });
            AdaptiveJudgement.BindValueChanged(e =>
            {
                if (e.NewValue)
                {
                    UseHardTemplate.Value = false;
                    Ez2AcTemplate.Value = false;
                }
                // else
                // {
                //     scoreProcessor.Accuracy.UnbindAll();
                // }
            }, true);
            PerfectOffset.BindValueChanged(_ => updateHitWindows());
            GreatOffset.BindValueChanged(_ => updateHitWindows());
            GoodOffset.BindValueChanged(_ => updateHitWindows());
            OkOffset.BindValueChanged(_ => updateHitWindows());
            MehOffset.BindValueChanged(_ => updateHitWindows());
            MissOffset.BindValueChanged(_ => updateHitWindows());
        }

        private void updateHitWindows()
        {
        }

        public void ApplyTemplate(HitWindowTemplate template)
        {
            PerfectOffset.Value = template.TemplatePerfect;
            GreatOffset.Value = template.TemplateGreat;
            GoodOffset.Value = template.TemplateGood;
            OkOffset.Value = template.TemplateOk;
            MehOffset.Value = template.TemplateMeh;
            MissOffset.Value = template.TemplateMiss;
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                var settings = new List<(LocalisableString setting, LocalisableString value)>
                {
                    (new LocalisableString("Perfect"), new LocalisableString(PerfectOffset.Value.ToString(CultureInfo.CurrentCulture))),
                    (new LocalisableString("Great"), new LocalisableString(GreatOffset.Value.ToString(CultureInfo.InvariantCulture))),
                    (new LocalisableString("Good"), new LocalisableString(GoodOffset.Value.ToString(CultureInfo.InvariantCulture))),
                    (new LocalisableString("Ok"), new LocalisableString(OkOffset.Value.ToString(CultureInfo.InvariantCulture))),
                    (new LocalisableString("Meh"), new LocalisableString(MehOffset.Value.ToString(CultureInfo.InvariantCulture))),
                    (new LocalisableString("Miss"), new LocalisableString(MissOffset.Value.ToString(CultureInfo.InvariantCulture)))
                };

                return settings;
            }
        }

        private HitWindows hitWindows { get; set; } = new ManiaHitWindows();

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            hitWindows.SetDifficultyRange(PerfectOffset.Value, GreatOffset.Value, GoodOffset.Value, OkOffset.Value, MehOffset.Value, MissOffset.Value);
            difficulty.OverallDifficulty = 0;
            hitWindows.SetDifficulty(difficulty.OverallDifficulty);
        }

        public override void ResetSettingsToDefaults()
        {
            hitWindows.ResetHitWindows();
        }
    }
}
