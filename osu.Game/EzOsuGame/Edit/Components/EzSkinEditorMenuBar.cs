// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Screens.Edit.Components.Menus;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Top menu bar for Ez skin editor, matching <see cref="SkinEditor"/> <see cref="EditorMenuBar"/> layout.
    /// </summary>
    public partial class EzSkinEditorMenuBar : EditorMenuBar
    {
        public const float HEIGHT = SkinEditor.MENU_HEIGHT;

        public Action? ApplyAction { get; set; }

        public Action? ExitAction { get; set; }

        public EzSkinEditorMenuBar()
        {
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            RelativeSizeAxes = Axes.Both;

            Items = new[]
            {
                new MenuItem(CommonStrings.MenuBarFile)
                {
                    Items = new OsuMenuItem[]
                    {
                        new EditorMenuItem("应用", MenuItemType.Standard, () => ApplyAction?.Invoke()),
                        new OsuMenuItemSpacer(),
                        new EditorMenuItem(CommonStrings.Exit, MenuItemType.Standard, () => ExitAction?.Invoke()),
                    },
                },
            };
        }
    }
}
