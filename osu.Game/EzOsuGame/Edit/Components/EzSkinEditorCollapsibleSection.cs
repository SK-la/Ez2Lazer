// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Edit.Components;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Sidebar group using <see cref="EditorSidebarSection"/> styling with optional collapse.
    /// </summary>
    public partial class EzSkinEditorCollapsibleSection : EditorSidebarSection
    {
        private readonly EzSkinEditorSidebarGroupDefinition definition;
        private bool expanded;

        public EzSkinEditorCollapsibleSection(EzSkinEditorSidebarGroupDefinition definition)
            : base(definition.Title)
        {
            this.definition = definition;
            expanded = definition.ExpandedByDefault;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Content.Child = definition.CreateContent();
            applyExpandedState();
        }

        protected override bool OnClick(ClickEvent e)
        {
            // Toggle when clicking the section header area (top portion).
            if (e.ScreenSpaceMousePosition.Y < ScreenSpaceDrawQuad.TopLeft.Y + 40)
            {
                expanded = !expanded;
                applyExpandedState();
                return true;
            }

            return base.OnClick(e);
        }

        private void applyExpandedState()
        {
            if (expanded)
                Content.Show();
            else
                Content.Hide();
        }
    }
}
