// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    public partial class BindingSettings : SettingsSubsection
    {
        protected override LocalisableString Header => BindingSettingsStrings.ShortcutAndGameplayBindings;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { @"keybindings", @"controls", @"keyboard", @"keys", @"buttons" });

        public BindingSettings(Action openKeyConfig)
        {
            Children = new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = BindingSettingsStrings.Configure,
                    TooltipText = BindingSettingsStrings.ChangeBindingsButton,
                    Action = openKeyConfig,
                    Height = 60
                },
            };
        }
    }
}
