// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
    public partial class DrawableBMSHoldNote : DrawableBMSHitObject<BMSHoldNote>, IKeyBindingHandler<BMSAction>
    {
        private const float note_height = 20f;

        [Resolved]
        private BMSStageLayout stageLayout { get; set; } = null!;

        private Container bodyContainer = null!;
        private Drawable noteBody = null!;
        private Drawable noteHead = null!;
        private Drawable noteTail = null!;

        private bool isHolding;

        public DrawableBMSHoldNote()
            : this(null!)
        {
        }

        public DrawableBMSHoldNote(BMSHoldNote? hitObject)
            : base(hitObject)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(bodyContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Children = new[]
                {
                    // Body (the hold bar)
                    noteBody = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteBody), _ => new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    })
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    // Head
                    noteHead = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteHead), _ => new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    })
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = note_height,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                    },
                    // Tail
                    noteTail = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteTail), _ => new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    })
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = note_height,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                }
            });
        }

        protected override void Update()
        {
            base.Update();

            // Calculate visual height based on duration and scroll speed
            // This is a simplified calculation
            float height = (float)(HitObject.Duration * 0.5f); // Adjust multiplier based on scroll speed
            Height = height;
            bodyContainer.Height = height;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (Time.Current < HitObject.EndTime)
                return;

            // Simple hold note logic: check if held through the entire duration
            ApplyResult(isHolding ? HitResult.Great : HitResult.Miss);
        }

        public bool OnPressed(KeyBindingPressEvent<BMSAction> e)
        {
            if (e.Action == stageLayout.ActionFor(HitObject))
            {
                isHolding = true;
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<BMSAction> e)
        {
            if (e.Action == stageLayout.ActionFor(HitObject))
            {
                isHolding = false;

                // If released before the tail, miss
                if (Time.Current < HitObject.EndTime - HitObject.HitWindows.WindowFor(HitResult.Good))
                {
                    ApplyMinResult();
                }
            }
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
