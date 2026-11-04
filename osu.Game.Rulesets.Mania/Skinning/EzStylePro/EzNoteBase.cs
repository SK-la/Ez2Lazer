// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public abstract partial class EzNoteBase : CompositeDrawable, IColumnNote
    {
        protected virtual ManiaNoteDisplayColourPart DisplayColourPart => ManiaNoteDisplayColourPart.None;

        protected virtual bool ShowSeparators => false;

        [Resolved(CanBeNull = true)]
        protected Column Column { get; private set; } = null!;

        [Resolved(CanBeNull = true)]
        private ManiaPlayfieldNoteDisplayTint? noteDisplayTint { get; set; }

        [Resolved]
        protected EzLocalTextureFactory Factory { get; private set; } = null!;

        protected Container MainContainer { get; private set; } = null!;
        private Container? lineContainer;

        private bool tintBaseApplied;
        private string appliedPrefix = string.Empty;

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

            if (noteDisplayTint != null)
                noteDisplayTint.Changed += OnColourChanged;

            Scheduler.AddOnce(OnLoadChanged);
        }

        protected float NoteHeight => Column.EzNoteSizeBindable.Value.Y;

        protected string NoteName => ColorPrefix + "note";
        protected string HeadName => ColorPrefix + "longnote/head";
        protected string TailName => ColorPrefix + "longnote/tail";

        protected virtual string ColorPrefix
            => noteDisplayTint?.GetEzProTextureColourPrefix(DisplayColourPart, Column) ?? "white";

        protected virtual void UpdateTexture() { }
        protected virtual void UpdateDrawable() { }

        protected virtual void UpdateColor()
        {
        }

        protected void OnLoadChanged()
        {
            // Stale instances can still be reached by global config subscriptions after they leave the scene graph.
            if (!IsAlive || IsDisposed)
                return;

            tintBaseApplied = noteDisplayTint?.UseTintBaseTexture(DisplayColourPart, Column) == true;
            appliedPrefix = ColorPrefix;
            MainContainer.Clear();
            UpdateTexture();
            OnDrawableChanged();
            UpdateColor();
        }

        protected void OnDrawableChanged()
        {
            UpdateDrawable();
        }

        protected void OnColourChanged()
        {
            if (!IsAlive || IsDisposed)
                return;

            bool tintBase = noteDisplayTint?.UseTintBaseTexture(DisplayColourPart, Column) == true;
            string prefix = ColorPrefix;

            if (tintBaseApplied != tintBase || appliedPrefix != prefix)
            {
                OnLoadChanged();
                return;
            }

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
            {
                ColumnWatcher.Remove(Column, this);

                if (noteDisplayTint != null)
                    noteDisplayTint.Changed -= OnColourChanged;
            }
            base.Dispose(isDisposing);
        }

        void IColumnNote.ForwardOnNoteSetChanged() => OnLoadChanged();
        void IColumnNote.ForwardOnNoteSizeChanged() => OnDrawableChanged();
        void IColumnNote.ForwardOnColourChanged() => OnColourChanged();
    }
}
