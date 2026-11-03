// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// 侧栏里的一个栏目。对象在场景第一次显示时创建，之后一直复用。
    /// 子控件在 <c>load</c> 里绑定配置。显隐走 <see cref="SectionVisible"/>，不拆掉重建。
    /// </summary>
    public abstract partial class EzSkinEditorSettingsGroup : SettingsToolboxGroup
    {
        public LocalisableString SectionTitle { get; }

        public BindableBool SectionVisible { get; } = new BindableBool(true);

        protected EzSkinEditorSettingsGroup(LocalisableString title)
            : base(title)
        {
            SectionTitle = title;
        }

        protected override bool OnHover(HoverEvent e)
        {
            base.OnHover(e);
            return true;
        }
    }
}
