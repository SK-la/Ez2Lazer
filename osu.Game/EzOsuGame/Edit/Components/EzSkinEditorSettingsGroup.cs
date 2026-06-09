// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Input.Events;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Sidebar settings group using the same toolbox styling as pause-menu settings groups.
    /// </summary>
    public partial class EzSkinEditorSettingsGroup : SettingsToolboxGroup
    {
        private readonly EzSkinEditorSidebarGroupDefinition definition;

        public EzSkinEditorSettingsGroup(EzSkinEditorSidebarGroupDefinition definition)
            : base(definition.Title)
        {
            this.definition = definition;
            Expanded.Value = definition.ExpandedByDefault;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(definition.CreateContent());
        }

        protected override bool OnHover(HoverEvent e)
        {
            base.OnHover(e);
            return true;
        }
    }
}
