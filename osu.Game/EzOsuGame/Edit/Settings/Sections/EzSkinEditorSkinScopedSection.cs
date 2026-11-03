// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    /// <summary>
    /// Appearance 的「皮肤专用」栏目。对象保持不变，换肤只改可见性和内部控件。
    /// </summary>
    public partial class EzSkinEditorSkinScopedSection : EzSkinEditorSettingsGroup
    {
        private IBindable<Skin> skin = null!;
        private EzSkinFamily shownKind = EzSkinFamily.None;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        public EzSkinEditorSkinScopedSection()
            : base(EzEditorStrings.GROUP_SKIN_SPECIFIC)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin = skinManager.CurrentSkin.GetBoundCopy();
            skin.BindValueChanged(_ => syncKind(), true);
        }

        private void syncKind()
        {
            var kind = EzSkinEditorSkinKindResolver.Resolve(skin.Value);
            SectionVisible.Value = kind != EzSkinFamily.None;

            if (kind == shownKind)
                return;

            shownKind = kind;
            Clear();

            if (kind == EzSkinFamily.None)
                return;

            var provider = EzSkinEditorSkinScopedGroupRegistry.For(kind).FirstOrDefault();

            if (provider != null)
                Add(provider.CreateControls());
        }
    }
}
