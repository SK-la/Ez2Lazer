// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;
using SkinEditorControl = osu.Game.Overlays.SkinEditor.SkinEditor;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Skin editor UI pointed at <see cref="EzLayoutLayer"/>, persisting to <see cref="EzLayoutStore"/>.
    /// </summary>
    public partial class EzLayoutEditor : SkinEditorControl
    {
        [Resolved]
        private EzLayoutLayer layoutLayer { get; set; } = null!;

        [Resolved]
        private EzLayoutEditorOverlay editorOverlay { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        [Cached(typeof(ISkinComponentToolboxFilter))]
        private readonly ISkinComponentToolboxFilter toolboxFilter = EzLayoutToolboxBlacklist.INSTANCE;

        protected override IEnumerable<SkinnableContainer> AvailableTargets =>
            layoutLayer.LayoutContainers.Where(c => c.Alpha > 0);

        protected override void RequestClose() => editorOverlay.Hide();

        protected override void OnSkinChanged()
        {
            HeaderText.Clear();
            HeaderText.AddText(EzEditorStrings.LAYOUT_EDITOR_TITLE, cp => cp.Font = OsuFont.Default.With(size: 16));
            HeaderText.NewLine();
            HeaderText.AddText(EzEditorStrings.LAYOUT_EDITOR_SUBTITLE, cp =>
            {
                cp.Font = OsuFont.Default.With(size: 12);
                cp.Colour = colours.Yellow;
            });

            Schedule(() =>
            {
                HasBegunMutating = true;
                SelectedTarget.TriggerChange();
            });
        }

        protected override void SaveLayout(Skin skin, bool userTriggered = true)
        {
            if (!HasBegunMutating)
                return;

            var targets = AvailableTargets.ToArray();

            if (targets.Length == 0 || !targets.All(c => c.ComponentsLoaded))
                return;

            foreach (var t in targets)
                layoutLayer.Store.Save(t);

            if (userTriggered)
            {
                onScreenDisplay?.Display(new SkinEditorToast(
                    EzEditorStrings.LAYOUT_SAVED,
                    EzModifyPath.CONFIG_LAYOUT_PATH));
            }
        }

        protected override void RevertToDefault()
        {
            foreach (var t in AvailableTargets.ToArray())
            {
                layoutLayer.Store.Reset(t.Lookup);

                if (t is EzLayoutContainer ez)
                    ez.LoadFromStore();
                else
                    t.Reload();
            }
        }
    }
}
