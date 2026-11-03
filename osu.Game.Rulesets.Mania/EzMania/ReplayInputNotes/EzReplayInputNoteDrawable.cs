// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.Edit.Extension;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayInputNotes
{
    /// <summary>
    /// Draws one replay input note using the editor's note silhouette.
    /// </summary>
    /// <remarks>
    /// Position along the scroll axis and, for hold notes, the drawn length are supplied by the playfield's
    /// <see cref="osu.Game.Rulesets.UI.Scrolling.ScrollingHitObjectContainer"/> — the same container the beatmap's notes
    /// go through. This class only chooses the anchor convention (mirroring <see cref="Objects.Drawables.DrawableManiaHitObject"/>),
    /// lays out head/body/tail inside that span and picks colours.
    /// </remarks>
    public abstract partial class EzReplayInputNoteDrawable : DrawableHitObject<EzReplayInputNoteObject>
    {
        /// <summary>Matches the editor's least note height; also the height of the head band, like the editor's head piece.</summary>
        protected const float HEAD_BAND_HEIGHT = ExtensionLane.MINIMUM_NOTE_HEIGHT;

        public readonly ExtensionNoteVisual Visual;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        [Resolved]
        private EzReplayInputNoteModel model { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IEzSkinInfo? ezSkinInfo { get; set; }

        private bool tailHidden;
        private bool scrollsDown;

        protected EzReplayInputNoteDrawable()
            : base(null!)
        {
            RelativeSizeAxes = Axes.X;
            AddInternal(Visual = new ExtensionNoteVisual());
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            // Mirrors the playfield: with the fake tail on, the tail piece gives way to the body reaching the end.
            tailHidden = ezSkinInfo?.ManiaLNGradientEnable.Value != false;

            direction.BindTo(scrollingInfo.Direction);

            // Same anchor rule as the playfield's own notes (see DrawableManiaHitObject.OnDirectionChanged), so the
            // container-assigned position always lands on the same edge of the visual.
            direction.BindValueChanged(d =>
            {
                scrollsDown = d.NewValue is ScrollingDirection.Down or ScrollingDirection.Right;
                Anchor = Origin = d.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            var (headColour, bodyColour) = resolveColours();

            Visual.TailHidden = tailHidden;
            Visual.HeadColour = headColour;
            Visual.BodyColour = bodyColour;
            Visual.TailColour = bodyColour;
            Visual.ApplySpan(DrawHeight, headHeight, scrollsDown);
        }

        private float headHeight => HitObject is EzReplayInputHoldNote && DrawHeight > 0
            ? Math.Min(HEAD_BAND_HEIGHT, DrawHeight * 0.5f)
            : 0;

        private (Colour4 Head, Colour4 Body) resolveColours()
        {
            // Colours need judgement results; until they arrive the overlay stays the plain uncoloured look instead of
            // painting everything as "no result", which would be indistinguishable from a broken results path.
            if (!model.ColourByHitResult.Value || !model.ResultsApplied)
                return (Colour4.White, Colour4.White);

            if (HitObject.Kind == EzReplayInputNoteKind.None)
                return (Colour4.Gray, Colour4.Gray);

            Colour4 head = colourFor(HitObject.HeadResult);

            if (HitObject.Kind == EzReplayInputNoteKind.Note)
                return (head, head);

            Colour4 body = HitObject.BodyBroken ? Colour4.Gray : colourFor(HitObject.NonHeadResult ?? HitObject.HeadResult);

            return (head, body);
        }

        private Colour4 colourFor(HitResult? result)
        {
            if (result == null)
                return Colour4.Gray;

            switch (result.Value)
            {
                case HitResult.None:
                case HitResult.IgnoreHit:
                case HitResult.IgnoreMiss:
                    return Colour4.Gray;

                default:
                    return colours.ForHitResult(result.Value);
            }
        }
    }

    /// <summary>A press drawn as a fixed-height block: the editor's tap note silhouette.</summary>
    public partial class EzReplayInputTapNoteDrawable : EzReplayInputNoteDrawable
    {
        public EzReplayInputTapNoteDrawable()
        {
            Height = HEAD_BAND_HEIGHT;
        }
    }

    /// <summary>A held press; the playfield assigns the length from <see cref="EzReplayInputHoldNote.EndTime"/>.</summary>
    public partial class EzReplayInputHoldNoteDrawable : EzReplayInputNoteDrawable
    {
    }
}
