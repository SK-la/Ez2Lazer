// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
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
                    applyTemplate(HitWindowTemplates.EASY);
                    UseHardTemplate.Value = false;
                    AdaptiveJudgement.Value = false;
                }
            });
            UseHardTemplate.BindValueChanged(e =>
            {
                if (e.NewValue)
                {
                    applyTemplate(HitWindowTemplates.HARD);
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

        private void applyTemplate(HitWindowTemplate template)
        {
            PerfectOffset.Value = template.PerfectOffset;
            GreatOffset.Value = template.GreatOffset;
            GoodOffset.Value = template.GoodOffset;
            OkOffset.Value = template.OkOffset;
            MehOffset.Value = template.MehOffset;
            MissOffset.Value = template.MissOffset;
        }

        public class HitWindowTemplate
        {
            public double PerfectOffset { get; set; }
            public double GreatOffset { get; set; }
            public double GoodOffset { get; set; }
            public double OkOffset { get; set; }
            public double MehOffset { get; set; }
            public double MissOffset { get; set; }
        }

        public static class HitWindowTemplates
        {
            public static readonly HitWindowTemplate EASY = new HitWindowTemplate
            {
                PerfectOffset = 50,
                GreatOffset = 100,
                GoodOffset = 150,
                OkOffset = 200,
                MehOffset = 250,
                MissOffset = 300
            };

            public static readonly HitWindowTemplate HARD = new HitWindowTemplate
            {
                PerfectOffset = 20,
                GreatOffset = 40,
                GoodOffset = 60,
                OkOffset = 80,
                MehOffset = 100,
                MissOffset = 120
            };

            // 可以添加更多模板
        }

        public void UpdateHitWindowsBasedOnScore(double accuracy)
        {
            if (accuracy != 0)
            {
                if (accuracy > 0.95)
                {
                    // 缩小判定区间
                    PerfectOffset.Value = 10;
                    GreatOffset.Value = 20;
                    GoodOffset.Value = 21;
                    OkOffset.Value = 90;
                    MehOffset.Value = 100;
                    MissOffset.Value = 120;
                }
                else if (accuracy < 0.95)
                {
                    // 放宽判定区间
                    PerfectOffset.Value = 30;
                    GreatOffset.Value = 60;
                    GoodOffset.Value = 100;
                    OkOffset.Value = 150;
                    MehOffset.Value = 151;
                    MissOffset.Value = 200;
                }
            }
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

        public HitWindows HitWindows { get; set; } = new ManiaHitWindows();

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            HitWindows.SetDifficultyRange(PerfectOffset.Value, GreatOffset.Value, GoodOffset.Value, OkOffset.Value, MehOffset.Value, MissOffset.Value);
            difficulty.OverallDifficulty = 0;
            HitWindows.SetDifficulty(difficulty.OverallDifficulty);
        }

        public override void ResetSettingsToDefaults()
        {
            HitWindows.ResetRange();
        }
    }
}
