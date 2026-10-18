// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public abstract partial class EzNoteBase : CompositeDrawable, IColumnNote
    {
        protected virtual bool UseColorization => false;
        protected virtual bool ShowSeparators => false;

        [Resolved(CanBeNull = true)]
        protected Column Column { get; private set; } = null!;

        [Resolved]
        protected EzLocalTextureFactory Factory { get; private set; } = null!;

        // Read through the column instead of caching the shared bindables: a drawable that stores a shared bindable in a field
        // would unbind it from everyone else when disposed (see Drawable.UnbindAllBindables).
        private bool ColorizationEnabled => Column.ColorSettingsEnabledBindable.Value;

        private Colour4 ColumnNoteColour => Column.EzNoteColourBindable.Value;

        private EzColumnType ColumnNoteType => Column.EzNoteTypeBindable.Value;

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

            if (ShowSeparators) createSeparators();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ColumnWatcher.GetOrCreate(Column).Add(this);
            Scheduler.AddOnce(OnLoadChanged);
        }

        protected float NoteHeight => Column.EzNoteSizeBindable.Value.Y;

        protected Colour4 NoteColor
        {
            get
            {
                if (ColorizationEnabled && UseColorization && !Column.ConfigTimingBasedNoteColouring)
                    return ColumnNoteColour;

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
                if ((ColorizationEnabled && UseColorization) || !UseColorization || Column.ConfigTimingBasedNoteColouring) return "white";

                return ColumnNoteType switch
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

        protected void OnLoadChanged()
        {
            // Stale instances can still be reached by global config subscriptions after they leave the scene graph.
            if (!IsAlive || IsDisposed)
                return;

            MainContainer.Clear();
            UpdateTexture();
            OnDrawableChanged();
            OnColourChanged();
        }

        protected void OnDrawableChanged()
        {
            UpdateDrawable();
        }

        protected void OnColourChanged()
        {
            if (UseColorization || !Column.ConfigTimingBasedNoteColouring)
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
