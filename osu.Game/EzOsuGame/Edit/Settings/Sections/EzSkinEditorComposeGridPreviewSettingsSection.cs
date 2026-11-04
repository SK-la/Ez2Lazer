// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorComposeGridPreviewSettingsSection : EzSkinEditorSettingsGroup
    {
        public EzSkinEditorComposeGridPreviewSettingsSection()
            : base(EzEditorStrings.GROUP_COMPOSE_GRID_PREVIEW)
        {
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 120,
                Child = new BeatDivisorControl
                {
                    RelativeSizeAxes = Axes.Both,
                },
            });
        }
    }
}
