// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.UserInterface;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// HUD 雷达图：外圈样式固定，不随 BaseLineColour 变化。
    /// </summary>
    public partial class EzHUDRadarChart : EzRadarChart
    {
        // HUD 外圈调试：颜色 / 线宽 / 相对底色外缘的径向外扩（改这里即可）
        private static Color4 hudOuterRingColour => new Color4(255, 255, 255, 128);
        private static float hudOuterRingThickness => 2.5f;
        private static float hudOuterRingRadialOffset => hudOuterRingThickness - 1.5f;

        public EzHUDRadarChart()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            OuterRingColour = hudOuterRingColour;
            OuterRingThickness = hudOuterRingThickness;
            OuterRingRadialOffset = hudOuterRingRadialOffset;
        }
    }
}
