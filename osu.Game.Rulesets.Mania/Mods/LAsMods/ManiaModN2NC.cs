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
    public class ManiaModN2NC : Mod, IApplicableToBeatmapConverter, IApplicableAfterBeatmapConversion
    {
        public override string Name => "N2NC";

        public override string Acronym => "N2N";

        public override LocalisableString Description => "N2NC keycount conversion (port stub).";

        public override double ScoreMultiplier => 1;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        [SettingSource("Target Keys", "目标键数（用于修改列数）")]
        public BindableInt TargetKeys { get; } = new BindableInt(8)
        {
            MinValue = 1,
            MaxValue = 18,
        };

        [SettingSource("Max Keys", "Density max (stub)")]
        public BindableInt MaxKeys { get; } = new BindableInt(18) { MinValue = 1, MaxValue = 18 };

        [SettingSource("Min Keys", "Density min (stub)")]
        public BindableInt MinKeys { get; } = new BindableInt(1) { MinValue = 1, MaxValue = 18 };

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;
            // Set target columns using converter entrypoint (must be done here)
            mbc.TargetColumns = TargetKeys.Value;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            // N2NC is a temporary conversion mod; options are not persisted.
            // Use default behavior for conversion when invoked from code.
            KrrConverters.N2NCConverter.Transform(maniaBeatmap, null);
        }
    }
}
