// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.UI;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// Footer-chrome button sized for editor scene / tool bars
    /// (<see cref="EzUIStyleV1.SCENE_BAR_HEIGHT"/> − <see cref="EzUIStyleV1.SCENE_BAR_GAP"/>).
    /// </summary>
    public partial class EzSceneBarButton : EzButton
    {
        public const float DEFAULT_WIDTH = 100;

        /// <summary>Selected / pressed chrome (maps to <see cref="EzButton.Accented"/>).</summary>
        public bool Active
        {
            get => Accented;
            set => Accented = value;
        }

        public EzSceneBarButton()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            Width = DEFAULT_WIDTH;
            Height = EzUIStyleV1.SceneBarControlHeight();
            TextSize = 14;
        }
    }
}
