// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorMenuBar : Container
    {
        public const float HEIGHT = SkinEditor.MENU_HEIGHT;

        public Action? ApplyAction { get; set; }

        public EzSkinEditorMenuBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background6,
                },
                new MenuBarButton
                {
                    Text = "应用",
                    X = 10,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Action = () => ApplyAction?.Invoke(),
                },
            };
        }

        private partial class MenuBarButton : OsuButton
        {
            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                BackgroundColour = colours.Blue3;
                Content.CornerRadius = 4;
                Width = 80;
                Height = 28;
            }
        }
    }
}
