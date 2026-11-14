// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.Skinning.EzStylePro;
using osu.Game.Rulesets.Mania.UI;
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
        [Resolved(CanBeNull = true)]
        private ManiaPlayfieldNoteDisplayTint noteDisplayTint { get; set; }

        [Resolved(canBeNull: true)]
        private Column column { get; set; }

        protected virtual ManiaSkinComponents Component => ManiaSkinComponents.Note;

        protected virtual ManiaNoteDisplayColourPart DisplayColourPart => ManiaNoteDisplayColourPart.Note;

        protected virtual bool UseColorization => true;

        private Drawable headPiece;

        private bool displayColourBound;
        private bool layoutColourBound;

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
        private void load()
        {
            var skinnable = new SkinnableDrawable(new ManiaSkinComponentLookup(Component), _ => new DefaultNotePiece())
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y
            };

            skinnable.OnSkinChanged += updateSnapColour;
            AddInternal(headPiece = skinnable);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (receivesDisplayColour())
            {
                StartTimeBindable.BindValueChanged(_ => updateSnapColour(), true);

                if (column != null)
                {
                    column.NoteColourChanged += onColumnNoteColourSettingsChanged;
                    column.NoteSizeChanged += onColumnNoteColourSettingsChanged;
                    displayColourBound = true;
                    layoutColourBound = true;
                }
            }

            updateSnapColour();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (headPiece is SkinnableDrawable skinnable)
                    skinnable.OnSkinChanged -= updateSnapColour;

                if (displayColourBound && column != null)
                    column.NoteColourChanged -= onColumnNoteColourSettingsChanged;

                if (layoutColourBound && column != null)
                    column.NoteSizeChanged -= onColumnNoteColourSettingsChanged;
            }

            base.Dispose(isDisposing);
        }

        private void onColumnNoteColourSettingsChanged() => updateSnapColour();

        protected override void OnApply()
        {
            base.OnApply();
            updateSnapColour();
        }

        protected override void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> e)
        {
            base.OnDirectionChanged(e);

            headPiece.Anchor = headPiece.Origin = e.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (UsesEzJudgement && ManiaEzDrawableJudgement.TryJudgeModeCheckForResult(this, userTriggered, timeOffset))
                return;

            Debug.Assert(HitObject.HitWindows != null);

            if (!userTriggered)
            {
                if (!HitObject.HitWindows.CanBeHit(timeOffset))
                    EzApplyPassiveMissWithStoredOffset();

                return;
            }

            var result = HitObject.HitWindows.ResultFor(timeOffset);

            if (result == HitResult.None)
                return;

            result = GetCappedResult(result);
            ApplyResult(result);
        }

        protected virtual HitResult GetCappedResult(HitResult result) => result;

        public virtual bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (ShouldSkipColumnRoutedPress?.Invoke(this) == true)
                return false;

            if (e.Action == Action.Value && ManiaEzDrawableJudgement.TryBmsOnPressed(this, e))
                return true;

            if (e.Action != Action.Value)
                return false;

            if (CheckHittable?.Invoke(this, Time.Current) == false)
                return false;

            return ApplyColumnRoutedPress();
        }

        internal bool ApplyColumnRoutedPress() => UpdateResult(true);

        public virtual void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }

        private bool receivesDisplayColour()
            => UseColorization && ManiaPlayfieldNoteDisplayTint.AllowsDisplayColour(DisplayColourPart);

        private bool skinOwnsDisplayColour(SkinnableDrawable skin)
        {
            if (skin.Drawable is FastNoteBase piece && piece.OwnsDisplayColour)
                return true;

            return noteDisplayTint?.DivisorNoteColouringEnabled != true && skin.Drawable is EzNoteBase;
        }

        private void updateSnapColour()
        {
            if (headPiece == null)
                return;

            if (!receivesDisplayColour() || noteDisplayTint == null || column == null
                || headPiece is SkinnableDrawable skin && skinOwnsDisplayColour(skin))
            {
                headPiece.Colour = Color4.White;
                return;
            }

            headPiece.Colour = noteDisplayTint.GetDisplayColour(DisplayColourPart, column, HitObject?.StartTime ?? 0);
        }

        public bool CanRouteToKPoor => ManiaEzDrawableJudgement.CanRouteToKPoor(this);

        internal virtual void EzApplyFinalResult(HitResult result, EzEnumJudgeMode judgeMode)
        {
            ApplyResult(static (r, data) =>
            {
                r.Type = data.result;

                if (data.result == HitResult.Miss
                    || (data.result == HitResult.Meh && JudgeModeHelper.MehBreaksCombo(data.judgeMode)))
                {
                    r.IsComboHit = false;
                }
            }, (result, judgeMode));
        }

        internal void EzDispatchExtraResult(HitResult result) => DispatchNewResult(result);
    }
}
