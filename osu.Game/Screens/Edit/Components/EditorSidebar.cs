// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.Components
{
    /// <summary>
    /// A sidebar area that can be attached to the left or right edge of the screen.
    /// Houses scrolling sectionised content.
    /// </summary>
    internal partial class EditorSidebar : Container<EditorSidebarSection>
    {
        public const float WIDTH = 250;

        public const float PADDING = 3;

        private readonly Box background;

        protected override Container<EditorSidebarSection> Content { get; }

        public const float TAB_CONTROL_HEIGHT = 30;
        public readonly OsuTabControl<EditorMode>? ModeTabControl;

        protected virtual EditorMode[] GetAvailableModes() => new[]
        {
            EditorMode.Default,
            EditorMode.EzSettings
        };

        public EditorSidebar()
        {
            Width = WIDTH;
            RelativeSizeAxes = Axes.Y;

            var availableModes = GetAvailableModes();
            var gridContent = new List<Drawable[]>();

            if (availableModes.Length > 1)
            {
                gridContent.Add(new Drawable[]
                {
                    ModeTabControl = new OsuTabControl<EditorMode>
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = TAB_CONTROL_HEIGHT,
                        Margin = new MarginPadding { Left = 5 },
                        Items = availableModes
                    },
                });
            }

            gridContent.Add(new Drawable[]
            {
                new OsuScrollContainer
                {
                    ScrollbarOverlapsContent = false,
                    RelativeSizeAxes = Axes.Both,
                    Child = Content = new FillFlowContainer<EditorSidebarSection>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(PADDING),
                        Direction = FillDirection.Vertical,
                    }
                }
            });

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = availableModes.Length > 1
                        ? new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension()
                        }
                        : new[]
                        {
                            new Dimension()
                        },
                    Content = gridContent.ToArray()
                }
            };

            if (ModeTabControl != null)
            {
                ModeTabControl.Current.ValueChanged += e =>
                {
                    Content.Clear();

                    switch (e.NewValue)
                    {
                        case EditorMode.Default:
                            loadDefaultComponents();
                            break;

                        case EditorMode.EzSettings:
                            loadEzSettingsComponents();
                            break;
                    }
                };
            }
            else
            {
                loadDefaultComponents();
            }
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background5;
        }

        private void loadEzSettingsComponents()
        {
            Add(new EzSkinSettings
            {
                RelativeSizeAxes = Axes.X,
            });
        }

        private void loadDefaultComponents()
        {
        }
    }
}
