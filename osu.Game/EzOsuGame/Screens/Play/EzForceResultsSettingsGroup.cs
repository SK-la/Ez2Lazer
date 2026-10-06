// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Play.PlayerSettings;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Play
{
    /// <summary>
    /// Bottom-of-sidebar entry which force-settles a gameplay session that cannot reach the results screen on its own.
    /// Autoplay playback runs as <see cref="osu.Game.Screens.Play.ReplayPlayer"/>, where pausing (and therefore the
    /// pause-screen force button) is unavailable, so this is the only way out of a stuck replay.
    /// </summary>
    public partial class EzForceResultsSettingsGroup : PlayerSettingsGroup
    {
        public Action? Action { get; set; }

        private EzHoldToConfirmButton holdButton = null!;
        private OsuSpriteText labelText = null!;

        public EzForceResultsSettingsGroup()
            : base(EzHUDStrings.PAUSE_FORCE_RESULTS_LABEL)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Add(new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(12, 0),
                Children = new Drawable[]
                {
                    holdButton = new EzHoldToConfirmButton
                    {
                        Action = () => Action?.Invoke(),
                    },
                    labelText = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = OsuFont.GetFont(weight: FontWeight.Bold),
                        Text = EzHUDStrings.PAUSE_FORCE_RESULTS_LABEL,
                        Colour = colours.Orange1,
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            holdButton.Progress.BindValueChanged(p =>
            {
                labelText.Text = p.NewValue > 0
                    ? EzHUDStrings.PAUSE_FORCE_RESULTS_HOLDING
                    : EzHUDStrings.PAUSE_FORCE_RESULTS_LABEL;
            }, true);
        }
    }
}
