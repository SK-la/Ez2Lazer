// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzDeselectRestoreModsButton : DeselectAllModsButton
    {
        private readonly ModSelectOverlay modSelectOverlay;
        private readonly EzModSelectionRestoreController restoreController;
        private readonly Bindable<IReadOnlyList<Mod>> selectedMods = new Bindable<IReadOnlyList<Mod>>();

        public EzDeselectRestoreModsButton(ModSelectOverlay modSelectOverlay, EzModSelectionRestoreController restoreController)
            : base(modSelectOverlay)
        {
            this.modSelectOverlay = modSelectOverlay;
            this.restoreController = restoreController;

            Action = () => restoreController.ClearOrRestore(selectedMods, modSelectOverlay);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedMods.BindTo(modSelectOverlay.SelectedMods);
            selectedMods.BindValueChanged(_ => updateState(), true);
            restoreController.StateChanged += updateState;
        }

        protected override void Dispose(bool isDisposing)
        {
            restoreController.StateChanged -= updateState;
            base.Dispose(isDisposing);
        }

        private void updateState()
        {
            if (restoreController.CanRestoreNow(selectedMods.Value))
            {
                Text = EzSongSelectStrings.RESTORE_MOD_SELECTION;
                Enabled.Value = true;
            }
            else
            {
                Text = CommonStrings.DeselectAll;
                Enabled.Value = selectedMods.Value.Any(m => m.Type != ModType.System);
            }
        }
    }
}
