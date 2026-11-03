// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Objects;
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
        private DrawableBMSHoldNoteHead? head;
        private DrawableBMSHoldNoteBody? body;
        private DrawableBMSHoldNoteTail? tail;

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
            if (tail == null)
                return;

            if (!tail.AllJudged && Time.Current >= HitObject.EndTime)
                tail.TriggerUpdate(false);

            if (!tail.AllJudged)
                return;

            ApplyResult(tail.IsHit ? HitResult.Great : HitResult.Miss);
        }

        protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
        {
            switch (hitObject)
            {
                case BMSHoldNoteHead holdHead:
                    return new DrawableBMSHoldNoteHead(holdHead);

                case BMSHoldNoteTail holdTail:
                    return new DrawableBMSHoldNoteTail(holdTail);

                case BMSHoldNoteBody holdBody:
                    return new DrawableBMSHoldNoteBody(holdBody);
            }

            return base.CreateNestedHitObject(hitObject);
        }

        protected override void AddNestedHitObject(DrawableHitObject hitObject)
        {
            base.AddNestedHitObject(hitObject);

            switch (hitObject)
            {
                case DrawableBMSHoldNoteHead nestedHead:
                    head = nestedHead;
                    break;

                case DrawableBMSHoldNoteBody nestedBody:
                    body = nestedBody;
                    break;

                case DrawableBMSHoldNoteTail nestedTail:
                    tail = nestedTail;
                    break;
            }
        }

        protected override void ClearNestedHitObjects()
        {
            base.ClearNestedHitObjects();
            head = null;
            body = null;
            tail = null;
        }

        public bool OnPressed(KeyBindingPressEvent<BMSAction> e)
        {
            if (AllJudged)
                return false;

            if (e.Action == stageLayout.ActionFor(HitObject))
            {
                if (CheckHittable?.Invoke(this, Time.Current) == false)
                    return false;

                if (head == null || tail == null)
                    return false;

                // Do not begin holds deep into the tail miss-lenience window.
                if (Time.Current > tail.HitObject.StartTime && !tail.HitObject.HitWindows.CanBeHit(Time.Current - tail.HitObject.StartTime))
                    return false;

                bool consumed = head.TriggerUpdate(true);
                isHolding = head.IsHit;
                return consumed;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<BMSAction> e)
        {
            if (AllJudged)
                return;

            if (e.Action == stageLayout.ActionFor(HitObject))
            {
                if (!isHolding || tail == null)
                    return;

                tail.TriggerUpdate(false);
                isHolding = false;
            }
        }

        public override void MissForcefully()
        {
            isHolding = false;
            head?.MissForcefully();
            body?.MissForcefully();
            tail?.MissForcefully();
            base.MissForcefully();
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

    public partial class DrawableBMSHoldNoteHead : DrawableBMSNote
    {
        public DrawableBMSHoldNoteHead(BMSHoldNoteHead hitObject)
            : base(hitObject)
        {
            Alpha = 0;
        }

        public bool TriggerUpdate(bool userTriggered) => UpdateResult(userTriggered);
    }

    public partial class DrawableBMSHoldNoteTail : DrawableBMSNote
    {
        public DrawableBMSHoldNoteTail(BMSHoldNoteTail hitObject)
            : base(hitObject)
        {
            Alpha = 0;
        }

        public bool TriggerUpdate(bool userTriggered) => UpdateResult(userTriggered);
    }

    public partial class DrawableBMSHoldNoteBody : DrawableBMSHitObject<BMSHoldNoteBody>
    {
        public DrawableBMSHoldNoteBody(BMSHoldNoteBody hitObject)
            : base(hitObject)
        {
            Alpha = 0;
            RelativeSizeAxes = Axes.X;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
        }
    }
}
