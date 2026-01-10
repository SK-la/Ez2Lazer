// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public partial class ManiaModReleaseAdjust : Mod, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "Release Adjust";

        public override string Acronym => "RA";

        public override LocalisableString Description => EzManiaModStrings.ReleaseAdjust_Description;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff) };

        [SettingSource("Offset")]
        public BindableInt ReleaseOffset { get; set; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 250,
            Precision = 10,
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var hitObjects = maniaBeatmap.HitObjects.Select(obj =>
            {
                if (obj is HoldNote hold)
                    return new NoReleaseHoldNote(hold);

                return obj;
            }).ToList();

            maniaBeatmap.HitObjects = hitObjects;

            NoReleaseDrawableHoldNoteTail.ReleaseOffset = ReleaseOffset.Value;
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            var maniaRuleset = (DrawableManiaRuleset)drawableRuleset;

            foreach (var stage in maniaRuleset.Playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    column.RegisterPool<NoReleaseTailNote, NoReleaseDrawableHoldNoteTail>(10, 50);
                }
            }
        }

        public partial class NoReleaseDrawableHoldNoteTail : DrawableHoldNoteTail
        {
            public static int ReleaseOffset = 50;

            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                if (HoldNote.IsHolding.Value && Math.Abs(timeOffset) <= ReleaseOffset)
                    ApplyResult(GetCappedResult(HitResult.Perfect));
                else
                    base.CheckForResult(userTriggered, timeOffset);
            }
        }

        private class NoReleaseTailNote : TailNote
        {
        }

        private class NoReleaseHoldNote : HoldNote
        {
            public NoReleaseHoldNote(HoldNote hold)
            {
                StartTime = hold.StartTime;
                Duration = hold.Duration;
                Column = hold.Column;
                NodeSamples = hold.NodeSamples;
            }

            protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
            {
                AddNested(Head = new HeadNote
                {
                    StartTime = StartTime,
                    Column = Column,
                    Samples = GetNodeSamples(0),
                });

                AddNested(Tail = new NoReleaseTailNote
                {
                    StartTime = EndTime,
                    Column = Column,
                    Samples = GetNodeSamples(NodeSamples?.Count - 1 ?? 1),
                });

                AddNested(Body = new HoldNoteBody
                {
                    StartTime = StartTime,
                    Column = Column
                });
            }
        }
    }
}
