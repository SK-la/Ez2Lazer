// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
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

        public EzSkinEditorScriptedSkinSettingsSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (skins.CurrentSkin.Value is not ScriptedSkinWrapper wrapper)
            {
                Alpha = 0;
                return;
            }

            Child = new ScriptedSkinConfigEditor();
            ((ScriptedSkinConfigEditor)Child).SetSkin(wrapper.GetScriptedSkin());
        }
    }
}
