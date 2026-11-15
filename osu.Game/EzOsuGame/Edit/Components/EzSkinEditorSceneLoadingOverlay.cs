// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Covers the plugged scene viewport while the screen stack swaps or loads a new preview screen.
    /// </summary>
    public partial class EzSkinEditorSceneLoadingOverlay : Container
    {
        private readonly LoadingLayer loadingLayer;

        public EzSkinEditorSceneLoadingOverlay()
        {
            RelativeSizeAxes = Axes.Both;
            Depth = float.MinValue;
            AlwaysPresent = true;

            Child = loadingLayer = new LoadingLayer(dimBackground: true, withBox: true)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
            };
        }

        public void ShowLoading() => loadingLayer.Show();

        public void HideLoading() => loadingLayer.Hide();
    }
}
