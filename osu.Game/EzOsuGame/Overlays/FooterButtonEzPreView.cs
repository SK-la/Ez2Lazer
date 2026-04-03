// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Screens.Footer;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class FooterButtonEzPreView : ScreenFooterButton
    {
        private readonly Action togglePreview;
        private readonly IBindable<bool> previewExpanded;

        public FooterButtonEzPreView(Action togglePreview, IBindable<bool> previewExpanded)
            : base(null)
        {
            this.togglePreview = togglePreview;
            this.previewExpanded = previewExpanded.GetBoundCopy();
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colour)
        {
            Text = FooterButtonEzPreViewStrings.BUTTON_TEXT;
            Icon = FontAwesome.Solid.Eye;
            AccentColour = colour.Blue1;
            Action = togglePreview;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            previewExpanded.BindValueChanged(value => OverlayState.Value = value.NewValue ? Visibility.Visible : Visibility.Hidden, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            previewExpanded.UnbindAll();
            base.Dispose(isDisposing);
        }

        private static class FooterButtonEzPreViewStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString BUTTON_TEXT = new EzLocalizationManager.EzLocalisableString("谱面预览", "Preview");
        }
    }
}
