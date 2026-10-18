// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Edit;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    /// <summary>
    /// Draws a hold note's head / body / tail as a single composite. The judgement drawables nested in
    /// <see cref="DrawableHoldNote"/> keep their scoring behaviour but carry no visuals of their own.
    /// </summary>
    internal partial class HoldNoteVisual : CompositeDrawable
    {
        /// <summary>Length of the drawn head, used by the parent hold to size the body and by editor blueprints.</summary>
        public float HeadLength => headPiece.DrawHeight;

        /// <summary>Length of the drawn tail, used by the parent hold to size the body and by editor blueprints.</summary>
        public float TailLength => tailPiece.DrawHeight;

        private readonly DrawableHoldNote holdNote;

        private Container sizingContainer;
        private Container maskingContainer;
        private Container maskedContents;
        private Container tailSlot;
        private SkinnableDrawable headPiece;
        private SkinnableDrawable bodyPiece;
        private SkinnableDrawable tailPiece;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly Bindable<bool> timingBasedColouring = new Bindable<bool>();

        private readonly IBindable<double> headStartTime = new Bindable<double>();
        private readonly IBindable<double> tailStartTime = new Bindable<double>();

        private DrawableHoldNoteTail boundTail;

        [Resolved]
        private OsuColour colours { get; set; }

        [Resolved(canBeNull: true)]
        private IBeatmap beatmap { get; set; }

        public HoldNoteVisual(DrawableHoldNote holdNote)
        {
            this.holdNote = holdNote;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ManiaRulesetConfigManager rulesetConfig, IScrollingInfo scrollingInfo)
        {
            rulesetConfig?.BindWith(ManiaRulesetSetting.TimingBasedNoteColouring, timingBasedColouring);

            AddRangeInternal(new Drawable[]
            {
                sizingContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        maskingContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = maskedContents = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = !ManiaDenseScrollAblation.DisableHoldMasking,
                            }
                        },
                        headPiece = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteHead), _ => new DefaultNotePiece())
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                    }
                },
                bodyPiece = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteBody), _ => new DefaultBodyPiece
                {
                    RelativeSizeAxes = Axes.Both,
                })
                {
                    RelativeSizeAxes = Axes.X,
                },
                tailSlot = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = tailPiece = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.HoldNoteTail), _ => new DefaultNotePiece())
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                },
            });

            if (!ManiaDenseScrollAblation.DisableHoldProxies)
            {
                maskedContents.AddRange(new[]
                {
                    bodyPiece.CreateProxy(),
                    tailSlot.CreateProxy(),
                });
            }

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            timingBasedColouring.BindValueChanged(_ => updateColours());
        }

        /// <summary>
        /// Called by the parent hold after a (possibly different) hit object has been applied to it.
        /// </summary>
        public void OnHoldApplied()
        {
            sizingContainer.Size = Vector2.One;
            tailSlot.ClearTransforms();
            tailSlot.Alpha = 1;

            headStartTime.UnbindAll();
            tailStartTime.UnbindAll();

            if (boundTail != null)
                boundTail.ApplyCustomUpdateState -= onTailUpdateState;

            var head = holdNote.HeadOrNull;

            if (head != null)
            {
                headStartTime.BindTo(head.StartTimeBindable);
                headStartTime.BindValueChanged(_ => updateColours());
            }

            boundTail = holdNote.TailOrNull;

            if (boundTail != null)
            {
                tailStartTime.BindTo(boundTail.StartTimeBindable);
                tailStartTime.BindValueChanged(_ => updateColours());

                boundTail.ApplyCustomUpdateState += onTailUpdateState;
            }

            updateColours();
        }

        /// <summary>
        /// Positions the three segments. Called every frame from <see cref="DrawableHoldNote"/>.
        /// </summary>
        public void UpdateLayout()
        {
            float headLength = headPiece.DrawHeight;
            float tailLength = tailPiece.DrawHeight;
            bool scrollingUp = direction.Value == ScrollingDirection.Up;

            // Pad the full size container so its contents (i.e. the masking container) reach under the tail.
            // This is required for the tail to not be masked away, since it lies outside the bounds of the hold note.
            sizingContainer.Padding = new MarginPadding
            {
                Top = scrollingUp ? 0 : -tailLength,
                Bottom = scrollingUp ? -tailLength : 0,
            };

            // Pad the masking container to the starting position of the body piece (half-way under the head).
            // This is required to make the body start getting masked immediately as soon as the note is held.
            maskingContainer.Padding = new MarginPadding
            {
                Top = scrollingUp ? headLength / 2 : 0,
                Bottom = scrollingUp ? 0 : headLength / 2,
            };

            // Position and resize the body to lie half-way under the head and the tail notes.
            // The rationale for this is account for heads/tails with corner radius.
            bodyPiece.Y = (scrollingUp ? 1 : -1) * headLength / 2;
            bodyPiece.Height = DrawHeight - headLength / 2 + tailLength / 2;

            tailSlot.Y = (scrollingUp ? 1 : -1) * DrawHeight;

            if (Time.Current >= holdNote.HitObject.StartTime)
            {
                // As the note is being held, adjust the size of the sizing container. This has two effects:
                // 1. The contained masking container will mask the body and ticks.
                // 2. The head note will move along with the new "head position" in the container.
                //
                // As per stable, this should not apply for early hits, waiting until the object starts to touch the
                // judgement area first.
                if (holdNote.Head.IsHit && !holdNote.Result.DroppedHoldAfter(holdNote.HitObject.StartTime) && DrawHeight > 0)
                {
                    float yOffset = scrollingUp ? -holdNote.Y : holdNote.Y;
                    sizingContainer.Height = 1 - yOffset / DrawHeight;
                }
            }
            else
                sizingContainer.Height = 1;
        }

        /// <summary>
        /// Resets per-note state before the hold note is returned to its pool.
        /// </summary>
        public void Recycle()
        {
            (bodyPiece.Drawable as IHoldNoteBody)?.Recycle();
            tailSlot.ClearTransforms();
            tailSlot.Alpha = 1;
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> e)
        {
            if (e.NewValue == ScrollingDirection.Up)
            {
                bodyPiece.Anchor = bodyPiece.Origin = Anchor.TopLeft;
                sizingContainer.Anchor = sizingContainer.Origin = Anchor.BottomLeft;
                headPiece.Anchor = headPiece.Origin = Anchor.TopCentre;
                tailPiece.Anchor = tailPiece.Origin = Anchor.TopCentre;
            }
            else
            {
                bodyPiece.Anchor = bodyPiece.Origin = Anchor.BottomLeft;
                sizingContainer.Anchor = sizingContainer.Origin = Anchor.TopLeft;
                headPiece.Anchor = headPiece.Origin = Anchor.BottomCentre;
                tailPiece.Anchor = tailPiece.Origin = Anchor.BottomCentre;
            }
        }

        private void onTailUpdateState(DrawableHitObject obj, ArmedState state)
        {
            switch (state)
            {
                case ArmedState.Miss:
                    tailSlot.FadeOut(150, Easing.In);
                    break;

                case ArmedState.Hit:
                    tailSlot.FadeOut();
                    break;
            }
        }

        private void updateColours()
        {
            headPiece.Colour = resolveSnapColour(holdNote.HitObject.Head.StartTime);
            tailPiece.Colour = resolveSnapColour(holdNote.HitObject.Tail.StartTime);
        }

        private Color4 resolveSnapColour(double startTime)
        {
            if (beatmap == null || holdNote.HitObject == null)
                return Color4.White;

            int snapDivisor = beatmap.ControlPointInfo.GetClosestBeatDivisor(startTime);

            return timingBasedColouring.Value ? BindableBeatDivisor.GetColourFor(snapDivisor, colours) : Color4.White;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && boundTail != null)
                boundTail.ApplyCustomUpdateState -= onTailUpdateState;

            base.Dispose(isDisposing);
        }
    }
}
