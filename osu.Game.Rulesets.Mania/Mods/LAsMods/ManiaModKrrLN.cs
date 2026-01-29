// Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModKrrLN : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "KRRLN";

        public override string Acronym => "KL";

        public override LocalisableString Description => "KRRLN long-note transformer (port stub).";

        public override double ScoreMultiplier => 1;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        [SettingSource("Level", "Transform level (stub)")]
        public BindableInt Level { get; } = new BindableInt(3)
        {
            MinValue = -3,
            MaxValue = 10,
        };

        [SettingSource("Seed", "Random seed (optional)")]
        public Bindable<int?> Seed { get; } = new Bindable<int?>(114514);

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            // call into helper converter (minimal ported behavior)
            var options = new KrrLNOptions { Level = Level.Value, Seed = Seed.Value };
            KrrConverters.KRRLNConverter.Transform(maniaBeatmap, options);
        }
    }

    public class KrrLNOptions
    {
        public int Level { get; set; }
        public int? Seed { get; set; }

        // Additional options approximating krrTools' options with sensible defaults
        public bool ProcessOriginalIsChecked { get; set; } = false;
        public int LengthThreshold { get; set; } = 4;
        public double LongPercentage { get; set; } = 100;
        public double ShortPercentage { get; set; } = 100;
        public int LongLimit { get; set; } = 4;
        public int ShortLimit { get; set; } = 4;
        public int LongRandom { get; set; } = 50;
        public int ShortRandom { get; set; } = 50;
        public int? Alignment { get; set; } = null; // null means no alignment
        public float? ODValue { get; set; } = null;
    }
}
