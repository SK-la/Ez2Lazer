// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning.SbI
{
    internal abstract partial class FastNoteBase : CompositeDrawable, IColumnNote
    {
        protected virtual bool UseColorization => true;

        [Resolved]
        protected Column Column { get; private set; } = null!;

        // [Resolved]
        // protected EzLocalTextureFactory Factory { get; private set; } = null!;

        protected IBindable<double> NoteHeightScaleBindable = new Bindable<double>();
        protected IBindable<bool> EnabledColorBindable = null!;
        protected IBindable<Colour4> NoteColourBindable = null!;
        protected readonly IBindable<double> CornerRadiusBindable = new Bindable<double>();

        protected Container MainContainer { get; private set; } = null!;

        protected float UnitHeight => DrawWidth * 0.5f * (float)NoteHeightScaleBindable.Value;

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            InternalChildren = new Drawable[]
            {
                MainContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

            NoteHeightScaleBindable.BindTo(ezSkinInfo.NoteHeightScaleToWidth);
            EnabledColorBindable = Column.ColorSettingsEnabledBindable;
            NoteColourBindable = Column.EzNoteColourBindable;

            CornerRadiusBindable.BindTo(ezSkinInfo.NoteCornerRadius);
            CornerRadiusBindable.BindValueChanged(_ => UpdateDrawable());
            NoteHeightScaleBindable.BindValueChanged(_ => UpdateDrawable());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ColumnWatcher.GetOrCreate(Column).Add(this);
            Scheduler.AddOnce(OnLoadChanged);
        }

        protected virtual Colour4 NoteColor
        {
            get
            {
                if (!EnabledColorBindable.Value || !UseColorization || Column.ConfigTimingBasedNoteColouring)
                    return Colour4.White;

                return NoteColourBindable.Value;
            }
        }

        protected virtual void UpdateLoad()
        {
        }

        protected virtual void UpdateDrawable()
        {
        }

        protected virtual void UpdateColor()
        {
        }

        private void OnLoadChanged()
        {
            UpdateLoad();
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
            {
                UpdateColor();
            }
        }

        void IColumnNote.ForwardOnNoteSetChanged() => OnLoadChanged();
        void IColumnNote.ForwardOnNoteSizeChanged() => OnDrawableChanged();
        void IColumnNote.ForwardOnColourChanged() => OnColourChanged();

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                ColumnWatcher.Remove(Column, this);
            }

            base.Dispose(isDisposing);
        }
    }
}
