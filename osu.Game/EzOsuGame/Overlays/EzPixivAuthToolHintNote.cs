// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Informational settings note with a clickable EzPixivAuth Releases link.
    /// </summary>
    public partial class EzPixivAuthToolHintNote : CompositeDrawable
    {
        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Top = SettingsSection.ITEM_SPACING_V2 },
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CornerRadius = 5,
                    CornerExponent = 2.5f,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = colourProvider.Dark2,
                            RelativeSizeAxes = Axes.Both,
                        },
                        createLinkFlow(),
                    },
                },
            };
        }

        private LinkFlowContainer createLinkFlow()
        {
            var linkFlow = new LinkFlowContainer(s => s.Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold))
            {
                Padding = new MarginPadding(8),
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Colour = colourProvider.Content2,
            };

            linkFlow.AddText(EzSettingsStrings.PIXIV_AUTH_TOOL_HINT_PREFIX);
            linkFlow.AddLink(
                EzSettingsStrings.PIXIV_AUTH_TOOL_HINT_LINK,
                EzSettingsStrings.PIXIV_AUTH_TOOL_RELEASES_URL,
                sp => sp.Font = sp.Font.With(weight: FontWeight.SemiBold));
            linkFlow.AddText(EzSettingsStrings.PIXIV_AUTH_TOOL_HINT_SUFFIX);

            return linkFlow;
        }
    }
}
