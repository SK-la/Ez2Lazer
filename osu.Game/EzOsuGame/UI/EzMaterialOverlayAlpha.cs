// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.UI
{
    /// <summary>
    /// 穿透材质开关辅助：经典 M 与玻璃 N 互斥（不会出现半透明 M 叠在玻璃上）。
    /// </summary>
    public static class EzMaterialOverlayAlpha
    {
        /// <summary>
        /// 除关闭外的任何档位都表示经典层要让位给穿透材质。
        /// </summary>
        public static bool IsEnabled(EzMaterialUiMode mode) => mode != EzMaterialUiMode.Default;

        /// <summary>
        /// 非关闭档位隐藏经典 M、显示玻璃 N；关闭时反过来。
        /// 两侧都 ClearTransforms，避免残留的淡入淡出让 chrome 一直不可见。
        /// </summary>
        public static void BindExclusive(Drawable classicM, Drawable glassN, IBindable<EzMaterialUiMode> materialMode)
        {
            materialMode.BindValueChanged(e =>
            {
                classicM.ClearTransforms();
                glassN.ClearTransforms();

                if (IsEnabled(e.NewValue))
                {
                    classicM.Alpha = 0f;
                    glassN.Alpha = 1f;
                }
                else
                {
                    glassN.Alpha = 0f;
                    classicM.Alpha = 1f;
                }
            }, true);
        }

        /// <summary>
        /// 非关闭档位隐藏没有配对玻璃兄弟的经典 chrome（例如选歌右侧渐变）；关闭时恢复 Alpha 1。
        /// </summary>
        public static void BindHiddenWhenAcrylic(Drawable classicM, IBindable<EzMaterialUiMode> materialMode)
        {
            materialMode.BindValueChanged(e =>
            {
                classicM.ClearTransforms();
                classicM.Alpha = IsEnabled(e.NewValue) ? 0f : 1f;
            }, true);
        }
    }
}
