// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mania.EzMania.HUD;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    public partial class ManiaModO2Judgement : Mod, IApplicableToDifficulty, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>, IApplicableToHUD
    {
        public static ManiaHitWindows HitWindows = new ManiaHitWindows();

        public override string Name => "O2JAM Judgement";

        public override string Acronym => "OJ";

        public override LocalisableString Description => O2JudgementStrings.O2_JUDGEMENT_DESCRIPTION;

        public override double ScoreMultiplier => 1.0;

        public override ModType Type => ModType.CommunityMod;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (PillMode.Value) yield return (O2JudgementStrings.PILL_SWITCH_LABEL, "On");
            }
        }

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(O2JudgementStrings), nameof(O2JudgementStrings.PILL_SWITCH_LABEL), nameof(O2JudgementStrings.PILL_SWITCH_DESCRIPTION))]
        public BindableBool PillMode { get; set; } = new BindableBool(true);

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            var maniaRuleset = (DrawableManiaRuleset)drawableRuleset;

            foreach (var stage in maniaRuleset.Playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    column.RegisterPool<Note, O2DrawableNote>(10, 50);
                    column.RegisterPool<HoldNote, O2DrawableHoldNote>(10, 50);
                    column.RegisterPool<HeadNote, O2DrawableHoldNoteHead>(10, 50);
                    column.RegisterPool<TailNote, O2DrawableHoldNoteTail>(10, 50);
                }
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            double bpm = beatmap.BeatmapInfo.BPM;
            O2HitModeExtension.SetOriginalBPM(bpm);
            O2HitModeExtension.SetControlPoints(beatmap.ControlPointInfo);
            O2HitModeExtension.PillActivated = PillMode.Value;
            O2HitModeExtension.PILL_COUNT.Value = 0;
            HitWindows.BPM = bpm;
            // HitWindows.ModifyManiaHitRange(new ManiaModifyHitRange(
            //     O2HitModeExtension.BASE_COOL / bpm,
            //     O2HitModeExtension.BASE_COOL / bpm,
            //     O2HitModeExtension.BASE_GOOD / bpm,
            //     O2HitModeExtension.BASE_GOOD / bpm,
            //     O2HitModeExtension.BASE_BAD / bpm,
            //     O2HitModeExtension.BASE_BAD / bpm
            // ));
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
        }

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            HitWindows.ResetRange();
        }

        public void ApplyToHUD(HUDOverlay overlay)
        {
            if (!PillMode.Value)
                return;

            var pillUI = new EzHUDO2JamPillFlow
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
            };
            pillUI.BackgroundAlpha.Value = 0.7f;
            overlay.Add(pillUI);
        }
    }

    public static class O2JudgementStrings
    {
        public static readonly LocalisableString O2_JUDGEMENT_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("为O2JAM玩家设计的判定系统", "Judgement System for O2JAM players.");
        public static readonly LocalisableString PILL_SWITCH_LABEL = new EzLocalizationManager.EzLocalisableString("药丸开关", "Pill Switch");
        public static readonly LocalisableString PILL_SWITCH_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("使用O2JAM药丸功能", "Use O2JAM pill function.");
    }
}
