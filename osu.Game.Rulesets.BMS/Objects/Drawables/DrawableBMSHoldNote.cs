// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.BMS.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.Objects.Drawables
{
    public partial class DrawableBMSHoldNote : DrawableBMSHitObject<BMSHoldNote>, IKeyBindingHandler<BMSAction>
    {
        private const float note_height = 20f;

        private Container bodyContainer = null!;
        private Box noteBody = null!;
        private Box noteHead = null!;
        private Box noteTail = null!;

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
            var colour = BMSColumn.GetColumnColour(HitObject.Column);

            AddInternal(bodyContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Children = new Drawable[]
                {
                    // Body (the hold bar)
                    noteBody = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colour,
                        Alpha = 0.6f,
                    },
                    // Head
                    noteHead = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = note_height,
                        Colour = colour,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                    },
                    // Tail
                    noteTail = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = note_height,
                        Colour = colour,
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
            if (e.Action == GetActionForColumn(HitObject.Column))
            {
                isHolding = true;
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<BMSAction> e)
        {
            if (e.Action == GetActionForColumn(HitObject.Column))
            {
                isHolding = false;

                // If released before the tail, miss
                if (Time.Current < HitObject.EndTime - HitObject.HitWindows.WindowFor(HitResult.Good))
                {
                    ApplyMinResult();
                }
            }
        }

        private static BMSAction GetActionForColumn(int column)
        {
            return column switch
            {
                0 => BMSAction.Scratch1,
                1 => BMSAction.Key1,
                2 => BMSAction.Key2,
                3 => BMSAction.Key3,
                4 => BMSAction.Key4,
                5 => BMSAction.Key5,
                6 => BMSAction.Key6,
                7 => BMSAction.Key7,
                8 => BMSAction.Scratch2,
                9 => BMSAction.Key8,
                10 => BMSAction.Key9,
                11 => BMSAction.Key10,
                12 => BMSAction.Key11,
                13 => BMSAction.Key12,
                14 => BMSAction.Key13,
                15 => BMSAction.Key14,
                _ => BMSAction.Key1
            };
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
