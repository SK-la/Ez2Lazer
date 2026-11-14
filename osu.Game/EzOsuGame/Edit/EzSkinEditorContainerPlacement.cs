// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    internal static class EzSkinEditorContainerPlacement
    {
        public static bool TryPlace(SkinnableContainer target, ISerialisableDrawable component, bool applyDefaults = true)
        {
            if (target is EzLayoutContainer ezLayout)
                return ezLayout.TryPlace(component, applyDefaults);

            if (component is not Drawable drawable)
                return false;

            if (applyDefaults)
            {
                drawable.Origin = Anchor.TopCentre;
                drawable.Anchor = Anchor.TopCentre;
                drawable.Y = target.DrawSize.Y / 2;
            }

            try
            {
                target.Add(component);
            }
            catch
            {
                return false;
            }

            if (applyDefaults && !component.UsesFixedAnchor)
                SkinSelectionHandler.ApplyClosestAnchorOrigin(drawable);

            return true;
        }
    }
}
