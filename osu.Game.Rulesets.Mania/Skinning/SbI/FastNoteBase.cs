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
    public abstract partial class FastNoteBase : CompositeDrawable, IColumnNote
    {
        protected virtual bool UseColorization => true;

        [Resolved]
        protected Column Column { get; private set; } = null!;

        // [Resolved]
        // protected EzLocalTextureFactory Factory { get; private set; } = null!;

        // 本地需要始终有实例的 Bindable —— 构造时 new
        protected readonly Bindable<double> NoteHeightScaleBindable = new Bindable<double>();
        protected readonly Bindable<double> CornerRadiusBindable = new Bindable<double>();

        // 只依赖外部的 Bindable —— 在 load() 时赋值，不要自己 new
        protected IBindable<bool> EnabledColorBindable = null!;
        protected IBindable<Colour4> NoteColourBindable = null!;

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

            // 本地 Bindable 绑定到外部
            NoteHeightScaleBindable.BindTo(ezSkinInfo.NoteHeightScaleToWidth);
            CornerRadiusBindable.BindTo(ezSkinInfo.NoteCornerRadius);

            // 外部 Bindable 直接赋值
            EnabledColorBindable = Column.ColorSettingsEnabledBindable;
            NoteColourBindable = Column.EzNoteColourBindable;

            // 只对本地 Bindable 添加监听
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
