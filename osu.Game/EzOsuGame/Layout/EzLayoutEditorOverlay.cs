// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Screens;
using SkinEditorControl = osu.Game.Overlays.SkinEditor.SkinEditor;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Overlay host for <see cref="EzLayoutEditor"/>. No hotkey; opened from settings.
    /// </summary>
    [Cached(typeof(SkinEditorOverlay))]
    public partial class EzLayoutEditorOverlay : SkinEditorOverlay
    {
        private readonly EzLayoutLayer layoutLayer;

        protected override bool PresentsGameplayFromMainMenu => false;

        public EzLayoutEditorOverlay(ScalingContainer scalingContainer, EzLayoutLayer layoutLayer)
            : base(scalingContainer)
        {
            this.layoutLayer = layoutLayer;
        }

        protected override SkinEditorControl CreateEditor() => new EzLayoutEditor();

        protected override Drawable GetEditorTarget(OsuScreen screen) => layoutLayer;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            layoutLayer.LayoutTargetsChanged += onLayoutTargetsChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            layoutLayer.LayoutTargetsChanged -= onLayoutTargetsChanged;
            base.Dispose(isDisposing);
        }

        private void onLayoutTargetsChanged()
        {
            if (LastTargetScreen != null)
                SetTarget(LastTargetScreen);
        }
    }
}
