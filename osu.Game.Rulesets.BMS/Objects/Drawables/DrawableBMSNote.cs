// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.BMS.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.Objects.Drawables
{
    public partial class DrawableBMSNote : DrawableBMSHitObject<BMSNote>, IKeyBindingHandler<BMSAction>
    {
        private const float note_height = 20f;

        private Box noteBody = null!;

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
                Colour = BMSColumn.GetColumnColour(HitObject.Column),
            });
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
            if (e.Action == GetActionForColumn(HitObject.Column))
            {
                return UpdateResult(true);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<BMSAction> e)
        {
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
