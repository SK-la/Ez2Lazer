// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorScriptedSkinSettingsSection : FillFlowContainer
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private readonly Bindable<Skin> currentSkin = new Bindable<Skin>();

        public EzSkinEditorScriptedSkinSettingsSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            currentSkin.BindTo(skins.CurrentSkin);
            currentSkin.BindValueChanged(_ => refresh(), true);
        }

        private void refresh()
        {
            Clear();

            if (currentSkin.Value is not ScriptedSkinWrapper wrapper)
            {
                Alpha = 0;
                return;
            }

            Alpha = 1;
            Child = new ScriptedSkinConfigEditor();
            ((ScriptedSkinConfigEditor)Child).SetSkin(wrapper.GetScriptedSkin());
        }
    }
}
