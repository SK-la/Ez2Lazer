// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    public partial class TestSceneHoldNote : ManiaHitObjectTestScene
    {
        [Test]
        public void TestHoldNote()
        {
            AddToggleStep("toggle hitting", v =>
            {
                foreach (var holdNote in CreatedDrawables.SelectMany(d => d.ChildrenOfType<DrawableHoldNote>()))
                {
                    ((Bindable<bool>)holdNote.IsHolding).Value = v;
                }
            });
        }

        /// <summary>
        /// 视觉拆分后 head / tail 判定框不带皮肤子树，三段像素全部由 hold 的合成 drawable 绘制。
        /// </summary>
        [Test]
        public void TestHoldNoteSplitVisual()
        {
            AddStep("use composite visual", () =>
            {
                ManiaDenseScrollAblation.SplitHoldVisual = true;
                SetUp();
            });

            AddUntilStep("hold notes created", () => holdNotes.Any());

            AddAssert("head carries no visual", () =>
                holdNotes.All(h => h.ChildrenOfType<DrawableHoldNoteHead>().All(head => !head.ChildrenOfType<SkinnableDrawable>().Any())));

            AddAssert("tail carries no visual", () =>
                holdNotes.All(h => h.ChildrenOfType<DrawableHoldNoteTail>().All(tail => !tail.ChildrenOfType<SkinnableDrawable>().Any())));

            AddAssert("composite draws three pieces", () =>
                holdNotes.All(h => h.ChildrenOfType<SkinnableDrawable>().Count() == 3));

            AddToggleStep("toggle hitting", v =>
            {
                foreach (var holdNote in holdNotes)
                    ((Bindable<bool>)holdNote.IsHolding).Value = v;
            });
        }

        [TearDown]
        public void RestoreVisualMode()
        {
            ManiaDenseScrollAblation.SplitHoldVisual = false;
        }

        [Test]
        public void TestFadeOnMiss()
        {
            AddStep("miss tick", () =>
            {
                foreach (var holdNote in holdNotes)
                    holdNote.ChildrenOfType<DrawableHoldNoteHead>().First().MissForcefully();
            });
        }

        private IEnumerable<DrawableHoldNote> holdNotes => CreatedDrawables.SelectMany(d => d.ChildrenOfType<DrawableHoldNote>());

        protected override DrawableManiaHitObject CreateHitObject()
        {
            var note = new HoldNote { Duration = 1000 };
            note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

            return new DrawableHoldNote(note);
        }
    }
}
