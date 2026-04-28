// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Edit;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    /// <summary>
    /// Visualises a <see cref="Note"/> hit object.
    /// </summary>
    public partial class DrawableNote : DrawableManiaHitObject<Note>, IKeyBindingHandler<ManiaAction>
    {
        private const float timing_based_target_grayscale = 1.5f;//0.72f;
        private const float timing_based_colour_alpha = 0.8f;

        [Resolved]
        private OsuColour colours { get; set; }

        [Resolved(canBeNull: true)]
        private IBeatmap beatmap { get; set; }

        private readonly Bindable<bool> configTimingBasedNoteColouring = new Bindable<bool>();

        protected virtual ManiaSkinComponents Component => ManiaSkinComponents.Note;

        private BufferedContainer headPieceColourContainer;
        private Color4 timingBasedGrayscaleColour;
        private Color4 timingBasedOutputColour;
        private bool hasTimingBasedOutputColour;
        private double lastSnapDataStartTime = double.NaN;
        private IBeatmap lastSnapDataBeatmap;
        private Drawable headPiece;

        public DrawableNote()
            : this(null)
        {
        }

        public DrawableNote(Note hitObject)
            : base(hitObject)
        {
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ManiaRulesetConfigManager rulesetConfig)
        {
            rulesetConfig?.BindWith(ManiaRulesetSetting.TimingBasedNoteColouring, configTimingBasedNoteColouring);

            timingBasedGrayscaleColour = new Color4(
                timing_based_target_grayscale,
                timing_based_target_grayscale,
                timing_based_target_grayscale,
                1f);

            AddInternal(headPieceColourContainer = new BufferedContainer(cachedFrameBuffer: false, pixelSnapping: true)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = headPiece = new SkinnableDrawable(new ManiaSkinComponentLookup(Component), _ => new DefaultNotePiece())
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                }
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            configTimingBasedNoteColouring.BindValueChanged(_ =>
            {
                if (configTimingBasedNoteColouring.Value)
                    updateSnapData();

                updateSnapColour();
            });

            StartTimeBindable.BindValueChanged(_ =>
            {
                if (configTimingBasedNoteColouring.Value)
                    updateSnapData();

                updateSnapColour();
            }, true);
        }

        protected override void OnApply()
        {
            base.OnApply();

            if (configTimingBasedNoteColouring.Value)
                updateSnapData();

            updateSnapColour();
        }

        protected override void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> e)
        {
            base.OnDirectionChanged(e);

            Anchor anchor = e.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;

            headPieceColourContainer.Anchor = headPieceColourContainer.Origin = anchor;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            Debug.Assert(HitObject.HitWindows != null);

            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    ApplyMinResult();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);
            ApplyResult(result);
        }

        /// <summary>
        /// Some objects in mania may want to limit the max result.
        /// </summary>
        protected virtual HitResult GetCappedResult(HitResult result) => result;

        public virtual bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            if (CheckHittable?.Invoke(this, Time.Current) == false)
                return false;

            return UpdateResult(true);
        }

        public virtual void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }

        private void updateSnapColour()
        {
            if (!hasTimingBasedOutputColour)
            {
                headPieceColourContainer.GrayscaleStrength = 0;
                headPieceColourContainer.Colour = Color4.White;

                Colour = Color4.White;
                return;
            }

            if (configTimingBasedNoteColouring.Value)
            {
                headPieceColourContainer.GrayscaleStrength = 1;
                headPieceColourContainer.Colour = timingBasedOutputColour;

                Colour = Color4.White;
            }
            else
            {
                headPieceColourContainer.GrayscaleStrength = 0;
                headPieceColourContainer.Colour = Color4.White;

                Colour = Color4.White;
            }
        }

        private void updateSnapData()
        {
            if (beatmap == null || HitObject == null)
            {
                hasTimingBasedOutputColour = false;
                lastSnapDataStartTime = double.NaN;
                lastSnapDataBeatmap = null;
                return;
            }

            double startTime = HitObject.StartTime;

            if (hasTimingBasedOutputColour && lastSnapDataStartTime == startTime && ReferenceEquals(lastSnapDataBeatmap, beatmap))
                return;

            int snapDivisor = beatmap.ControlPointInfo.GetClosestBeatDivisor(startTime);

            timingBasedOutputColour = getTimingBasedOutputColour(BindableBeatDivisor.GetColourFor(snapDivisor, colours));
            hasTimingBasedOutputColour = true;
            lastSnapDataStartTime = startTime;
            lastSnapDataBeatmap = beatmap;
        }

        private Color4 getTimingBasedOutputColour(Color4 timingBasedColour)
        {
            // Equivalent to drawing an alpha-tinted layer on top of an opaque grayscale base,
            // but computed directly to avoid a second proxy/container draw path.
            return new Color4(
                timingBasedGrayscaleColour.R + (timingBasedColour.R - timingBasedGrayscaleColour.R) * timing_based_colour_alpha,
                timingBasedGrayscaleColour.G + (timingBasedColour.G - timingBasedGrayscaleColour.G) * timing_based_colour_alpha,
                timingBasedGrayscaleColour.B + (timingBasedColour.B - timingBasedGrayscaleColour.B) * timing_based_colour_alpha,
                1f);
        }

        private static Color4 GetColourFor(int beatDivisor, OsuColour colours)
        {
            switch (beatDivisor)
            {
                case 1:
                    return colours.Red;

                case 2:
                    return colours.Blue;

                case 4:
                    return colours.YellowLight;

                case 8:
                    return colours.DarkOrange2;

                case 16:
                    return colours.Green;


                case 3:
                    return colours.Purple;

                case 6:
                    return colours.Pink;

                case 12:
                    return colours.BlueLight;

                case 5:
                case 7:
                case 9:
                    return Color4.White;

                default:
                    return Color4.White;
            }
        }
    }
}
