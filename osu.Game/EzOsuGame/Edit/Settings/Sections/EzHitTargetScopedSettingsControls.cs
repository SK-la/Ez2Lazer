// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    /// <summary>
    /// Hit-target float amplitude and alpha. Used by EzStylePro and Ez2 scoped providers.
    /// </summary>
    public partial class EzHitTargetScopedSettingsControls : FillFlowContainer
    {
        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public EzHitTargetScopedSettingsControls()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.HIT_TARGET_FLOAT_FIXED,
                    TooltipText = EzSkinStrings.HIT_TARGET_FLOAT_FIXED_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetFloatFixed),
                    KeyboardStep = 0.1f,
                },
                new SettingsSlider<double>
                {
                    LabelText = EzSkinStrings.HIT_TARGET_ALPHA,
                    TooltipText = EzSkinStrings.HIT_TARGET_ALPHA_TOOLTIP,
                    Current = ezSkinConfig.GetBindable<double>(Ez2Setting.HitTargetAlpha),
                    KeyboardStep = 0.01f,
                },
            };
        }
    }
}
