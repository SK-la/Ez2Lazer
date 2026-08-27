// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.UI
{
    /// <summary>
    /// Acrylic UI switch helpers: classic M vs glass N are mutually exclusive (never half-alpha M).
    /// </summary>
    public static class EzAcrylicOverlayAlpha
    {
        /// <summary>
        /// ON: hide classic M, show glass N. OFF: hide N, show M.
        /// Both sides ClearTransforms so leftover fades cannot leave chrome invisible.
        /// </summary>
        public static void BindExclusive(Drawable classicM, Drawable glassN, Bindable<bool> acrylicEnabled)
        {
            acrylicEnabled.BindValueChanged(e =>
            {
                classicM.ClearTransforms();
                glassN.ClearTransforms();

                if (e.NewValue)
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
        /// ON: hide classic chrome with no paired glass sibling (e.g. song-select right gradient).
        /// OFF: restore Alpha 1.
        /// </summary>
        public static void BindHiddenWhenAcrylic(Drawable classicM, Bindable<bool> acrylicEnabled)
        {
            acrylicEnabled.BindValueChanged(e =>
            {
                classicM.ClearTransforms();
                classicM.Alpha = e.NewValue ? 0f : 1f;
            }, true);
        }
    }
}
