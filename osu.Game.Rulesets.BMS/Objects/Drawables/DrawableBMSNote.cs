// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.Objects.Drawables
{
    public partial class DrawableBMSNote : DrawableBMSHitObject<BMSNote>, IKeyBindingHandler<BMSAction>
    {
        private const float note_height = 20f;

        [Resolved]
        private BMSStageLayout stageLayout { get; set; } = null!;

        private Drawable noteBody = null!;

        public DrawableBMSNote()
            : this(null!)
        {
        }

        public DrawableBMSNote(BMSNote? hitObject)
            : base(hitObject)
        {
            Height = note_height;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(noteBody = new Box
            {
                RelativeSizeAxes = Axes.Both,
            });

            noteBody = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.Note), _ => new Box
            {
                RelativeSizeAxes = Axes.Both,
            });

            ClearInternal();
            AddInternal(noteBody);
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                {
                    ApplyMinResult();
                }

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);
            if (result == HitResult.None)
                return;

            ApplyResult(result);
        }

        public bool OnPressed(KeyBindingPressEvent<BMSAction> e)
        {
            if (e.Action == stageLayout.ActionFor(HitObject))
            {
                if (CheckHittable?.Invoke(this, Time.Current) == false)
                    return false;

                return UpdateResult(true);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<BMSAction> e)
        {
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            switch (state)
            {
                case ArmedState.Hit:
                    this.FadeOut(100);
                    break;

                case ArmedState.Miss:
                    this.FadeColour(Color4.Red, 100);
                    this.FadeOut(100);
                    break;
            }
        }
    }
}
