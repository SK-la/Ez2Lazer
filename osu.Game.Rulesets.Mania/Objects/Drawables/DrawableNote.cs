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
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    /// <summary>
    /// Visualises a <see cref="Note"/> hit object.
    /// </summary>
    public partial class DrawableNote : DrawableManiaHitObject<Note>, IKeyBindingHandler<ManiaAction>
    {
        [Resolved]
        private OsuColour colours { get; set; }

        [Resolved(canBeNull: true)]
        private IBeatmap beatmap { get; set; }

        private readonly Bindable<bool> configTimingBasedNoteColouring = new Bindable<bool>();
        private readonly Bindable<double> configTimingBasedNoteColouringTargetGrayscale = new Bindable<double>(ManiaTimingBasedNoteColour.DEFAULT_TARGET_GRAYSCALE);
        private readonly Bindable<double> configTimingBasedNoteColouringColourAlpha = new Bindable<double>(ManiaTimingBasedNoteColour.DEFAULT_COLOUR_ALPHA);

        protected virtual ManiaSkinComponents Component => ManiaSkinComponents.Note;
        protected virtual bool SupportsTimingBasedNoteColouring => true;

        private BufferedContainer headPieceColourContainer;
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
            rulesetConfig?.BindWith(ManiaRulesetSetting.TimingBasedNoteColouringTargetGrayscale, configTimingBasedNoteColouringTargetGrayscale);
            rulesetConfig?.BindWith(ManiaRulesetSetting.TimingBasedNoteColouringColourAlpha, configTimingBasedNoteColouringColourAlpha);

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

            if (headPiece is SkinnableDrawable skinnableDrawable)
                skinnableDrawable.OnSkinChanged += updateSnapColour;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            configTimingBasedNoteColouring.BindValueChanged(_ =>
            {
                if (SupportsTimingBasedNoteColouring && configTimingBasedNoteColouring.Value)
                    updateSnapData();

                updateSnapColour();
            });

            configTimingBasedNoteColouringTargetGrayscale.BindValueChanged(_ => updateSnapDataAndColour());
            configTimingBasedNoteColouringColourAlpha.BindValueChanged(_ => updateSnapDataAndColour());

            StartTimeBindable.BindValueChanged(_ =>
            {
                if (SupportsTimingBasedNoteColouring && configTimingBasedNoteColouring.Value)
                    updateSnapData();

                updateSnapColour();
            }, true);
        }

        protected override void OnApply()
        {
            base.OnApply();

            if (SupportsTimingBasedNoteColouring && configTimingBasedNoteColouring.Value)
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
            if (!SupportsTimingBasedNoteColouring || !hasTimingBasedOutputColour)
            {
                headPieceColourContainer.GrayscaleStrength = 0;
                headPieceColourContainer.Colour = Color4.White;

                Colour = Color4.White;
                return;
            }

            if (configTimingBasedNoteColouring.Value)
            {
                headPieceColourContainer.GrayscaleStrength = usesTimingColourTexture ? 0 : 1;
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

        private bool usesTimingColourTexture
        {
            get
            {
                if (headPiece is SkinnableDrawable skinnableDrawable)
                    return skinnableDrawable.Drawable is IManiaTimingColourTextureProvider { UsesTimingColourTexture: true };

                return headPiece is IManiaTimingColourTextureProvider { UsesTimingColourTexture: true };
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

            timingBasedOutputColour = ManiaTimingBasedNoteColour.GetColourFor(
                beatmap,
                startTime,
                colours,
                configTimingBasedNoteColouringTargetGrayscale.Value,
                configTimingBasedNoteColouringColourAlpha.Value);
            hasTimingBasedOutputColour = true;
            lastSnapDataStartTime = startTime;
            lastSnapDataBeatmap = beatmap;
        }

        private void updateSnapDataAndColour()
        {
            lastSnapDataStartTime = double.NaN;
            lastSnapDataBeatmap = null;

            if (SupportsTimingBasedNoteColouring && configTimingBasedNoteColouring.Value)
                updateSnapData();

            updateSnapColour();
        }
    }
}
