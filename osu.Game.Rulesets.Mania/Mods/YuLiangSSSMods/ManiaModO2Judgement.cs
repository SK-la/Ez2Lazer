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
        public const double COOL = 7500.0;
        public const double GOOD = 22500.0;
        public const double BAD = 31250.0;

        public static double CoolRange => COOL / NowBeatmapBPM;
        public static double GoodRange => GOOD / NowBeatmapBPM;
        public static double BadRange => BAD / NowBeatmapBPM;

        // MISS

        public const double DEFAULT_BPM = 200;

        public static int Pill;
        public static bool PillActivated;
        public static int CoolCombo;
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

        public static double NowBeatmapBPM
        {
            get
            {
                if (BeatmapTitleWedge.SelectedWorkingBeatmap is not null)
                    return BeatmapTitleWedge.SelectedWorkingBeatmap.BeatmapInfo.BPM;
                else
                    return DEFAULT_BPM;
            }
        }

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
            HitWindows.SetSpecialDifficultyRange(CoolRange, CoolRange, GoodRange, GoodRange, BadRange, BadRange);
            Pill = 0;
            PillActivated = PillMode.Value;
            Windows = HitWindows;
        }

        public override void ResetSettingsToDefaults()
        {
            base.ResetSettingsToDefaults();
            HitWindows.ResetRange();
        }

        public partial class O2DrawableNote : DrawableNote
        {
            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (userTriggered)
                {
                    bool flowControl = PillCheck(timeOffset);
                    if (!flowControl) return;
                }

                base.CheckForResult(userTriggered, timeOffset);
            }

            public bool PillCheck(double timeOffset)
            {
                if (PillActivated)
                {
                    double offset = Math.Abs(timeOffset);

                    if (offset <= CoolRange)
                    {
                        CoolCombo++;

                        if (CoolCombo >= 15)
                        {
                            CoolCombo -= 15;

                            if (Pill < 5)
                                Pill++;
                        }
                    }
                    else if (offset > CoolRange && offset <= GoodRange)
                        CoolCombo = 0;
                    else if (offset > GoodRange && offset <= BadRange)
                    {
                        CoolCombo = 0;

                        if (Pill > 0)
                        {
                            Pill--;

                            ApplyResult(GetCappedResult(HitResult.Perfect));
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public partial class O2DrawableHoldNoteHead : DrawableHoldNoteHead
        {
            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (userTriggered)
                {
                    bool flowControl = PillCheck(timeOffset);
                    if (!flowControl) return;
                }

                base.CheckForResult(userTriggered, timeOffset);
            }

            public bool PillCheck(double timeOffset)
            {
                if (PillActivated)
                {
                    double offset = Math.Abs(timeOffset);

                    if (offset <= CoolRange)
                    {
                        CoolCombo++;

                        if (CoolCombo >= 15)
                        {
                            CoolCombo -= 15;

                            if (Pill < 5)
                                Pill++;
                        }
                    }
                    else if (offset > CoolRange && offset <= GoodRange)
                        CoolCombo = 0;
                    else if (offset > GoodRange && offset <= BadRange)
                    {
                        CoolCombo = 0;

                        if (Pill > 0)
                        {
                            Pill--;

                            ApplyResult(GetCappedResult(HitResult.Perfect));
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public partial class O2DrawableHoldNoteTail : DrawableHoldNoteTail
        {
            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (userTriggered)
                {
                    bool flowControl = PillCheck(timeOffset);
                    if (!flowControl) return;
                }

                base.CheckForResult(userTriggered, timeOffset * TailNote.RELEASE_WINDOW_LENIENCE);
            }

            public bool PillCheck(double timeOffset)
            {
                if (PillActivated)
                {
                    double offset = Math.Abs(timeOffset);

                    if (offset <= CoolRange)
                    {
                        CoolCombo++;

                        if (CoolCombo >= 15)
                        {
                            CoolCombo -= 15;

                            if (Pill < 5)
                                Pill++;
                        }
                    }
                    else if (offset > CoolRange && offset <= GoodRange)
                        CoolCombo = 0;
                    else if (offset > GoodRange && offset <= BadRange)
                    {
                        CoolCombo = 0;

                        if (Pill > 0)
                        {
                            Pill--;

                            ApplyResult(GetCappedResult(HitResult.Perfect));
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        private class O2Note : Note
        {
            public O2Note(Note note)
            {
                StartTime = note.StartTime;
                Column = note.Column;
                Samples = note.Samples;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
            }
        }

        private class O2HeadNote : HeadNote
        {
        }

        private class O2TailNote : TailNote
        {
            public override double MaximumJudgementOffset => base.MaximumJudgementOffset / RELEASE_WINDOW_LENIENCE;
        }

        private class O2HoldNote : HoldNote
        {
            public O2HoldNote(HoldNote hold)
            {
                StartTime = hold.StartTime;
                Duration = hold.Duration;
                Column = hold.Column;
                NodeSamples = hold.NodeSamples;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
                AddNested(Head = new O2HeadNote
                {
                    StartTime = StartTime,
                    Column = Column,
                    Samples = GetNodeSamples(0)
                });

                AddNested(Tail = new O2TailNote
                {
                    StartTime = EndTime,
                    Column = Column,
                    Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
                });

                AddNested(Body = new HoldNoteBody
                {
                    StartTime = StartTime,
                    Column = Column
                });
            }

            public override double MaximumJudgementOffset => base.MaximumJudgementOffset / TailNote.RELEASE_WINDOW_LENIENCE;
        }
    }
}
