// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning
{
    public abstract partial class FastNoteBase : CompositeDrawable, IColumnNote
    {
        protected virtual bool UseColorization => true;

        /// <summary>
        /// 内置色彩模板开关（关联游戏设置，需为实时 bindable）。非 null 时接管列色来源：
        /// true → <see cref="BuiltInColourTemplate"/>；false → 皮肤编辑器的列色配置。
        /// <para>默认 null，即不启用模板、始终使用编辑器列色（SbI 原行为）。</para>
        /// </summary>
        protected virtual IBindable<bool>? BuiltInColourTemplateSwitch => null;

        /// <summary>
        /// <see cref="BuiltInColourTemplateSwitch"/> 为 true 时的模板色，默认白色。
        /// </summary>
        protected virtual Colour4 BuiltInColourTemplate => Colour4.White;

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

        /// <summary><see cref="BuiltInColourTemplateSwitch"/> 的持有副本（弱绑定需自行保活）。</summary>
        private IBindable<bool>? builtInColourTemplateSwitch;

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

            // 在 LoadComplete 订阅：子类覆写的该属性可能依赖其自身 [Resolved]（构造/load 期间未必已注入）。
            builtInColourTemplateSwitch = BuiltInColourTemplateSwitch?.GetBoundCopy();
            builtInColourTemplateSwitch?.BindValueChanged(_ => OnColourChanged());

            Scheduler.AddOnce(OnLoadChanged);
        }

        protected virtual Colour4 NoteColor
        {
            get
            {
                // 模板模式下不受编辑器配色总开关影响：10k2s1p 开启时 Ez 皮肤本来就无视列色配置。
                if (BuiltInColourTemplateSwitch?.Value == true)
                    return BuiltInColourTemplate;

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
            {
                ColumnWatcher.Remove(Column, this);
                builtInColourTemplateSwitch?.UnbindAll();
            }

            base.Dispose(isDisposing);
        }

        void IColumnNote.ForwardOnNoteSetChanged() => OnLoadChanged();
        void IColumnNote.ForwardOnNoteSizeChanged() => OnDrawableChanged();
        void IColumnNote.ForwardOnColourChanged() => OnColourChanged();
    }
}
