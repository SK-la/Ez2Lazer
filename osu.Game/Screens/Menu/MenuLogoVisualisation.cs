// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    public partial class MenuLogoVisualisation : LogoVisualisation
    {
        private IBindable<APIUser> user = null!;
        private Bindable<Skin> skin = null!;
        private Color4? colourOverride;

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api, SkinManager skinManager)
        {
            user = api.LocalUser.GetBoundCopy();
            skin = skinManager.CurrentSkin.GetBoundCopy();

            user.ValueChanged += _ => UpdateColour();
            skin.BindValueChanged(_ => UpdateColour(), true);
        }

        /// <summary>
        /// Temporarily overrides skin/user glow colour. Pass <c>null</c> to restore immediately.
        /// When setting a colour, the caller may animate with <c>FadeColour</c>; skin updates still respect the override.
        /// </summary>
        public void SetColourOverride(Color4? colour)
        {
            colourOverride = colour;

            if (colour == null)
                UpdateColour();
        }

        protected virtual void UpdateColour()
        {
            if (colourOverride != null)
            {
                Colour = colourOverride.Value;
                return;
            }

            if (user.Value?.IsSupporter ?? false)
                Colour = skin.Value.GetConfig<GlobalSkinColours, Color4>(GlobalSkinColours.MenuGlow)?.Value ?? Color4.White;
            else
                Colour = Color4.White;
        }
    }
}
