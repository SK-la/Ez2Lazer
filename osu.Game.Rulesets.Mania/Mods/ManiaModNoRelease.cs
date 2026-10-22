// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.Mods
{
    public partial class ManiaModNoRelease : Mod, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>, IEzApplyOrder
    {
        public override string Name => "No Release";

        public override string Acronym => "NR";

        public override LocalisableString Description => "No more timing the end of hold notes.";

        public override IconUsage? Icon => OsuIcon.ModNoRelease;

        public override ModType Type => ModType.DifficultyReduction;

        /// <summary>
        /// 释放窗口改写属于谱面成型后的后处理：排在转键与内容 mod 之后，才不会让前面生成的 LN 被提前改写。
        /// </summary>
        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff), typeof(ManiaModInvert) };

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

        private partial class NoReleaseDrawableHoldNoteTail : DrawableHoldNoteTail
        {
            protected override void CheckForResult(bool userTriggered, double timeOffset)
            {
                // apply perfect once the tail is reached
                if (HoldNote.IsHolding.Value && timeOffset >= 0)
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
                Samples = hold.Samples;
                NodeSamples = hold.NodeSamples;
                PlaySlidingSamples = hold.PlaySlidingSamples;
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
