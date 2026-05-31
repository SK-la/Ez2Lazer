// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Mods;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzModSelectFooterContent : ModSelectFooterContent
    {
        public required EzModSelectionRestoreController RestoreController { get; init; }

        private readonly ModSelectOverlay modSelectOverlay;

        public EzModSelectFooterContent(ModSelectOverlay overlay)
            : base(overlay)
        {
            modSelectOverlay = overlay;
        }

        protected override IEnumerable<ShearedButton> CreateButtons() => new[]
        {
            DeselectAllModsButton = new EzDeselectRestoreModsButton(modSelectOverlay, RestoreController)
        };
    }
}
