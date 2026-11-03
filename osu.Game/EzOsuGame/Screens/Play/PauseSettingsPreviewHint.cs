// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens.Play
{
    public partial class PauseSettingsPreviewHint : Container
    {
        private const int transition_duration = 200;

        private Container hintContent = null!;

        public PauseSettingsPreviewHint()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => false;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Child = hintContent = new Container
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Margin = new MarginPadding { Bottom = 100 },
                AutoSizeAxes = Axes.Both,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 8,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Shadow,
                            Colour = Color4.Black.Opacity(0.5f),
                            Radius = 10,
                        },
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black,
                                Alpha = 0.6f,
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(12, 0),
                                Padding = new MarginPadding { Horizontal = 20, Vertical = 10 },
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = EzHUDStrings.PAUSE_SETTINGS_PREVIEW_PAUSED,
                                        Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                        Colour = colours.Yellow,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = "·",
                                        Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                        Colour = Color4.White.Opacity(0.5f),
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = EzHUDStrings.PAUSE_SETTINGS_PREVIEW_LABEL,
                                        Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                        Colour = Color4.White,
                                    },
                                }
                            },
                        }
                    },
                }
            };
        }

        public void SetVisible(bool visible)
        {
            if (!IsLoaded)
                return;

            hintContent.FadeTo(visible ? 1 : 0, transition_duration, Easing.OutQuint);
        }
    }
}
