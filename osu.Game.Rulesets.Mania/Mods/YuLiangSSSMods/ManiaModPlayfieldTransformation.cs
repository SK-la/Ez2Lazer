// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class ManiaModPlayfieldTransformation : Mod, IApplicableToPlayer, IUpdatableByPlayfield, IApplicableToScoreProcessor, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "Playfield Scale";

        public override string Acronym => "PS";

        public override LocalisableString Description => EzManiaModStrings.PlayfieldTransformation_Description;

        public override double ScoreMultiplier => 1.0;

        public override ModType Type => ModType.YuLiangSSS_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.MinimumScale_Label), nameof(EzManiaModStrings.MinimumScale_Description))]
        public BindableFloat MinScale { get; } = new BindableFloat(0.3f)
        {
            MinValue = 0.3f,
            MaxValue = 1.0f,
            Precision = 0.01f
        };

        private readonly BindableInt combo = new BindableInt();
        private readonly IBindable<bool> isBreakTime = new Bindable<bool>();

        private const int max_combo_for_min_scale = 300; // Combo value at which min scale is reached

        public void ApplyToScoreProcessor(ScoreProcessor scoreProcessor)
        {
            combo.UnbindAll();
            combo.BindTo(scoreProcessor.Combo);
        }

        public void ApplyToPlayer(Player player)
        {
            isBreakTime.UnbindAll();
            isBreakTime.BindTo(player.IsBreakTime);
        }

        public void ApplyToManiaPlayfield(ManiaPlayfield playfield)
        {
            // No-op
        }

        public void Update(Playfield playfield)
        {
            var maniaPlayfield = (ManiaPlayfield)playfield;

            float targetScale;

            if (isBreakTime.Value)
            {
                targetScale = 1f;
            }
            else
            {
                // Calculate scale based on combo, interpolating between 1f and MinScale
                // The scale reaches MinScale at max_combo_for_min_scale
                float comboRatio = Math.Min(1f, (float)combo.Value / max_combo_for_min_scale);
                targetScale = 1f - comboRatio * (1f - MinScale.Value);
            }

            foreach (var stage in maniaPlayfield.Stages)
            {
                stage.ScaleTo(new Vector2(targetScale, 1f), 1000, Easing.OutQuint);
            }
        }

        public ScoreRank AdjustRank(ScoreRank rank, double accuracy)
        {
            switch (rank)
            {
                case ScoreRank.X:
                    return ScoreRank.XH;

                case ScoreRank.S:
                    return ScoreRank.SH;

                default:
                    return rank;
            }
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
        }
    }
}
