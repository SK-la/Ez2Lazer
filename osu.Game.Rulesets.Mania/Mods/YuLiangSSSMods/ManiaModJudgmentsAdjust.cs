﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModJudgmentsAdjust : Mod, IApplicableToScoreProcessor, IApplicableToDifficulty
    {
        public override string Name => "Judgments Adjust";

        public override string Acronym => "JU";

        public override LocalisableString Description => "Modify your judgement.";

        public override ModType Type => ModType.CustomMod;

        public override IconUsage? Icon => FontAwesome.Solid.Shower;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override string SettingDescription
        {
            get
            {
                List<string> descriptions = new List<string>();

                if (CustomHitRange.Value)
                {
                    descriptions.AddRange(new[]
                    {
                        "Custom Hit Range True",
                        $"Perfect Hit {PerfectHit.Value:0.#}",
                        $"Great Hit {GreatHit.Value:0.#}",
                        $"Good Hit {GoodHit.Value:0.#}",
                        $"Ok Hit {OkHit.Value:0.#}",
                        $"Meh Hit {MehHit.Value:0.#}",
                        $"Miss Hit {MissHit.Value:0.#}"
                    });
                }

                if (CustomProportionScore.Value)
                {
                    descriptions.AddRange(new[]
                    {
                        "Custom Proportion Score True",
                        $"Perfect {Perfect.Value:0.#}",
                        $"Great {Great.Value:0.#}",
                        $"Good {Good.Value:0.#}",
                        $"Ok {Ok.Value:0.#}",
                        $"Meh {Meh.Value:0.#}",
                        $"Miss {Miss.Value:0.#}"
                    });
                }

                return string.Join(", ", descriptions);
            }
        }

        [SettingSource("Custom Hit Range", "Adjust the hit range of notes.")]
        public BindableBool CustomHitRange { get; set; } = new BindableBool(true);

        [SettingSource("Perfect")]
        public BindableDouble PerfectHit { get; set; } = new BindableDouble(22.4D)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Great")]
        public BindableDouble GreatHit { get; set; } = new BindableDouble(64)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Good")]
        public BindableDouble GoodHit { get; set; } = new BindableDouble(97)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Ok")]
        public BindableDouble OkHit { get; set; } = new BindableDouble(127)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Meh")]
        public BindableDouble MehHit { get; set; } = new BindableDouble(151)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Miss")]
        public BindableDouble MissHit { get; set; } = new BindableDouble(188)
        {
            Precision = 0.1,
            MinValue = 0,
            MaxValue = 250
        };

        [SettingSource("Custom Proportion Score")]
        public BindableBool CustomProportionScore { get; set; } = new BindableBool(true);

        [SettingSource("Perfect")]
        public BindableInt Perfect { get; set; } = new BindableInt(300)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Great")]
        public BindableInt Great { get; set; } = new BindableInt(300)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Good")]
        public BindableInt Good { get; set; } = new BindableInt(200)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Ok")]
        public BindableInt Ok { get; set; } = new BindableInt(100)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Meh")]
        public BindableInt Meh { get; set; } = new BindableInt(50)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        [SettingSource("Miss")]
        public BindableInt Miss { get; set; } = new BindableInt(0)
        {
            Precision = 5,
            MinValue = 0,
            MaxValue = 500
        };

        public HitWindows HitWindows { get; set; } = new ManiaHitWindows();

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy)
        {
            return rank;
        }

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            var mania = (ManiaScoreProcessor)scoreProcessor;
            mania.HitProportionScore.Perfect = Perfect.Value;
            mania.HitProportionScore.Great = Great.Value;
            mania.HitProportionScore.Good = Good.Value;
            mania.HitProportionScore.Ok = Ok.Value;
            mania.HitProportionScore.Meh = Meh.Value;
            mania.HitProportionScore.Miss = Miss.Value;
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            if (CustomHitRange.Value)
            {
                HitWindows.SetDifficultyRange(PerfectHit.Value, GreatHit.Value, GoodHit.Value, OkHit.Value, MehHit.Value, MissHit.Value);
                difficulty.OverallDifficulty = 0;
                HitWindows.SetDifficulty(difficulty.OverallDifficulty);
            }
        }
    }
}
