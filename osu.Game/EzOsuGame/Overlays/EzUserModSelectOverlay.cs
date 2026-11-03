// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzUserModSelectOverlay : UserModSelectOverlay
    {
        public required EzModSelectionRestoreController RestoreController { get; init; }

        public EzUserModSelectOverlay(OverlayColourScheme colourScheme = OverlayColourScheme.Green)
            : base(colourScheme)
        {
        }

        [BackgroundDependencyLoader]
        private void loadEzSongSelectKeyboardHandler()
        {
            Add(EzSongSelectKeyboardHandler.ForModOverlay(RestoreController, SelectedMods, this));
        }

        public override VisibilityContainer CreateFooterContent() => new EzModSelectFooterContent(this)
        {
            RestoreController = RestoreController,
            Beatmap = { BindTarget = Beatmap },
            ActiveMods = { BindTarget = ActiveMods },
            Ruleset = { BindTarget = Ruleset },
        };
    }
}
