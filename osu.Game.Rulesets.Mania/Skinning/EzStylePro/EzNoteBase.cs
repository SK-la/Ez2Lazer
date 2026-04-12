// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal abstract partial class EzNoteBase : CompositeDrawable, IColumnNote
    {
        protected virtual bool UseColorization => false;
        protected virtual bool ShowSeparators => false;

        [Resolved]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        protected EzLocalTextureFactory Factory { get; private set; } = null!;

        private IBindable<bool> enabledColorBindable = null!;
        private IBindable<Colour4> noteColourBindable = null!;
        private IBindable<Vector2> noteSizeBindable = null!;
        private IBindable<EzColumnType> noteTypeBindable = null!;

        protected Container MainContainer { get; private set; } = null!;
        private Container? lineContainer;

        [BackgroundDependencyLoader]
        private void load()
        {
            MainContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
            AddInternal(MainContainer); //允许多个子元素

            noteTypeBindable = Column.EzNoteTypeBindable;
            noteSizeBindable = Column.EzNoteSizeBindable;

            enabledColorBindable = Column.ColorSettingsEnabledBindable;
            noteColourBindable = Column.EzNoteColourBindable;

            if (ShowSeparators) createSeparators();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ColumnWatcher.GetOrCreate(Column).Add(this);
            Scheduler.AddOnce(OnLoadChanged);
        }

        protected float NoteHeight => noteSizeBindable.Value.Y;

        protected Colour4 NoteColor
        {
            get
            {
                if (enabledColorBindable.Value && UseColorization)
                    return noteColourBindable.Value;

                return Colour4.White;
            }
        }

        protected string NoteName => ColorPrefix + "note";
        protected string HeadName => ColorPrefix + "longnote/head";
        protected string TailName => ColorPrefix + "longnote/tail";

        protected string ColorPrefix
        {
            get
            {
                if ((enabledColorBindable.Value && UseColorization) || !UseColorization) return "white";

                return noteTypeBindable.Value switch
                {
                    EzColumnType.A => "white",
                    EzColumnType.B => "blue",
                    EzColumnType.S => "green",
                    EzColumnType.E => "white",
                    EzColumnType.P => "green",
                    _ => "white"
                };
            }
        }

        protected virtual void UpdateTexture() { }
        protected virtual void UpdateDrawable() { }

        protected virtual void UpdateColor()
        {
            MainContainer.Colour = NoteColor;
        }

        private void OnLoadChanged()
        {
            if (!IsLoaded)
                return;

            MainContainer.Clear();
            UpdateTexture();
            OnDrawableChanged();
            OnColourChanged();
        }

        private void OnDrawableChanged()
        {
            UpdateDrawable();
        }

        private void OnColourChanged()
        {
            if (UseColorization)
                UpdateColor();
        }

        private void createSeparators()
        {
            if (lineContainer != null)
                return;

            var ezNoteSideLine1 = new EzNoteSideLine
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Fill,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
            };

            var ezNoteSideLine2 = new EzNoteSideLine
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Fill,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
            };

            lineContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Stretch,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[] { ezNoteSideLine1, ezNoteSideLine2 }
            };

            AddInternal(lineContainer);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                ColumnWatcher.Remove(Column, this);
            base.Dispose(isDisposing);
        }

        void IColumnNote.ForwardOnNoteSetChanged() => OnLoadChanged();
        void IColumnNote.ForwardOnNoteSizeChanged() => OnDrawableChanged();
        void IColumnNote.ForwardOnColourChanged() => OnColourChanged();
    }
}
