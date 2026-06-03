// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    /// <summary>
    /// Splits a single-stage beatmap with more than 10 keys into dual stages while preserving total column count.
    /// </summary>
    public class ManiaModSplitStages : Mod, IPlayfieldTypeMod, IApplicableToBeatmapConverter, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "Split Stages";
        public override string Acronym => "DS";
        public override LocalisableString Description => @"Split keys across two stages without changing the total key count.";
        public override IconUsage? Icon => OsuIcon.ModDualStages;
        public override ModType Type => ModType.Conversion;

        private bool requiresDualPlayfield;

        /// <summary>
        /// The spacing between the two stages in pixels.
        /// </summary>
        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.STAGE_SPACING_LABEL), nameof(EzCommonModStrings.STAGE_SPACING_DESCRIPTION))]
        public BindableFloat Spacing { get; } = new BindableFloat
        {
            MinValue = 0,
            MaxValue = 100,
            Default = 20,
            Value = 20,
            Precision = 1
        };

        public void ApplyToBeatmapConverter(IBeatmapConverter beatmapConverter)
        {
            var mbc = (ManiaBeatmapConverter)beatmapConverter;
            int totalKeys = mbc.TargetColumns;

            requiresDualPlayfield = false;

            if (totalKeys <= 10 || totalKeys > ManiaRuleset.MAX_STAGE_KEYS)
                return;

            // Odd key counts cannot be evenly split, so dual stages stay disabled.
            if (totalKeys % 2 != 0)
                return;

            mbc.TargetColumns = totalKeys / 2;
            mbc.Dual = true;
            requiresDualPlayfield = true;
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            if (drawableRuleset.Playfield is not ManiaPlayfield maniaPlayfield)
                return;

            applySpacing(maniaPlayfield);
            Spacing.BindValueChanged(_ => applySpacing(maniaPlayfield));
        }

        private void applySpacing(ManiaPlayfield maniaPlayfield) => maniaPlayfield.SetStageSpacing(Spacing.Value);

        public PlayfieldType PlayfieldType => requiresDualPlayfield ? PlayfieldType.Dual : PlayfieldType.Single;
    }
}
