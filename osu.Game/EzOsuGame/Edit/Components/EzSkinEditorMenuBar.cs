// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Screens.Edit.Components.Menus;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorMenuBar : Container
    {
        public const float HEIGHT = SkinEditor.MENU_HEIGHT;

        public Action? ApplyAction { get; set; }

        public Action? ExitAction { get; set; }

        public EzSkinEditorMenuBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new EditorMenuBar
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.Both,
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
                },
            };
        }
    }
}
