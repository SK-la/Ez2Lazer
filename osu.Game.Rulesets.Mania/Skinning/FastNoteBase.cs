// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning
{
    public abstract partial class FastNoteBase : CompositeDrawable, IColumnNote
    {
        /// <summary>
        /// 内置色彩模板开关。非 null 且为 true 时，note 图形使用 <see cref="BuiltInColourTemplate"/>。
        /// 列色和节拍线色由外层 <c>headPiece</c> 提供。
        /// </summary>
        protected virtual IBindable<bool>? BuiltInColourTemplateSwitch => null;

        /// <summary>
        /// <see cref="BuiltInColourTemplateSwitch"/> 为 true 时的模板色，默认白色。
        /// </summary>
        protected virtual Colour4 BuiltInColourTemplate => Colour4.White;

        [Resolved]
        protected Column Column { get; private set; } = null!;

        // [Resolved]
        // private OsuColour colours { get; set; }

        // [Resolved]
        // protected EzLocalTextureFactory Factory { get; private set; } = null!;

        // 本地需要始终有实例的 Bindable —— 构造时 new
        protected readonly Bindable<double> NoteHeightScaleBindable = new Bindable<double>();
        protected readonly Bindable<double> CornerRadiusBindable = new Bindable<double>();

        protected Container MainContainer { get; private set; } = null!;

        protected float UnitHeight => DrawWidth * 0.5f * (float)NoteHeightScaleBindable.Value;

        public bool OwnsDisplayColour => BuiltInColourTemplateSwitch?.Value == true;

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
                if (BuiltInColourTemplateSwitch?.Value == true)
                    return BuiltInColourTemplate;

                return Color4.White;
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
            UpdateColor();
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
