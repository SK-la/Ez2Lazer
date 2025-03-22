// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public abstract partial class ManiaModNoTail : Mod, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "No Tail";

        public override string Acronym => "NR";

        public override LocalisableString Description => "No more timing the end of hold notes.";

        public override ModType Type => ModType.DifficultyReduction;

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff) };

        private Bindable<HitWindows.HitMode> hitMode = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            hitMode = config.GetBindable<HitWindows.HitMode>(OsuSetting.HitMode);
            hitMode.BindValueChanged(OnHitModeChanged, true);
        }

        private void OnHitModeChanged(ValueChangedEvent<HitWindows.HitMode> e)
        {
            if (e.NewValue == HitWindows.HitMode.Ez2AcStyle)
            {
            }
            else
            {
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var hitObjects = maniaBeatmap.HitObjects.Select(obj =>
            {
                if (obj is HoldNote hold)
                    return new NoTailHoldNote(hold);

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
                    column.RegisterPool<NoTailTailNote, NoTailDrawableHoldNoteTail>(10, 50);
                }
            }
        }

        private partial class NoTailDrawableHoldNoteTail : DrawableHoldNoteTail
        {
            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                // apply perfect once the tail is reached
                if (HoldNote.HoldStartTime != null && timeOffset >= 0)
                    ApplyResult(GetCappedResult(HitResult.Perfect));
                else
                    base.CheckForResult(userTriggered, timeOffset);
            }
        }

        private class NoTailTailNote : TailNote
        {
        }

        private class NoTailHoldNote : HoldNote
        {
            public NoTailHoldNote(HoldNote hold)
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

                AddNested(Tail = new NoTailTailNote
                {
                    StartTime = EndTime,
                    Column = Column,
                    Samples = GetNodeSamples((NodeSamples?.Count - 1) ?? 1),
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
