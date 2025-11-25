// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.SelectV2;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class ManiaModO2Judgement : Mod, IApplicableToDifficulty, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public static ManiaHitWindows Windows = new ManiaHitWindows();

        public override string Name => "O2JAM Judgement";

        public override string Acronym => "OJ";

        public override LocalisableString Description => "Judgement System for O2JAM players.";

        public override double ScoreMultiplier => 1.0;

        public override ModType Type => ModType.CustomMod;

        public ManiaHitWindows HitWindows { get; set; } = new ManiaHitWindows();

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (PillMode.Value) yield return ("Pill", "On");
            }
        }

        [SettingSource("Pill Switch", "Use O2JAM pill function.")]
        public BindableBool PillMode { get; set; } = new BindableBool(true);

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            var maniaRuleset = (DrawableManiaRuleset)drawableRuleset;

            foreach (var stage in maniaRuleset.Playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    column.RegisterPool<O2Note, O2DrawableNote>(10, 50);
                    column.RegisterPool<O2HeadNote, O2DrawableHoldNoteHead>(10, 50);
                    column.RegisterPool<O2TailNote, O2DrawableHoldNoteTail>(10, 50);
                }
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var hitObjects = maniaBeatmap.HitObjects.Select(obj =>
            {
                if (obj is Note note)
                    return new O2Note(note);

                if (obj is HoldNote hold)
                    return new O2HoldNote(hold);

                return obj;
            }).ToList();

            maniaBeatmap.HitObjects = hitObjects;
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            HitWindows.SetSpecialDifficultyRange(O2HitObject.CoolRange, O2HitObject.CoolRange, O2HitObject.GoodRange, O2HitObject.GoodRange, O2HitObject.BadRange, O2HitObject.BadRange);
            O2HitObject.Pill = 0;
            O2HitObject.PillActivated = PillMode.Value;
            Windows = HitWindows;
        }

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            HitWindows.ResetRange();
        }
    }
}
