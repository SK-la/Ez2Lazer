// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class ManiaModLNJudgementAdjust : Mod, IApplicableToDifficulty, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "LN Judgement Adjust";

        public override string Acronym => "LA";

        public override LocalisableString Description => EzManiaModStrings.LNJudgementAdjust_Description;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public HitWindows HitWindows { get; set; } = new ManiaHitWindows();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.BodyJudgementSwitch_Label), nameof(EzManiaModStrings.BodyJudgementSwitch_Description))]
        public BindableBool BodyJudgementSwitch { get; } = new BindableBool();

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.TailJudgementSwitch_Label), nameof(EzManiaModStrings.TailJudgementSwitch_Description))]
        public BindableBool TailJudgementSwitch { get; } = new BindableBool();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var hitObjects = maniaBeatmap.HitObjects.Select(obj =>
            {
                //if (obj is Note note)
                //    return new NoLNNote(note);

                if (obj is HoldNote hold) return new LNHoldNote(hold);

                return obj;
            }).ToList();

            maniaBeatmap.HitObjects = hitObjects;
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            var maniaRuleset = (DrawableManiaRuleset)drawableRuleset;

            foreach (var stage in maniaRuleset.Playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    column.RegisterPool<NoLNNote, DrawableNote>(10, 50);
                    column.RegisterPool<NoLNHeadNote, DrawableHoldNoteHead>(10, 50);

                    if (!TailJudgementSwitch.Value && !BodyJudgementSwitch.Value)
                    {
                        column.RegisterPool<NoLNBodyNote, NoLNDrawableHoldNoteBody>(10, 50);
                        column.RegisterPool<NoLNTailNote, NoLNDrawableHoldNoteTail>(10, 50);
                    }

                    if (BodyJudgementSwitch.Value && !TailJudgementSwitch.Value)
                    {
                        column.RegisterPool<AllLNBodyNote, AllLNDrawableHoldNoteBody>(10, 50);
                        column.RegisterPool<NoLNTailNote, NoLNDrawableHoldNoteTail>(10, 50);
                    }

                    if (BodyJudgementSwitch.Value && TailJudgementSwitch.Value)
                    {
                        column.RegisterPool<AllLNBodyNote, AllLNDrawableHoldNoteBody>(10, 50);
                        column.RegisterPool<TailNote, DrawableHoldNoteTail>(10, 50);
                    }

                    if (!BodyJudgementSwitch.Value && TailJudgementSwitch.Value)
                    {
                        column.RegisterPool<HoldNoteBody, DrawableHoldNoteBody>(10, 50);
                        column.RegisterPool<TailNote, DrawableHoldNoteTail>(10, 50);
                        // Vanilla LN
                    }
                }
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            HitWindows = new ManiaHitWindows();
            HitWindows.SetDifficulty(difficulty.OverallDifficulty);

            NoLNDrawableHoldNoteTail.HitWindows = HitWindows;
            LNHoldNote.BodyJudgementSwitch = BodyJudgementSwitch.Value;
            LNHoldNote.TailJudgementSwitch = TailJudgementSwitch.Value;
        }

        private class NoLNNote : Note
        {
            public NoLNNote(Note note)
            {
                StartTime = note.StartTime;
                Column = note.Column;
                Samples = note.Samples;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
            }
        }

        private class NoLNHeadNote : HeadNote
        {
        }

        private class NoLNBodyNote : HoldNoteBody
        {
            public override Judgement CreateJudgement()
            {
                return new NoLNBodyJudgement();
            }

            protected override HitWindows CreateHitWindows()
            {
                return HitWindows.Empty;
            }
        }

        private class AllLNBodyNote : HoldNoteBody
        {
            public override Judgement CreateJudgement()
            {
                return new AllLNBodyJudgement();
            }

            protected override HitWindows CreateHitWindows()
            {
                return HitWindows.Empty;
            }
        }

        private class NoLNTailNote : TailNote
        {
            public override Judgement CreateJudgement()
            {
                return new NoLNTailJudgement();
            }

            protected override HitWindows CreateHitWindows()
            {
                return new ManiaHitWindows();
            }
        }

        private class LNHoldNote : HoldNote
        {
            public static bool BodyJudgementSwitch;

            public static bool TailJudgementSwitch;

            public LNHoldNote(HoldNote hold)
            {
                StartTime = hold.StartTime;
                Duration = hold.Duration;
                Column = hold.Column;
                NodeSamples = hold.NodeSamples;
            }

            static LNHoldNote()
            {
                TailJudgementSwitch = false;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
                AddNested(Head = new HeadNote
                {
                    StartTime = StartTime,
                    Column = Column,
                    Samples = GetNodeSamples(0)
                });

                if (!BodyJudgementSwitch && !TailJudgementSwitch)
                {
                    AddNested(Body = new NoLNBodyNote
                    {
                        StartTime = StartTime,
                        Column = Column
                    });

                    AddNested(Tail = new NoLNTailNote
                    {
                        StartTime = EndTime,
                        Column = Column,
                        Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
                    });
                }
                else if (BodyJudgementSwitch && !TailJudgementSwitch)
                {
                    AddNested(Body = new AllLNBodyNote
                    {
                        StartTime = StartTime,
                        Column = Column
                    });

                    AddNested(Tail = new NoLNTailNote
                    {
                        StartTime = EndTime,
                        Column = Column,
                        Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
                    });
                }
                else if (!BodyJudgementSwitch && TailJudgementSwitch)
                {
                    AddNested(Body = new HoldNoteBody
                    {
                        StartTime = StartTime,
                        Column = Column
                    });

                    AddNested(Tail = new TailNote
                    {
                        StartTime = EndTime,
                        Column = Column,
                        Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
                    });
                }
                else
                {
                    AddNested(Body = new AllLNBodyNote
                    {
                        StartTime = StartTime,
                        Column = Column
                    });

                    AddNested(Tail = new TailNote
                    {
                        StartTime = EndTime,
                        Column = Column,
                        Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1)
                    });
                }
            }
        }

        private class NoLNBodyJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;

            public override HitResult MinResult => HitResult.IgnoreMiss;
        }

        private class AllLNBodyJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.Perfect;

            public override HitResult MinResult => HitResult.Miss;
        }

        private class NoLNTailJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;

            public override HitResult MinResult => HitResult.ComboBreak;
        }

        public partial class NoLNDrawableHoldNoteBody : DrawableHoldNoteBody
        {
            public new bool HasHoldBreak => false;

            internal override void TriggerResult(bool hit)
            {
                if (AllJudged) return;

                ApplyMaxResult();
            }
        }

        public partial class AllLNDrawableHoldNoteBody : DrawableHoldNoteBody
        {
            public override bool DisplayResult => true;

            protected internal DrawableHoldNote HoldNote => (DrawableHoldNote)ParentHitObject;

            internal override void TriggerResult(bool hit)
            {
                if (AllJudged) return;

                if (hit)
                    ApplyResult(HoldNote.Head.Result.Type);
                else
                    ApplyResult(HitResult.Miss);
            }
        }

        public partial class NoLNDrawableHoldNoteTail : DrawableHoldNoteTail
        {
            public static HitWindows HitWindows = new ManiaHitWindows();

            public override bool DisplayResult => false;

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (!HoldNote.Head.IsHit) return;

                if (timeOffset > 0 && HoldNote.Head.IsHit)
                {
                    ApplyMaxResult();
                    return;
                }
                else if (timeOffset > 0)
                {
                    ApplyMinResult();
                    return;
                }

                if (HoldNote.IsHolding.Value) return;

                if (HoldNote.Head.IsHit && Math.Abs(timeOffset) < Math.Abs(HitWindows.WindowFor(HitResult.Meh) * TailNote.RELEASE_WINDOW_LENIENCE))
                {
                    ApplyMaxResult();
                }
                else
                {
                    ApplyMinResult();
                }
            }
        }
    }
}
