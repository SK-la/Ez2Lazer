// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    internal partial class ManiaModMalodyStyleLN : Mod, IApplicableToDifficulty, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "No LN Judgement";

        public override string Acronym => "NL";

        public override LocalisableString Description => EzManiaModStrings.MalodyStyleLN_Description;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public HitWindows HitWindows { get; set; } = new ManiaHitWindows();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var hitObjects = maniaBeatmap.HitObjects.Select(obj =>
            {
                if (obj is Note note)
                    return new NoLNNote(note);

                if (obj is HoldNote hold)
                    return new NoLNHoldNote(hold);

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
                    column.RegisterPool<NoLNBodyNote, NoLNDrawableHoldNoteBody>(10, 50);
                    column.RegisterPool<NoLNTailNote, NoLNDrawableHoldNoteTail>(10, 50);
                }
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            HitWindows = new ManiaHitWindows();
            HitWindows.SetDifficulty(difficulty.OverallDifficulty);

            NoLNDrawableHoldNoteTail.HitWindows = HitWindows;
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
            public override Judgement CreateJudgement() => new NoLNBodyJudgement();

            protected override HitWindows CreateHitWindows() => HitWindows.Empty;
        }

        private class NoLNTailNote : TailNote
        {
            public override Judgement CreateJudgement() => new NoLNJudgement();

            protected override HitWindows CreateHitWindows() => HitWindows.Empty;
        }

        private class NoLNHoldNote : HoldNote
        {
            public NoLNHoldNote(HoldNote hold)
            {
                StartTime = hold.StartTime;
                Duration = hold.Duration;
                Column = hold.Column;
                NodeSamples = hold.NodeSamples;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
                AddNested(Head = new NoLNHeadNote
                {
                    StartTime = StartTime,
                    Column = Column,
                    Samples = GetNodeSamples(0),
                });

                AddNested(Tail = new NoLNTailNote
                {
                    StartTime = EndTime,
                    Column = Column,
                    Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1),
                });

                AddNested(Body = new NoLNBodyNote
                {
                    StartTime = StartTime,
                    Column = Column
                });
            }
        }

        private class NoLNBodyJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;

            public override HitResult MinResult => HitResult.IgnoreMiss;
        }

        private class NoLNJudgement : ManiaJudgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;

            public override HitResult MinResult => HitResult.ComboBreak;
        }

        public partial class NoLNDrawableHoldNoteBody : DrawableHoldNoteBody
        {
            public new bool HasHoldBreak => false;

            internal new void TriggerResult(bool hit)
            {
                if (AllJudged) return;

                ApplyMaxResult();
            }
        }

        public partial class NoLNDrawableHoldNoteTail : DrawableHoldNoteTail
        {
            public static HitWindows HitWindows = new ManiaHitWindows();

            public override bool DisplayResult => false;

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (!HoldNote.Head.IsHit)
                {
                    return;
                }

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

                if (HoldNote.IsHolding.Value)
                {
                    return;
                }

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
