// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    public class ManiaModO2Health : ModFailCondition, IApplicableAfterBeatmapConversion
    {
        public const int MAX_HEALTH = 1000;

        public Bindable<int> HP = new Bindable<int>(1000);

        private readonly int[][] difficultySettings =
        {
            new[] { 3, 2, -10, -50 }, // Easy
            new[] { 2, 1, -7, -40 }, // Normal
            new[] { 1, 0, -5, -30 } // Hard
        };

        public double Health => (double)HP.Value / MAX_HEALTH;

        public override string Name => "O2JAM Health";

        public override string Acronym => "OH";

        public override LocalisableString Description => O2HealthStrings.O2_HEALTH_DESCRIPTION;

        public override double ScoreMultiplier => 1.0;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                string difficultyName = Difficulty.Value switch
                {
                    1 => "Easy",
                    2 => "Normal",
                    3 => "Hard",
                    _ => "Unknown"
                };
                yield return (O2HealthStrings.DIFFICULTY_LABEL, difficultyName);
            }
        }

        public override ModType Type => ModType.CommunityMod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(O2HealthStrings), nameof(O2HealthStrings.DIFFICULTY_LABEL), nameof(O2HealthStrings.DIFFICULTY_DESCRIPTION))]
        public BindableInt Difficulty { get; set; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 3,
            Precision = 1
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            HP.Value = MAX_HEALTH;
        }

        protected override bool FailCondition(HealthProcessor healthProcessor, JudgementResult result)
        {
            int difficultyIndex = Difficulty.Value - 1;
            int healthChange = 0;

            switch (result.Type)
            {
                case HitResult.Perfect:
                    healthChange = difficultySettings[difficultyIndex][0];
                    break;

                case HitResult.Good:
                    healthChange = difficultySettings[difficultyIndex][1];
                    break;

                case HitResult.Meh:
                    healthChange = difficultySettings[difficultyIndex][2];
                    break;

                case HitResult.Miss:
                    healthChange = difficultySettings[difficultyIndex][3];
                    break;
            }

            HP.Value += healthChange;

            if (HP.Value > MAX_HEALTH)
                HP.Value = MAX_HEALTH;

            healthProcessor.Health.Value = Health;

            return HP.Value <= 0;
        }
    }

    public static class O2HealthStrings
    {
        public static readonly LocalisableString O2_HEALTH_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("为O2JAM玩家设计的生命值系统", "Health system for O2JAM players.");
        public static readonly LocalisableString DIFFICULTY_LABEL = new EzLocalizationManager.EzLocalisableString("难度", "Difficulty");
        public static readonly LocalisableString DIFFICULTY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("1: 简单  2: 普通  3: 困难", "1: Easy  2: Normal  3: Hard");
    }
}
